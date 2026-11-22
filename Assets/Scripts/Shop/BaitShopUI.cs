using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// หน้าต่างร้านเหยื่อตกปลา (เปิดจากการคุยกับแม่ค้าเหยื่อ) สไตล์กรอบไม้ + กระดาษเก่า:
/// ซื้อเหยื่อทีละ 1 หรือ 5 ชิ้น และเลือกเหยื่อที่จะใช้ตกปลา
/// </summary>
public class BaitShopUI : GameWindow
{
    public static BaitShopUI Instance { get; private set; }

    private static readonly Vector2 Size = new Vector2(1180f, 740f);
    private static readonly Color Disabled = new Color(0.62f, 0.6f, 0.58f);

    private Text titleText;
    private Text coinsText;
    private Text bannerText;
    private readonly List<Row> rows = new List<Row>();
    private VendorNPC vendor;

    private class Row
    {
        public Bait bait;
        public Image border;
        public Text owned;
        public Button buy1, buy5, equip;
        public Image equipImage, hookIcon, lockIcon;
        public Text equipLabel;
    }

    public static BaitShopUI GetOrCreate()
    {
        if (Instance == null) new GameObject("Bait Shop UI").AddComponent<BaitShopUI>();
        return Instance;
    }

    protected override void Awake()
    {
        Instance = this;
        base.Awake();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Open(VendorNPC from)
    {
        vendor = from;
        titleText.text = from != null ? $"ร้านเหยื่อของ{from.NpcName}" : "ร้านเหยื่อตกปลา";
        SetOpen(true);
    }

    protected override void Refresh()
    {
        GameState state = GameState.Instance;
        coinsText.text = $"เหรียญของคุณ  <b>{state.Coins:N0}</b>";
        Bait equipped = state.EquippedBait;
        string using_ = equipped != null ? $"กำลังใช้ <b>{equipped.Name}</b> (เหลือ {state.BaitCount(equipped.Id)})" : "ยังไม่ได้ใช้เหยื่อ";
        bannerText.text = $"{using_}  •  ใช้ 1 ชิ้นทุกครั้งที่ทุ่นตกน้ำ  •  กด <b>B</b> สลับเหยื่อระหว่างตกปลา";

        foreach (Row row in rows)
        {
            int owned = state.BaitCount(row.bait.Id);
            bool isEquipped = equipped == row.bait;
            row.owned.text = $"มีอยู่ {owned} ชิ้น";

            SetButton(row.buy1, state.Coins >= row.bait.Price, Color.white);
            SetButton(row.buy5, state.Coins >= row.bait.Price * 5, Color.white);

            if (isEquipped)
            {
                SetButton(row.equip, false, new Color(0.72f, 0.95f, 0.6f));
                row.equipLabel.text = "กำลังใช้";
            }
            else if (owned > 0)
            {
                SetButton(row.equip, true, Color.white);
                row.equipLabel.text = "ใช้เหยื่อนี้";
            }
            else
            {
                SetButton(row.equip, false, Disabled);
                row.equipLabel.text = "ยังไม่มี";
            }
            row.hookIcon.gameObject.SetActive(owned > 0);
            row.lockIcon.gameObject.SetActive(owned == 0);

            row.border.color = isEquipped ? new Color(0.85f, 0.62f, 0.18f) : new Color(0.68f, 0.52f, 0.34f);
        }
    }

    private static void SetButton(Button button, bool interactable, Color enabledTint)
    {
        button.interactable = interactable;
        button.targetGraphic.color = interactable || enabledTint != Color.white ? enabledTint : Disabled;
    }

    private void Buy(Bait bait, int amount)
    {
        GameState state = GameState.Instance;
        int cost = bait.Price * amount;
        if (!state.TrySpend(cost))
        {
            if (vendor != null) vendor.Say("เงินไม่พอนะจ๊ะ ไปตกปลามาขายก่อนน้า");
            return;
        }
        state.AddBait(bait.Id, amount);
        if (vendor != null) vendor.Say(amount > 1 ? $"ขอบใจจ้า {bait.Name} {amount} ชิ้น ขอให้ได้ปลาตัวโตๆ!" : $"{bait.Name} 1 ชิ้นจ้า ขอบใจนะ!");
        if (GameHUD.Instance != null) GameHUD.Instance.ShowToast($"ซื้อ{bait.Name} {amount} ชิ้น ราคา {cost:N0} เหรียญ", new Color(1f, 0.9f, 0.55f));
        Refresh();
    }

    private void Equip(Bait bait)
    {
        GameState.Instance.EquipBait(bait.Id);
        Refresh();
    }

    // ------------------------------------------------------------------ Build

    protected override void BuildUI()
    {
        Canvas canvas = UIFactory.CreateCanvas(transform, "Bait Shop Canvas", 25);
        Image dim = UIFactory.Panel("Dim", canvas.transform, Vector2.zero, Vector2.zero, new Color(0.02f, 0.03f, 0.05f, 0.55f));
        UIFactory.Stretch(dim.rectTransform);
        window = dim.gameObject;
        Transform root = window.transform;

        // ---- กรอบไม้ + กระดาษ
        UISkin.Sliced("Shadow", root, UISkin.Rounded(30), Size + new Vector2(16f, 16f), new Vector2(8f, -12f), new Color(0f, 0f, 0f, 0.45f));
        Image frame = UISkin.Sliced("Frame", root, UISkin.Wood(30), Size, Vector2.zero, Color.white);
        Vector2 paperSize = Size - new Vector2(56f, 56f);
        Transform paper = UISkin.Sliced("Paper", frame.transform, UISkin.ParchmentSheet(16), paperSize, Vector2.zero, Color.white).transform;

        // ปลาประดับมุมบน
        foreach (float side in new[] { -1f, 1f })
        {
            Image fish = UISkin.Sliced("Fish Ornament", frame.transform, UISkin.FishOrnament, new Vector2(110f, 55f),
                new Vector2(side * (Size.x / 2f - 40f), Size.y / 2f - 20f), new Color(0.72f, 0.52f, 0.32f));
            fish.rectTransform.localRotation = Quaternion.Euler(0f, side > 0f ? 0f : 180f, -35f);
            fish.raycastTarget = false;
        }

        float top = paperSize.y / 2f, right = paperSize.x / 2f;

        // ---- หัวเรื่อง
        titleText = UIFactory.Label("Title", paper, "ร้านเหยื่อตกปลา", 56, TextAnchor.MiddleLeft, new Vector2(700f, 84f), new Vector2(-right + 30f + 350f, top - 58f), UISkin.TitleRed, FontStyle.Bold);
        UISkin.Shadowed(titleText, new Color(1f, 0.97f, 0.88f, 0.9f), 2.5f);

        // ---- ป้ายเหรียญ
        Image plaque = UISkin.Sliced("Coins", paper, UISkin.Wood(22), new Vector2(370f, 70f), new Vector2(right - 30f - 185f, top - 56f), Color.white);
        coinsText = UIFactory.Label("Text", plaque.transform, "", 28, TextAnchor.MiddleRight, new Vector2(280f, 60f), new Vector2(-26f, 0f), UISkin.Cream);
        UISkin.Shadowed(coinsText, new Color(0f, 0f, 0f, 0.5f));
        UISkin.Sliced("CoinIcon", plaque.transform, UISkin.Coin, new Vector2(42f, 42f), new Vector2(148f, 0f), Color.white);

        // ---- แถบม้วนกระดาษ
        Vector2 bannerPos = new Vector2(0f, top - 128f);
        foreach (float side in new[] { -1f, 1f })
        {
            UISkin.Sliced("Roll", paper, UISkin.Rounded(12), new Vector2(30f, 62f), bannerPos + new Vector2(side * 448f, 0f), UISkin.ParchmentDark * 0.92f);
            UISkin.Sliced("RollLine", paper, UISkin.Rounded(3), new Vector2(4f, 50f), bannerPos + new Vector2(side * 448f, 0f), UISkin.InkSoft * 1.2f);
        }
        Image banner = UISkin.Sliced("Banner", paper, UISkin.ParchmentSheet(8), new Vector2(880f, 52f), bannerPos, new Color(1.03f, 1f, 0.95f));
        bannerText = UIFactory.Label("Text", banner.transform, "", 24, TextAnchor.MiddleCenter, new Vector2(860f, 50f), Vector2.zero, UISkin.Ink);

        // ---- รายการเหยื่อ
        Vector2 rowSize = new Vector2(paperSize.x - 50f, 146f);
        for (int i = 0; i < Bait.All.Count; i++)
            rows.Add(BuildRow(paper, Bait.All[i], rowSize, new Vector2(0f, top - 248f - i * (rowSize.y + 12f))));

        // ---- ปุ่มปิด
        Button close = WoodButton("Close", frame.transform, "Esc / E ปิด", new Vector2(190f, 58f), new Vector2(Size.x / 2f - 130f, -Size.y / 2f + 10f), () => SetOpen(false), 24, true);
        close.transform.SetAsLastSibling();
    }

    private Row BuildRow(Transform parent, Bait bait, Vector2 size, Vector2 position)
    {
        var row = new Row { bait = bait };
        row.border = UISkin.Sliced("Row " + bait.Id, parent, UISkin.Rounded(12), size + new Vector2(6f, 6f), position, Color.white);
        Transform t = UISkin.Sliced("Inner", row.border.transform, UISkin.ParchmentSheet(10), size, Vector2.zero, new Color(1.02f, 0.98f, 0.92f)).transform;

        float left = -size.x / 2f, right = size.x / 2f;

        // ไอคอนในกรอบไม้
        UISkin.Sliced("IconFrame", t, UISkin.Wood(14), new Vector2(132f, 132f), new Vector2(left + 76f, 0f), Color.white);
        UISkin.Sliced("Icon", t, UISkin.BaitIcon(bait.Id), new Vector2(118f, 118f), new Vector2(left + 76f, 0f), Color.white);

        // ชื่อ คำอธิบาย ผล
        float textX = left + 160f;
        UIFactory.Label("Name", t, bait.Name, 42, TextAnchor.MiddleLeft, new Vector2(420f, 56f), new Vector2(textX + 210f, 36f), UISkin.Ink, FontStyle.Bold);
        UIFactory.Label("Desc", t, bait.Description, 22, TextAnchor.MiddleLeft, new Vector2(430f, 30f), new Vector2(textX + 215f, -12f), UISkin.Ink);
        string effect = $"ปลากินเร็วขึ้น {Mathf.RoundToInt((1f - bait.BiteDelayMultiplier) * 100f)}%";
        if (bait.RareLuck > 1f) effect += $"  •  ปลาหายาก x{bait.RareLuck:0.#}";
        UIFactory.Label("Effect", t, effect, 22, TextAnchor.MiddleLeft, new Vector2(430f, 30f), new Vector2(textX + 215f, -44f), new Color(0.24f, 0.34f, 0.1f), FontStyle.Bold);

        // ราคา + จำนวนที่มี
        float equipX = right - 100f;
        Text price = UIFactory.Label("Price", t, $"{bait.Price:N0} เหรียญ/ชิ้น", 28, TextAnchor.MiddleRight, new Vector2(260f, 44f), new Vector2(equipX - 260f, 36f), UISkin.Ink, FontStyle.Bold);
        price.horizontalOverflow = HorizontalWrapMode.Overflow;
        UISkin.Sliced("Coin", t, UISkin.Coin, new Vector2(32f, 32f), new Vector2(equipX - 108f, 36f), Color.white);

        Image pill = UISkin.Sliced("Owned", t, UISkin.Rounded(12), new Vector2(170f, 44f), new Vector2(equipX, 36f), new Color(0.6f, 0.45f, 0.28f));
        UISkin.Sliced("OwnedInner", pill.transform, UISkin.ParchmentSheet(10), new Vector2(164f, 38f), Vector2.zero, Color.white);
        row.owned = UIFactory.Label("Text", pill.transform, "", 21, TextAnchor.MiddleCenter, new Vector2(164f, 38f), Vector2.zero, UISkin.Ink, FontStyle.Bold);

        // ปุ่ม
        row.buy1 = WoodButton("Buy1", t, "ซื้อ 1", new Vector2(118f, 60f), new Vector2(equipX - 330f, -30f), () => Buy(bait, 1), 26, false);
        row.buy5 = WoodButton("Buy5", t, "ซื้อ 5", new Vector2(118f, 60f), new Vector2(equipX - 200f, -30f), () => Buy(bait, 5), 26, false);
        row.equip = WoodButton("Equip", t, "ใช้เหยื่อนี้", new Vector2(176f, 60f), new Vector2(equipX, -30f), () => Equip(bait), 24, true);
        row.equipImage = (Image)row.equip.targetGraphic;
        row.equipLabel = row.equip.GetComponentInChildren<Text>();
        row.equipLabel.rectTransform.offsetMin = new Vector2(36f, 0f);
        row.hookIcon = UISkin.Sliced("Hook", row.equip.transform, UISkin.Hook, new Vector2(22f, 30f), new Vector2(-62f, 0f), UISkin.Cream);
        row.lockIcon = UISkin.Sliced("Lock", row.equip.transform, UISkin.Lock, new Vector2(24f, 28f), new Vector2(-62f, 0f), UISkin.Cream);
        return row;
    }

    private static Button WoodButton(string name, Transform parent, string text, Vector2 size, Vector2 position, UnityAction onClick, int fontSize, bool light)
        => UISkin.WoodButton(name, parent, text, size, position, onClick, fontSize, light);
}
