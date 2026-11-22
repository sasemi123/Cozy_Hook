using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>ปลาหนึ่งตัวที่จับได้ (ชนิด + น้ำหนักจริง)</summary>
[Serializable]
public class CaughtFish
{
    [SerializeField] private FishData data;
    [SerializeField] private float weight;

    public CaughtFish(FishData data, float weight)
    {
        this.data = data;
        this.weight = weight;
    }

    public FishData Data => data;
    public float Weight => weight;
}

/// <summary>
/// กระเป๋าเก็บปลาของผู้เล่น มีจำนวนช่องจำกัด วางไว้บนตัวผู้เล่น
/// ข้อมูลจริงเก็บใน GameState จึงอยู่ข้ามฉากและถูกบันทึกลงไฟล์
/// </summary>
public class FishInventory : MonoBehaviour
{
    private static readonly List<CaughtFish> Empty = new List<CaughtFish>();

    [SerializeField, Min(1)] private int capacity = 20;

    /// <summary>เรียกเมื่อของในกระเป๋าเปลี่ยน (เพิ่ม/ขาย/ปล่อยปลา)</summary>
    public event Action Changed;

    public IReadOnlyList<CaughtFish> Fish => Application.isPlaying ? GameState.Instance.Fish : Empty;
    public int Capacity => capacity;
    public int Count => Fish.Count;
    public bool IsFull => Count >= capacity;

    public float TotalWeight
    {
        get
        {
            float total = 0f;
            foreach (CaughtFish f in Fish) total += f.Weight;
            return total;
        }
    }

    private void OnEnable()
    {
        if (Application.isPlaying) GameState.Instance.FishChanged += RaiseChanged;
    }

    private void OnDisable()
    {
        if (GameState.Exists) GameState.Instance.FishChanged -= RaiseChanged;
    }

    private void RaiseChanged() => Changed?.Invoke();

    /// <summary>เพิ่มปลาลงกระเป๋า คืนค่า false ถ้ากระเป๋าเต็ม</summary>
    public bool TryAdd(CaughtFish caught)
    {
        if (caught == null || caught.Data == null || IsFull) return false;
        GameState.Instance.AddFish(caught);
        return true;
    }

    /// <summary>เอาปลาตัวที่ index ออกจากกระเป๋า (ปล่อย/ขาย)</summary>
    public void RemoveAt(int index) => GameState.Instance.RemoveFishAt(index);
}
