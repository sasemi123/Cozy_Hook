using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// หน้าต่างป้ายประกาศหมู่บ้าน (สไตล์กระดาษบนบอร์ดไม้):
/// ข่าวเด่นประจำวัน + ตารางราคาปลาทุกชนิดต่อกิโลกรัม เทียบกับเมื่อวาน (▲ ▼)
/// </summary>
public class NoticeBoardUI : GameWindow
{
    public static NoticeBoardUI Instance { get; private set; }

    private static readonly Vector2 Size = new Vector2(1400f, 900f);
    private const float RowPitch = 64f;
    private static readonly Color Ink = UISkin.Ink;
    private const int MaxRows = 8;

    private Text subtitle;
    private Text headline;
    private Text footer;
    private readonly List<Row> rows = new List<Row>();

    private class Row
    {
        public GameObject root;
        public RawImage icon;
        public Text name;
        public Text rarity;
        public Text price;
        public Text change;
    }

    protected override void Awake()
    {
        Instance = this;
        base.Awake();
    }

    protected override void Update()
    {
        base.Update();
        if (IsOpen)
        {
            int minutes = Mathf.CeilToInt(GameState.Instance.SecondsUntilNextDay / 60f);
            footer.text = $"ราคาจะเปลี่ยนเมื่อขึ้นวันใหม่ (อีกประมาณ {minutes} นาที)";
        }
    }

    protected override void OnOpened()
    {
        FishDatabase db = FishDatabase.Load();
        if (db == null) return;
        bool rendered = false;
        foreach (FishData f in db.Fish) rendered |= FishPreviewRenderer.Instance.EnsureIcon(f);
        if (rendered) RefreshNextFrame();
    }

    protected override void Refresh()
    {
        int day = GameState.Instance.Day;
        FishDatabase db = FishDatabase.Load();
        IReadOnlyList<FishData> fish = db != null ? db.Fish : new List<FishData>();

        subtitle.text = $"ราคารับซื้อปลาประจำวันที่ {day}";
        headline.text = FishMarket.Headline(fish, day);

        for (int i = 0; i < rows.Count; i++)
        {
            Row row = rows[i];
            bool has = i < fish.Count && fish[i] != null;
            row.root.SetActive(has);
            if (!has) continue;

            FishData f = fish[i];
            row.icon.texture = FishPreviewRenderer.Instance.GetIcon(f);
            row.name.text = f.FishName;
            row.rarity.text = $"<color=#{ColorUtility.ToHtmlStringRGB(Color.Lerp(FishRarityInfo.Color(f.Rarity), new Color(0.85f, 0.6f, 0.1f), f.Rarity == FishRarity.Common ? 0.8f : 0.15f))}><size=28>{FishRarityInfo.Stars(f.Rarity)}</size></color>  {FishRarityInfo.DisplayName(f.Rarity)}";
            row.rarity.color = UISkin.RarityInk(f.Rarity);
            row.price.text = $"{FishMarket.PricePerKg(f, day):0.#} เหรียญ/กก.";

            float change = FishMarket.ChangePercent(f, day);
            FishMarket.PriceEvent e = FishMarket.EventOf(f, day);
            string tag = e == FishMarket.PriceEvent.Boom ? "  พุ่ง!" : e == FishMarket.PriceEvent.Crash ? "  ร่วง!" : "";
            if (day <= 1 || Mathf.Abs(change) < 0.5f)
            {
                row.change.text = "—  คงที่" + tag;
                row.change.color = new Color(0.4f, 0.35f, 0.3f);
            }
            else if (change > 0f)
            {
                row.change.text = $"▲ +{change:0}%{tag}";
                row.change.color = new Color(0.12f, 0.5f, 0.15f);
            }
            else
            {
                row.change.text = $"▼ {change:0}%{tag}";
                row.change.color = new Color(0.7f, 0.15f, 0.1f);
            }
        }
    }

