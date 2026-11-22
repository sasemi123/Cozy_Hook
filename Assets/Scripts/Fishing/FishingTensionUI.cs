using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI แสดงความตึงสายเบ็ดตอนสู้กับปลา: แถบความตึง (เขียว → เหลือง → แดง), เปอร์เซ็นต์,
/// ระยะทุ่นถึงตัว, คำแนะนำ และข้อความผลลัพธ์ (จับได้ / สายขาด / ปลาหนี)
/// สร้าง UI เองตอนเริ่มเกม วางสคริปต์นี้บน GameObject ใดก็ได้ในฉาก
/// </summary>
public class FishingTensionUI : MonoBehaviour
{
    [SerializeField] private FishingLineTension tension;
    [SerializeField] private Gradient tensionColors = DefaultGradient();
    [Tooltip("ความตึงที่ถือว่าอันตราย (แถบจะสั่นและเตือน)")]
    [SerializeField, Range(0f, 1f)] private float dangerThreshold = 0.8f;
    [SerializeField, Min(0.1f)] private float messageDuration = 2.5f;
    [Tooltip("แสดงข้อความ 'จับปลาได้แล้ว!' (ปิดได้ถ้ามีหน้าต่างผลการจับปลาแยก)")]
    [SerializeField] private bool showCaughtMessage = true;
    [Tooltip("ฟอนต์จากระบบที่รองรับภาษาไทย (เลือกตัวแรกที่มี)")]
    [SerializeField] private string[] fontNames = { "Leelawadee UI", "Tahoma", "Arial" };

    private RectTransform panel;
    private CanvasGroup panelGroup;
    private RectTransform fill;
    private Image fillImage;
    private Text percentText;
    private Text hintText;
    private Text distanceText;
    private Text messageText;
    private Vector2 panelBasePosition;
    private float messageTimer;
    private bool wasFighting;

    private void Awake()
    {
        if (tension == null) tension = FindAnyObjectByType<FishingLineTension>();
        BuildUI();
    }

    private void OnEnable()
    {
        if (tension != null) tension.FightEnded += OnFightEnded;
    }

    private void OnDisable()
    {
        if (tension != null) tension.FightEnded -= OnFightEnded;
    }

    private void Update()
    {
        bool fighting = tension != null && tension.IsFighting;
        if (fighting && !wasFighting) ShowMessage("ปลากินเบ็ด!", new Color(1f, 0.86f, 0.3f));
        wasFighting = fighting;

        panelGroup.alpha = Mathf.MoveTowards(panelGroup.alpha, fighting ? 1f : 0f, Time.deltaTime * 4f);

        if (fighting)
        {
            float t = tension.Tension01;
            fill.anchorMax = new Vector2(t, 1f);
            fillImage.color = tensionColors.Evaluate(t);
            percentText.text = Mathf.RoundToInt(t * 100f) + "%";
            distanceText.text = $"ระยะ {tension.DistanceToPlayer:F1} / {tension.CatchDistance:F0} ม.";

            if (t >= dangerThreshold) hintText.text = "<color=#ff7a6b>ตึงเกินไป! ปล่อยคลิกซ้าย</color>";
            else if (tension.IsReeling) hintText.text = "กำลังดึงทุ่นเข้ามา...";
            else hintText.text = "กดคลิกซ้ายค้างเพื่อดึง  •  ปล่อยเพื่อลดความตึง";

            float shake = t >= dangerThreshold ? 6f : tension.IsThrashing ? 2.5f : 0f;
            panel.anchoredPosition = panelBasePosition + Random.insideUnitCircle * shake;
        }
        else
        {
            panel.anchoredPosition = panelBasePosition;
        }

        if (messageTimer > 0f)
        {
            messageTimer -= Time.deltaTime;
            Color c = messageText.color;
            c.a = Mathf.Clamp01(messageTimer / 0.5f);
            messageText.color = c;
        }
    }

    private void OnFightEnded(FishingResult result)
    {
        switch (result)
        {
            case FishingResult.Caught:
                if (showCaughtMessage) ShowMessage("จับปลาได้แล้ว!", new Color(0.55f, 1f, 0.55f));
                break;
            case FishingResult.LineSnapped:
                ShowMessage("สายขาด! ปลาหลุดไปแล้ว", new Color(1f, 0.45f, 0.4f));
                break;
            default:
                ShowMessage("ปลาว่ายหนีไปแล้ว...", new Color(0.7f, 0.85f, 1f));
                break;
        }
    }

