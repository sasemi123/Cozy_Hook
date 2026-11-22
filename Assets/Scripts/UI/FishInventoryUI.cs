using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// หน้าต่างกระเป๋าปลา: กด Tab หรือ I เพื่อเปิด/ปิด (Esc ปิด)
/// ซ้ายเป็นช่องเก็บปลา (กรอบสีตามความหายาก) ขวาเป็นรายละเอียดปลาที่เลือก พร้อมปุ่มปล่อยปลา
/// ระหว่างเปิดหน้าต่าง จะเหวี่ยงเบ็ดและหมุนกล้องไม่ได้
/// </summary>
public class FishInventoryUI : MonoBehaviour
{
    private const int Columns = 6;
    private const float SlotSize = 136f;
    private const float SlotSpacing = 14f;
    private static readonly Vector2 WindowSize = new Vector2(1500f, 900f);

    [SerializeField] private FishInventory inventory;
    [SerializeField] private FishingLineTension lineTension;

    private GameObject window;
    private Text summaryText;
    private readonly List<Slot> slots = new List<Slot>();
    private int selectedIndex = -1;
    private bool refreshNextFrame;

    private GameObject detailRoot;
    private Text emptyDetailText;
    private RawImage detailIcon;
    private Image detailFrame;
    private Text detailName;
    private Text detailRarity;
    private Text detailWeight;
    private Text detailDescription;

    private class Slot
    {
        public Image glow;
        public Image frame;
        public Image inner;
        public RawImage icon;
        public Text name;
        public Text weight;
    }

    public bool IsOpen { get; private set; }

    private void Awake()
    {
        if (inventory == null) inventory = FindAnyObjectByType<FishInventory>();
        if (lineTension == null) lineTension = FindAnyObjectByType<FishingLineTension>();
        UIFactory.EnsureEventSystem();
        BuildUI();
        window.SetActive(false);
    }

    private void OnEnable()
    {
        if (inventory != null) inventory.Changed += Refresh;
    }

    private void OnDisable()
    {
        if (inventory != null) inventory.Changed -= Refresh;
        if (IsOpen) SetOpen(false);
    }

    private void Update()
    {
        if (refreshNextFrame)
        {
            refreshNextFrame = false;
            Refresh();
        }

        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        if (PauseMenu.IsOpen || TitleScreen.IsShowing) return;
        if (kb.tabKey.wasPressedThisFrame || kb.iKey.wasPressedThisFrame) SetOpen(!IsOpen);
        else if (IsOpen && kb.escapeKey.wasPressedThisFrame) SetOpen(false);
    }

    public void SetOpen(bool open)
    {
        // ห้ามเปิดกระเป๋าระหว่างสู้กับปลา
        if (open && lineTension != null && lineTension.IsFighting) return;
        if (open == IsOpen) return;

        IsOpen = open;
        window.SetActive(open);
        if (open)
        {
            PlayerInputGate.Block();
            if (selectedIndex >= (inventory != null ? inventory.Count : 0)) selectedIndex = -1;
            Refresh();
        }
        else PlayerInputGate.Unblock();
    }

    private void Refresh()
    {
        if (inventory == null || !IsOpen) return;

        // เรนเดอร์ไอคอนที่ยังไม่มีให้ครบก่อนแก้ UI (การเรนเดอร์กลางคันทำให้ uGUI ไม่วาดช่องใหม่)
        bool renderedNew = false;
        foreach (CaughtFish f in inventory.Fish)
            renderedNew |= FishPreviewRenderer.Instance.EnsureIcon(f.Data);
        if (renderedNew) refreshNextFrame = true;

        summaryText.text = $"{inventory.Count}/{inventory.Capacity} ตัว  •  น้ำหนักรวม {inventory.TotalWeight:F2} กก.";

        for (int i = 0; i < slots.Count; i++)
        {
            Slot slot = slots[i];
            bool hasFish = i < inventory.Count;
            bool locked = i >= inventory.Capacity;
            bool selected = hasFish && i == selectedIndex;
            slot.icon.enabled = hasFish;
            slot.name.text = hasFish ? inventory.Fish[i].Data.FishName : "";
            slot.weight.text = hasFish ? $"{inventory.Fish[i].Weight:F2} กก." : "";

            if (hasFish)
            {
                FishData data = inventory.Fish[i].Data;
                slot.icon.texture = FishPreviewRenderer.Instance.GetIcon(data);
                Color c = FishRarityInfo.Color(data.Rarity);
                // กรอบเรืองแสงตามความหายาก (เลือกอยู่ = สีทองสว่าง)
                slot.glow.enabled = true;
                slot.glow.color = selected ? new Color(1f, 0.85f, 0.3f, 1f) : new Color(c.r, c.g, c.b, 0.85f);
                slot.glow.rectTransform.sizeDelta = Vector2.one * (SlotSize + (selected ? 18f : 12f));
                slot.frame.color = Color.Lerp(Color.white, c, 0.45f);
                slot.inner.color = Color.Lerp(Color.white, c, 0.22f);
            }
            else
            {
                slot.glow.enabled = false;
                slot.frame.color = locked ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
                slot.inner.color = locked ? new Color(0.8f, 0.75f, 0.65f, 0.5f) : Color.white;
            }
        }

        RefreshDetail();
    }

