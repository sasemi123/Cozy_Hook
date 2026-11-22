using UnityEngine;

/// <summary>
/// แอนิเมชันแบบโปรซีเดอรัลสำหรับตัวละครบล็อก: แกว่งแขนขาตามความเร็วเดิน และหายใจเบาๆ ตอนยืน
/// </summary>
public class FishermanAnimator : MonoBehaviour
{
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private Transform body;
    [SerializeField] private Transform leftLeg;
    [SerializeField] private Transform rightLeg;
    [SerializeField] private Transform leftArm;
    [SerializeField] private Transform rightArm;

    [SerializeField, Min(0f)] private float strideFrequency = 1.6f;
    [SerializeField, Min(0f)] private float legSwing = 32f;
    [SerializeField, Min(0f)] private float armSwing = 26f;
    [SerializeField, Min(0f)] private float bobHeight = 0.04f;

    [Header("Holding Rod")]
    [Tooltip("มุมยกแขนขวาไปข้างหน้าตอนถือคันเบ็ด (ค่าลบ = ยกไปข้างหน้า)")]
    [SerializeField] private float rightArmHoldPitch = 0f;
    [Tooltip("สัดส่วนการแกว่งแขนขวาตอนเดิน (ถือคันเบ็ดจะแกว่งน้อยลง)")]
    [SerializeField, Range(0f, 1f)] private float rightArmSwingScale = 1f;

    private float phase;
    private float blend;
    private Vector3 bodyRestPosition;
    private float holdPitchDefault;
    private float holdSwingDefault;
    private bool holdingRod = true;

    /// <summary> สลับท่าแขนขวา: ถือคันเบ็ด หรือแขนว่าง (ตอนเก็บคันไว้ที่หลัง) </summary>
    public void SetHoldingRod(bool holding) => holdingRod = holding;

    private void Awake()
    {
        holdPitchDefault = rightArmHoldPitch;
        holdSwingDefault = rightArmSwingScale;

        if (movement == null) movement = GetComponent<PlayerMovement>();
        if (body != null) bodyRestPosition = body.localPosition;
    }

    private void Update()
    {
        float speed = movement != null ? movement.HorizontalSpeed : 0f;
        float maxSpeed = movement != null ? Mathf.Max(0.1f, movement.RunSpeed) : 4.5f;
        blend = Mathf.MoveTowards(blend, Mathf.Clamp01(speed / maxSpeed * 1.6f), Time.deltaTime * 4f);

        phase += Time.deltaTime * strideFrequency * Mathf.PI * 2f * Mathf.Lerp(0.6f, 1.4f, speed / maxSpeed);
        float swing = Mathf.Sin(phase) * blend;

        SetPitch(leftLeg, swing * legSwing);
        SetPitch(rightLeg, -swing * legSwing);
        SetPitch(leftArm, -swing * armSwing);
        float targetHold = holdingRod ? holdPitchDefault : 0f;
        rightArmHoldPitch = Mathf.MoveTowards(rightArmHoldPitch, targetHold, Time.deltaTime * 180f);
        rightArmSwingScale = holdingRod ? holdSwingDefault : 1f;
        SetPitch(rightArm, rightArmHoldPitch + swing * armSwing * rightArmSwingScale);

        if (body != null)
        {
            float breathe = Mathf.Sin(Time.time * 1.8f) * 0.008f * (1f - blend);
            float bob = Mathf.Abs(Mathf.Sin(phase)) * bobHeight * blend;
            body.localPosition = bodyRestPosition + Vector3.up * (breathe + bob);
        }
    }

    private static void SetPitch(Transform t, float angle)
    {
        if (t != null) t.localRotation = Quaternion.Euler(angle, 0f, 0f);
    }
}
