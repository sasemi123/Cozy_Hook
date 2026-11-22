using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// หน้าต่างแสดงผลเมื่อจับปลาได้: ภาพ 3D ของปลาหมุนช้าๆ, ชื่อ, ความหายาก (สี + ดาว), น้ำหนัก
/// และสถานะการเก็บเข้ากระเป๋า ปิดเองอัตโนมัติ หรือกดปุ่ม "ตกลง" / Space / Enter
/// </summary>
public class CatchResultUI : MonoBehaviour
{
    [SerializeField] private FishCatchManager catchManager;
    [Tooltip("ปิดหน้าต่างเองหลังจากกี่วินาที (0 = ไม่ปิดเอง)")]
    [SerializeField, Min(0f)] private float autoCloseSeconds = 7f;

    private GameObject window;
    private CanvasGroup group;
    private Image frame;
    private RawImage preview;
    private Text nameText;
    private Text rarityText;
    private Text weightText;
    private Text statusText;
    private Text chanceText;
    private float closeTimer;
    private bool isOpen;

    public bool IsOpen => isOpen;

    private void Awake()
    {
        if (catchManager == null) catchManager = FindAnyObjectByType<FishCatchManager>();
        UIFactory.EnsureEventSystem();
        BuildUI();
        window.SetActive(false);
    }

    private void OnEnable()
    {
        if (catchManager != null) catchManager.FishCaught += Show;
    }

    private void OnDisable()
    {
        if (catchManager != null) catchManager.FishCaught -= Show;
    }

    private void Update()
    {
        if (!isOpen) return;

        group.alpha = Mathf.MoveTowards(group.alpha, 1f, Time.unscaledDeltaTime * 5f);
        window.transform.localScale = Vector3.one * Mathf.Lerp(window.transform.localScale.x, 1f, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));

        Keyboard kb = Keyboard.current;
        if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)) Close();

        if (autoCloseSeconds > 0f)
        {
            closeTimer -= Time.unscaledDeltaTime;
            if (closeTimer <= 0f) Close();
        }
    }

    public void Show(CaughtFish fish, bool addedToBag)
    {
        FishData data = fish.Data;
        Color rarityColor = FishRarityInfo.Color(data.Rarity);

        frame.color = rarityColor;
        preview.texture = FishPreviewRenderer.Instance.StartLive(data);
        nameText.text = data.FishName;
        rarityText.text = FishRarityInfo.Label(data.Rarity);
        rarityText.color = rarityColor;
        weightText.text = $"น้ำหนัก <b>{fish.Weight:F2}</b> กก.  <size=20><color=#9fb0ad>({data.MinWeight:0.##}-{data.MaxWeight:0.##} กก.)</color></size>";
        chanceText.text = catchManager != null ? $"โอกาสพบระดับนี้ {catchManager.ChancePercent(data.Rarity):0.#}%" : "";

        FishInventory bag = catchManager != null ? catchManager.Inventory : null;
        statusText.text = addedToBag
            ? $"<color=#8fe39a>เก็บเข้ากระเป๋าแล้ว</color>  ({bag?.Count}/{bag?.Capacity})"
            : "<color=#ff8a7a>กระเป๋าเต็ม! ปล่อยปลากลับลงน้ำไปแล้ว</color>";

        window.SetActive(true);
        group.alpha = 0f;
        window.transform.localScale = Vector3.one * 0.85f;
        closeTimer = autoCloseSeconds;
        isOpen = true;
    }

    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;
        window.SetActive(false);
        FishPreviewRenderer.Instance.StopLive();
    }

    private void BuildUI()
    {
        Canvas canvas = UIFactory.CreateCanvas(transform, "Catch Result Canvas", 20);

        frame = UIFactory.Panel("Catch Window", canvas.transform, new Vector2(620f, 680f), new Vector2(0f, -20f), Color.white);
        window = frame.gameObject;
        group = window.AddComponent<CanvasGroup>();
        Transform inner = UIFactory.Stretch(UIFactory.Panel("Inner", window.transform, Vector2.zero, Vector2.zero, UIFactory.PanelColor).rectTransform, 5f);

        UIFactory.Label("Title", inner, "จับปลาได้!", 40, TextAnchor.MiddleCenter, new Vector2(560f, 60f), new Vector2(0f, 270f), new Color(1f, 0.93f, 0.7f), FontStyle.Bold);

        UIFactory.Panel("Preview Bg", inner, new Vector2(420f, 300f), new Vector2(0f, 90f), UIFactory.PanelLight);
        preview = UIFactory.RawImage("Preview", inner, new Vector2(400f, 300f), new Vector2(0f, 90f));

        nameText = UIFactory.Label("Name", inner, "", 44, TextAnchor.MiddleCenter, new Vector2(560f, 60f), new Vector2(0f, -100f), Color.white, FontStyle.Bold);
        rarityText = UIFactory.Label("Rarity", inner, "", 28, TextAnchor.MiddleCenter, new Vector2(560f, 40f), new Vector2(0f, -145f));
        weightText = UIFactory.Label("Weight", inner, "", 28, TextAnchor.MiddleCenter, new Vector2(560f, 40f), new Vector2(0f, -185f));
        chanceText = UIFactory.Label("Chance", inner, "", 18, TextAnchor.MiddleCenter, new Vector2(560f, 30f), new Vector2(0f, -218f), new Color(0.62f, 0.69f, 0.68f));
        statusText = UIFactory.Label("Status", inner, "", 22, TextAnchor.MiddleCenter, new Vector2(560f, 34f), new Vector2(0f, -248f));

        UIFactory.Button("OK Button", inner, "ตกลง", new Vector2(200f, 50f), new Vector2(0f, -298f), UIFactory.ButtonColor, Close);
    }
}