    private void RefreshDetail()
    {
        bool has = inventory != null && selectedIndex >= 0 && selectedIndex < inventory.Count;
        detailRoot.SetActive(has);
        emptyDetailText.gameObject.SetActive(!has);
        if (!has) return;

        CaughtFish fish = inventory.Fish[selectedIndex];
        FishData data = fish.Data;
        Color c = FishRarityInfo.Color(data.Rarity);
        detailFrame.color = new Color(c.r, c.g, c.b, 0.9f);
        detailIcon.texture = FishPreviewRenderer.Instance.GetIcon(data);
        detailName.text = data.FishName;
        detailRarity.text = FishRarityInfo.Label(data.Rarity);
        detailRarity.color = UISkin.RarityInk(data.Rarity);
        detailWeight.text = $"น้ำหนัก <b>{fish.Weight:F2}</b> กก.";
        detailDescription.text = data.Description;
    }

    private void Select(int index)
    {
        selectedIndex = inventory != null && index < inventory.Count ? index : -1;
        Refresh();
    }

    private void ReleaseSelected()
    {
        if (inventory == null || selectedIndex < 0 || selectedIndex >= inventory.Count) return;
        inventory.RemoveAt(selectedIndex);
        if (selectedIndex >= inventory.Count) selectedIndex = inventory.Count - 1;
        Refresh();
    }

    // ------------------------------------------------------------------ Build

    private void BuildUI()
    {
        Canvas canvas = UIFactory.CreateCanvas(transform, "Inventory Canvas", 15);

        // ฉากหลังมืดคลุมจอ
        Image dim = UIFactory.Panel("Dim", canvas.transform, Vector2.zero, Vector2.zero, new Color(0.02f, 0.03f, 0.05f, 0.55f));
        UIFactory.Stretch(dim.rectTransform);
        window = dim.gameObject;

        RectTransform paper = UISkin.WoodWindow(window.transform, WindowSize, out Image frame);
        float top = paper.sizeDelta.y / 2f, right = paper.sizeDelta.x / 2f, left = -right;

        // หัวหน้าต่าง
        Text title = UIFactory.Label("Title", paper, "กระเป๋าปลา", 60, TextAnchor.MiddleLeft, new Vector2(340f, 84f), new Vector2(left + 40f + 170f, top - 62f), UISkin.TitleRed, FontStyle.Bold);
        UISkin.Shadowed(title, new Color(1f, 0.97f, 0.88f, 0.9f), 2.5f);
        summaryText = UIFactory.Label("Summary", paper, "", 30, TextAnchor.MiddleLeft, new Vector2(620f, 50f), new Vector2(left + 40f + 330f + 310f, top - 58f), UISkin.Ink);
        UISkin.CloseButton(paper, new Vector2(right - 52f, top - 52f), () => SetOpen(false));

        // ช่องเก็บปลา
        float gridLeft = left + 44f, gridTop = top - 130f;
        int capacity = inventory != null ? inventory.Capacity : 20;
        int rows = Mathf.CeilToInt(capacity / (float)Columns);
        int slotCount = Mathf.Max(capacity, rows * Columns);
        for (int i = 0; i < slotCount; i++)
        {
            int col = i % Columns, row = i / Columns;
            var pos = new Vector2(gridLeft + SlotSize / 2f + col * (SlotSize + SlotSpacing), gridTop - SlotSize / 2f - row * (SlotSize + SlotSpacing));
            slots.Add(CreateSlot(paper, i, pos));
        }

        // แผงรายละเอียดด้านขวา (กรอบไม้ + คานหัว + กระดาษ)
        float detailLeft = gridLeft + Columns * (SlotSize + SlotSpacing) + 22f;
        float detailRight = right - 34f;
        float detailTop = gridTop, detailBottom = -top + 86f;
        Vector2 detailSize = new Vector2(detailRight - detailLeft, detailTop - detailBottom);
        Vector2 detailPos = new Vector2((detailLeft + detailRight) / 2f, (detailTop + detailBottom) / 2f);
        Image detailWood = UISkin.Sliced("Detail", paper, UISkin.Wood(16), detailSize, detailPos, Color.white);
        UISkin.Sliced("Header", detailWood.transform, UISkin.Wood(10), new Vector2(detailSize.x + 16f, 46f), new Vector2(0f, detailSize.y / 2f), Color.white);
        Vector2 innerSize = detailSize - new Vector2(36f, 66f);
        Transform detailBg = UISkin.Sliced("Inner", detailWood.transform, UISkin.ParchmentSheet(12), innerSize, new Vector2(0f, -10f), Color.white).transform;

        emptyDetailText = UIFactory.Label("Empty", detailBg, "เลือกปลาในกระเป๋า\nเพื่อดูรายละเอียด", 32, TextAnchor.MiddleCenter, new Vector2(innerSize.x - 40f, 200f), Vector2.zero, UISkin.Ink);

        detailRoot = new GameObject("Detail Content", typeof(RectTransform));
        detailRoot.transform.SetParent(detailBg, false);
        UIFactory.Stretch((RectTransform)detailRoot.transform);

        float h = innerSize.y / 2f, w = innerSize.x;
        Vector2 picSize = new Vector2(w - 40f, 210f);
        detailFrame = UISkin.Sliced("Rarity Glow", detailRoot.transform, UISkin.Rounded(16), picSize + new Vector2(14f, 14f), new Vector2(0f, h - 130f), Color.white);
        detailIcon = UISkin.PictureFrame(detailRoot.transform, picSize, new Vector2(0f, h - 130f));

        detailName = UIFactory.Label("Name", detailRoot.transform, "", 40, TextAnchor.MiddleCenter, new Vector2(w - 30f, 54f), new Vector2(0f, h - 280f), UISkin.Ink, FontStyle.Bold);
        detailRarity = UIFactory.Label("Rarity", detailRoot.transform, "", 26, TextAnchor.MiddleCenter, new Vector2(w - 30f, 36f), new Vector2(0f, h - 322f), UISkin.Ink, FontStyle.Bold);
        detailWeight = UIFactory.Label("Weight", detailRoot.transform, "", 26, TextAnchor.MiddleCenter, new Vector2(w - 30f, 36f), new Vector2(0f, h - 360f), UISkin.Ink);
        detailDescription = UIFactory.Label("Description", detailRoot.transform, "", 21, TextAnchor.UpperCenter, new Vector2(w - 50f, 100f), new Vector2(0f, h - 432f), UISkin.InkSoft);
        UISkin.WoodButton("Release", detailRoot.transform, "ปล่อยปลากลับลงน้ำ", new Vector2(280f, 60f), new Vector2(0f, -h + 46f), ReleaseSelected, 24, true);

        // แถบคำแนะนำ + ปุ่มปิด
        Image hintBar = UISkin.Sliced("Hint", paper, UISkin.Wood(12), new Vector2(470f, 56f), new Vector2(left + 30f + 235f, -top + 40f), Color.white);
        Text hint = UIFactory.Label("Text", hintBar.transform, "กด Tab / I เพื่อปิดกระเป๋า", 22, TextAnchor.MiddleLeft, new Vector2(430f, 50f), Vector2.zero, UISkin.Cream);
        UISkin.Shadowed(hint, new Color(0f, 0f, 0f, 0.5f));
        UISkin.WoodButton("Close", frame.transform, "Esc ปิด", new Vector2(160f, 58f), new Vector2(WindowSize.x / 2f - 115f, -WindowSize.y / 2f + 10f), () => SetOpen(false), 24, true)
            .transform.SetAsLastSibling();
    }

