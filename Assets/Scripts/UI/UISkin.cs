using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// สกิน UI แนวไม้ + กระดาษเก่า (cozy) สร้างสไปรต์ทั้งหมดด้วยโค้ด ไม่ต้องใช้ไฟล์ภาพ:
/// กรอบมุมมน, ลายไม้, กระดาษ parchment, เหรียญ, แม่กุญแจ, ตะขอเบ็ด, ปลาประดับ และไอคอนเหยื่อ
/// </summary>
public static class UISkin
{
    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    public static readonly Color WoodDark = new Color(0.27f, 0.16f, 0.09f);
    public static readonly Color WoodMid = new Color(0.45f, 0.28f, 0.16f);
    public static readonly Color WoodLight = new Color(0.62f, 0.41f, 0.24f);
    public static readonly Color Parchment = new Color(0.95f, 0.89f, 0.75f);
    public static readonly Color ParchmentDark = new Color(0.88f, 0.79f, 0.62f);
    public static readonly Color Ink = new Color(0.25f, 0.15f, 0.08f);
    public static readonly Color InkSoft = new Color(0.4f, 0.27f, 0.16f);
    public static readonly Color TitleRed = new Color(0.47f, 0.18f, 0.09f);
    public static readonly Color Cream = new Color(1f, 0.95f, 0.85f);

    // ------------------------------------------------------------------ Public sprites

    /// <summary>สี่เหลี่ยมมุมมนสีขาว (ย้อมสีด้วย Image.color) ใช้แบบ Sliced</summary>
    public static Sprite Rounded(int radius) => Get("round" + radius, () =>
    {
        int size = radius * 2 + 8;
        return Paint(size, size, radius + 3, (p, w, h) =>
        {
            float a = Cov(RoundedBox(p, new Vector2(w / 2f, h / 2f), new Vector2(w / 2f - 1f, h / 2f - 1f), radius));
            return new Color(1f, 1f, 1f, a);
        });
    });

    /// <summary>แผ่นไม้มุมมนมีลายไม้ ขอบเข้ม และไฮไลต์ด้านบน (light = ไม้สีอ่อน/ส้ม)</summary>
    public static Sprite Wood(int radius, bool light = false) => Get($"wood{radius}{light}", () =>
    {
        int size = Mathf.Max(96, radius * 2 + 40);
        Color a = light ? new Color(0.72f, 0.46f, 0.24f) : WoodMid;
        Color b = light ? new Color(0.86f, 0.6f, 0.33f) : WoodLight;
        Color rim = light ? new Color(0.4f, 0.22f, 0.1f) : WoodDark;
        return Paint(size, size, radius + 6, (p, w, h) =>
        {
            float d = RoundedBox(p, new Vector2(w / 2f, h / 2f), new Vector2(w / 2f - 1f, h / 2f - 1f), radius);
            float alpha = Cov(d);
            if (alpha <= 0f) return Color.clear;
            float grain = Mathf.Sin((p.y + Mathf.PerlinNoise(p.x * 0.025f, p.y * 0.06f) * 22f) * 0.42f) * 0.5f + 0.5f;
            float fine = Mathf.PerlinNoise(p.x * 0.3f + 9f, p.y * 0.9f);
            Color c = Color.Lerp(a, b, grain * 0.55f + fine * 0.25f);
            float edge = -d;
            float v = p.y / h;
            if (edge < 3f) c = rim;                                                  // ขอบเข้ม
            else if (edge < 6f) c = v > 0.5f ? Color.Lerp(c, Color.white, 0.22f) : Color.Lerp(c, rim, 0.45f);  // นูน
            c *= Mathf.Lerp(0.86f, 1.04f, v);
            c.a = alpha;
            return c;
        });
    });

