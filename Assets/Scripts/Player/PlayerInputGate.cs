using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// ตัวกลางบอกว่าตอนนี้ผู้เล่นใช้เมาส์ควบคุมเกมได้ไหม
/// เช่น ตอนเปิดหน้าต่างกระเป๋า หรือเมาส์ชี้อยู่บนปุ่ม UI จะไม่เหวี่ยงเบ็ด/หมุนกล้อง
/// </summary>
public static class PlayerInputGate
{
    private static int blockers;

    /// <summary>มีหน้าต่าง UI เปิดอยู่ที่บล็อกการควบคุม</summary>
    public static bool IsBlocked => blockers > 0;

    public static bool PointerOverUI => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

    /// <summary>ใช้คลิกเมาส์ในเกมได้ (ไม่มีหน้าต่างบล็อก และไม่ได้ชี้บน UI)</summary>
    public static bool CanUseMouse => !IsBlocked && !PointerOverUI;

    public static void Block() => blockers++;

    public static void Unblock() => blockers = Mathf.Max(0, blockers - 1);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay() => blockers = 0;
}
