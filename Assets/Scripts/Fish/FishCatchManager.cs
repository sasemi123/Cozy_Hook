using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// เมื่อจับปลาได้ จะสุ่มระดับความหายากตามน้ำหนักโอกาส
/// แล้วสุ่มชนิดปลาในระดับนั้น สุ่มน้ำหนัก และเก็บเข้ากระเป๋า
/// </summary>
public class FishCatchManager : MonoBehaviour
{
    [Serializable]
    public struct RarityChance
    {
        public FishRarity rarity;
        [Min(0f)] public float weight;

        public RarityChance(FishRarity rarity, float weight)
        {
            this.rarity = rarity;
            this.weight = weight;
        }
    }

    [Tooltip("ปลาทั้งหมดที่จับได้ในบริเวณนี้")]
    [SerializeField] private List<FishData> fishPool = new List<FishData>();

    [Tooltip("น้ำหนักโอกาสของแต่ละระดับ (ไม่จำเป็นต้องรวมเป็น 100)")]
    [SerializeField] private RarityChance[] rarityChances =
    {
        new RarityChance(FishRarity.Common, 60f),
        new RarityChance(FishRarity.Uncommon, 25f),
        new RarityChance(FishRarity.Rare, 10f),
        new RarityChance(FishRarity.Epic, 4f),
        new RarityChance(FishRarity.Legendary, 1f),
    };

    [SerializeField] private FishingLineTension lineTension;
    [SerializeField] private FishInventory inventory;

    /// <summary> จับปลาได้ : (ปลา, เก็บเข้ากระเป๋าได้หรือไม่) </summary>
    public event Action<CaughtFish, bool> FishCaught;

    public FishInventory Inventory => inventory;

    private void Awake()
    {
        if (lineTension == null) lineTension = FindAnyObjectByType<FishingLineTension>();
        if (inventory == null) inventory = FindAnyObjectByType<FishInventory>();
    }

    private void OnEnable()
    {
        if (lineTension != null) lineTension.onFishCaught.AddListener(CatchRandomFish);
    }

    private void OnDisable()
    {
        if (lineTension != null) lineTension.onFishCaught.RemoveListener(CatchRandomFish);
    }

    /// <summary> สุ่มปลาหนึ่งตัวแล้วเก็บเข้ากระเป๋า </summary>
    public void CatchRandomFish()
    {
        FishData data = RollFish();
        if (data == null)
        {
            Debug.LogWarning("[FishCatchManager] ยังไม่มี FishData ใน fishPool");
            return;
        }

        Bait.OnHook = null;   // เหยื่อถูกกินไปแล้ว
        var caught = new CaughtFish(data, data.RollWeight());
        bool added = inventory != null && inventory.TryAdd(caught);
        Debug.Log($"[FishCatchManager] จับได้ {data.FishName} ({FishRarityInfo.DisplayName(data.Rarity)}) {caught.Weight:F2} กก. เก็บเข้ากระเป๋า={added}");
        FishCaught?.Invoke(caught, added);
    }

    /// <summary> สุ่มระดับความหายาก (เฉพาะระดับที่มีปลาอยู่จริง) แล้วสุ่มชนิดในระดับนั้น </summary>
    public FishData RollFish()
    {
        float total = 0f;
        foreach (RarityChance chance in rarityChances)
            if (HasFishOf(chance.rarity)) total += WeightOf(chance);

        if (total <= 0f) return RandomFrom(fishPool);

        float roll = UnityEngine.Random.value * total;
        foreach (RarityChance chance in rarityChances)
        {
            if (!HasFishOf(chance.rarity)) continue;
            roll -= WeightOf(chance);
            if (roll <= 0f) return RandomFrom(fishPool.FindAll(f => f != null && f.Rarity == chance.rarity));
        }
        return RandomFrom(fishPool);
    }

    /// <summary> โอกาสจริงของแต่ละระดับ (%) หลังตัดระดับที่ไม่มีปลาออก </summary>
    public float ChancePercent(FishRarity rarity)
    {
        float total = 0f, mine = 0f;
        foreach (RarityChance chance in rarityChances)
        {
            if (!HasFishOf(chance.rarity)) continue;
            total += WeightOf(chance);
            if (chance.rarity == rarity) mine += WeightOf(chance);
        }
        return total > 0f ? mine / total * 100f : 0f;
    }

    /// <summary>น้ำหนักโอกาสหลังคิดผลของเหยื่อบนเบ็ด (เหยื่อดีเพิ่มโอกาสระดับ Rare ขึ้นไป)</summary>
    private static float WeightOf(RarityChance chance)
    {
        Bait bait = Bait.OnHook;
        return bait != null && chance.rarity >= FishRarity.Rare ? chance.weight * bait.RareLuck : chance.weight;
    }

    private bool HasFishOf(FishRarity rarity) => fishPool.Exists(f => f != null && f.Rarity == rarity);

    private static FishData RandomFrom(List<FishData> list)
    {
        list = list.FindAll(f => f != null);
        return list.Count == 0 ? null : list[UnityEngine.Random.Range(0, list.Count)];
    }
}
