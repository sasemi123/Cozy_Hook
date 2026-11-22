using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// คันเบ็ด 3D: กดคลิกซ้ายค้างเพื่อสะสมพลัง ปล่อยเพื่อเหวี่ยงทุ่นออกจากปลายคัน
/// และวาดสายเบ็ดด้วย LineRenderer ระหว่างปลายคันกับทุ่น
/// วางสคริปต์นี้บน GameObject ของคันเบ็ด
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class FishingRodCaster : MonoBehaviour
{
    [Header("References")]
    [Tooltip("จุดปลายคันเบ็ด ที่ทุ่นจะถูกเสกออกมาและสายเบ็ดเริ่มต้น")]
    [SerializeField] private Transform rodTip;

    [Tooltip("Prefab ของทุ่น (ต้องมี Rigidbody และ Collider)")]
    [SerializeField] private Rigidbody bobberPrefab;

    [Tooltip("ทิศทางการเหวี่ยงอ้างอิงจาก Transform นี้ (ปกติคือกล้องหรือตัวผู้เล่น) ถ้าว่างจะใช้ตัวคันเบ็ด")]
    [SerializeField] private Transform aimReference;

    [Header("Charge")]
    [Tooltip("เวลา (วินาที) ที่ต้องกดค้างจนพลังเต็ม")]
    [SerializeField, Min(0.05f)] private float maxChargeTime = 1.5f;

    [Tooltip("รูปทรงการเพิ่มพลังตามเวลา (แกน X = เวลา 0-1, แกน Y = พลัง 0-1)")]
    [SerializeField] private AnimationCurve chargeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Cast Force")]
    [SerializeField, Min(0f)] private float minForwardForce = 3f;
    [SerializeField, Min(0f)] private float maxForwardForce = 15f;
    [SerializeField, Min(0f)] private float minUpwardForce = 2f;
    [SerializeField, Min(0f)] private float maxUpwardForce = 7f;

    [Header("Fishing Line")]
    [Tooltip("จำนวนจุดของเส้น ยิ่งมากยิ่งโค้งเนียน")]
    [SerializeField, Range(2, 64)] private int lineSegments = 20;

    [Tooltip("ความหย่อนของสาย (เมตรต่อระยะ 1 เมตร) 0 = เส้นตรง")]
    [SerializeField, Min(0f)] private float lineSag = 0.08f;

    [SerializeField, Min(0.001f)] private float lineWidth = 0.01f;

    [Header("Reel In")]
    [Tooltip("ความเร็วตอนเริ่มม้วนสาย (เมตร/วินาที) จะเร่งขึ้นเรื่อยๆ")]
    [SerializeField, Min(0.1f)] private float reelSpeed = 6f;
    [SerializeField, Min(0f)] private float reelAcceleration = 18f;
    [Tooltip("ระยะจากปลายคันที่ถือว่าเก็บทุ่นเสร็จ แล้วทุ่นจะหายไป")]
    [SerializeField, Min(0.01f)] private float reelFinishDistance = 0.2f;

    private LineRenderer line;
    private Rigidbody currentBobber;
    private bool isCharging;
    private float chargeTimer;
    private bool isReeling;
    private float currentReelSpeed;
    private FishingLineTension lineTension;

    /// <summary> พลังปัจจุบัน 0-1 (เอาไปใช้กับ UI แถบพลังได้) </summary>
    public float ChargePercent => isCharging ? chargeCurve.Evaluate(Mathf.Clamp01(chargeTimer / maxChargeTime)) : 0f;
    public bool IsCharging => isCharging;
    public Rigidbody CurrentBobber => currentBobber;
    public bool IsReeling => isReeling;

    private void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.startWidth = lineWidth;
        line.endWidth = lineWidth;
        line.positionCount = 0;

        if (aimReference == null) aimReference = transform;
        lineTension = GetComponent<FishingLineTension>();
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || rodTip == null || bobberPrefab == null) return;

        // ระหว่างสู้กับปลา คลิกซ้ายเป็นของระบบแรงตึงสาย
        bool fishOnLine = lineTension != null && lineTension.WantsInput;

        if (mouse.leftButton.wasPressedThisFrame && !isReeling && !fishOnLine && PlayerInputGate.CanUseMouse)
        {
            // มีทุ่นอยู่ = คลิกเพื่อดึงทุ่นกลับ, ไม่มีทุ่น = เริ่มง้างเหวี่ยง
            if (currentBobber != null) ReelIn();
            else StartCharge();
        }

        if (isReeling) UpdateReel();

        if (isCharging)
        {
            chargeTimer += Time.deltaTime;

            if (mouse.leftButton.wasReleasedThisFrame)
            {
                Cast(ChargePercent);
            }
        }
    }

    private void LateUpdate()
    {
        // วาดสายหลังจากฟิสิกส์และแอนิเมชันอัปเดตแล้ว เพื่อไม่ให้เส้นสั่น
        DrawLine();
    }

    private void StartCharge()
    {
        ReelInImmediate();
        isCharging = true;
        chargeTimer = 0f;
    }

    private void Cast(float power)
    {
        isCharging = false;

        currentBobber = Instantiate(bobberPrefab, rodTip.position, rodTip.rotation);

        // ใช้ทิศไปข้างหน้าในแนวราบ แล้วค่อยบวกแรงขึ้นด้านบนแยก เพื่อให้ควบคุมวิถีได้ง่าย
        Vector3 forward = Vector3.ProjectOnPlane(aimReference.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.0001f) forward = transform.forward;

        float forwardForce = Mathf.Lerp(minForwardForce, maxForwardForce, power);
        float upwardForce = Mathf.Lerp(minUpwardForce, maxUpwardForce, power);

        Vector3 impulse = forward * forwardForce + Vector3.up * upwardForce;
        currentBobber.AddForce(impulse, ForceMode.Impulse);
    }

    /// <summary>เริ่มม้วนสายดึงทุ่นกลับมาที่ปลายคัน เมื่อถึงแล้วทุ่นจะหายไป</summary>
    public void ReelIn()
    {
        if (currentBobber == null || isReeling) return;

        isReeling = true;
        currentReelSpeed = reelSpeed;

        // หยุดการลอยน้ำ/ฟิสิกส์ของทุ่น แล้วควบคุมตำแหน่งเอง
        var floatScript = currentBobber.GetComponent<FishingBobber>();
        if (floatScript != null) floatScript.enabled = false;
        currentBobber.isKinematic = true;
        currentBobber.useGravity = false;
    }

    /// <summary>เก็บทุ่นทันที (ลบทุ่นและซ่อนสาย) และยกเลิกการง้าง</summary>
    public void ReelInImmediate()
    {
        isCharging = false;
        isReeling = false;
        if (currentBobber != null)
        {
            Destroy(currentBobber.gameObject);
            currentBobber = null;
        }
        if (line != null) line.positionCount = 0;
    }

    private void UpdateReel()
    {
        if (currentBobber == null)
        {
            isReeling = false;
            return;
        }

        currentReelSpeed += reelAcceleration * Time.deltaTime;
        Vector3 target = rodTip.position;
        Vector3 pos = Vector3.MoveTowards(currentBobber.position, target, currentReelSpeed * Time.deltaTime);
        currentBobber.transform.position = pos;
        currentBobber.position = pos;

        if (Vector3.Distance(pos, target) <= reelFinishDistance) ReelInImmediate();
    }

    private void OnDisable()
    {
        // เก็บคันเบ็ด/ปิดสคริปต์ = เก็บทุ่นทันที
        ReelInImmediate();
    }

    private void DrawLine()
    {
        if (currentBobber == null)
        {
            line.positionCount = 0;
            return;
        }

        Vector3 start = rodTip.position;
        Vector3 end = currentBobber.position;

        line.positionCount = lineSegments;

        // จุดควบคุมกลางสายถูกดึงลงตามระยะทาง ทำให้สายดูหย่อน
        float distance = Vector3.Distance(start, end);
        Vector3 control = (start + end) * 0.5f + Vector3.down * (distance * lineSag);

        for (int i = 0; i < lineSegments; i++)
        {
            float t = i / (float)(lineSegments - 1);
            Vector3 a = Vector3.Lerp(start, control, t);
            Vector3 b = Vector3.Lerp(control, end, t);
            line.SetPosition(i, Vector3.Lerp(a, b, t));
        }
    }

    private void OnValidate()
    {
        if (maxForwardForce < minForwardForce) maxForwardForce = minForwardForce;
        if (maxUpwardForce < minUpwardForce) maxUpwardForce = minUpwardForce;
    }
}
