using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// รายชื่อปลาทุกชนิดในเกม เก็บไว้ที่ Resources/FishDatabase เพื่อโหลดตอนเล่น
/// (ใช้สำหรับโหลดเซฟ, ป้ายประกาศราคา และสินค้าในตลาด)
/// </summary>
[CreateAssetMenu(menuName = "Cozy Hook/Fish Database", fileName = "FishDatabase")]
public class FishDatabase : ScriptableObject
{
    [SerializeField] private List<FishData> fish = new List<FishData>();

    private static FishDatabase cached;

    public IReadOnlyList<FishData> Fish => fish;

    public static FishDatabase Load()
    {
        if (cached == null) cached = Resources.Load<FishDatabase>("FishDatabase");
        return cached;
    }

    public FishData GetById(string id) => fish.Find(f => f != null && f.Id == id);

#if UNITY_EDITOR
    public void EditorSetFish(List<FishData> list) => fish = new List<FishData>(list);
#endif
}