    /// <summary>กระดาษเก่า มีจุดด่างและขอบไหม้เข้ม</summary>
    public static Sprite ParchmentSheet(int radius) => Get("parch" + radius, () =>
    {
        const int size = 256;
        return Paint(size, size, 40, (p, w, h) =>
        {
            float d = RoundedBox(p, new Vector2(w / 2f, h / 2f), new Vector2(w / 2f - 1f, h / 2f - 1f), radius);
            float alpha = Cov(d);
            if (alpha <= 0f) return Color.clear;
            float n = Mathf.PerlinNoise(p.x * 0.05f, p.y * 0.05f) * 0.6f + Mathf.PerlinNoise(p.x * 0.2f + 3f, p.y * 0.2f) * 0.4f;
            Color c = Color.Lerp(Parchment, ParchmentDark, n * 0.7f);
            float burn = Mathf.Clamp01(1f + d / 30f);   // 1 ที่ขอบ -> 0 ด้านใน
            c = Color.Lerp(c, new Color(0.7f, 0.53f, 0.34f), burn * burn * 0.8f);
            c.a = alpha;
            return c;
        });
    });

    /// <summary>เหรียญทองแดง</summary>
    public static Sprite Coin => Get("coin", () => Paint(64, 64, 0, (p, w, h) =>
    {
        Vector2 c = new Vector2(32f, 32f);
        float d = (p - c).magnitude;
        float a = Cov(d - 30f);
        if (a <= 0f) return Color.clear;
        Color col = Color.Lerp(new Color(0.62f, 0.3f, 0.15f), new Color(0.93f, 0.6f, 0.38f), p.y / 64f);
        if (d > 26f) col = new Color(0.5f, 0.24f, 0.12f);                       // ขอบนอก
        else if (Mathf.Abs(d - 17f) < 2.2f) col = new Color(0.55f, 0.27f, 0.13f); // วงใน
        float hook = Mathf.Min(Segment(p, new Vector2(34f, 40f), new Vector2(34f, 26f)), Mathf.Abs((p - new Vector2(29f, 26f)).magnitude - 5f) + (p.y > 26f ? 99f : 0f));
        if (hook < 2.2f) col = new Color(0.5f, 0.24f, 0.12f);
        col.a = a;
        return col;
    }));

    /// <summary>เหรียญทองลายปลา (ใช้บน HUD)</summary>
    public static Sprite GoldCoin => Get("goldcoin", () => Paint(128, 128, 0, (p, w, h) => GoldCoinPixel(p)));

    /// <summary>เหรียญทองลายปลาความละเอียดสูง (โลโก้หน้า Title)</summary>
    public static Sprite GoldCoinLarge => Get("goldcoin512", () => Paint(512, 512, 0, (p, w, h) =>
    {
        Color c = GoldCoinPixel(p * 0.25f);
        c.a = Mathf.Clamp01(0.5f - (((p * 0.25f) - new Vector2(64f, 64f)).magnitude - 61f) * 4f);
        return c;
    }));

    /// <summary>แสงฟุ้งกลม (ขาว โปร่งที่ขอบ) ใช้ทำหิ่งห้อย/แสงเรือง</summary>
    public static Sprite Glow => Get("glow", () => Paint(64, 64, 0, (p, w, h) =>
    {
        float d = (p - new Vector2(32f, 32f)).magnitude / 32f;
        float a = Mathf.Clamp01(1f - d);
        return new Color(1f, 1f, 1f, a * a * a);
    }));

    /// <summary>ขอบจอมืด (vignette) ยืดเต็มจอ</summary>
    public static Sprite Vignette => Get("vignette", () => Paint(256, 256, 0, (p, w, h) =>
    {
        Vector2 q = new Vector2((p.x - 128f) / 128f, (p.y - 128f) / 128f);
        float d = Mathf.Clamp01((q.magnitude - 0.55f) / 0.9f);
        return new Color(0.05f, 0.03f, 0.02f, d * d * 0.85f);
    }));

