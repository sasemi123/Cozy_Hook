using UnityEngine;

/// <summary>
/// แอนิเมชันคันเบ็ด: ตอนกดค้างจะง้างคันไปด้านหลังตามพลัง พอปล่อยจะสะบัดไปข้างหน้าแล้วกลับท่าปกติ
/// </summary>
public class RodCastAnimator : MonoBehaviour
{
    [SerializeField] private FishingRodCaster caster;
    [Tooltip("ส่วนของคันที่หมุน (pivot อยู่ที่มือ)")]
    [SerializeField] private Transform rodPivot;
    [SerializeField] private float restPitch = 75f;
    [SerializeField] private float pullBackAngle = 70f;
    [SerializeField] private float flickAngle = 25f;
    [SerializeField, Min(0.01f)] private float flickDuration = 0.25f;
    [SerializeField, Min(0f)] private float sharpness = 14f;
    [Tooltip("มุมที่คันเบ็ดถูกยกขึ้นตอนสู้กับปลา (ยิ่งตึงยิ่งยกสูง)")]
    [SerializeField] private float fightLiftAngle = 40f;

    private FishingLineTension lineTension;

    public Transform RodPivot => rodPivot;

    private float currentPitch;
    private float flickTimer;
    private bool wasCharging;

    private void Awake()
    {
        if (caster == null) caster = GetComponent<FishingRodCaster>();
        lineTension = GetComponent<FishingLineTension>();
        currentPitch = restPitch;
    }

    private void LateUpdate()
    {
        if (caster == null || rodPivot == null) return;

        bool charging = caster.IsCharging;
        if (wasCharging && !charging) flickTimer = flickDuration;
        wasCharging = charging;

        float target = restPitch;
        if (lineTension != null && lineTension.IsFighting)
        {
            // สู้กับปลา: ยกคันขึ้นตามความตึง และสั่นตอนปลาดิ้น
            target = restPitch - fightLiftAngle * (0.35f + 0.65f * lineTension.Tension01);
            if (lineTension.IsThrashing) target += Mathf.Sin(Time.time * 40f) * 3f;
        }
        else if (charging) target = restPitch - pullBackAngle * caster.ChargePercent;
        else if (flickTimer > 0f)
        {
            flickTimer -= Time.deltaTime;
            target = restPitch + flickAngle;
        }

        float speed = flickTimer > 0f ? sharpness * 2.5f : sharpness;
        currentPitch = Mathf.Lerp(currentPitch, target, 1f - Mathf.Exp(-speed * Time.deltaTime));
        rodPivot.localRotation = Quaternion.Euler(currentPitch, 0f, 0f);
    }
}
