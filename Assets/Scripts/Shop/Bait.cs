using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// เหยื่อตกปลาที่ซื้อจากร้านเหยื่อในหมู่บ้าน
/// ติดตั้งไว้แล้วจะถูกใช้ไป 1 ชิ้นทุกครั้งที่ทุ่นตกน้ำ: ปลากินเร็วขึ้น และ/หรือ มีโอกาสได้ปลาหายากมากขึ้น
/// </summary>
public class Bait
{
    public string Id;
    public string Name;
    public string Description;
    public int Price;
    /// <summary>คูณเวลารอปลากิน (น้อย = กินเร็ว)</summary>
    public float BiteDelayMultiplier = 1f;
    /// <summary>คูณโอกาสได้ปลาระดับหายาก (Rare ขึ้นไป)</summary>
    public float RareLuck = 1f;
    public Color Color;

    public static readonly IReadOnlyList<Bait> All = new List<Bait>
    {
        new Bait
        {
            Id = "worm", Name = "ไส้เดือน", Price = 3, BiteDelayMultiplier = 0.55f, RareLuck = 1f,
            Description = "เหยื่อพื้นฐาน ปลากินเร็วขึ้นเกือบเท่าตัว", Color = new Color(0.85f, 0.55f, 0.5f),
        },
        new Bait
        {
            Id = "shrimp", Name = "กุ้งฝอย", Price = 9, BiteDelayMultiplier = 0.75f, RareLuck = 1.8f,
            Description = "ปลาตัวใหญ่ชอบ โอกาสเจอปลาหายากเพิ่มขึ้น", Color = new Color(1f, 0.65f, 0.45f),
        },
        new Bait
        {
            Id = "golden", Name = "เหยื่อทองคำ", Price = 30, BiteDelayMultiplier = 0.6f, RareLuck = 3.5f,
            Description = "เหยื่อในตำนาน ปลาหายากแทบจะว่ายเข้ามาหาเอง", Color = new Color(1f, 0.82f, 0.3f),
        },
    };

    public static Bait Get(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (Bait b in All)
            if (b.Id == id) return b;
        return null;
    }

    /// <summary>เหยื่อที่อยู่บนทุ่นที่ลอยน้ำอยู่ตอนนี้ (null = ไม่มีเหยื่อ)</summary>
    public static Bait OnHook { get; set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => OnHook = null;
}
