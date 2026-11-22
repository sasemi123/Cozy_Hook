using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>หา Interactable ที่ใกล้ที่สุดในระยะ แล้วกด E เพื่อโต้ตอบ วางไว้บนตัวผู้เล่น</summary>
public class PlayerInteractor : MonoBehaviour
{
    /// <summary>สิ่งที่โต้ตอบได้ตอนนี้ (null = ไม่มี) ใช้แสดงข้อความ [E] บน HUD</summary>
    public Interactable Current { get; private set; }

    private void Update()
    {
        Current = PlayerInputGate.IsBlocked ? null : FindNearest();

        Keyboard kb = Keyboard.current;
        if (Current != null && kb != null && kb.eKey.wasPressedThisFrame) Current.Interact(this);
    }

    private Interactable FindNearest()
    {
        Interactable best = null;
        float bestDistance = float.MaxValue;
        Vector3 me = transform.position;

        foreach (Interactable item in Interactable.All)
        {
            Vector3 d = item.transform.position - me;
            d.y = 0f;
            float distance = d.magnitude;
            if (distance <= item.Range && distance < bestDistance)
            {
                best = item;
                bestDistance = distance;
            }
        }
        return best;
    }
}
