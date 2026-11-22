using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>ตัวช่วยสร้าง uGUI ด้วยโค้ด (ฟอนต์ไทย แผง ข้อความ ปุ่ม) ใช้ร่วมกันใน UI ของเกม</summary>
public static class UIFactory
{
    private static Font font;

    /// <summary>ฟอนต์จากระบบที่รองรับภาษาไทย</summary>
    public static Font Font
    {
        get
        {
            if (font == null) font = Font.CreateDynamicFontFromOSFont(new[] { "Leelawadee UI", "Tahoma", "Arial" }, 32);
            return font;
        }
    }

    public static readonly Color PanelColor = new Color(0.07f, 0.1f, 0.1f, 0.94f);
    public static readonly Color PanelLight = new Color(0.13f, 0.17f, 0.17f, 1f);
    public static readonly Color ButtonColor = new Color(0.24f, 0.45f, 0.36f, 1f);

    public static Canvas CreateCanvas(Transform parent, string name, int sortingOrder)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    /// <summary>สร้าง EventSystem (Input System) ถ้ายังไม่มี เพื่อให้กดปุ่มได้</summary>
    public static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        Object.DontDestroyOnLoad(go);
    }

    /// <summary>RectTransform ยึดกลางแม่ ตำแหน่ง/ขนาดเป็นพิกเซล (อ้างอิง 1920x1080)</summary>
    public static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    /// <summary>ให้ rect ขยายเต็มแม่ เว้นขอบ inset</summary>
    public static RectTransform Stretch(RectTransform rect, float inset = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
        return rect;
    }

    public static Image Panel(string name, Transform parent, Vector2 size, Vector2 position, Color color)
    {
        var image = Rect(name, parent, size, position).gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    public static Text Label(string name, Transform parent, string text, int size, TextAnchor align, Vector2 boxSize, Vector2 position, Color? color = null, FontStyle style = FontStyle.Normal)
    {
        var label = Rect(name, parent, boxSize, position).gameObject.AddComponent<Text>();
        label.font = Font;
        label.text = text;
        label.fontSize = size;
        label.fontStyle = style;
        label.alignment = align;
        label.color = color ?? Color.white;
        label.supportRichText = true;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;
        return label;
    }

    public static Button Button(string name, Transform parent, string text, Vector2 size, Vector2 position, Color color, UnityAction onClick, int fontSize = 26)
    {
        Image bg = Panel(name, parent, size, position, color);
        var button = bg.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        button.colors = colors;
        button.onClick.AddListener(onClick);

        Text label = Label("Text", bg.transform, text, fontSize, TextAnchor.MiddleCenter, size, Vector2.zero);
        Stretch(label.rectTransform);
        return button;
    }

    public static RawImage RawImage(string name, Transform parent, Vector2 size, Vector2 position)
    {
        var image = Rect(name, parent, size, position).gameObject.AddComponent<RawImage>();
        image.raycastTarget = false;
        return image;
    }
}
