using UnityEngine;

/// <summary>หมุนแสงอาทิตย์ตามเวลาในเกม: เช้าแสงอุ่นต่ำ เที่ยงสูงสว่าง เย็นแสงส้ม วางไว้บน Directional Light</summary>
[RequireComponent(typeof(Light))]
public class SunCycle : MonoBehaviour
{
    [SerializeField] private float yaw = 135f;
    [SerializeField] private float minPitch = 28f;
    [SerializeField] private float maxPitch = 60f;
    [SerializeField] private Gradient color = DefaultGradient();
    [SerializeField] private AnimationCurve intensity = new AnimationCurve(
        new Keyframe(0f, 0.8f), new Keyframe(0.5f, 1.45f), new Keyframe(1f, 0.6f));

    private Light sun;

    private void Awake() => sun = GetComponent<Light>();

    private void Update()
    {
        float t = GameState.Instance.TimeOfDay01;
        float pitch = Mathf.Lerp(minPitch, maxPitch, Mathf.Sin(t * Mathf.PI));
        transform.rotation = Quaternion.Euler(pitch, yaw + Mathf.Lerp(-40f, 40f, t), 0f);
        sun.color = color.Evaluate(t);
        sun.intensity = intensity.Evaluate(t);
    }

    private static Gradient DefaultGradient()
    {
        var g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.78f, 0.6f), 0f),
                new GradientColorKey(new Color(1f, 0.95f, 0.85f), 0.45f),
                new GradientColorKey(new Color(1f, 0.7f, 0.45f), 1f),
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return g;
    }
}
