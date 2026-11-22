using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// HUD หลัก: วันที่และเวลาในเกม, จำนวนเหรียญ, ข้อความ [E] เมื่ออยู่ใกล้สิ่งที่โต้ตอบได้,
/// ข้อความแจ้งเตือน (วันใหม่ / ขายปลาได้เงิน) และปุ่มลัดด้านล่าง
/// </summary>
public class GameHUD : MonoBehaviour
{
    public static GameHUD Instance { get; private set; }

    private Text dayText;
    private Text clockText;
    private Text coinsText;
    private Text coinDeltaText;
    private Text baitText;
    private Image baitIcon;
    private Image baitHook;
    private GameObject promptRoot;
    private Text promptText;
    private Text toastText;
    private float toastTimer;
    private float coinDeltaTimer;
    private float shownCoins;
    private int lastCoins;
    private PlayerInteractor interactor;

    private void Awake()
    {
        Instance = this;
        BuildUI();
    }

    private void Start()
    {
        GameState state = GameState.Instance;
        state.CoinsChanged += OnCoinsChanged;
        state.DayChanged += OnDayChanged;
        shownCoins = lastCoins = state.Coins;
        interactor = FindAnyObjectByType<PlayerInteractor>();

        if (!state.HasReadBoardToday && FindAnyObjectByType<NoticeBoard>() != null)
            ShowToast("ราคาปลาวันนี้ประกาศแล้วที่ป้ายประกาศกลางหมู่บ้าน", new Color(1f, 0.9f, 0.6f));
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (!GameState.Exists) return;
        GameState.Instance.CoinsChanged -= OnCoinsChanged;
        GameState.Instance.DayChanged -= OnDayChanged;
    }

    private void Update()
    {
        GameState state = GameState.Instance;
        dayText.text = $"วันที่ {state.Day}";
        clockText.text = state.ClockText;

        shownCoins = Mathf.MoveTowards(shownCoins, state.Coins, Mathf.Max(20f, Mathf.Abs(state.Coins - shownCoins) * 4f) * Time.deltaTime);
        coinsText.text = $"{Mathf.RoundToInt(shownCoins):N0} เหรียญ";

        Interactable current = interactor != null ? interactor.Current : null;
        promptRoot.SetActive(current != null);
        if (current != null) promptText.text = $"<color=#ffd76b>[E]</color>  {current.Prompt}";

        // เหยื่อที่ใช้อยู่ + กด B สลับ
        Bait bait = state.EquippedBait;
        baitText.text = bait != null
            ? $"เหยื่อ: <b>{bait.Name}</b> x{state.BaitCount(bait.Id)}  [B]"
            : "เหยื่อ: ไม่มี  [B]";
        bool showBaitIcon = bait != null;
        baitIcon.enabled = showBaitIcon;
        baitHook.enabled = !showBaitIcon;
        if (showBaitIcon) baitIcon.sprite = UISkin.BaitIcon(bait.Id);
        Keyboard kb = Keyboard.current;
        if (kb != null && kb.bKey.wasPressedThisFrame && !PlayerInputGate.IsBlocked)
        {
            Bait next = state.CycleBait();
            ShowToast(next != null ? $"เปลี่ยนเหยื่อเป็น {next.Name} (เหลือ {state.BaitCount(next.Id)})"
                : "ไม่ใช้เหยื่อ (ซื้อเหยื่อได้ที่ร้านเหยื่อในหมู่บ้าน)", new Color(1f, 0.9f, 0.6f), 2f);
        }

        if (toastTimer > 0f)
        {
            toastTimer -= Time.deltaTime;
            Color c = toastText.color;
            c.a = Mathf.Clamp01(toastTimer / 0.6f);
            toastText.color = c;
        }

        if (coinDeltaTimer > 0f)
        {
            coinDeltaTimer -= Time.deltaTime;
            Color c = coinDeltaText.color;
            c.a = Mathf.Clamp01(coinDeltaTimer / 0.5f);
            coinDeltaText.color = c;
        }
    }

    public void ShowToast(string message, Color color, float seconds = 3.5f)
    {
        toastText.text = message;
        toastText.color = color;
        toastTimer = seconds;
    }

    private void OnCoinsChanged()
    {
        int delta = GameState.Instance.Coins - lastCoins;
        lastCoins = GameState.Instance.Coins;
        if (delta == 0) return;
        coinDeltaText.text = delta > 0 ? $"+{delta:N0}" : $"{delta:N0}";
        coinDeltaText.color = delta > 0 ? new Color(0.55f, 1f, 0.55f) : new Color(1f, 0.55f, 0.5f);
        coinDeltaTimer = 2f;
    }

    private void OnDayChanged(int day)
    {
        ShowToast($"เช้าวันใหม่! วันที่ {day} — ราคาปลาในตลาดเปลี่ยนแล้ว", new Color(1f, 0.9f, 0.6f), 5f);
    }

    // ------------------------------------------------------------------ Build

