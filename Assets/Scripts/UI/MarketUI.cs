using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// หน้าต่างตลาดปลา (สไตล์กรอบไม้ + กระดาษเก่า):
/// - ซ้าย: ขายปลาในกระเป๋า เป็นการ์ดเลื่อนดูได้ (ราคาเป็น ??? ถ้าวันนี้ยังไม่ได้อ่านป้ายประกาศ)
/// - ขวา: ปลาที่ตลาดวางขายวันนี้ ซื้อได้ด้วยเหรียญ (เอาไปเก็งราคาขายวันอื่นได้)
/// </summary>
public class MarketUI : GameWindow
{
    public static MarketUI Instance { get; private set; }

    private static readonly Vector2 Size = new Vector2(1500f, 900f);
    private static readonly Color SellColor = new Color(0.32f, 0.62f, 0.62f);
    private const int SellCards = 20;
    private const float SellPitch = 114f;

    private FishInventory inventory;
    private Text coinsText;
    private Text infoText;
    private Text sellTitle;
    private Text sellTotalText;
    private Button sellAllButton;
    private RectTransform sellContent;
    private Text sellEmptyText;
    private readonly List<SellCard> sellCards = new List<SellCard>();
    private readonly List<StockCard> stockCards = new List<StockCard>();
    private List<CaughtFish> todayStock = new List<CaughtFish>();
    private int stockDay = -1;

    private class SellCard
    {
        public GameObject root;
        public RawImage icon;
        public Text name;
        public Text weight;
        public Text price;
        public Image coin;
        public Button sell;
    }

    private class StockCard
    {
        public GameObject root;
        public RawImage icon;
        public Text name;
        public Text detail;
        public Text price;
        public Image coin;
        public Button buy;
        public Text buyLabel;
    }

    protected override void Awake()
    {
        Instance = this;
        base.Awake();
    }

    private FishInventory Inventory
    {
        get
        {
            if (inventory == null) inventory = FindAnyObjectByType<FishInventory>();
            return inventory;
        }
    }

    protected override void OnOpened()
    {
        GameState state = GameState.Instance;
        if (stockDay != state.Day)
        {
            stockDay = state.Day;
            todayStock = FishMarket.DailyStock(FishDatabase.Load(), state.Day);
        }

        // เรนเดอร์ไอคอนให้ครบก่อน แล้ววาด UI ใหม่เฟรมถัดไป
        bool rendered = false;
        foreach (CaughtFish f in todayStock) rendered |= FishPreviewRenderer.Instance.EnsureIcon(f.Data);
        if (Inventory != null)
            foreach (CaughtFish f in Inventory.Fish) rendered |= FishPreviewRenderer.Instance.EnsureIcon(f.Data);
        if (rendered) RefreshNextFrame();
    }