    private static Color GoldCoinPixel(Vector2 p)
    {
        Vector2 c = new Vector2(64f, 64f);
        float d = (p - c).magnitude;
        float a = Cov(d - 61f);
        if (a <= 0f) return Color.clear;
        float v = p.y / 128f;
        Color col = Color.Lerp(new Color(0.75f, 0.5f, 0.1f), new Color(1f, 0.86f, 0.4f), v);
        if (d > 54f) col = Color.Lerp(new Color(0.55f, 0.34f, 0.06f), new Color(0.85f, 0.62f, 0.2f), v);   // ขอบนอก
        else if (Mathf.Abs(d - 46f) < 2.5f) col = new Color(0.68f, 0.44f, 0.1f);                            // วงใน
        // ปลานูน
        float body = Ellipse(p, new Vector2(58f, 64f), new Vector2(22f, 14f));
        float tail = TriangleSD(p, new Vector2(76f, 64f), new Vector2(94f, 80f), new Vector2(94f, 48f));
        float fish = Mathf.Min(body, tail);
        if (fish < 0f) col = Color.Lerp(new Color(0.66f, 0.42f, 0.08f), new Color(0.9f, 0.7f, 0.28f), Mathf.Clamp01((p.y - 50f) / 28f));
        else if (fish < 2.5f) col = new Color(0.55f, 0.34f, 0.06f);
        if ((p - new Vector2(46f, 67f)).magnitude < 2.8f) col = new Color(0.45f, 0.28f, 0.05f);
        // ประกายมุมบนซ้าย
        if (Ellipse(p, new Vector2(38f, 94f), new Vector2(9f, 4f)) < 0f) col = Color.Lerp(col, Color.white, 0.35f);
        col.a = a;
        return col;
    }

    /// <summary>ตะขอเบ็ดสีขาว (ย้อมสีได้)</summary>
    public static Sprite Hook => Get("hook", () => Paint(48, 64, 0, (p, w, h) =>
    {
        float shaft = Segment(p, new Vector2(30f, 58f), new Vector2(30f, 22f));
        float bend = Mathf.Abs((p - new Vector2(21f, 22f)).magnitude - 9f) + (p.y > 22f ? 99f : 0f);
        float tip = Segment(p, new Vector2(12f, 22f), new Vector2(12f, 30f));
        float barb = Segment(p, new Vector2(12f, 30f), new Vector2(17f, 25f));
        float eye = Mathf.Abs((p - new Vector2(30f, 58f)).magnitude - 3.5f);
        float d = Mathf.Min(Mathf.Min(shaft, bend), Mathf.Min(Mathf.Min(tip, barb), eye)) - 2.6f;
        return new Color(1f, 1f, 1f, Cov(d));
    }));

    /// <summary>แม่กุญแจสีขาว</summary>
    public static Sprite Lock => Get("lock", () => Paint(48, 56, 0, (p, w, h) =>
    {
        float body = RoundedBox(p, new Vector2(24f, 18f), new Vector2(17f, 14f), 4f);
        float shackle = Mathf.Abs((p - new Vector2(24f, 32f)).magnitude - 10f) - 3f;
        if (p.y < 32f) shackle = Mathf.Min(Segment(p, new Vector2(14f, 32f), new Vector2(14f, 28f)), Segment(p, new Vector2(34f, 32f), new Vector2(34f, 28f))) - 3f;
        float hole = (p - new Vector2(24f, 19f)).magnitude - 3.5f;
        float a = Mathf.Max(Cov(Mathf.Min(body, shackle)) - Cov(hole), 0f);
        return new Color(1f, 1f, 1f, a);
    }));

    /// <summary>ปลาประดับมุมกรอบ (ขาว ย้อมสีได้)</summary>
    public static Sprite FishOrnament => Get("fishorn", () => Paint(128, 64, 0, (p, w, h) =>
    {
        float body = Ellipse(p, new Vector2(56f, 32f), new Vector2(42f, 20f));
        float tail = TriangleSD(p, new Vector2(92f, 32f), new Vector2(124f, 54f), new Vector2(124f, 10f));
        float d = Mathf.Min(body, tail);
        float a = Cov(d);
        if (a <= 0f) return Color.clear;
        float shade = 0.85f + 0.15f * Mathf.Sin(p.x * 0.8f) * Mathf.Sin(p.y * 0.8f);   // เกล็ด
        if ((p - new Vector2(30f, 37f)).magnitude < 4f) shade = 0.35f;                 // ตา
        if (Mathf.Abs(p.x - 46f) < 1.5f && Mathf.Abs(p.y - 32f) < 15f) shade = 0.6f;     // เหงือก
        return new Color(shade, shade, shade, a);
    }));

