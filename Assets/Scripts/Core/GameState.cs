using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// สถานะเกมที่อยู่ข้ามฉาก: เหรียญ, วันในเกม, เวลา, ปลาในกระเป๋า, การอ่านป้ายประกาศ, สินค้าที่ซื้อไปแล้ว
/// บันทึกลงไฟล์อัตโนมัติ (Application.persistentDataPath/cozyhook_save.json)
/// สร้างตัวเองอัตโนมัติเมื่อถูกเรียกใช้ครั้งแรกตอนเล่น
/// </summary>
public class GameState : MonoBehaviour
{
    /// <summary>ความยาวหนึ่งวันในเกม (วินาทีจริง)</summary>
    public static float DayLengthSeconds = 240f;
    public const int StartingCoins = 50;
    private const float DayStartHour = 6f;
    private const float DayEndHour = 22f;

    [Serializable]
    private class SavedFish
    {
        public string id;
        public float weight;
    }

    [Serializable]
    private class SaveData
    {
        public int coins = StartingCoins;
        public int day = 1;
        public float timeOfDay;
        public int boardReadDay = -1;
        public int stockDay = -1;
        public List<int> soldStock = new List<int>();
        public List<SavedFish> fish = new List<SavedFish>();
        public List<BaitStack> bait = new List<BaitStack>();
        public string equippedBait = "";
    }

    [Serializable]
    private class BaitStack
    {
        public string id;
        public int count;
    }

    private static GameState instance;
    private SaveData data = new SaveData();
    private readonly List<CaughtFish> fish = new List<CaughtFish>();