    protected override void Refresh()
    {
        GameState state = GameState.Instance;
        int day = state.Day;
        bool knowsPrice = state.HasReadBoardToday;
        FishInventory bag = Inventory;

        coinsText.text = $"เหรียญของคุณ  <b>{state.Coins:N0}</b>";
        infoText.text = knowsPrice
            ? $"ราคาวันที่ {day} ตามป้ายประกาศ  •  ราคาเปลี่ยนทุกวัน เก็บปลาไว้ขายวันที่ราคาดีได้"
            : "<color=#8a2a10><b>วันนี้ยังไม่ได้ดูป้ายประกาศ!</b></color>  ลุงไม่บอกราคา — ไปอ่านป้ายประกาศกลางหมู่บ้านก่อน";

        // ---- ขาย
        int count = bag != null ? bag.Count : 0;
        int total = 0;
        sellTitle.text = $"ขายปลาของคุณ ({count}/{(bag != null ? bag.Capacity : 0)})";
        for (int i = 0; i < sellCards.Count; i++)
        {
            SellCard card = sellCards[i];
            bool has = i < count;
            card.root.SetActive(has);
            if (!has) continue;

            CaughtFish f = bag.Fish[i];
            int price = FishMarket.SellPrice(f, day);
            total += price;
            card.icon.texture = FishPreviewRenderer.Instance.GetIcon(f.Data);
            card.name.text = f.Data.FishName;
            card.name.color = UISkin.RarityInk(f.Data.Rarity);
            card.weight.text = $"{f.Weight:F2} กก.";
            card.price.text = knowsPrice ? $"{price:N0} เหรียญ" : "??? เหรียญ";
            UISkin.PlaceCoin(card.price, card.coin);
        }
        sellContent.sizeDelta = new Vector2(sellContent.sizeDelta.x, Mathf.Max(1, count) * SellPitch + 8f);
        sellEmptyText.gameObject.SetActive(count == 0);
        sellTotalText.text = count == 0 ? "กระเป๋าว่าง" : knowsPrice ? $"รวมทั้งหมด {total:N0} เหรียญ" : "รวมทั้งหมด ??? เหรียญ";
        UISkin.SetButton(sellAllButton, count > 0, Color.white);

        // ---- ซื้อ
        for (int i = 0; i < stockCards.Count; i++)
        {
            StockCard card = stockCards[i];
            bool has = i < todayStock.Count;
            card.root.SetActive(has);
            if (!has) continue;

            CaughtFish f = todayStock[i];
            int price = FishMarket.BuyPrice(f, day);
            bool sold = state.IsStockSold(i);
            bool canBuy = !sold && state.Coins >= price && bag != null && !bag.IsFull;
            card.icon.texture = FishPreviewRenderer.Instance.GetIcon(f.Data);
            card.name.text = f.Data.FishName;
            card.detail.text = $"<color=#{ColorUtility.ToHtmlStringRGB(UISkin.RarityInk(f.Data.Rarity))}>{FishRarityInfo.Label(f.Data.Rarity)}</color>  •  {f.Weight:F2} กก.";
            card.price.text = sold ? "ขายไปแล้ว" : $"{price:N0} เหรียญ";
            UISkin.PlaceCoin(card.price, card.coin);
            if (sold) card.coin.gameObject.SetActive(false);
            UISkin.SetButton(card.buy, canBuy, Color.white);
            card.buyLabel.text = sold ? "หมดแล้ว" : state.Coins < price ? "เงินไม่พอ" : bag != null && bag.IsFull ? "กระเป๋าเต็ม" : "ซื้อ";
        }
    }

    private void Sell(int index)
    {
        FishInventory bag = Inventory;
        if (bag == null || index < 0 || index >= bag.Count) return;

        CaughtFish f = bag.Fish[index];
        int price = FishMarket.SellPrice(f, GameState.Instance.Day);
        bag.RemoveAt(index);
        GameState.Instance.AddCoins(price);
        Toast($"ขาย{f.Data.FishName} {f.Weight:F2} กก. ได้ {price:N0} เหรียญ");
        Refresh();
    }

    private void SellAll()
    {
        FishInventory bag = Inventory;
        if (bag == null || bag.Count == 0) return;

        int day = GameState.Instance.Day, total = 0, n = bag.Count;
        for (int i = bag.Count - 1; i >= 0; i--)
        {
            total += FishMarket.SellPrice(bag.Fish[i], day);
            bag.RemoveAt(i);
        }
        GameState.Instance.AddCoins(total);
        Toast($"ขายปลาทั้งหมด {n} ตัว ได้ {total:N0} เหรียญ");
        Refresh();
    }

    private void Buy(int index)
    {
        GameState state = GameState.Instance;
        FishInventory bag = Inventory;
        if (index < 0 || index >= todayStock.Count || state.IsStockSold(index) || bag == null || bag.IsFull) return;

        CaughtFish f = todayStock[index];
        int price = FishMarket.BuyPrice(f, state.Day);
        if (!state.TrySpend(price)) return;

        bag.TryAdd(new CaughtFish(f.Data, f.Weight));
        state.MarkStockSold(index);
        Toast($"ซื้อ{f.Data.FishName} {f.Weight:F2} กก. ราคา {price:N0} เหรียญ");
        Refresh();
    }

    private static void Toast(string message)
    {
        if (GameHUD.Instance != null) GameHUD.Instance.ShowToast(message, new Color(1f, 0.9f, 0.55f));
    }

    // ------------------------------------------------------------------ Build

