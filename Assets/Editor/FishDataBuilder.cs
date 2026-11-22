using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// สร้างข้อมูลปลาเริ่มต้น (FishData) จากโมเดลใน Fish Pack และติดตั้งระบบจับปลา + กระเป๋าในฉาก
/// เมนู: Tools > Cozy Hook > Add Fish Catch System
/// ไฟล์ FishData ที่มีอยู่แล้วจะไม่ถูกเขียนทับ (แก้ค่าใน Inspector ได้อิสระ)
/// </summary>
public static class FishDataBuilder
{
    private const string Folder = "Assets/Data/Fish";
    private const string Models = "Assets/model/Fish Pack Animated by Quaternius/FBX/";

    private struct FishDef
    {
        public string file, name, description, model;
        public FishRarity rarity;
        public float min, max, price;
    }

    private static readonly FishDef[] Defaults =
    {
        new FishDef { file = "Fish_Tilapia", price = 12f, name = "ปลานิล", rarity = FishRarity.Common, min = 0.3f, max = 1.8f, model = "Fish1.fbx",
            description = "ปลาน้ำจืดที่พบได้ทั่วไป เนื้อขาวนุ่ม เหมาะกับการทอดหรือนึ่ง" },
        new FishDef { file = "Fish_SilverBarb", price = 15f, name = "ปลาตะเพียน", rarity = FishRarity.Common, min = 0.2f, max = 1.2f, model = "Fish2.fbx",
            description = "ปลาเกล็ดสีเงินที่ชอบว่ายเป็นฝูงใกล้ผิวน้ำ" },
        new FishDef { file = "Fish_Snakehead", price = 24f, name = "ปลาช่อน", rarity = FishRarity.Uncommon, min = 0.6f, max = 3.5f, model = "Fish3.fbx",
            description = "นักล่าแห่งบึงน้ำ ดุดันและดึงสายแรงกว่าที่คิด" },
        new FishDef { file = "Fish_Stingray", price = 16f, name = "ปลากระเบนน้ำจืด", rarity = FishRarity.Rare, min = 4f, max = 35f, model = "Manta ray.fbx",
            description = "ชอบซ่อนตัวใต้โคลนก้นทะเลสาบ จึงหาตัวได้ยาก" },
        new FishDef { file = "Fish_GiantCatfish", price = 8f, name = "ปลาบึก", rarity = FishRarity.Epic, min = 40f, max = 200f, model = "Shark.fbx",
            description = "ยักษ์ใหญ่แห่งสายน้ำ ว่ากันว่ามีอายุยืนหลายสิบปี" },
        new FishDef { file = "Fish_IrrawaddyDolphin", price = 20f, name = "โลมาอิรวดี", rarity = FishRarity.Legendary, min = 60f, max = 130f, model = "Dolphin.fbx",
            description = "โลมาน้ำจืดหน้าตาใจดี เชื่อกันว่าใครได้พบจะโชคดีไปทั้งปี" },
        new FishDef { file = "Fish_LakeGuardian", price = 6f, name = "เจ้าแห่งทะเลสาบ", rarity = FishRarity.Legendary, min = 300f, max = 800f, model = "Whale.fbx",
            description = "สิ่งมีชีวิตในตำนานที่ชาวบ้านเล่าขานกันมานาน ไม่มีใครเชื่อว่ามีอยู่จริง..." },
    };

    [MenuItem("Tools/Cozy Hook/Add Fish Catch System")]
    public static void AddToOpenScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.isDirty)
        {
            Debug.LogError("[FishData] ฉากปัจจุบันยังไม่ได้บันทึก กรุณาบันทึกก่อนแล้วรันใหม่");
            return;
        }

        CreateDefaultFish();
        FishermanBuilder.BuildPrefab();   // เพิ่มกระเป๋า (FishInventory) ให้ตัวละคร
        FishermanBuilder.PlaceInScene();  // วางผู้เล่น + ระบบจับปลา + UI
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[FishData] ติดตั้งระบบจับปลาแล้ว มีปลา {AllFish().Count} ชนิด");
    }

    /// <summary>สร้าง FishData เริ่มต้นที่ยังไม่มี</summary>
    public static void CreateDefaultFish()
    {
        EnsureFolder(Folder);
        foreach (FishDef def in Defaults)
        {
            string path = $"{Folder}/{def.file}.asset";
            if (AssetDatabase.LoadAssetAtPath<FishData>(path) != null) continue;

            var data = ScriptableObject.CreateInstance<FishData>();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Models + def.model);
            if (model == null) Debug.LogWarning("[FishData] ไม่พบโมเดล " + Models + def.model);
            data.EditorSetup(def.name, def.description, def.rarity, def.min, def.max, model);
            AssetDatabase.CreateAsset(data, path);
        }

        // ราคาพื้นฐาน (ตั้งให้เฉพาะไฟล์ที่ยังไม่เคยกำหนดราคา)
        foreach (FishDef def in Defaults)
        {
            var data = AssetDatabase.LoadAssetAtPath<FishData>($"{Folder}/{def.file}.asset");
            if (data == null || data.EditorRawPrice > 0f) continue;
            data.EditorSetPrice(def.price);
            EditorUtility.SetDirty(data);
        }

        UpdateDatabase();
        AssetDatabase.SaveAssets();
    }

    /// <summary>อัปเดต Resources/FishDatabase ให้มีปลาทุกชนิดในโปรเจกต์ (โหลดตอนเล่นได้)</summary>
    public static FishDatabase UpdateDatabase()
    {
        const string path = "Assets/Resources/FishDatabase.asset";
        EnsureFolder("Assets/Resources");
        var db = AssetDatabase.LoadAssetAtPath<FishDatabase>(path);
        if (db == null)
        {
            db = ScriptableObject.CreateInstance<FishDatabase>();
            AssetDatabase.CreateAsset(db, path);
        }
        db.EditorSetFish(AllFish());
        EditorUtility.SetDirty(db);
        return db;
    }

    /// <summary>FishData ทั้งหมดในโปรเจกต์ (รวมที่ผู้ใช้สร้างเอง)</summary>
    public static List<FishData> AllFish()
    {
        var list = new List<FishData>();
        foreach (string guid in AssetDatabase.FindAssets("t:FishData"))
        {
            var data = AssetDatabase.LoadAssetAtPath<FishData>(AssetDatabase.GUIDToAssetPath(guid));
            if (data != null) list.Add(data);
        }
        list.Sort((a, b) => a.Rarity != b.Rarity ? a.Rarity.CompareTo(b.Rarity) : string.CompareOrdinal(a.name, b.name));
        return list;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