    private void BuildUI()
    {
        Canvas canvas = UIFactory.CreateCanvas(transform, "HUD Canvas", 5);
        canvas.GetComponent<GraphicRaycaster>().enabled = false;

        // มุมซ้ายบน: วัน เวลา เหรียญ
        // ป้ายสถานะ: กรอบไม้ + กระดาษ (วัน เวลา เหรียญ เหยื่อ)
        Vector2 panelSize = new Vector2(380f, 262f);
        Image wood = UISkin.Sliced("Status", canvas.transform, UISkin.Wood(22), panelSize, Vector2.zero, Color.white);
        RectTransform panel = wood.rectTransform;
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0f, 1f);
        panel.anchoredPosition = new Vector2(20f, -20f);
        Transform paper = UISkin.Sliced("Paper", panel, UISkin.ParchmentSheet(12), panelSize - new Vector2(36f, 36f), Vector2.zero, Color.white).transform;
        foreach (float sx in new[] { -1f, 1f })
        foreach (float sy in new[] { -1f, 1f })
        {
            Image fish = UISkin.Sliced("Fish", panel, UISkin.FishOrnament, new Vector2(64f, 32f),
                new Vector2(sx * (panelSize.x / 2f - 22f), sy * (panelSize.y / 2f - 14f)), new Color(0.72f, 0.52f, 0.32f));
            fish.rectTransform.localRotation = Quaternion.Euler(0f, sx > 0f ? 0f : 180f, sy > 0f ? -35f : 35f);
        }

        dayText = UIFactory.Label("Day", paper, "", 44, TextAnchor.MiddleLeft, new Vector2(200f, 60f), new Vector2(-50f, 68f), UISkin.Ink, FontStyle.Bold);
        clockText = UIFactory.Label("Clock", paper, "", 34, TextAnchor.MiddleRight, new Vector2(130f, 56f), new Vector2(92f, 66f), UISkin.Ink);

        Image coinBar = UISkin.Sliced("CoinBar", paper, UISkin.Wood(14), new Vector2(256f, 56f), new Vector2(30f, 2f), new Color(0.82f, 0.75f, 0.7f));
        coinsText = UIFactory.Label("Coins", coinBar.transform, "", 30, TextAnchor.MiddleLeft, new Vector2(196f, 52f), new Vector2(18f, 0f), new Color(1f, 0.85f, 0.42f), FontStyle.Bold);
        UISkin.Shadowed(coinsText, new Color(0f, 0f, 0f, 0.6f));
        UISkin.Sliced("CoinIcon", paper, UISkin.GoldCoin, new Vector2(72f, 72f), new Vector2(-112f, 2f), Color.white);
        coinDeltaText = UIFactory.Label("CoinDelta", panel, "", 28, TextAnchor.MiddleLeft, new Vector2(160f, 40f), new Vector2(panelSize.x / 2f + 90f, 2f), Color.clear, FontStyle.Bold);
        var deltaOutline = coinDeltaText.gameObject.AddComponent<Outline>();
        deltaOutline.effectColor = new Color(0f, 0f, 0f, 0.7f);

        baitText = UIFactory.Label("Bait", paper, "", 24, TextAnchor.MiddleLeft, new Vector2(230f, 40f), new Vector2(-52f, -70f), UISkin.Ink);
        baitText.resizeTextForBestFit = true;
        baitText.resizeTextMinSize = 16;
        baitText.resizeTextMaxSize = 24;
        Image baitBox = UISkin.Sliced("BaitBox", paper, UISkin.Wood(12), new Vector2(72f, 72f), new Vector2(118f, -66f), Color.white);
        baitIcon = UISkin.Sliced("BaitIcon", baitBox.transform, UISkin.BaitIcon("worm"), new Vector2(54f, 54f), Vector2.zero, Color.white);
        baitHook = UISkin.Sliced("Hook", baitBox.transform, UISkin.Hook, new Vector2(30f, 40f), Vector2.zero, UISkin.Cream);

        // ข้อความ [E] ด้านล่างกลาง
        Image prompt = UIFactory.Panel("Prompt", canvas.transform, new Vector2(620f, 58f), Vector2.zero, new Color(0.06f, 0.09f, 0.09f, 0.8f));
        prompt.rectTransform.anchorMin = prompt.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        prompt.rectTransform.anchoredPosition = new Vector2(0f, 270f);
        promptRoot = prompt.gameObject;
        promptText = UIFactory.Label("Text", prompt.transform, "", 28, TextAnchor.MiddleCenter, new Vector2(600f, 58f), Vector2.zero);
        promptRoot.SetActive(false);

        // ข้อความแจ้งเตือนด้านบน
        toastText = UIFactory.Label("Toast", canvas.transform, "", 34, TextAnchor.MiddleCenter, new Vector2(1400f, 60f), Vector2.zero, Color.clear, FontStyle.Bold);
        toastText.rectTransform.anchorMin = toastText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        toastText.rectTransform.anchoredPosition = new Vector2(0f, -70f);
        var outline = toastText.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.7f);
        outline.effectDistance = new Vector2(2f, -2f);

        // ปุ่มลัด
        Text hint = UIFactory.Label("Hint", canvas.transform,
            "WASD เดิน · Shift วิ่ง · คลิกซ้าย เหวี่ยง/ดึงเบ็ด · คลิกขวา หมุนกล้อง · Tab กระเป๋า · 1 เก็บคันเบ็ด · B สลับเหยื่อ · E โต้ตอบ/คุย",
            18, TextAnchor.MiddleLeft, new Vector2(1200f, 30f), Vector2.zero, new Color(1f, 1f, 1f, 0.55f));
        hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = hint.rectTransform.pivot = new Vector2(0f, 0f);
        hint.rectTransform.anchoredPosition = new Vector2(24f, 16f);
    }
}