    protected override void BuildUI()
    {
        Canvas canvas = UIFactory.CreateCanvas(transform, "Market Canvas", 25);
        Image dim = UIFactory.Panel("Dim", canvas.transform, Vector2.zero, Vector2.zero, new Color(0.02f, 0.03f, 0.05f, 0.55f));
        UIFactory.Stretch(dim.rectTransform);
        window = dim.gameObject;

        RectTransform paper = UISkin.WoodWindow(window.transform, Size, out Image frame);
        float top = paper.sizeDelta.y / 2f, right = paper.sizeDelta.x / 2f;

        // ---- หัวเรื่อง + แถบข้อมูล
        Text title = UIFactory.Label("Title", paper, "ตลาดปลา", 60, TextAnchor.MiddleLeft, new Vector2(360f, 80f), new Vector2(-right + 30f + 180f, top - 52f), UISkin.TitleRed, FontStyle.Bold);
        UISkin.Shadowed(title, new Color(1f, 0.97f, 0.88f, 0.9f), 2.5f);

        Vector2 bannerPos = new Vector2(-right + 30f + 430f, top - 118f);
        UISkin.Sliced("Roll", paper, UISkin.Rounded(12), new Vector2(28f, 56f), bannerPos + new Vector2(440f, 0f), UISkin.ParchmentDark * 0.92f);
        Image banner = UISkin.Sliced("Banner", paper, UISkin.ParchmentSheet(8), new Vector2(880f, 46f), bannerPos, Color.white);
        infoText = UIFactory.Label("Info", banner.transform, "", 21, TextAnchor.MiddleLeft, new Vector2(850f, 44f), Vector2.zero, UISkin.Ink);

        // ---- เหรียญ + ปุ่มปิด
        Image plaque = UISkin.Sliced("Coins", paper, UISkin.Wood(22), new Vector2(370f, 70f), new Vector2(right - 120f - 185f, top - 52f), Color.white);
        coinsText = UIFactory.Label("Text", plaque.transform, "", 28, TextAnchor.MiddleRight, new Vector2(280f, 60f), new Vector2(-26f, 0f), UISkin.Cream);
        UISkin.Shadowed(coinsText, new Color(0f, 0f, 0f, 0.5f));
        UISkin.Sliced("CoinIcon", plaque.transform, UISkin.Coin, new Vector2(42f, 42f), new Vector2(148f, 0f), Color.white);
        UISkin.CloseButton(paper, new Vector2(right - 52f, top - 52f), () => SetOpen(false));

        // ---- แผงซ้าย/ขวา
        Vector2 panelSize = new Vector2(690f, 660f);
        float panelY = top - 160f - panelSize.y / 2f;
        Transform sellPanel = Panel("Sell Panel", paper, panelSize, new Vector2(-right + 24f + panelSize.x / 2f, panelY));
        Transform buyPanel = Panel("Buy Panel", paper, panelSize, new Vector2(right - 24f - panelSize.x / 2f, panelY));
        BuildSellPanel(sellPanel, panelSize);
        BuildBuyPanel(buyPanel, panelSize);

        // ---- ปุ่มปิดล่างขวา
        UISkin.WoodButton("Close", frame.transform, "Esc / E ปิด", new Vector2(190f, 58f), new Vector2(Size.x / 2f - 130f, -Size.y / 2f + 10f), () => SetOpen(false), 24, true)
            .transform.SetAsLastSibling();
    }

    private static Transform Panel(string name, Transform parent, Vector2 size, Vector2 position)
    {
        Image border = UISkin.Sliced(name, parent, UISkin.Rounded(14), size + new Vector2(6f, 6f), position, new Color(0.55f, 0.4f, 0.25f));
        return UISkin.Sliced("Inner", border.transform, UISkin.ParchmentSheet(12), size, Vector2.zero, new Color(0.95f, 0.9f, 0.8f)).transform;
    }

