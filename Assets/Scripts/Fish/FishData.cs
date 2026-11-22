using UnityEngine;

public enum FishRarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary
}

/// <summary>ข้อมูลปลาแต่ละชนิด สร้างได้จากเมนู Assets > Create > Cozy Hook > Fish Data</summary>
[CreateAssetMenu(menuName = "Cozy Hook/Fish Data", fileName = "NewFish")]
public class FishData : ScriptableObject
{
    [SerializeField] private string fishName = "ปลาใหม่";
    [SerializeField, TextArea(2, 4)] private string description;
    [SerializeField] private FishRarity rarity = FishRarity.Common;
    [Tooltip("น้ำหนักต่ำสุด (กก.)")]
    [SerializeField, Min(0.01f)] private float minWeight = 0.5f;
    [Tooltip("น้ำหนักสูงสุด (กก.)")]
    [SerializeField, Min(0.01f)] private float maxWeight = 2f;
    [Tooltip("โมเดล 3D ของปลา (prefab หรือไฟล์ FBX)")]
    [SerializeField] private GameObject model;
    [Tooltip("ราคาพื้นฐานต่อกิโลกรัม (เหรียญ) 0 = ใช้ค่าเริ่มต้นตามความหายาก")]
    [SerializeField, Min(0f)] private float pricePerKg;

    /// <summary>รหัสปลา (ชื่อไฟล์ asset) ใช้บันทึกเกมและคำนวณราคา</summary>
    public string Id => name;
    public float PricePerKg => pricePerKg > 0f ? pricePerKg : DefaultPricePerKg(rarity);

    public static float DefaultPricePerKg(FishRarity r) => r switch
    {
        FishRarity.Common => 12f,
        FishRarity.Uncommon => 22f,
        FishRarity.Rare => 18f,
        FishRarity.Epic => 9f,
        FishRarity.Legendary => 10f,
        _ => 10f
    };

    /// <summary>น้ำหนักที่ตำแหน่ง t (0-1) ใช้สุ่มแบบกำหนด seed</summary>
    public float WeightAt(float t) => Mathf.Round(Mathf.Lerp(minWeight, maxWeight, Mathf.Pow(Mathf.Clamp01(t), 1.8f)) * 100f) / 100f;

    public string FishName => fishName;
    public string Description => description;
    public FishRarity Rarity => rarity;
    public float MinWeight => minWeight;
    public float MaxWeight => maxWeight;
    public GameObject Model => model;

    /// <summary>สุ่มน้ำหนัก ตัวเล็กเจอบ่อยกว่าตัวใหญ่</summary>
    public float RollWeight() => WeightAt(Random.value);

    private void OnValidate()
    {
        if (maxWeight < minWeight) maxWeight = minWeight;
    }

#if UNITY_EDITOR
    /// <summary>ใช้โดยตัวสร้างข้อมูลปลาใน Editor</summary>
    public void EditorSetup(string name, string desc, FishRarity r, float min, float max, GameObject mdl)
    {
        fishName = name;
        description = desc;
        rarity = r;
        minWeight = min;
        maxWeight = max;
        model = mdl;
    }

    public void EditorSetPrice(float perKg) => pricePerKg = perKg;
    public float EditorRawPrice => pricePerKg;
#endif
}

/// <summary>ชื่อภาษาไทย สี และดาวของแต่ละระดับความหายาก</summary>
public static class FishRarityInfo
{
    public static string DisplayName(FishRarity rarity) => rarity switch
    {
        FishRarity.Common => "ธรรมดา",
        FishRarity.Uncommon => "ไม่ธรรมดา",
        FishRarity.Rare => "หายาก",
        FishRarity.Epic => "หายากมาก",
        FishRarity.Legendary => "ตำนาน",
        _ => rarity.ToString()
    };

    public static Color Color(FishRarity rarity) => rarity switch
    {
        FishRarity.Common => new Color(0.82f, 0.84f, 0.82f),
        FishRarity.Uncommon => new Color(0.42f, 0.86f, 0.42f),
        FishRarity.Rare => new Color(0.38f, 0.62f, 1f),
        FishRarity.Epic => new Color(0.74f, 0.47f, 1f),
        FishRarity.Legendary => new Color(1f, 0.76f, 0.2f),
        _ => UnityEngine.Color.white
    };

    public static string Stars(FishRarity rarity) => new string('★', (int)rarity + 1);

    public static string Label(FishRarity rarity) => $"{Stars(rarity)} {DisplayName(rarity)}";
}
