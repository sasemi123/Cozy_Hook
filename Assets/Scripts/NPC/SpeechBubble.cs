using UnityEngine;
using UnityEngine.UI;

/// <summary>ลูกโป่งคำพูดลอยเหนือหัว NPC (world space) หันเข้ากล้องเสมอ ค่อยๆ จางหายเมื่อหมดเวลา</summary>
public class SpeechBubble : MonoBehaviour
{
    private const float WorldHeight = 0.32f;   // ความสูงตัวอักษรในโลก (เมตรต่อ 100 พิกเซล)
    private const float MaxWidth = 900f;

    private CanvasGroup group;
    private RectTransform background;
    private Text label;
    private Camera cam;
    private float timer;
    private float duration;

    public bool IsShowing => timer > 0f;

    public static SpeechBubble Create(Transform parent, Vector3 localPosition)
    {
        var go = new GameObject("Speech Bubble", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        var bubble = go.AddComponent<SpeechBubble>();
        bubble.Build();
        return bubble;
    }

    private void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 2;
        var rect = (RectTransform)transform;
        rect.sizeDelta = new Vector2(MaxWidth, 160f);
        transform.localScale = Vector3.one * (WorldHeight / 100f);
        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;

        Image bg = UIFactory.Panel("Bg", transform, new Vector2(300f, 110f), Vector2.zero, new Color(1f, 0.98f, 0.92f, 0.96f));
        background = bg.rectTransform;
        // หางลูกโป่ง (สี่เหลี่ยมหมุน 45°)
        Image tail = UIFactory.Panel("Tail", background, new Vector2(34f, 34f), Vector2.zero, bg.color);
        tail.rectTransform.anchorMin = tail.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        tail.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        tail.transform.SetAsFirstSibling();

        label = UIFactory.Label("Text", transform, "", 50, TextAnchor.MiddleCenter, new Vector2(MaxWidth - 60f, 140f), Vector2.zero, new Color(0.22f, 0.16f, 0.1f), FontStyle.Bold);
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        gameObject.SetActive(true);
    }

    public void Show(string text, float seconds)
    {
        label.text = text;
        float width = Mathf.Min(MaxWidth, label.preferredWidth + 70f);
        label.rectTransform.sizeDelta = new Vector2(width - 60f, 140f);
        float height = Mathf.Max(100f, label.preferredHeight + 40f);
        background.sizeDelta = new Vector2(width, height);
        duration = seconds;
        timer = seconds;
    }

    private void LateUpdate()
    {
        if (timer > 0f) timer -= Time.deltaTime;
        float target = timer > 0f ? 1f : 0f;
        group.alpha = Mathf.MoveTowards(group.alpha, target, Time.deltaTime * (target > 0f ? 6f : 2.5f));
        // เด้งขึ้นเล็กน้อยตอนเริ่มพูด
        float pop = timer > 0f ? 1f + Mathf.Max(0f, (timer - duration + 0.25f) * 0.6f) : 1f;
        transform.localScale = Vector3.one * (WorldHeight / 100f) * pop;

        if (group.alpha <= 0f) return;
        if (cam == null) cam = Camera.main;
        if (cam == null) return;
        Vector3 forward = Vector3.ProjectOnPlane(transform.position - cam.transform.position, Vector3.up);
        if (forward.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
    }
}
