using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public enum FishingResult
{
    Caught,      // ดึงทุ่นเข้ามาในระยะได้
    LineSnapped, // ความตึงถึง 100% สายขาด
    Escaped      // ปล่อยให้ปลาลากออกไปไกลเกินไป
}

/// <summary>
/// ระบบแรงตึงสายเบ็ดตอนปลากินเหยื่อ:
/// - กดคลิกซ้ายค้าง = ม้วนสายดึงทุ่นเข้าหาตัว ความตึงเพิ่มขึ้น (ปลาดิ้นแรงเป็นช่วงๆ)
/// - ปล่อยคลิก = ความตึงลดลง แต่ปลาจะลากทุ่นออกไป
/// - ความตึงถึง 100% = สายขาด ปลาหลุด
/// - ดึงทุ่นเข้ามาในระยะ 2 เมตรจากผู้เล่น = จับปลาสำเร็จ
/// วางไว้บน GameObject เดียวกับ FishingRodCaster
/// </summary>
[RequireComponent(typeof(FishingRodCaster))]
public class FishingLineTension : MonoBehaviour
{
    [Tooltip("ตัวผู้เล่น ใช้วัดระยะทุ่น ถ้าว่างจะใช้ root ของคันเบ็ด")]
    [SerializeField] private Transform player;

    [Header("Reeling")]
    [Tooltip("ความเร็วดึงทุ่นเข้าหาตัวตอนกดค้าง (เมตร/วินาที)")]
    [SerializeField, Min(0f)] private float reelSpeed = 2.2f;
    [Tooltip("ระยะห่างจากผู้เล่น (แนวราบ) ที่ถือว่าจับปลาได้")]
    [SerializeField, Min(0.1f)] private float catchDistance = 2f;

    [Header("Tension (% ต่อวินาที)")]
    [Tooltip("ความตึงที่เพิ่มขึ้นตอนกดค้าง")]
    [SerializeField, Min(0f)] private float tensionGain = 18f;
    [Tooltip("ความตึงเพิ่มเติมตอนปลาดิ้นแรง (สุ่มเป็นช่วง)")]
    [SerializeField, Min(0f)] private float thrashGain = 22f;
    [Tooltip("ความตึงที่ลดลงตอนปล่อยคลิก")]
    [SerializeField, Min(0f)] private float tensionDecay = 30f;

    [Header("Fish")]
    [Tooltip("ความเร็วที่ปลาลากทุ่นหนีตอนไม่ได้ดึง (เมตร/วินาที)")]
    [SerializeField, Min(0f)] private float fishPullSpeed = 0.6f;
    [Tooltip("ความแรงของปลาแต่ละตัวสุ่มในช่วงนี้ (คูณกับแรงดิ้น)")]
    [SerializeField] private Vector2 fishStrengthRange = new Vector2(0.6f, 1.4f);
    [Tooltip("ถ้าปลาลากทุ่นไปไกลเกินระยะนี้ ปลาจะหลุด")]
    [SerializeField, Min(1f)] private float escapeDistance = 35f;

    [Header("Effects")]
    [SerializeField] private ParticleSystem splashPrefab;

    [Header("Events")]
    public UnityEvent onFightStarted;
    public UnityEvent onFishCaught;
    public UnityEvent onLineSnapped;
    public UnityEvent onFishEscaped;

    private FishingRodCaster caster;
    private FishingBobber bobber;
    private float fishStrength;
    private float noiseSeed;

    /// <summary>กำลังสู้กับปลาอยู่หรือไม่ (ระหว่างนี้คลิกซ้ายใช้ดึงสาย ไม่ใช่เหวี่ยง)</summary>
    public bool IsFighting { get; private set; }
    /// <summary>ความตึงสาย 0-1</summary>
    public float Tension01 { get; private set; }
    public bool IsReeling { get; private set; }
    /// <summary>ปลากำลังดิ้นแรง (ใช้ทำ UI สั่น)</summary>
    public bool IsThrashing { get; private set; }
    public float DistanceToPlayer { get; private set; }
    public float CatchDistance => catchDistance;

    /// <summary>กดดึงจากปุ่มอื่นนอกจากเมาส์ (เช่นปุ่มบนจอมือถือ หรือเมนูทดสอบ)</summary>
    public bool ReelButtonHeld { get; set; }