    protected override void BuildUI()
    {
        Canvas canvas = UIFactory.CreateCanvas(transform, "Notice Board Canvas", 25);
        Image dim = UIFactory.Panel("Dim", canvas.transform, Vector2.zero, Vector2.zero, new Color(0.02f, 0.03f, 0.05f, 0.55f));
        UIFactory.Stretch(dim.rectTransform);
        window = dim.gameObject;

        RectTransform paper = UISkin.WoodWindow(window.transform, Size, out Image frame);
        float top = paper.sizeDelta.y / 2f, right = paper.sizeDelta.x / 2f, left = -right;

        // หัวเรื่อง
        Text title = UIFactory.Label("Title", paper, "ป้ายประกาศหมู่บ้าน", 58, TextAnchor.MiddleLeft, new Vector2(700f, 76f), new Vector2(left + 40f + 350f, top - 50f), Ink, FontStyle.Bold);
        UISkin.Shadowed(title, new Color(1f, 0.97f, 0.88f, 0.9f), 2.5f);
        subtitle = UIFactory.Label("Subtitle", paper, "", 28, TextAnchor.MiddleLeft, new Vector2(700f, 40f), new Vector2(left + 40f + 350f, top - 104f), Ink);
        UISkin.CloseButton(paper, new Vector2(right - 52f, top - 52f), () => SetOpen(false));

        // ข่าววันนี้ (ม้วนกระดาษ)
        Vector2 newsPos = new Vector2(0f, top - 162f);
        float newsWidth = paper.sizeDelta.x - 110f;
        foreach (float side in new[] { -1f, 1f })
            UISkin.Sliced("Roll", paper, UISkin.Rounded(12), new Vector2(28f, 66f), newsPos + new Vector2(side * (newsWidth / 2f + 6f), 0f), UISkin.ParchmentDark * 0.9f);
        Image news = UISkin.Sliced("News", paper, UISkin.ParchmentSheet(8), new Vector2(newsWidth, 58f), newsPos, new Color(1.03f, 1f, 0.95f));
        UIFactory.Label("NewsTag", news.transform, "ข่าววันนี้", 28, TextAnchor.MiddleLeft, new Vector2(160f, 54f), new Vector2(-newsWidth / 2f + 24f + 80f, 0f), UISkin.TitleRed, FontStyle.Bold);
        headline = UIFactory.Label("Headline", news.transform, "", 26, TextAnchor.MiddleLeft, new Vector2(newsWidth - 230f, 54f), new Vector2(90f, 0f), Ink);

        // หัวตาราง
        float headerY = top - 228f;
        float colIcon = left + 100f, colName = left + 170f + 150f, colRarity = left + 520f + 150f, colPrice = left + 820f + 150f, colChange = left + 1100f + 100f;
        Color headerColor = Ink;
        UIFactory.Label("H_Name", paper, "ปลา", 26, TextAnchor.MiddleLeft, new Vector2(300f, 34f), new Vector2(colName, headerY), headerColor);
        UIFactory.Label("H_Rarity", paper, "ความหายาก", 26, TextAnchor.MiddleLeft, new Vector2(300f, 34f), new Vector2(colRarity, headerY), headerColor);
        UIFactory.Label("H_Price", paper, "ราคาวันนี้", 26, TextAnchor.MiddleLeft, new Vector2(300f, 34f), new Vector2(colPrice, headerY), headerColor);
        UIFactory.Label("H_Change", paper, "เทียบเมื่อวาน", 26, TextAnchor.MiddleCenter, new Vector2(200f, 34f), new Vector2(colChange, headerY), headerColor);

        float rowWidth = paper.sizeDelta.x - 100f;
        for (int i = 0; i < MaxRows; i++)
        {
            float y = headerY - 46f - i * RowPitch;
            var row = new Row();
            Image bg = UISkin.Sliced("Row " + i, paper, UISkin.Rounded(10), new Vector2(rowWidth, RowPitch - 6f), new Vector2(0f, y),
                i % 2 == 0 ? new Color(0.78f, 0.63f, 0.42f, 0.4f) : new Color(0.78f, 0.63f, 0.42f, 0.0f));
            row.root = bg.gameObject;
            // ตำแหน่งคอลัมน์อ้างอิงกระดาษ แต่ลูกอยู่ใต้แถว (แถวอยู่กลางแนวนอน)
            Transform t = bg.transform;
            row.icon = UIFactory.RawImage("Icon", t, new Vector2(110f, 62f), new Vector2(colIcon, 0f));
            row.name = UIFactory.Label("Name", t, "", 30, TextAnchor.MiddleLeft, new Vector2(300f, 48f), new Vector2(colName, 0f), Ink);
            row.rarity = UIFactory.Label("Rarity", t, "", 24, TextAnchor.MiddleLeft, new Vector2(300f, 48f), new Vector2(colRarity, 0f), Ink);
            row.price = UIFactory.Label("Price", t, "", 28, TextAnchor.MiddleLeft, new Vector2(300f, 48f), new Vector2(colPrice, 0f), Ink);
            row.change = UIFactory.Label("Change", t, "", 28, TextAnchor.MiddleCenter, new Vector2(220f, 48f), new Vector2(colChange, 0f), Ink);
            rows.Add(row);
        }

        // แถบล่าง
        Image bar = UISkin.Sliced("Footer", paper, UISkin.Wood(12), new Vector2(620f, 56f), new Vector2(left + 24f + 310f, -top + 40f), Color.white);
        footer = UIFactory.Label("Text", bar.transform, "", 22, TextAnchor.MiddleCenter, new Vector2(600f, 50f), Vector2.zero, UISkin.Cream);
        UISkin.Shadowed(footer, new Color(0f, 0f, 0f, 0.5f));
        UIFactory.Label("Note", paper, "•  ตลาดรับซื้อตามราคานี้ × น้ำหนักปลา", 22, TextAnchor.MiddleLeft, new Vector2(500f, 40f), new Vector2(left + 24f + 620f + 20f + 250f, -top + 40f), Ink);

        UISkin.WoodButton("Close", frame.transform, "Esc / E ปิด", new Vector2(190f, 58f), new Vector2(Size.x / 2f - 130f, -Size.y / 2f + 10f), () => SetOpen(false), 24, true)
            .transform.SetAsLastSibling();
    }
}
