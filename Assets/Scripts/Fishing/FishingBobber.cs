using UnityEngine;
using UnityEngine.Events;

public enum BobberState
{
    Flying,   // ลอยอยู่กลางอากาศหลังเหวี่ยง
    Floating, // ลอยบนน้ำ รอปลา
    FishBite  // ปลากินเบ็ด
}

/// <summary>
/// ทุ่นตกปลา: เมื่อชนกับวัตถุที่มี Tag "Water" จะหยุดแรงตกแล้วลอยน้ำพร้อมกระเพื่อมเบาๆ
/// จากนั้นสุ่มรอ 3-8 วินาที แล้วเข้าสู่สถานะ FishBite: ทุ่นถูกดึงจมลงและกระตุก พร้อมน้ำกระจายและเสียง
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(AudioSource))]
public class FishingBobber : MonoBehaviour
{
    [SerializeField] private string waterTag = "Water";

    [Header("Floating")]
    [Tooltip("ความสูงของจุดกึ่งกลางทุ่นเทียบกับผิวน้ำ (0 = จมครึ่งลูก)")]
    [SerializeField] private float floatOffset = 0.01f;
    [SerializeField, Min(0f)] private float bobAmplitude = 0.02f;
    [SerializeField, Min(0f)] private float bobFrequency = 0.9f;
    [SerializeField, Min(0f)] private float tiltAngle = 5f;
    [Tooltip("ความลึกที่ทุ่นจุ่มลงตอนตกกระทบน้ำ ก่อนเด้งกลับ")]
    [SerializeField, Min(0f)] private float landingDip = 0.1f;

    [Header("Fish Bite")]
    [SerializeField, Min(0f)] private float minBiteDelay = 3f;
    [SerializeField, Min(0f)] private float maxBiteDelay = 8f;
    [SerializeField, Min(0f)] private float biteSinkDepth = 0.12f;
    [SerializeField, Min(0f)] private float biteJerkAmount = 0.03f;
    [SerializeField, Min(0f)] private float biteJerkFrequency = 7f;

    [Header("Effects")]
    [Tooltip("Prefab เอฟเฟกต์น้ำกระจาย (ParticleSystem) จะถูกสร้างที่ผิวน้ำ")]
    [SerializeField] private ParticleSystem splashPrefab;
    [Tooltip("เสียงตอนทุ่นตกน้ำ ถ้าว่างจะใช้เสียงที่สร้างขึ้นเอง")]
    [SerializeField] private AudioClip landClip;
    [Tooltip("เสียงตอนปลากินเบ็ด ถ้าว่างจะใช้เสียงที่สร้างขึ้นเอง")]
    [SerializeField] private AudioClip biteClip;
    [SerializeField, Range(0f, 1f)] private float volume = 0.8f;

    [Header("Events")]
    public UnityEvent onLanded;
    public UnityEvent onFishBite;

    private Rigidbody rb;
    private AudioSource audioSource;
    private Vector3 restPosition;
    private Quaternion restRotation;
    private float stateTime;
    private float biteDelay;
    private float noiseSeed;

    private static AudioClip proceduralSplash;

    public BobberState State { get; private set; } = BobberState.Flying;
    /// <summary>ตำแหน่งที่ทุ่นลอยอยู่บนผิวน้ำ (ก่อนบวกการกระเพื่อม)</summary>
    public Vector3 RestPosition => restPosition;

    /// <summary>ย้ายจุดลอยของทุ่นในแนวราบ (ใช้ตอนดึงสู้กับปลา) ระดับน้ำไม่เปลี่ยน</summary>
    public void SetRestPositionXZ(Vector3 position)
    {
        restPosition = new Vector3(position.x, restPosition.y, position.z);
    }