    public static GameState Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("GameState");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<GameState>();
                instance.Load();
            }
            return instance;
        }
    }

    public static bool Exists => instance != null;

    public event Action CoinsChanged;
    public event Action FishChanged;
    /// <summary>เริ่มวันใหม่ (ส่งเลขวันใหม่)</summary>
    public event Action<int> DayChanged;

    public int Coins => data.coins;
    public int Day => data.day;
    /// <summary>เวลาในวัน 0 (06:00) ถึง 1 (22:00)</summary>
    public float TimeOfDay01 => data.timeOfDay;
    public float Hour => Mathf.Lerp(DayStartHour, DayEndHour, data.timeOfDay);
    public string ClockText
    {
        get
        {
            float h = Hour;
            int hh = Mathf.FloorToInt(h);
            int mm = Mathf.FloorToInt((h - hh) * 60f / 10f) * 10;
            return $"{hh:00}:{mm:00}";
        }
    }
    public float SecondsUntilNextDay => (1f - data.timeOfDay) * DayLengthSeconds;
    public bool HasReadBoardToday => data.boardReadDay == data.day;
    public IReadOnlyList<CaughtFish> Fish => fish;
    /// <summary>ฉากถัดไปจะวางผู้เล่นที่ SpawnPoint ชื่อนี้</summary>
    public string PendingSpawnId { get; set; }

    private static string SavePath => Path.Combine(Application.persistentDataPath, "cozyhook_save.json");

    /// <summary>มีไฟล์เซฟอยู่แล้วหรือไม่ (ใช้เปิด/ปิดปุ่ม "เล่นต่อ" ในหน้า Title)</summary>
    public static bool SaveExists => File.Exists(SavePath);

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    private void Update()
    {
        data.timeOfDay += Time.deltaTime / Mathf.Max(1f, DayLengthSeconds);
        if (data.timeOfDay >= 1f) NextDay();
    }

    private void OnApplicationQuit() => Save();

    // ------------------------------------------------------------------ Day

    public void NextDay()
    {
        data.day++;
        data.timeOfDay = 0f;
        Save();
        DayChanged?.Invoke(data.day);
    }

    public void MarkBoardRead()
    {
        if (data.boardReadDay == data.day) return;
        data.boardReadDay = data.day;
        Save();
    }

    // ------------------------------------------------------------------ Coins

    public void AddCoins(int amount)
    {
        if (amount == 0) return;
        data.coins = Mathf.Max(0, data.coins + amount);
        Save();
        CoinsChanged?.Invoke();
    }

    public bool TrySpend(int amount)
    {
        if (amount > data.coins) return false;
        AddCoins(-amount);
        return true;
    }

    // ------------------------------------------------------------------ Fish

    public void AddFish(CaughtFish caught)
    {
        fish.Add(caught);
        Save();
        FishChanged?.Invoke();
    }

    public void RemoveFishAt(int index)
    {
        if (index < 0 || index >= fish.Count) return;
        fish.RemoveAt(index);
        Save();
        FishChanged?.Invoke();
    }

    // ------------------------------------------------------------------ Market stock

    public bool IsStockSold(int slot) => data.stockDay == data.day && data.soldStock.Contains(slot);

    public void MarkStockSold(int slot)
    {
        if (data.stockDay != data.day)
        {
            data.stockDay = data.day;
            data.soldStock.Clear();
        }
        if (!data.soldStock.Contains(slot)) data.soldStock.Add(slot);
        Save();
    }

    // ------------------------------------------------------------------ Bait

    /// <summary>จำนวนเหยื่อ หรือเหยื่อที่ติดตั้งเปลี่ยน</summary>
    public event Action BaitChanged;

    public int BaitCount(string id)
    {
        BaitStack s = data.bait.Find(b => b.id == id);
        return s != null ? s.count : 0;
    }

    public void AddBait(string id, int amount)
    {
        if (Bait.Get(id) == null || amount == 0) return;
        BaitStack s = data.bait.Find(b => b.id == id);
        if (s == null) data.bait.Add(s = new BaitStack { id = id });
        s.count = Mathf.Max(0, s.count + amount);
        if (amount > 0 && EquippedBait == null) data.equippedBait = id;   // ซื้อครั้งแรกติดตั้งให้เลย
        Save();
        BaitChanged?.Invoke();
    }

    /// <summary>เหยื่อที่ติดตั้งอยู่ (null = ไม่ใช้เหยื่อ หรือเหยื่อหมดแล้ว)</summary>
    public Bait EquippedBait => BaitCount(data.equippedBait) > 0 ? Bait.Get(data.equippedBait) : null;

    public void EquipBait(string id)
    {
        data.equippedBait = id ?? "";
        Save();
        BaitChanged?.Invoke();
    }

    /// <summary>สลับไปเหยื่อชนิดถัดไปที่มีอยู่ (วนกลับมา "ไม่ใช้เหยื่อ")</summary>
    public Bait CycleBait()
    {
        var options = new List<string> { "" };
        foreach (Bait b in Bait.All)
            if (BaitCount(b.Id) > 0) options.Add(b.Id);
        string current = EquippedBait != null ? data.equippedBait : "";
        int next = (options.IndexOf(current) + 1) % options.Count;
        EquipBait(options[next]);
        return EquippedBait;
    }

    /// <summary>ใช้เหยื่อที่ติดตั้งไป 1 ชิ้น (คืน null ถ้าไม่มี)</summary>
    public Bait ConsumeEquippedBait()
    {
        Bait b = EquippedBait;
        if (b == null) return null;
        AddBait(b.Id, -1);
        return b;
    }

    // ------------------------------------------------------------------ Save / Load

    public void Save()
    {
        data.fish.Clear();
        foreach (CaughtFish f in fish)
            if (f.Data != null) data.fish.Add(new SavedFish { id = f.Data.Id, weight = f.Weight });

        try
        {
            File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
        }
        catch (Exception e)
        {
            Debug.LogWarning("[GameState] บันทึกเกมไม่สำเร็จ: " + e.Message);
        }
    }

    private void Load()
    {
        data = new SaveData();
        fish.Clear();
        try
        {
            if (File.Exists(SavePath)) data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath)) ?? new SaveData();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[GameState] โหลดเซฟไม่สำเร็จ เริ่มใหม่: " + e.Message);
            data = new SaveData();
        }

        FishDatabase db = FishDatabase.Load();
        foreach (SavedFish saved in data.fish)
        {
            FishData fd = db != null ? db.GetById(saved.id) : null;
            if (fd != null) fish.Add(new CaughtFish(fd, saved.weight));
        }
    }

    /// <summary>ลบเซฟแล้วเริ่มเกมใหม่ (ใช้ทดสอบ)</summary>
    public void ResetSave()
    {
        data = new SaveData();
        fish.Clear();
        Save();
        CoinsChanged?.Invoke();
        FishChanged?.Invoke();
        BaitChanged?.Invoke();
        DayChanged?.Invoke(data.day);
    }
}
