using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// ฐานของหน้าต่าง UI ที่เปิดจากการโต้ตอบ (ป้ายประกาศ, ตลาด):
/// เปิดได้ทีละหน้าต่าง, บล็อกการเหวี่ยงเบ็ด/หมุนกล้อง, ปิดด้วย Esc / E / ปุ่ม ✕
/// </summary>
public abstract class GameWindow : MonoBehaviour
{
    private static GameWindow openWindow;

    protected GameObject window;
    private int openedFrame;
    private bool refreshNextFrame;

    public bool IsOpen { get; private set; }

    protected virtual void Awake()
    {
        UIFactory.EnsureEventSystem();
        BuildUI();
        window.SetActive(false);
    }

    protected virtual void OnDisable()
    {
        if (IsOpen) SetOpen(false);
    }

    protected virtual void Update()
    {
        if (!IsOpen) return;

        if (refreshNextFrame)
        {
            refreshNextFrame = false;
            Refresh();
        }

        Keyboard kb = Keyboard.current;
        if (kb != null && Time.frameCount != openedFrame && (kb.escapeKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame))
            SetOpen(false);
    }

    public void SetOpen(bool open)
    {
        if (open == IsOpen) return;
        if (open && openWindow != null && openWindow != this) openWindow.SetOpen(false);

        IsOpen = open;
        window.SetActive(open);
        if (open)
        {
            openWindow = this;
            openedFrame = Time.frameCount;
            PlayerInputGate.Block();
            OnOpened();
            Refresh();
        }
        else
        {
            if (openWindow == this) openWindow = null;
            PlayerInputGate.Unblock();
        }
    }

    /// <summary>ให้วาด UI ใหม่ในเฟรมถัดไป (ใช้หลังเรนเดอร์ไอคอนปลา)</summary>
    protected void RefreshNextFrame() => refreshNextFrame = true;

    protected abstract void BuildUI();
    protected virtual void OnOpened() { }
    protected virtual void Refresh() { }

    /// <summary>สร้างฉากหลังมืด + กรอบหน้าต่าง + หัวเรื่อง + ปุ่มปิด คืนค่าพื้นที่ด้านใน</summary>
    protected RectTransform CreateFrame(string name, string title, Vector2 size, int sortingOrder, Color frameColor, Color innerColor, Color titleColor)
    {
        Canvas canvas = UIFactory.CreateCanvas(transform, name + " Canvas", sortingOrder);
        Image dim = UIFactory.Panel("Dim", canvas.transform, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.45f));
        UIFactory.Stretch(dim.rectTransform);
        window = dim.gameObject;

        Image frame = UIFactory.Panel(name, window.transform, size, Vector2.zero, frameColor);
        RectTransform inner = UIFactory.Stretch(UIFactory.Panel("Inner", frame.transform, Vector2.zero, Vector2.zero, innerColor).rectTransform, 6f);

        float top = size.y / 2f;
        UIFactory.Label("Title", inner, title, 40, TextAnchor.MiddleLeft, new Vector2(size.x - 160f, 56f), new Vector2(-50f, top - 44f), titleColor, FontStyle.Bold);
        UIFactory.Button("Close", inner, "✕", new Vector2(48f, 48f), new Vector2(size.x / 2f - 42f, top - 42f), new Color(0.55f, 0.22f, 0.2f), () => SetOpen(false), 28);
        UIFactory.Label("CloseHint", inner, "Esc / E ปิด", 18, TextAnchor.MiddleRight, new Vector2(200f, 26f), new Vector2(size.x / 2f - 120f, -top + 24f), new Color(titleColor.r, titleColor.g, titleColor.b, 0.6f));
        return inner;
    }
}
