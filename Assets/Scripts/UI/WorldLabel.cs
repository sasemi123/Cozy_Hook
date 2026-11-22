using UnityEngine;
using UnityEngine.UI;

/// <summary>ป้ายข้อความลอยในโลก 3D (เช่น "ตลาดปลา") หันเข้าหากล้องเสมอ รองรับภาษาไทย</summary>
public class WorldLabel : MonoBehaviour
{
    [SerializeField] private string text = "ป้าย";
    [SerializeField] private Color color = new Color(1f, 0.95f, 0.8f);
    [SerializeField, Min(0.001f)] private float worldHeight = 0.5f;
    [SerializeField] private bool background = true;

    private Transform canvasTransform;
    private Camera cam;

    public void SetText(string value)
    {
        text = value;
        var label = GetComponentInChildren<Text>();
        if (label != null) label.text = value;
    }

    private void Awake()
    {
        var canvasGo = new GameObject("Label Canvas", typeof(RectTransform));
        canvasTransform = canvasGo.transform;
        canvasTransform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rect = (RectTransform)canvasTransform;
        rect.sizeDelta = new Vector2(600f, 100f);
        canvasTransform.localScale = Vector3.one * (worldHeight / 100f);

        if (background)
        {
            var bg = new GameObject("Bg", typeof(RectTransform)).AddComponent<Image>();
            bg.transform.SetParent(canvasTransform, false);
            bg.color = new Color(0.08f, 0.1f, 0.1f, 0.65f);
            var br = bg.rectTransform;
            br.sizeDelta = new Vector2(80f + text.Length * 30f, 90f);
        }

        Text label = UIFactory.Label("Text", canvasTransform, text, 64, TextAnchor.MiddleCenter, new Vector2(600f, 100f), Vector2.zero, color, FontStyle.Bold);
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        var outline = label.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.6f);
        outline.effectDistance = new Vector2(2f, -2f);
    }

    private void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;
        // หันหน้าป้ายเข้าหากล้อง (หมุนเฉพาะแนวนอน)
        Vector3 forward = Vector3.ProjectOnPlane(canvasTransform.position - cam.transform.position, Vector3.up);
        if (forward.sqrMagnitude > 0.001f) canvasTransform.rotation = Quaternion.LookRotation(forward, Vector3.up);
    }

#if UNITY_EDITOR
    public void EditorSetup(string value, float height, Color c)
    {
        text = value;
        worldHeight = height;
        color = c;
    }
#endif
}
