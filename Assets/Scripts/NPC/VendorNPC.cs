using UnityEngine;

/// <summary>ร้านที่พ่อค้าแต่ละคนเปิดเมื่อคุยด้วย</summary>
public enum VendorShop
{
    FishMarket,
    BaitShop,
}

/// <summary>
/// NPC พ่อค้า/แม่ค้าที่ยืนประจำแผง:
/// - หันมามองผู้เล่นเมื่อเดินเข้าใกล้ และตะโกนเรียกลูกค้าพร้อมโบกมือ
/// - กด E คุยแล้วจะทักทาย (ลูกโป่งคำพูด) และเปิดหน้าต่างร้าน
/// - ท่ายืนหายใจ แกว่งแขนเบาๆ ไม่เดินออกจากแผง
/// </summary>
public class VendorNPC : Interactable
{
    [Header("Vendor")]
    [SerializeField] private string npcName = "พ่อค้า";
    [SerializeField] private VendorShop shop = VendorShop.FishMarket;
    [Tooltip("คำทักทายตอนกด E (สุ่ม)")]
    [SerializeField, TextArea] private string[] greetings = { "สวัสดีจ้า วันนี้รับอะไรดี?" };
    [Tooltip("คำตะโกนเรียกลูกค้าตอนผู้เล่นเดินผ่าน (สุ่ม)")]
    [SerializeField, TextArea] private string[] callouts = { "แวะก่อนจ้า!" };

    [Header("Body parts (หาจากชื่อลูกอัตโนมัติถ้าเว้นว่าง)")]
    [SerializeField] private Transform body;
    [SerializeField] private Transform head;
    [SerializeField] private Transform armLeft;
    [SerializeField] private Transform armRight;

    [Header("Behaviour")]
    [SerializeField, Min(1f)] private float noticeRange = 7f;
    [SerializeField, Min(0f)] private float turnSpeed = 140f;
    [SerializeField, Min(0f)] private float maxTurnFromHome = 70f;
    [SerializeField, Min(0f)] private float calloutCooldown = 25f;
    [SerializeField] private float bubbleHeight = 2.55f;

    private Quaternion homeRotation;
    private Quaternion headRest;
    private Transform player;
    private SpeechBubble bubble;
    private float waveTimer;
    private float nextCalloutTime;
    private bool playerWasNear;
    private float seed;

    public string NpcName => npcName;
    public VendorShop Shop => shop;
    public override string Prompt => $"คุยกับ{npcName}";

    private void Awake()
    {
        if (body == null) body = transform.Find("Body");
        Transform root = body != null ? body : transform;
        if (head == null) head = FindDeep(root, "Head Pivot");
        if (armLeft == null) armLeft = FindDeep(root, "Arm_L");
        if (armRight == null) armRight = FindDeep(root, "Arm_R");

        homeRotation = transform.rotation;
        if (head != null) headRest = head.localRotation;
        seed = Random.value * 10f;

        bubble = SpeechBubble.Create(transform, Vector3.up * bubbleHeight);
    }

    private void Start()
    {
        var movement = FindAnyObjectByType<PlayerMovement>();
        if (movement != null) player = movement.transform;
        nextCalloutTime = Time.time + 2f;
    }

    public override void Interact(PlayerInteractor interactor)
    {
        if (player == null) player = interactor.transform;
        Say(Pick(greetings));
        waveTimer = 1.2f;
        OpenShop();
    }

    /// <summary>ให้ NPC พูด (แสดงลูกโป่งคำพูดเหนือหัว)</summary>
    public void Say(string line, float seconds = 3.5f)
    {
        if (!string.IsNullOrEmpty(line)) bubble.Show(line, seconds);
    }

    private void OpenShop()
    {
        switch (shop)
        {
            case VendorShop.FishMarket:
                if (MarketUI.Instance != null) MarketUI.Instance.SetOpen(true);
                break;
            case VendorShop.BaitShop:
                BaitShopUI.GetOrCreate().Open(this);
                break;
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        float t = Time.time + seed;

        // ---- หันมองผู้เล่น
        bool near = false;
        Quaternion target = homeRotation;
        if (player != null)
        {
            Vector3 to = player.position - transform.position;
            to.y = 0f;
            near = to.magnitude < noticeRange;
            if (near && to.sqrMagnitude > 0.01f)
            {
                Quaternion look = Quaternion.LookRotation(to, Vector3.up);
                float angle = Quaternion.Angle(homeRotation, look);
                target = angle <= maxTurnFromHome ? look : Quaternion.Slerp(homeRotation, look, maxTurnFromHome / angle);
            }
        }
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * dt);

        // ---- ตะโกนเรียกลูกค้าตอนผู้เล่นเพิ่งเดินเข้ามาใกล้
        if (near && !playerWasNear && Time.time >= nextCalloutTime && !bubble.IsShowing)
        {
            Say(Pick(callouts), 3f);
            waveTimer = 1.4f;
            nextCalloutTime = Time.time + calloutCooldown;
        }
        playerWasNear = near;

        // ---- ท่ายืน: หายใจ แกว่งแขนเบาๆ
        if (body != null)
        {
            float breath = Mathf.Sin(t * 1.9f);
            body.localScale = new Vector3(1f - breath * 0.006f, 1f + breath * 0.012f, 1f - breath * 0.006f);
        }

        if (head != null)
        {
            // เอียงหัวเล็กน้อยแบบสุ่ม และพยักหน้าตอนพูด
            float nod = bubble.IsShowing ? Mathf.Sin(t * 7f) * 4f : 0f;
            head.localRotation = headRest * Quaternion.Euler(Mathf.Sin(t * 0.7f) * 3f + nod, Mathf.Sin(t * 0.43f) * 12f, Mathf.Sin(t * 0.31f) * 3f);
        }

        if (armLeft != null)
            armLeft.localRotation = Quaternion.Euler(Mathf.Sin(t * 1.3f) * 4f, 0f, -4f - Mathf.Sin(t * 1.9f) * 1.5f);

        if (armRight != null)
        {
            waveTimer = Mathf.Max(0f, waveTimer - dt);
            float wave = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Min(waveTimer, 1.4f - waveTimer + 0.2f) * 4f));
            Quaternion idle = Quaternion.Euler(Mathf.Sin(t * 1.3f + 1.4f) * 4f, 0f, 4f + Mathf.Sin(t * 1.9f) * 1.5f);
            Quaternion waving = Quaternion.Euler(0f, 0f, 150f + Mathf.Sin(t * 14f) * 18f);
            armRight.localRotation = Quaternion.Slerp(idle, waving, wave);
        }
    }

    private static string Pick(string[] lines) => lines == null || lines.Length == 0 ? null : lines[Random.Range(0, lines.Length)];

    private static Transform FindDeep(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

#if UNITY_EDITOR
    public void EditorSetupVendor(string displayName, VendorShop shopKind, string[] greetingLines, string[] calloutLines)
    {
        npcName = displayName;
        shop = shopKind;
        greetings = greetingLines;
        callouts = calloutLines;
    }
#endif
}
