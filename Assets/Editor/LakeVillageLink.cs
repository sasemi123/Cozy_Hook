using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// เพิ่มป้าย "ไปหมู่บ้าน" + จุดเกิดเมื่อกลับจากหมู่บ้าน ให้ฉากทะเลสาบที่เปิดอยู่
/// พร้อมอัปเดตผู้เล่น/HUD และแสงตามเวลา (ไม่สร้างฉากใหม่ ของที่จัดไว้ไม่หาย)
/// เมนู: Tools > Cozy Hook > Add Village Link To Lake
/// </summary>
public static class LakeVillageLink
{
    [MenuItem("Tools/Cozy Hook/Add Village Link To Lake")]
    public static void AddToOpenScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.isDirty)
        {
            Debug.LogError("[LakeLink] ฉากปัจจุบันยังไม่ได้บันทึก กรุณาบันทึกก่อนแล้วรันใหม่");
            return;
        }

        FishDataBuilder.CreateDefaultFish();
        FishermanBuilder.BuildPrefab();
        FishermanBuilder.PlaceInScene(); // เพิ่ม PlayerInteractor / PlayerSpawner / HUD

        var sun = GameObject.Find("Sun");
        if (sun != null && sun.GetComponent<SunCycle>() == null) sun.AddComponent<SunCycle>();

        AddLink();
        EditorSceneManager.SaveScene(scene);
        ForestLakeSceneBuilder.AddToBuildSettings(scene.path);
        ForestLakeSceneBuilder.AddToBuildSettings("Assets/Scenes/Village.unity");
        Debug.Log("[LakeLink] เพิ่มทางไปหมู่บ้านในฉาก " + scene.path);
    }

    /// <summary>ป้ายไปหมู่บ้านริมฝั่งใกล้โคนท่าเรือ + SpawnPoint "FromVillage"</summary>
    public static void AddLink()
    {
        foreach (string old in new[] { "To Village", "Spawn FromVillage", "Label ไปหมู่บ้าน" })
        {
            GameObject o = GameObject.Find(old);
            if (o != null) Object.DestroyImmediate(o);
        }

        Terrain terrain = Terrain.activeTerrain;
        Vector3 Ground(float x, float z) => new Vector3(x, terrain != null ? terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y : 5f, z);

        Material wood = ForestLakeSceneBuilder.LoadOrCreateMaterial("VillageWood", Shader.Find("Universal Render Pipeline/Lit"));
        wood.SetColor("_BaseColor", new Color(0.45f, 0.3f, 0.18f));
        Material board = ForestLakeSceneBuilder.LoadOrCreateMaterial("BoardWood", Shader.Find("Universal Render Pipeline/Lit"));
        board.SetColor("_BaseColor", new Color(0.6f, 0.43f, 0.27f));

        Vector3 p = Ground(65f, 45.5f);
        var travel = new GameObject("To Village").transform;
        travel.SetPositionAndRotation(p, Quaternion.Euler(0f, 160f, 0f));
        Part("Post", travel, new Vector3(0f, 1.1f, 0f), new Vector3(0.14f, 2.2f, 0.14f), wood);
        Part("Arrow", travel, new Vector3(-0.35f, 1.8f, -0.1f), new Vector3(1.2f, 0.32f, 0.06f), board);
        var point = travel.gameObject.AddComponent<SceneTravelPoint>();
        point.EditorSetup("เดินทางไปหมู่บ้าน (ตลาดปลา)", 3.2f);
        point.EditorSetTarget("Village", "FromLake");

        var label = new GameObject("Label ไปหมู่บ้าน");
        label.transform.position = p + Vector3.up * 2.7f;
        label.AddComponent<WorldLabel>().EditorSetup("ไปหมู่บ้าน", 0.5f, new Color(1f, 0.92f, 0.7f));

        var spawn = new GameObject("Spawn FromVillage").AddComponent<SpawnPoint>();
        spawn.EditorSetId("FromVillage");
        spawn.transform.SetPositionAndRotation(Ground(69.8f, 45.2f) + Vector3.up * 0.05f, Quaternion.identity); // โคนท่าเรือ หันหน้าไปทางทะเลสาบ
    }

    private static void Part(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }
}
