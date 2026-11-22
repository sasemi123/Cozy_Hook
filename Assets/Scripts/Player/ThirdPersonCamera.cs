using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// กล้องมุมมองบุคคลที่สาม: คลิกขวาค้างแล้วลากเพื่อหมุนรอบตัวละคร, ลูกกลิ้งเมาส์เพื่อซูม
/// กล้องจะขยับเข้ามาเองเมื่อมีสิ่งกีดขวาง (ต้นไม้ ก้อนหิน)
/// </summary>
public class ThirdPersonCamera : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float targetHeight = 1.55f;
    [SerializeField, Min(0.5f)] private float distance = 4.5f;
    [SerializeField, Min(0.5f)] private float minDistance = 1.8f;
    [SerializeField, Min(0.5f)] private float maxDistance = 9f;
    [SerializeField, Min(0f)] private float orbitSensitivity = 0.15f;
    [SerializeField, Min(0f)] private float zoomSensitivity = 0.004f;
    [SerializeField] private float minPitch = -15f;
    [SerializeField] private float maxPitch = 65f;
    [SerializeField, Min(0f)] private float followSharpness = 12f;
    [SerializeField, Min(0f)] private float collisionRadius = 0.2f;

    private float yaw;
    private float pitch = 15f;
    private Vector3 smoothedPivot;

    public Transform Target
    {
        get => target;
        set => target = value;
    }

    private void Start()
    {
        Vector3 e = transform.eulerAngles;
        yaw = e.y;
        pitch = Mathf.Clamp(e.x > 180f ? e.x - 360f : e.x, minPitch, maxPitch);
        if (target != null) smoothedPivot = target.position + Vector3.up * targetHeight;
    }

    /// <summary>วางกล้องไว้ข้างหลังตัวละครทันที (ใช้ตอนเกิด/วาร์ป)</summary>
    public void SnapBehindTarget()
    {
        if (target == null) return;
        yaw = target.eulerAngles.y;
        smoothedPivot = target.position + Vector3.up * targetHeight;
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        transform.SetPositionAndRotation(smoothedPivot + rotation * Vector3.back * distance, rotation);
    }

    private void LateUpdate()
    {
        if (target == null) return;

        Mouse mouse = Mouse.current;
        if (mouse != null && !PlayerInputGate.IsBlocked)
        {
            if (mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue() * orbitSensitivity;
                yaw += delta.x;
                pitch = Mathf.Clamp(pitch - delta.y, minPitch, maxPitch);
            }
            distance = Mathf.Clamp(distance - mouse.scroll.ReadValue().y * zoomSensitivity, minDistance, maxDistance);
        }

        Vector3 pivot = target.position + Vector3.up * targetHeight;
        smoothedPivot = Vector3.Lerp(smoothedPivot, pivot, 1f - Mathf.Exp(-followSharpness * Time.deltaTime));

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 back = rotation * Vector3.back;
        float d = distance;

        foreach (RaycastHit hit in Physics.SphereCastAll(smoothedPivot, collisionRadius, back, distance, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(target)) continue;
            d = Mathf.Min(d, Mathf.Max(minDistance * 0.5f, hit.distance - 0.05f));
        }

        transform.SetPositionAndRotation(smoothedPivot + back * d, rotation);
    }
}