    /// <summary>ไอคอนเหยื่อแต่ละชนิด (วาดด้วยโค้ด)</summary>
    public static Sprite BaitIcon(string id) => Get("bait_" + id, () =>
    {
        switch (id)
        {
            case "worm": return Paint(160, 160, 0, WormIcon);
            case "shrimp": return Paint(160, 160, 0, ShrimpIcon);
            default: return Paint(160, 160, 0, GoldenIcon);
        }
    });

    /// <summary>Image แบบ Sliced จากสไปรต์</summary>
    public static Image Sliced(string name, Transform parent, Sprite sprite, Vector2 size, Vector2 position, Color color)
    {
        Image img = UIFactory.Panel(name, parent, size, position, color);
        img.sprite = sprite;
        img.type = sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
        return img;
    }

    /// <summary>ใส่เงาตัวอักษร</summary>
    public static T Shadowed<T>(T graphic, Color color, float distance = 2f) where T : Graphic
    {
        var s = graphic.gameObject.AddComponent<Shadow>();
        s.effectColor = color;
        s.effectDistance = new Vector2(distance, -distance);
        return graphic;
    }

    // ------------------------------------------------------------------ Window building blocks

    /// <summary>สีความหายากที่อ่านออกบนกระดาษ (เข้มขึ้น)</summary>
    public static Color RarityInk(FishRarity rarity) => Color.Lerp(FishRarityInfo.Color(rarity), Ink, rarity == FishRarity.Common ? 0.7f : 0.4f);

    /// <summary>ปุ่มทาสี (ย้อมสีได้ เช่น ปุ่มขายสีเขียวหัวเป็ด)</summary>
    public static Sprite Painted(int radius) => Get("paint" + radius, () =>
    {
        int size = radius * 2 + 24;
        return Paint(size, size, radius + 4, (p, w, h) =>
        {
            float d = RoundedBox(p, new Vector2(w / 2f, h / 2f), new Vector2(w / 2f - 1f, h / 2f - 1f), radius);
            float a = Cov(d);
            if (a <= 0f) return Color.clear;
            float v = p.y / h;
            float s = Mathf.Lerp(0.72f, 0.95f, v);
            if (-d < 2.5f) s = 0.4f;
            else if (-d < 5f && v > 0.55f) s = 1f;
            return new Color(s, s, s, a);
        });
    });

    /// <summary>
    /// สร้างหน้าต่างแบบกรอบไม้ + กระดาษ (เงา ปลาประดับมุม) ใต้ root
    /// คืนกระดาษด้านในสำหรับวางเนื้อหา (ขนาด size - 56)
    /// </summary>
    public static RectTransform WoodWindow(Transform root, Vector2 size, out Image frame)
    {
        Sliced("Shadow", root, Rounded(30), size + new Vector2(16f, 16f), new Vector2(8f, -12f), new Color(0f, 0f, 0f, 0.45f)).raycastTarget = false;
        frame = Sliced("Frame", root, Wood(30), size, Vector2.zero, Color.white);
        Image paper = Sliced("Paper", frame.transform, ParchmentSheet(16), size - new Vector2(56f, 56f), Vector2.zero, Color.white);
        foreach (float side in new[] { -1f, 1f })
        {
            foreach (float vert in new[] { -1f, 1f })
            {
                Image fish = Sliced("Fish Ornament", frame.transform, FishOrnament, new Vector2(110f, 55f),
                    new Vector2(side * (size.x / 2f - 38f), vert * (size.y / 2f - 22f)), new Color(0.72f, 0.52f, 0.32f));
                fish.rectTransform.localRotation = Quaternion.Euler(0f, side > 0f ? 0f : 180f, vert > 0f ? -35f : 35f);
                fish.raycastTarget = false;
            }
        }
        return paper.rectTransform;
    }