    /// <summary>เวลาที่เหลือก่อนปลากิน (วินาที) ใช้แสดงผล/ดีบัก</summary>
    public float TimeUntilBite => State == BobberState.Floating ? Mathf.Max(0f, biteDelay - stateTime) : 0f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        noiseSeed = Random.value * 100f;
    }

    private void OnTriggerEnter(Collider other) => TryLand(other);

    private void OnCollisionEnter(Collision collision) => TryLand(collision.collider);

    private void TryLand(Collider other)
    {
        if (State != BobberState.Flying || !other.CompareTag(waterTag)) return;

        float surfaceY = other.bounds.max.y;

        // หยุดแรงตก: ปิดฟิสิกส์แล้วควบคุมตำแหน่งเอง
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.useGravity = false;
        rb.isKinematic = true;

        Vector3 p = transform.position;
        restPosition = new Vector3(p.x, surfaceY + floatOffset, p.z);
        restRotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        State = BobberState.Floating;
        stateTime = 0f;
        biteDelay = Random.Range(minBiteDelay, Mathf.Max(minBiteDelay, maxBiteDelay));

        // ใช้เหยื่อที่ติดตั้งไว้ 1 ชิ้น (ปลากินเร็วขึ้น / ปลาหายากขึ้น)
        Bait bait = GameState.Instance.ConsumeEquippedBait();
        Bait.OnHook = bait;
        if (bait != null) biteDelay *= bait.BiteDelayMultiplier;

        SpawnSplash(new Vector3(p.x, surfaceY, p.z), 0.7f);
        PlaySound(landClip, 1.15f, 0.6f);
        onLanded?.Invoke();
    }

    private void Update()
    {
        if (State == BobberState.Flying) return;

        stateTime += Time.deltaTime;
        float t = Time.time + noiseSeed;

        // กระเพื่อมเบาๆ: คลื่นหลักผสมคลื่นรองให้ดูไม่ซ้ำ
        float bob = (Mathf.Sin(t * bobFrequency * Mathf.PI * 2f) + 0.4f * Mathf.Sin(t * bobFrequency * 4.7f)) / 1.4f * bobAmplitude;
        float y = restPosition.y + bob;

        if (State == BobberState.Floating)
        {
            // ตอนตกน้ำจะจุ่มลงแล้วเด้งกลับแบบสปริงหน่วง
            y -= landingDip * Mathf.Exp(-stateTime * 4f) * Mathf.Cos(stateTime * 11f);

            if (stateTime >= biteDelay) EnterFishBite();
        }
        else // FishBite
        {
            float sink = biteSinkDepth * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(stateTime / 0.15f));
            float jerk = Mathf.Sin(stateTime * biteJerkFrequency * Mathf.PI * 2f) * biteJerkAmount;
            y -= sink + Mathf.Abs(jerk);
        }

        float tiltX = Mathf.Sin(t * 1.3f) * tiltAngle;
        float tiltZ = Mathf.Cos(t * 1.1f) * tiltAngle;
        if (State == BobberState.FishBite) tiltX += Mathf.Sin(stateTime * biteJerkFrequency * Mathf.PI * 2f) * tiltAngle * 2f;

        Vector3 pos = new Vector3(restPosition.x, y, restPosition.z);
        Quaternion rot = restRotation * Quaternion.Euler(tiltX, 0f, tiltZ);
        transform.SetPositionAndRotation(pos, rot);
        rb.position = pos; // ให้สายเบ็ด (อ่าน rb.position) ตามทุ่นทันที
        rb.rotation = rot;
    }

    private void EnterFishBite()
    {
        State = BobberState.FishBite;
        stateTime = 0f;

        SpawnSplash(new Vector3(restPosition.x, restPosition.y - floatOffset, restPosition.z), 1.4f);
        PlaySound(biteClip, 0.8f, 1f);
        Debug.Log("[FishingBobber] ปลากินเบ็ด!");
        onFishBite?.Invoke();
    }

    private void SpawnSplash(Vector3 position, float scale)
    {
        if (splashPrefab == null) return;
        ParticleSystem splash = Instantiate(splashPrefab, position, Quaternion.identity);
        splash.transform.localScale = Vector3.one * scale;
        splash.Play(true);
        Destroy(splash.gameObject, 3f);
    }

    private void PlaySound(AudioClip clip, float pitch, float volumeScale)
    {
        if (clip == null) clip = GetProceduralSplash();
        audioSource.pitch = pitch * Random.Range(0.93f, 1.07f);
        audioSource.PlayOneShot(clip, volume * volumeScale);
    }

    /// <summary>สร้างเสียงน้ำกระจายแบบ procedural (noise + ฟองอากาศ) ใช้เมื่อยังไม่มีไฟล์เสียง</summary>
    private static AudioClip GetProceduralSplash()
    {
        if (proceduralSplash != null) return proceduralSplash;

        const int rate = 44100;
        int length = (int)(rate * 0.45f);
        var data = new float[length];
        var random = new System.Random(1234);
        float low = 0f;

        for (int i = 0; i < length; i++)
        {
            float time = i / (float)rate;
            float noise = (float)(random.NextDouble() * 2.0 - 1.0);
            float cutoff = Mathf.Lerp(0.5f, 0.08f, time / 0.45f);
            low += (noise - low) * cutoff;

            float envelope = Mathf.Clamp01(time / 0.005f) * Mathf.Exp(-time * 11f);
            float bubbles = 0f;
            for (int b = 0; b < 3; b++)
            {
                float start = 0.04f + b * 0.07f;
                if (time < start) continue;
                float bt = time - start;
                float freq = 520f + b * 230f + bt * 2600f;
                bubbles += Mathf.Sin(2f * Mathf.PI * freq * bt) * Mathf.Exp(-bt * 38f) * 0.35f;
            }
            data[i] = Mathf.Clamp(low * envelope * 1.6f + bubbles, -1f, 1f);
        }

        proceduralSplash = AudioClip.Create("ProceduralSplash", length, 1, rate, false);
        proceduralSplash.SetData(data, 0);
        return proceduralSplash;
    }
}
