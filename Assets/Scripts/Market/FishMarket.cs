using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// คำนวณราคาปลาประจำวัน: ราคาแต่ละชนิดขึ้นลงตามวันแบบต่อเนื่อง (Perlin noise)
/// และบางวันจะมีข่าว "ราคาพุ่ง" หรือ "ราคาร่วง" แบบสุ่ม
/// ผลลัพธ์กำหนดตาม seed (วัน + ชนิดปลา) จึงเหมือนเดิมทุกครั้งในวันเดียวกัน ทุกฉาก
/// </summary>
public static class FishMarket
{
    /// <summary>ราคาซื้อจากตลาดแพงกว่าราคาที่ตลาดรับซื้อเท่าไร</summary>
    public const float BuyMarkup = 1.4f;
    public const int DailyStockCount = 4;

    public enum PriceEvent { None, Boom, Crash }

    /// <summary>ตัวคูณราคาของปลาชนิดนี้ในวันที่กำหนด (ประมาณ 0.35 - 2.4)</summary>
    public static float Multiplier(FishData fish, int day)
    {
        uint h = Hash(fish.Id);
        float seedX = (h % 997) * 0.731f;
        float trend = Mathf.PerlinNoise(seedX, day * 0.45f + 3.1f);
        float m = Mathf.Lerp(0.6f, 1.5f, trend);

        switch (EventOf(fish, day))
        {
            case PriceEvent.Boom: m *= 1.6f; break;
            case PriceEvent.Crash: m *= 0.6f; break;
        }
        return Mathf.Round(m * 100f) / 100f;
    }

    public static PriceEvent EventOf(FishData fish, int day)
    {
        var rng = new System.Random(unchecked((int)(Hash(fish.Id) ^ (uint)(day * 7919))));
        double r = rng.NextDouble();
        if (r < 0.08) return PriceEvent.Boom;
        if (r < 0.14) return PriceEvent.Crash;
        return PriceEvent.None;
    }

    public static float PricePerKg(FishData fish, int day) => fish.PricePerKg * Multiplier(fish, day);

    /// <summary>ราคาที่ตลาดรับซื้อปลาตัวนี้</summary>
    public static int SellPrice(CaughtFish fish, int day) =>
        Mathf.Max(1, Mathf.RoundToInt(fish.Data.PricePerKg * fish.Weight * Multiplier(fish.Data, day)));

    /// <summary>ราคาที่ผู้เล่นต้องจ่ายเมื่อซื้อปลาจากตลาด</summary>
    public static int BuyPrice(CaughtFish fish, int day) => Mathf.CeilToInt(SellPrice(fish, day) * BuyMarkup);

    /// <summary>เปลี่ยนแปลงจากเมื่อวาน (%)</summary>
    public static float ChangePercent(FishData fish, int day)
    {
        if (day <= 1) return 0f;
        float yesterday = Multiplier(fish, day - 1);
        return yesterday > 0f ? (Multiplier(fish, day) / yesterday - 1f) * 100f : 0f;
    }

    /// <summary>ข่าวเด่นประจำวันสำหรับป้ายประกาศ</summary>
    public static string Headline(IReadOnlyList<FishData> fish, int day)
    {
        FishData boom = null, crash = null, best = null;
        float bestChange = 0f;
        foreach (FishData f in fish)
        {
            if (f == null) continue;
            PriceEvent e = EventOf(f, day);
            if (e == PriceEvent.Boom && boom == null) boom = f;
            if (e == PriceEvent.Crash && crash == null) crash = f;
            float c = ChangePercent(f, day);
            if (Mathf.Abs(c) > Mathf.Abs(bestChange)) { bestChange = c; best = f; }
        }

        if (boom != null) return $"ข่าวด่วน! ร้านอาหารในเมืองต้องการ{boom.FishName}จำนวนมาก ราคาพุ่งสูง!";
        if (crash != null) return $"ชาวประมงจับ{crash.FishName}ได้ล้นตลาด ราคาตกฮวบ";
        if (best != null && Mathf.Abs(bestChange) >= 10f)
            return bestChange > 0 ? $"ราคา{best.FishName}ขยับขึ้น {bestChange:+0}% จากเมื่อวาน" : $"ราคา{best.FishName}ลดลง {bestChange:0}% จากเมื่อวาน";
        return "ตลาดวันนี้ค่อนข้างเงียบ ราคาปลาทรงตัว";
    }

    /// <summary>ปลาที่ตลาดวางขายวันนี้ (สุ่มตามวัน ชนิดหายากออกน้อย)</summary>
    public static List<CaughtFish> DailyStock(FishDatabase db, int day)
    {
        var stock = new List<CaughtFish>();
        if (db == null || db.Fish.Count == 0) return stock;

        var rng = new System.Random(day * 104729 + 17);
        for (int i = 0; i < DailyStockCount; i++)
        {
            FishData pick = PickWeighted(db.Fish, rng);
            if (pick != null) stock.Add(new CaughtFish(pick, pick.WeightAt((float)rng.NextDouble())));
        }
        return stock;
    }

    private static FishData PickWeighted(IReadOnlyList<FishData> fish, System.Random rng)
    {
        float total = 0f;
        foreach (FishData f in fish) if (f != null) total += StockWeight(f.Rarity);
        double roll = rng.NextDouble() * total;
        foreach (FishData f in fish)
        {
            if (f == null) continue;
            roll -= StockWeight(f.Rarity);
            if (roll <= 0) return f;
        }
        return null;
    }

    private static float StockWeight(FishRarity r) => r switch
    {
        FishRarity.Common => 50f,
        FishRarity.Uncommon => 30f,
        FishRarity.Rare => 15f,
        FishRarity.Epic => 5f,
        _ => 0f // ปลาระดับตำนานไม่มีขายในตลาด
    };

    /// <summary>FNV-1a hash ที่ได้ค่าเดิมทุกเครื่อง ทุกครั้งที่รัน</summary>
    private static uint Hash(string s)
    {
        uint h = 2166136261;
        foreach (char c in s)
        {
            h ^= c;
            h *= 16777619;
        }
        return h;
    }
}