    private Slot CreateSlot(Transform parent, int index, Vector2 position)
    {
        var slot = new Slot();
        slot.glow = UISkin.Sliced("Glow " + index, parent, UISkin.Rounded(18), Vector2.one * (SlotSize + 12f), position, Color.clear);
        slot.glow.raycastTarget = false;

        slot.frame = UISkin.Sliced("Slot " + index, parent, UISkin.Wood(14), Vector2.one * SlotSize, position, Color.white);
        var button = slot.frame.gameObject.AddComponent<Button>();
        button.targetGraphic = slot.frame;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => Select(index));
        slot.frame.gameObject.AddComponent<UISkin.ButtonPress>();

        slot.inner = UISkin.Sliced("Inner", slot.frame.transform, UISkin.ParchmentSheet(8), Vector2.one * (SlotSize - 24f), Vector2.zero, Color.white);
        slot.inner.raycastTarget = false;

        slot.icon = UIFactory.RawImage("Icon", slot.frame.transform, new Vector2(104f, 66f), new Vector2(0f, 18f));
        slot.name = UIFactory.Label("Name", slot.frame.transform, "", 16, TextAnchor.MiddleCenter, new Vector2(SlotSize - 28f, 20f), new Vector2(0f, -28f), UISkin.Ink, FontStyle.Bold);
        slot.name.resizeTextForBestFit = true;
        slot.name.resizeTextMinSize = 10;
        slot.name.resizeTextMaxSize = 16;
        slot.name.verticalOverflow = VerticalWrapMode.Truncate;
        slot.weight = UIFactory.Label("Weight", slot.frame.transform, "", 14, TextAnchor.MiddleCenter, new Vector2(SlotSize - 28f, 18f), new Vector2(0f, -46f), UISkin.InkSoft);
        return slot;
    }
}
