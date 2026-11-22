using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// กดเลข 1 เพื่อเก็บคันเบ็ดไว้ที่หลังตัวผู้เล่น กดอีกครั้งเพื่อหยิบกลับมาถือ
/// ระหว่างเก็บคันเบ็ดจะเหวี่ยงเบ็ดไม่ได้ และทุ่นที่ยังอยู่ในน้ำจะถูกเก็บทันที
/// วางสคริปต์นี้บนตัวผู้เล่น
/// </summary>
public class RodHolster : MonoBehaviour
{
    [SerializeField] private Transform rod;
    [Tooltip("จุดในมือขวาสำหรับถือคันเบ็ด")]
    [SerializeField] private Transform handSocket;
    [Tooltip("จุดที่หลังสำหรับเก็บคันเบ็ด")]
    [SerializeField] private Transform backSocket;
    [SerializeField] private FishermanAnimator fishermanAnimator;
    [SerializeField] private bool startStowed;

    private FishingRodCaster caster;
    private RodCastAnimator castAnimator;

    public bool IsStowed { get; private set; }

    private void Awake()
    {
        if (fishermanAnimator == null) fishermanAnimator = GetComponent<FishermanAnimator>();
        if (rod != null)
        {
            caster = rod.GetComponent<FishingRodCaster>();
            castAnimator = rod.GetComponent<RodCastAnimator>();
        }
    }

    private void Start()
    {
        SetStowed(startStowed);
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null || PlayerInputGate.IsBlocked) return;
        if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) SetStowed(!IsStowed);
    }

    public void SetStowed(bool stowed)
    {
        if (rod == null || handSocket == null || backSocket == null) return;
        IsStowed = stowed;

        // ปิดสคริปต์เหวี่ยงเบ็ด (OnDisable จะเก็บทุ่นทันที) และหยุดแอนิเมชันง้างคัน
        if (caster != null) caster.enabled = !stowed;
        if (castAnimator != null)
        {
            castAnimator.enabled = !stowed;
            if (stowed && castAnimator.RodPivot != null) castAnimator.RodPivot.localRotation = Quaternion.identity;
        }

        rod.SetParent(stowed ? backSocket : handSocket, false);
        rod.localPosition = Vector3.zero;
        rod.localRotation = Quaternion.identity;

        if (fishermanAnimator != null) fishermanAnimator.SetHoldingRod(!stowed);
    }
}