    /// <summary>ปุ่มไม้ (light = ไม้สีส้มอ่อน)</summary>
    public static Button WoodButton(string name, Transform parent, string text, Vector2 size, Vector2 position, UnityEngine.Events.UnityAction onClick, int fontSize, bool light)
        => MakeButton(name, parent, Wood(14, light), Color.white, text, size, position, onClick, fontSize);

    /// <summary>ปุ่มทาสี ย้อมด้วย color</summary>
    public static Button PaintedButton(string name, Transform parent, string text, Vector2 size, Vector2 position, Color color, UnityEngine.Events.UnityAction onClick, int fontSize)
        => MakeButton(name, parent, Painted(14), color, text, size, position, onClick, fontSize);

    private static Button MakeButton(string name, Transform parent, Sprite sprite, Color color, string text, Vector2 size, Vector2 position, UnityEngine.Events.UnityAction onClick, int fontSize)
    {
        Image bg = Sliced(name, parent, sprite, size, position, color);
        var button = bg.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;   // ย้อมสีเองตอน Refresh
        button.onClick.AddListener(onClick);
        bg.gameObject.AddComponent<ButtonPress>();

        Text label = UIFactory.Label("Text", bg.transform, text, fontSize, TextAnchor.MiddleCenter, size, Vector2.zero, Cream, FontStyle.Bold);
        UIFactory.Stretch(label.rectTransform);
        Shadowed(label, new Color(0.12f, 0.06f, 0.02f, 0.8f));
        return button;
    }

    /// <summary>ตั้งสถานะปุ่ม: กดไม่ได้จะกลายเป็นสีเทา</summary>
    public static void SetButton(Button button, bool interactable, Color tint)
    {
        button.interactable = interactable;
        button.targetGraphic.color = interactable ? tint : new Color(tint.r * 0.62f + 0.05f, tint.g * 0.6f + 0.05f, tint.b * 0.58f + 0.05f, tint.a);
    }

    /// <summary>ปุ่มปิดสี่เหลี่ยมไม้มีกากบาท</summary>
    public static Button CloseButton(Transform parent, Vector2 position, UnityEngine.Events.UnityAction onClick)
    {
        Button b = WoodButton("Close X", parent, "✕", new Vector2(66f, 66f), position, onClick, 34, true);
        return b;
    }

    /// <summary>คานไม้หัวคอลัมน์</summary>
    public static Text HeaderBar(Transform parent, string text, Vector2 size, Vector2 position, int fontSize = 28)
    {
        Image bar = Sliced("Header", parent, Wood(10), size, position, Color.white);
        Text label = UIFactory.Label("Text", bar.transform, text, fontSize, TextAnchor.MiddleLeft, size - new Vector2(40f, 0f), Vector2.zero, Cream, FontStyle.Bold);
        Shadowed(label, new Color(0f, 0f, 0f, 0.55f));
        return label;
    }

    /// <summary>แผงกระดาษมีขอบสีน้ำตาลอ่อน (การ์ด)</summary>
    public static Image Card(string name, Transform parent, Vector2 size, Vector2 position, out Transform inner)
    {
        Image border = Sliced(name, parent, Rounded(12), size + new Vector2(6f, 6f), position, new Color(0.68f, 0.52f, 0.34f));
        inner = Sliced("Inner", border.transform, ParchmentSheet(10), size, Vector2.zero, new Color(1f, 0.98f, 0.93f)).transform;
        return border;
    }

    /// <summary>กรอบไม้ใส่รูปปลา (คืน RawImage สำหรับไอคอน)</summary>
    public static RawImage PictureFrame(Transform parent, Vector2 size, Vector2 position)
    {
        Image frame = Sliced("PictureFrame", parent, Wood(12), size, position, Color.white);
        Sliced("PictureBg", frame.transform, ParchmentSheet(10), size - new Vector2(14f, 14f), Vector2.zero, new Color(0.93f, 0.86f, 0.72f));
        return UIFactory.RawImage("Picture", frame.transform, size - new Vector2(18f, 18f), Vector2.zero);
    }