    /// <summary>
    /// ระบบนี้ต้องการใช้คลิกซ้ายหรือไม่: ระหว่างสู้ หรือทันทีที่ปลากินเบ็ด
    /// (FishingRodCaster จะไม่ใช้คลิกนั้นเหวี่ยงหรือเก็บทุ่น)
    /// </summary>
    public bool WantsInput
    {
        get
        {
            if (!isActiveAndEnabled) return false;
            if (IsFighting) return true;
            Rigidbody rb = caster != null ? caster.CurrentBobber : null;
            if (rb == null) return false;
            var b = rb.GetComponent<FishingBobber>();
            return b != null && b.State == BobberState.FishBite;
        }
    }

    /// <summary>แจ้งผลการสู้กับปลา (ใช้กับ UI)</summary>
    public event System.Action<FishingResult> FightEnded;

    private void Awake()
    {
        caster = GetComponent<FishingRodCaster>();
        if (player == null) player = transform.root;
    }

    private void OnDisable()
    {
        EndFight();
    }

    private void Update()
    {
        if (!IsFighting)
        {
            TryStartFight();
            return;
        }

        if (bobber == null)
        {
            // ทุ่นหายไป (เช่นเก็บคันเบ็ด) ระหว่างสู้
            EndFight();
            return;
        }

        Mouse mouse = Mouse.current;
        IsReeling = (mouse != null && mouse.leftButton.isPressed && !PlayerInputGate.IsBlocked) || ReelButtonHeld;

        // ปลาดิ้นแรงเป็นช่วงๆ ตาม Perlin noise
        float thrash = Mathf.PerlinNoise(noiseSeed, Time.time * 0.9f);
        IsThrashing = thrash > 0.62f;
        float thrashFactor = Mathf.Clamp01((thrash - 0.45f) / 0.55f) * fishStrength;

        Vector3 bobberPos = bobber.RestPosition;
        Vector3 toPlayer = Vector3.ProjectOnPlane(player.position - bobberPos, Vector3.up);
        float distance = toPlayer.magnitude;
        Vector3 dirToPlayer = distance > 0.001f ? toPlayer / distance : Vector3.zero;

        if (IsReeling)
        {
            Tension01 += (tensionGain + thrashGain * thrashFactor) / 100f * Time.deltaTime;
            bobberPos += dirToPlayer * Mathf.Min(distance, reelSpeed * Time.deltaTime);
        }
        else
        {
            Tension01 -= tensionDecay / 100f * Time.deltaTime;
            bobberPos -= dirToPlayer * fishPullSpeed * (0.5f + thrashFactor) * Time.deltaTime;
        }

        Tension01 = Mathf.Clamp01(Tension01);
        bobber.SetRestPositionXZ(bobberPos);
        DistanceToPlayer = Vector3.ProjectOnPlane(player.position - bobberPos, Vector3.up).magnitude;

        if (Tension01 >= 1f) Finish(FishingResult.LineSnapped);
        else if (DistanceToPlayer <= catchDistance) Finish(FishingResult.Caught);
        else if (DistanceToPlayer >= escapeDistance) Finish(FishingResult.Escaped);
    }

    private void TryStartFight()
    {
        Rigidbody rb = caster.CurrentBobber;
        if (rb == null || caster.IsReeling) return;

        var b = rb.GetComponent<FishingBobber>();
        if (b == null || b.State != BobberState.FishBite) return;

        bobber = b;
        IsFighting = true;
        Tension01 = 0f;
        fishStrength = Random.Range(fishStrengthRange.x, fishStrengthRange.y);
        noiseSeed = Random.value * 100f;
        onFightStarted?.Invoke();
    }

    private void Finish(FishingResult result)
    {
        Vector3 pos = bobber != null ? bobber.RestPosition : transform.position;
        if (splashPrefab != null)
        {
            ParticleSystem splash = Instantiate(splashPrefab, pos, Quaternion.identity);
            splash.transform.localScale = Vector3.one * (result == FishingResult.Caught ? 1.6f : 1.1f);
            splash.Play(true);
            Destroy(splash.gameObject, 3f);
        }

        Debug.Log($"[FishingLineTension] ผลการสู้กับปลา: {result} (ความตึง {Tension01:P0}, ระยะ {DistanceToPlayer:F1} ม.)");
        EndFight();
        caster.ReelInImmediate(); // ทุ่นหายไป (ปลาขึ้นมาแล้ว หรือสายขาด)

        if (result == FishingResult.Caught) onFishCaught?.Invoke();
        else if (result == FishingResult.LineSnapped) onLineSnapped?.Invoke();
        else onFishEscaped?.Invoke();
        FightEnded?.Invoke(result);
    }

    private void EndFight()
    {
        IsFighting = false;
        IsReeling = false;
        IsThrashing = false;
        Tension01 = 0f;
        bobber = null;
    }
}