    private void BuildSellPanel(Transform panel, Vector2 size)
    {
        float half = size.y / 2f;
        sellTitle = UISkin.HeaderBar(panel, "", new Vector2(size.x + 6f, 62f), new Vector2(0f, half - 28f));

        // พื้นที่เลื่อนดูการ์ดปลา
        float viewTop = half - 66f, viewBottom = -half + 84f;
        Image viewport = UIFactory.Panel("Viewport", panel, new Vector2(size.x - 20f, viewTop - viewBottom), new Vector2(0f, (viewTop + viewBottom) / 2f), new Color(1f, 1f, 1f, 0.001f));
        viewport.gameObject.AddComponent<RectMask2D>();

        sellContent = UIFactory.Rect("Content", viewport.transform, new Vector2(size.x - 20f, SellPitch * SellCards), Vector2.zero);
        sellContent.anchorMin = sellContent.anchorMax = sellContent.pivot = new Vector2(0.5f, 1f);
        sellContent.anchoredPosition = Vector2.zero;

        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport.rectTransform;
        scroll.content = sellContent;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        sellEmptyText = UIFactory.Label("Empty", viewport.transform, "กระเป๋าว่าง — ไปตกปลาที่ทะเลสาบก่อนนะ", 26, TextAnchor.MiddleCenter, new Vector2(size.x - 60f, 80f), Vector2.zero, UISkin.InkSoft);

        Vector2 cardSize = new Vector2(size.x - 44f, SellPitch - 12f);
        for (int i = 0; i < SellCards; i++)
        {
            int index = i;
            var card = new SellCard();
            Image border = UISkin.Card("Card " + i, sellContent, cardSize, Vector2.zero, out Transform t);
            RectTransform rt = border.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -8f - cardSize.y / 2f - i * SellPitch);
            card.root = border.gameObject;

            float l = -cardSize.x / 2f;
            card.icon = UISkin.PictureFrame(t, new Vector2(150f, 88f), new Vector2(l + 84f, 0f));
            card.name = UIFactory.Label("Name", t, "", 28, TextAnchor.MiddleLeft, new Vector2(300f, 40f), new Vector2(l + 172f + 150f, 20f), UISkin.Ink, FontStyle.Bold);
            card.weight = UIFactory.Label("Weight", t, "", 22, TextAnchor.MiddleLeft, new Vector2(140f, 32f), new Vector2(l + 172f + 70f, -22f), UISkin.InkSoft);
            card.price = UISkin.CoinLabel("Price", t, 22, new Vector2(170f, 32f), new Vector2(l + 330f + 85f, -22f), out card.coin);
            card.sell = UISkin.PaintedButton("Sell", t, "ขาย", new Vector2(128f, 56f), new Vector2(-l - 82f, 0f), SellColor, () => Sell(index), 24);
            sellCards.Add(card);
        }

        // แถบรวม + ขายทั้งหมด
        Image totalBar = UISkin.Sliced("Total", panel, UISkin.Wood(12), new Vector2(410f, 62f), new Vector2(-size.x / 2f + 16f + 205f, -half + 42f), Color.white);
        sellTotalText = UIFactory.Label("Text", totalBar.transform, "", 24, TextAnchor.MiddleLeft, new Vector2(370f, 56f), Vector2.zero, UISkin.Cream, FontStyle.Bold);
        UISkin.Shadowed(sellTotalText, new Color(0f, 0f, 0f, 0.5f));
        sellAllButton = UISkin.WoodButton("Sell All", panel, "ขายทั้งหมด", new Vector2(230f, 62f), new Vector2(size.x / 2f - 16f - 115f, -half + 42f), SellAll, 24, true);
    }

    private void BuildBuyPanel(Transform panel, Vector2 size)
    {
        float half = size.y / 2f;
        UISkin.HeaderBar(panel, "ปลาวางขายวันนี้", new Vector2(size.x + 6f, 62f), new Vector2(0f, half - 28f));
        UIFactory.Label("Note", panel, $"ราคาขายของตลาดแพงกว่าราคารับซื้อ {Mathf.RoundToInt((FishMarket.BuyMarkup - 1f) * 100f)}%", 18, TextAnchor.MiddleLeft,
            new Vector2(size.x - 40f, 26f), new Vector2(0f, half - 76f), UISkin.InkSoft);

        Vector2 cardSize = new Vector2(size.x - 30f, 124f);
        for (int i = 0; i < FishMarket.DailyStockCount; i++)
        {
            int index = i;
            var card = new StockCard();
            Image border = UISkin.Card("Stock " + i, panel, cardSize, new Vector2(0f, half - 100f - cardSize.y / 2f - i * (cardSize.y + 12f)), out Transform t);
            card.root = border.gameObject;

            float l = -cardSize.x / 2f;
            card.icon = UISkin.PictureFrame(t, new Vector2(180f, 108f), new Vector2(l + 100f, 0f));
            card.name = UIFactory.Label("Name", t, "", 30, TextAnchor.MiddleLeft, new Vector2(270f, 42f), new Vector2(l + 205f + 135f, 34f), UISkin.Ink, FontStyle.Bold);
            card.detail = UIFactory.Label("Detail", t, "", 21, TextAnchor.MiddleLeft, new Vector2(270f, 30f), new Vector2(l + 205f + 135f, 0f), UISkin.InkSoft);
            card.price = UISkin.CoinLabel("Price", t, 25, new Vector2(200f, 36f), new Vector2(l + 205f + 100f, -34f), out card.coin);
            card.price.fontStyle = FontStyle.Bold;
            card.buy = UISkin.WoodButton("Buy", t, "ซื้อ", new Vector2(150f, 58f), new Vector2(-l - 95f, -18f), () => Buy(index), 24, true);
            card.buyLabel = card.buy.GetComponentInChildren<Text>();
            stockCards.Add(card);
        }
    }
}