    private void ShowMessage(string text, Color color)
    {
        messageText.text = text;
        messageText.color = color;
        messageTimer = messageDuration;
    }

    // ------------------------------------------------------------------ Build

    private void BuildUI()
    {
        Font font = Font.CreateDynamicFontFromOSFont(fontNames, 32);

        var canvasGo = new GameObject("Fishing Canvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // แผงแรงตึง (ล่างกลางจอ)
        panel = NewRect("Tension Panel", canvasGo.transform, new Vector2(0.5f, 0f), new Vector2(640f, 150f), new Vector2(0f, 110f));
        panelBasePosition = panel.anchoredPosition;
        panel.gameObject.AddComponent<Image>().color = new Color(0.06f, 0.09f, 0.09f, 0.75f);
        panelGroup = panel.gameObject.AddComponent<CanvasGroup>();
        panelGroup.alpha = 0f;
        panelGroup.blocksRaycasts = false;

        NewText("Title", panel, font, "แรงตึงสายเบ็ด", 30, TextAnchor.MiddleLeft, new Vector2(-150f, 45f), new Vector2(300f, 44f));
        percentText = NewText("Percent", panel, font, "0%", 34, TextAnchor.MiddleRight, new Vector2(230f, 45f), new Vector2(120f, 44f));

        RectTransform barBg = NewRect("Bar", panel, new Vector2(0.5f, 0.5f), new Vector2(580f, 34f), new Vector2(0f, 2f));
        barBg.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

        fill = NewRect("Fill", barBg, Vector2.zero, Vector2.zero, Vector2.zero);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(0f, 1f);
        fill.offsetMin = new Vector2(3f, 3f);
        fill.offsetMax = new Vector2(-3f, -3f);
        fillImage = fill.gameObject.AddComponent<Image>();

        // เส้นบอกโซนอันตราย
        RectTransform marker = NewRect("Danger Marker", barBg, Vector2.zero, new Vector2(3f, 0f), Vector2.zero);
        marker.anchorMin = new Vector2(dangerThreshold, 0f);
        marker.anchorMax = new Vector2(dangerThreshold, 1f);
        marker.offsetMin = new Vector2(-1.5f, 0f);
        marker.offsetMax = new Vector2(1.5f, 0f);
        marker.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.7f);

        hintText = NewText("Hint", panel, font, "", 22, TextAnchor.MiddleLeft, new Vector2(-75f, -45f), new Vector2(450f, 36f));
        distanceText = NewText("Distance", panel, font, "", 22, TextAnchor.MiddleRight, new Vector2(215f, -45f), new Vector2(180f, 36f));

        // ข้อความผลลัพธ์กลางจอ
        RectTransform messageRect = NewRect("Message", canvasGo.transform, new Vector2(0.5f, 0.5f), new Vector2(1200f, 100f), new Vector2(0f, 200f));
        messageText = messageRect.gameObject.AddComponent<Text>();
        SetupText(messageText, font, "", 60, TextAnchor.MiddleCenter);
        var outline = messageRect.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.7f);
        outline.effectDistance = new Vector2(3f, -3f);
        messageText.color = new Color(1f, 1f, 1f, 0f);
    }

    private static RectTransform NewRect(string name, Transform parent, Vector2 anchor, Vector2 size, Vector2 position)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    private static Text NewText(string name, Transform parent, Font font, string value, int size, TextAnchor align, Vector2 position, Vector2 boxSize)
    {
        RectTransform rect = NewRect(name, parent, new Vector2(0.5f, 0.5f), boxSize, position);
        var text = rect.gameObject.AddComponent<Text>();
        SetupText(text, font, value, size, align);
        return text;
    }

    private static void SetupText(Text text, Font font, string value, int size, TextAnchor align)
    {
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.alignment = align;
        text.color = Color.white;
        text.supportRichText = true;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
    }

    private static Gradient DefaultGradient()
    {
        var g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.35f, 0.85f, 0.4f), 0f),
                new GradientColorKey(new Color(1f, 0.85f, 0.25f), 0.55f),
                new GradientColorKey(new Color(1f, 0.3f, 0.22f), 0.9f),
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return g;
    }
}
