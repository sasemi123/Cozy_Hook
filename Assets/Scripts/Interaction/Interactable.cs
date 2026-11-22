using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// วัตถุที่ผู้เล่นเดินเข้าใกล้แล้วกด E เพื่อโต้ตอบได้ (ป้ายประกาศ, ร้านค้า, ทางไปฉากอื่น)
/// </summary>
public abstract class Interactable : MonoBehaviour
{
    private static readonly List<Interactable> all = new List<Interactable>();

    [Tooltip("ข้อความที่แสดงตอนอยู่ใกล้ เช่น 'อ่านป้ายประกาศ'")]
    [SerializeField] private string prompt = "โต้ตอบ";
    [Tooltip("ระยะที่กดโต้ตอบได้ (เมตร)")]
    [SerializeField, Min(0.5f)] private float range = 2.5f;

    public static IReadOnlyList<Interactable> All => all;
    public virtual string Prompt => prompt;
    public float Range => range;

    protected virtual void OnEnable() => all.Add(this);
    protected virtual void OnDisable() => all.Remove(this);

    public abstract void Interact(PlayerInteractor player);

#if UNITY_EDITOR
    public void EditorSetup(string promptText, float interactRange)
    {
        prompt = promptText;
        range = interactRange;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, range);
    }
#endif
}