    /// <summary>ข้อความ + ไอคอนเหรียญต่อท้าย</summary>
    public static Text CoinLabel(string name, Transform parent, int fontSize, Vector2 size, Vector2 position, out Image coin)
    {
        Text label = UIFactory.Label(name, parent, "", fontSize, TextAnchor.MiddleLeft, size, position, Ink);
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        coin = Sliced("Coin", label.transform, Coin, new Vector2(fontSize * 1.15f, fontSize * 1.15f), Vector2.zero, Color.white);
        coin.rectTransform.anchorMin = coin.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        coin.raycastTarget = false;
        return label;
    }

    /// <summary>วางไอคอนเหรียญต่อท้ายข้อความ (เรียกหลังเปลี่ยนข้อความ)</summary>
    public static void PlaceCoin(Text label, Image coin)
    {
        coin.gameObject.SetActive(!string.IsNullOrEmpty(label.text));
        coin.rectTransform.anchoredPosition = new Vector2(label.preferredWidth + coin.rectTransform.sizeDelta.x * 0.5f + 8f, 0f);
    }

    /// <summary>เอฟเฟกต์ปุ่ม: เมาส์ชี้ขยายเล็กน้อย กดแล้วยุบ</summary>
    public class ButtonPress : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler,
        UnityEngine.EventSystems.IPointerDownHandler, UnityEngine.EventSystems.IPointerUpHandler
    {
        private Button button;
        private float target = 1f;

        private void Awake() => button = GetComponent<Button>();
        private void OnDisable() { target = 1f; transform.localScale = Vector3.one; }
        private void Update() => transform.localScale = Vector3.one * Mathf.MoveTowards(transform.localScale.x, target, Time.unscaledDeltaTime * 1.5f);

        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e) { if (button.interactable) target = 1.05f; }
        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) => target = 1f;
        public void OnPointerDown(UnityEngine.EventSystems.PointerEventData e) { if (button.interactable) target = 0.94f; }
        public void OnPointerUp(UnityEngine.EventSystems.PointerEventData e) => target = button.interactable ? 1.05f : 1f;
    }

    // ------------------------------------------------------------------ Bait icons

    private static Color IconBackground(Vector2 p, Color top, Color bottom, out float alpha)
    {
        float d = RoundedBox(p, new Vector2(80f, 80f), new Vector2(79f, 79f), 14f);
        alpha = Cov(d);
        float vignette = Mathf.Clamp01((p - new Vector2(80f, 90f)).magnitude / 110f);
        return Color.Lerp(Color.Lerp(bottom, top, p.y / 160f), bottom * 0.6f, vignette * vignette);
    }

    private static Color WormIcon(Vector2 p, int w, int h)
    {
        Color c = IconBackground(p, new Color(0.62f, 0.5f, 0.38f), new Color(0.36f, 0.27f, 0.2f), out float a);
        if (a <= 0f) return Color.clear;
        Vector2 center = new Vector2(80f, 76f);
        float bowl = Ellipse(p, center, new Vector2(64f, 56f));
        if (bowl < 0f)
        {
            float rim = Ellipse(p, center + new Vector2(0f, 4f), new Vector2(54f, 44f));
            if (rim > 0f) c = Color.Lerp(new Color(0.55f, 0.38f, 0.28f), new Color(0.8f, 0.64f, 0.52f), Mathf.Clamp01((p.y - 30f) / 100f)); // ชามดินเผา
            else
            {
                float n = Mathf.PerlinNoise(p.x * 0.25f, p.y * 0.25f);
                c = Color.Lerp(new Color(0.2f, 0.13f, 0.09f), new Color(0.33f, 0.22f, 0.15f), n);  // ดิน
                // ไส้เดือน 3 ตัว
                float worm = float.MaxValue;
                worm = Mathf.Min(worm, Curve(p, t => new Vector2(38f + t * 80f, 100f + Mathf.Sin(t * 10f) * 6f), 40));
                worm = Mathf.Min(worm, Curve(p, t => new Vector2(44f + t * 76f, 78f + Mathf.Sin(t * 9f + 2f) * 6f), 40));
                worm = Mathf.Min(worm, Curve(p, t => new Vector2(40f + t * 70f, 56f + Mathf.Sin(t * 11f + 4f) * 5f), 40));
                if (worm < 5.5f)
                {
                    float shade = 1f - worm / 5.5f;
                    c = Color.Lerp(new Color(0.62f, 0.3f, 0.32f), new Color(0.95f, 0.62f, 0.6f), shade);
                    if (Mathf.Repeat(p.x * 0.5f + p.y * 0.5f, 6f) < 0.8f) c *= 0.85f;   // ปล้อง
                }
            }
        }
        c.a = a;
        return c;
    }

    private static Color ShrimpIcon(Vector2 p, int w, int h)
    {
        Color c = IconBackground(p, new Color(0.62f, 0.78f, 0.72f), new Color(0.3f, 0.45f, 0.42f), out float a);
        if (a <= 0f) return Color.clear;
        Vector2 center = new Vector2(80f, 76f);
        float jar = Ellipse(p, center, new Vector2(64f, 58f));
        if (jar < 0f)
        {
            float inner = Ellipse(p, center + new Vector2(0f, 4f), new Vector2(55f, 47f));
            if (inner > 0f) c = Color.Lerp(new Color(0.55f, 0.42f, 0.35f), new Color(0.85f, 0.7f, 0.58f), Mathf.Clamp01((p.y - 30f) / 100f));
            else
            {
                c = Color.Lerp(new Color(0.24f, 0.5f, 0.5f), new Color(0.55f, 0.8f, 0.78f), Mathf.Clamp01((p.y - 40f) / 90f)); // น้ำ
                // สาหร่าย
                float weed = Mathf.Min(Curve(p, t => new Vector2(96f + Mathf.Sin(t * 5f) * 5f, 40f + t * 74f), 30), Curve(p, t => new Vector2(104f + t * 14f, 46f + t * 60f), 20));
                if (weed < 2.2f) c = new Color(0.3f, 0.55f, 0.25f);
                // กุ้ง 3 ตัว (ส่วนโค้ง)
                foreach (Vector3 s in new[] { new Vector3(58f, 90f, 18f), new Vector3(98f, 66f, 15f) })
                {
                    Vector2 sc = new Vector2(s.x, s.y);
                    float r = s.z;
                    float ang = Mathf.Atan2(p.y - sc.y, p.x - sc.x);
                    float ring = Mathf.Abs((p - sc).magnitude - r);
                    if (ang < 2.2f && ang > -2.4f && ring < r * 0.5f)
                    {
                        float shade = 1f - ring / (r * 0.5f);
                        c = Color.Lerp(new Color(0.85f, 0.4f, 0.25f), new Color(1f, 0.75f, 0.55f), shade);
                        if (Mathf.Repeat(ang * 4f, 1f) < 0.12f) c *= 0.85f;
                    }
                    if ((p - (sc + new Vector2(Mathf.Cos(2.2f), Mathf.Sin(2.2f)) * r)).magnitude < 2f) c = Color.black;   // ตา
                    if (Segment(p, sc + new Vector2(Mathf.Cos(2.2f), Mathf.Sin(2.2f)) * r, sc + new Vector2(-r * 1.6f, r * 1.3f)) < 0.8f) c = new Color(0.9f, 0.5f, 0.35f); // หนวด
                }
            }
            // แสงสะท้อนแก้ว
            if (Ellipse(p, center + new Vector2(-30f, 22f), new Vector2(6f, 16f)) < 0f) c = Color.Lerp(c, Color.white, 0.45f);
        }
        c.a = a;
        return c;
    }

    private static Color GoldenIcon(Vector2 p, int w, int h)
    {
        Color c = IconBackground(p, new Color(0.28f, 0.26f, 0.4f), new Color(0.1f, 0.1f, 0.16f), out float a);
        if (a <= 0f) return Color.clear;
        float velvet = Mathf.PerlinNoise(p.x * 0.08f, p.y * 0.08f);
        c = Color.Lerp(c, c * 1.3f, velvet * 0.4f);

        // กล่อง
        float box = RoundedBox(p, new Vector2(80f, 50f), new Vector2(62f, 28f), 6f);
        if (box < 0f)
        {
            c = Color.Lerp(new Color(0.08f, 0.07f, 0.08f), new Color(0.2f, 0.18f, 0.2f), p.y / 80f);
            if (box > -4f || Mathf.Abs(p.y - 66f) < 1.5f) c = new Color(0.82f, 0.62f, 0.25f);  // ขอบทอง
        }
        // ปลาทอง
        float body = Ellipse(p, new Vector2(74f, 92f), new Vector2(40f, 22f));
        float tail = TriangleSD(p, new Vector2(108f, 92f), new Vector2(142f, 118f), new Vector2(138f, 70f));
        float fish = Mathf.Min(body, tail);
        if (fish < 0f)
        {
            float shine = Mathf.Clamp01((p.y - 70f) / 44f);
            c = Color.Lerp(new Color(0.7f, 0.45f, 0.1f), new Color(1f, 0.88f, 0.45f), shine);
            if (Mathf.Sin(p.x * 0.7f) * Mathf.Sin(p.y * 0.7f) > 0.6f) c *= 0.88f;  // เกล็ด
            if ((p - new Vector2(48f, 97f)).magnitude < 3.5f) c = new Color(0.3f, 0.18f, 0.05f);
        }
        else if (fish < 2f) c = new Color(0.55f, 0.33f, 0.08f);
        // ประกาย
        foreach (Vector2 s in new[] { new Vector2(30f, 130f), new Vector2(126f, 138f), new Vector2(120f, 40f), new Vector2(40f, 118f) })
        {
            Vector2 d = p - s;
            if ((Mathf.Abs(d.x) < 1.2f && Mathf.Abs(d.y) < 7f) || (Mathf.Abs(d.y) < 1.2f && Mathf.Abs(d.x) < 7f) || d.magnitude < 2.2f)
                c = new Color(1f, 0.95f, 0.7f);
        }
        c.a = a;
        return c;
    }

    // ------------------------------------------------------------------ Drawing helpers

    private static Sprite Get(string key, Func<Sprite> make)
    {
        if (!cache.TryGetValue(key, out Sprite s) || s == null) cache[key] = s = make();
        return s;
    }

    private static Sprite Paint(int w, int h, float border, Func<Vector2, int, int, Color> pixel)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
            px[y * w + x] = pixel(new Vector2(x + 0.5f, y + 0.5f), w, h);
        tex.SetPixels(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
    }

    private static float Cov(float d) => Mathf.Clamp01(0.5f - d);

    private static float RoundedBox(Vector2 p, Vector2 c, Vector2 half, float r)
    {
        Vector2 q = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - half + new Vector2(r, r);
        return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
    }

    private static float Ellipse(Vector2 p, Vector2 c, Vector2 radius)
    {
        Vector2 q = new Vector2((p.x - c.x) / radius.x, (p.y - c.y) / radius.y);
        return (q.magnitude - 1f) * Mathf.Min(radius.x, radius.y);
    }

    private static float Segment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return (p - (a + ab * t)).magnitude;
    }

    private static float Curve(Vector2 p, Func<float, Vector2> f, int samples)
    {
        float best = float.MaxValue;
        Vector2 prev = f(0f);
        for (int i = 1; i <= samples; i++)
        {
            Vector2 next = f(i / (float)samples);
            best = Mathf.Min(best, Segment(p, prev, next));
            prev = next;
        }
        return best;
    }

    private static float TriangleSD(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d = Mathf.Min(Segment(p, a, b), Mathf.Min(Segment(p, b, c), Segment(p, c, a)));
        bool inside = Side(p, a, b) == Side(p, b, c) && Side(p, b, c) == Side(p, c, a);
        return inside ? -d : d;
    }

    private static bool Side(Vector2 p, Vector2 a, Vector2 b) => (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x) > 0f;
}
