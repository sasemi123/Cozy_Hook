using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// สร้างฉาก "หมู่บ้าน" อัตโนมัติ: ลานกลางหมู่บ้าน บ่อน้ำ ตลาดปลา ป้ายประกาศ บ้าน ป่ารอบๆ
/// และทางเดินกลับไปทะเลสาบ
/// เมนู: Tools > Cozy Hook > Build Village Scene (รันซ้ำได้ จะสร้างฉากใหม่ทับของเดิม)
/// </summary>
public static class VillageSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Village.unity";
    private const string LakeScenePath = "Assets/Scenes/ForestLake.unity";
    private const string Root = "Assets/Environment";
    private const string Pirate = "Assets/model/kenney_pirate-kit/Models/FBX format/";
    private const string Props = "Assets/model/Fantasy Props MegaKit[Standard]/Exports/FBX/";
    private const string FishPack = "Assets/model/Fish Pack Animated by Quaternius/FBX/";

    private const float TerrainSize = 120f;
    private const float TerrainHeight = 20f;
    private static readonly Vector2 Center = new Vector2(60f, 60f);

    private static Terrain terrain;
    private static System.Random rng;
    private static readonly List<Vector2> blocked = new List<Vector2>();

    [MenuItem("Tools/Cozy Hook/Build Village Scene")]
    public static void Build()
    {
        if (SceneManager.GetActiveScene().isDirty)
        {
            Debug.LogError("[Village] ฉากปัจจุบันยังไม่ได้บันทึก กรุณาบันทึกก่อนแล้วรันใหม่");
            return;
        }

        rng = new System.Random(11);
        blocked.Clear();
        ForestLakeSceneBuilder.EnsureFolder(Root + "/Prefabs");
        ForestLakeSceneBuilder.EnsureFolder(Root + "/Meshes");

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        ForestLakeSceneBuilder.BuildLighting();
        RenderSettings.fogStartDistance = 50f;
        RenderSettings.fogEndDistance = 200f;
        terrain = BuildTerrain();

        Transform env = new GameObject("Environment").transform;
        BuildPlaza(env);
        BuildMarket(env);
        BuildNoticeBoard(env);
        BuildHouses(env);
        BuildLakePath(env);
        BuildNature(env);

        foreach (Transform t in env.GetComponentsInChildren<Transform>())
            if (t.GetComponent<Interactable>() == null && t.GetComponentInParent<WorldLabel>() == null && t.GetComponentInParent<VendorNPC>() == null) t.gameObject.isStatic = true;

        // ผู้เล่น (ถ้าเริ่มเกมในฉากนี้โดยตรงจะยืนที่ทางเข้าหมู่บ้าน)
        FishDataBuilder.CreateDefaultFish();
        FishermanBuilder.BuildPrefab();
        FishermanBuilder.PlaceInScene(Ground(PathX(24f), 24f), 0f);

        EditorSceneManager.SaveScene(scene, ScenePath);
        ForestLakeSceneBuilder.AddToBuildSettings(LakeScenePath);
        ForestLakeSceneBuilder.AddToBuildSettings(ScenePath);
        Debug.Log("[Village] สร้างฉากหมู่บ้านเสร็จ: " + ScenePath);
    }

    // ------------------------------------------------------------------ Terrain

    private static float HeightAt(float x, float z)
    {
        float d = Vector2.Distance(new Vector2(x, z), Center);
        float n = Mathf.PerlinNoise(x * 0.045f + 7f, z * 0.045f + 3f);
        float h = 2f + (n - 0.5f) * 0.8f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(16f, 34f, d));
        float edge = Mathf.Max(Mathf.Abs(x / TerrainSize - 0.5f), Mathf.Abs(z / TerrainSize - 0.5f)) * 2f;
        h += Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 1f, edge)) * 9f;
        // ทางเดินลงใต้ (ไปทะเลสาบ) ให้ราบเรียบ
        if (OnPath(x, z, 3.5f)) h = Mathf.Lerp(h, 2f, 0.7f);
        return h;
    }

    private static float PathX(float z) => Center.x + Mathf.Sin(z * 0.09f) * 2.2f;

    private static bool OnPath(float x, float z, float halfWidth) => z < Center.y - 10f && Mathf.Abs(x - PathX(z)) < halfWidth;

    private static Terrain BuildTerrain()
    {
        string dataPath = Root + "/VillageTerrain.asset";
        AssetDatabase.DeleteAsset(dataPath);
        var data = new TerrainData { heightmapResolution = 257, alphamapResolution = 256 };
        data.size = new Vector3(TerrainSize, TerrainHeight, TerrainSize);

        int res = data.heightmapResolution;
        var heights = new float[res, res];
        for (int zi = 0; zi < res; zi++)
        for (int xi = 0; xi < res; xi++)
            heights[zi, xi] = HeightAt(xi / (float)(res - 1) * TerrainSize, zi / (float)(res - 1) * TerrainSize) / TerrainHeight;
        data.SetHeights(0, 0, heights);

        data.terrainLayers = new[]
        {
            LoadLayer("Grass", new Color(0.36f, 0.55f, 0.24f), new Color(0.3f, 0.47f, 0.2f)),
            LoadLayer("ForestFloor", new Color(0.27f, 0.37f, 0.18f), new Color(0.33f, 0.3f, 0.2f)),
            LoadLayer("Dirt", new Color(0.55f, 0.43f, 0.3f), new Color(0.47f, 0.36f, 0.25f)),
        };

        int ares = data.alphamapResolution;
        var alpha = new float[ares, ares, 3];
        for (int zi = 0; zi < ares; zi++)
        for (int xi = 0; xi < ares; xi++)
        {
            float x = xi / (float)(ares - 1) * TerrainSize, z = zi / (float)(ares - 1) * TerrainSize;
            float d = Vector2.Distance(new Vector2(x, z), Center);
            float n = Mathf.PerlinNoise(x * 0.2f, z * 0.2f);
            float dirt = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(13f, 16f + n * 2f, d));
            if (z < Center.y - 8f) dirt = Mathf.Max(dirt, 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.6f, 2.8f, Mathf.Abs(x - PathX(z)))));
            float forest = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(36f, 46f, d)) * (1f - dirt);
            alpha[zi, xi, 0] = Mathf.Max(0f, 1f - dirt - forest);
            alpha[zi, xi, 1] = forest;
            alpha[zi, xi, 2] = dirt;
        }
        data.SetAlphamaps(0, 0, alpha);
        AssetDatabase.CreateAsset(data, dataPath);

        GameObject go = Terrain.CreateTerrainGameObject(data);
        go.name = "Terrain";
        var t = go.GetComponent<Terrain>();
        if (GraphicsSettings.currentRenderPipeline != null && GraphicsSettings.currentRenderPipeline.defaultTerrainMaterial != null)
            t.materialTemplate = GraphicsSettings.currentRenderPipeline.defaultTerrainMaterial;
        t.Flush();
        return t;
    }

    /// <summary>ใช้ TerrainLayer เดิมถ้ามี (ไม่สร้างทับ เพื่อไม่ให้ฉากทะเลสาบเสีย)</summary>
    private static TerrainLayer LoadLayer(string name, Color a, Color b)
    {
        var existing = AssetDatabase.LoadAssetAtPath<TerrainLayer>(Root + "/Textures/" + name + ".terrainlayer");
        return existing != null ? existing : ForestLakeSceneBuilder.MakeLayer(name, a, b);
    }

    private static Vector3 Ground(float x, float z) => new Vector3(x, terrain.SampleHeight(new Vector3(x, 0f, z)), z);

    // ------------------------------------------------------------------ Plaza

    private static void BuildPlaza(Transform env)
    {
        var plaza = new GameObject("Plaza").transform;
        plaza.SetParent(env);

        Material stone = Mat("Stone", new Color(0.55f, 0.55f, 0.52f), 0.1f);
        Material wood = Mat("VillageWood", new Color(0.45f, 0.3f, 0.18f), 0.1f);
        Material roof = Mat("Roof_Red", new Color(0.62f, 0.24f, 0.18f), 0.15f);
        Material water = Mat("WellWater", new Color(0.12f, 0.28f, 0.32f), 0.9f);

        // บ่อน้ำกลางลาน
        Vector3 c = Ground(Center.x, Center.y);
        var well = new GameObject("Well").transform;
        well.SetParent(plaza);
        well.position = c;
        Prim("WellBase", well, PrimitiveType.Cylinder, new Vector3(0f, 0.45f, 0f), new Vector3(2.4f, 0.45f, 2.4f), stone, true);
        Prim("WellWater", well, PrimitiveType.Cylinder, new Vector3(0f, 0.91f, 0f), new Vector3(1.9f, 0.01f, 1.9f), water, false);
        Prim("Post_L", well, PrimitiveType.Cube, new Vector3(-1.05f, 1.6f, 0f), new Vector3(0.16f, 1.6f, 0.16f), wood, false);
        Prim("Post_R", well, PrimitiveType.Cube, new Vector3(1.05f, 1.6f, 0f), new Vector3(0.16f, 1.6f, 0.16f), wood, false);
        Prim("Beam", well, PrimitiveType.Cylinder, new Vector3(0f, 2.15f, 0f), new Vector3(0.12f, 1.05f, 0.12f), wood, false, Quaternion.Euler(0f, 0f, 90f));
        Roof("WellRoof", well, new Vector3(0f, 2.4f, 0f), new Vector3(3f, 0.9f, 1.9f), roof);
        ForestLakeSceneBuilder.PlaceModel(Props + "Bucket_Wooden_1.fbx", well, c + new Vector3(0.9f, 0.9f, 0.6f), 20f, 0.4f, true);
        Block(Center, 3f);

        // เสาตะเกียงรอบลาน
        Material lampPost = Mat("LampPost", new Color(0.22f, 0.18f, 0.14f), 0.2f);
        foreach (float deg in new[] { 20f, 60f, 125f, 160f, 200f, 240f, 300f, 340f })
        {
            float rad = deg * Mathf.Deg2Rad;
            Vector3 p = Ground(Center.x + Mathf.Cos(rad) * 13.5f, Center.y + Mathf.Sin(rad) * 13.5f);
            var lamp = new GameObject("Lamp").transform;
            lamp.SetParent(plaza);
            lamp.position = p;
            Prim("Post", lamp, PrimitiveType.Cylinder, new Vector3(0f, 1.25f, 0f), new Vector3(0.14f, 1.25f, 0.14f), lampPost, false);
            ForestLakeSceneBuilder.PlaceModel(Props + "Lantern_Wall.fbx", lamp, p + Vector3.up * 2.5f, deg, 0.45f, true);
            var light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(lamp, false);
            light.transform.localPosition = new Vector3(0f, 2.65f, 0f);
            light.type = LightType.Point;
            light.color = new Color(1f, 0.75f, 0.45f);
            light.range = 7f;
            light.intensity = 1.2f;
            light.shadows = LightShadows.None;
        }

        // ศาลาพักผ่อนฝั่งตะวันตก
        Vector3 pav = Ground(45f, 61f);
        ForestLakeSceneBuilder.PlaceModel(Pirate + "structure-roof.fbx", plaza, pav, 0f, 5f, false);
        ForestLakeSceneBuilder.PlaceModel(Props + "Table_Large.fbx", plaza, pav, 90f, 1.9f, false);
        ForestLakeSceneBuilder.PlaceModel(Props + "Bench.fbx", plaza, pav + new Vector3(-1.2f, 0f, 0f), 90f, 1.8f, false);
        ForestLakeSceneBuilder.PlaceModel(Props + "Bench.fbx", plaza, pav + new Vector3(1.2f, 0f, 0f), -90f, 1.8f, false);
        ForestLakeSceneBuilder.PlaceModel(Props + "Mug.fbx", plaza, pav + new Vector3(0.2f, 0.82f, 0.3f), 0f, 0.12f, true);
        Block(new Vector2(pav.x, pav.z), 4f);
    }

    // ------------------------------------------------------------------ Market

    private static void BuildMarket(Transform env)
    {
        var market = new GameObject("Fish Market").transform;
        market.SetParent(env);

        // แผงหลักขายปลา (หันหน้าลงใต้เข้าหาลาน)
        Vector3 main = Ground(Center.x, 72f);
        GameObject stall = ForestLakeSceneBuilder.PlaceModel(Props + "Stall_Empty.fbx", market, main, 180f, 3.6f, false);
        Vector3 counter = Ground(Center.x, 69.4f);
        GameObject table = ForestLakeSceneBuilder.PlaceModel(Props + "Table_Large.fbx", market, counter, 0f, 2.4f, false);
        float tableTop = table != null ? ForestLakeSceneBuilder.WorldBounds(table).max.y : counter.y + 0.8f;

        Material ice = Mat("Ice", new Color(0.78f, 0.9f, 0.95f), 0.85f);
        Prim("IceTray", market, PrimitiveType.Cube, new Vector3(counter.x, tableTop + 0.04f, counter.z), new Vector3(2f, 0.08f, 0.8f), ice, false);
        string[] fishModels = { "Fish1.fbx", "Fish2.fbx", "Fish3.fbx", "Fish1.fbx" };
        for (int i = 0; i < fishModels.Length; i++)
        {
            GameObject f = ForestLakeSceneBuilder.PlaceModel(FishPack + fishModels[i], market,
                new Vector3(counter.x - 0.7f + i * 0.47f, tableTop + 0.09f, counter.z + (i % 2 == 0 ? 0.12f : -0.12f)), 90f + i * 12f, 0.42f, false);
            if (f != null) foreach (var anim in f.GetComponentsInChildren<Animator>()) anim.enabled = false;
        }

        Label(market, "ตลาดปลา", main + Vector3.up * 4.4f, 0.7f, new Color(1f, 0.9f, 0.6f));

        // ลุงขายปลา ยืนประจำหลังโต๊ะ (กด E คุย = เปิดตลาดซื้อขายปลา)
        float behindTable = table != null ? ForestLakeSceneBuilder.WorldBounds(table).max.z + 0.4f : counter.z + 1.1f;
        NpcBuilder.PlaceVendor(NpcBuilder.Fishmonger, market, Ground(Center.x, behindTable), 180f, "ลุงบุญมี", "ร้านปลา", VendorShop.FishMarket,
            new[]
            {
                "มาแล้วเหรอ! วันนี้ได้ปลาอะไรมาขายบ้าง?",
                "ปลาสดๆ จากทะเลสาบจ้า ซื้อก็ได้ ขายก็ดี",
                "อย่าลืมไปดูราคาที่ป้ายประกาศก่อนนะ ลุงไม่บอกหรอก ฮ่าๆ",
                "ปลาหายากลุงให้ราคาดีเสมอ!",
            },
            new[] { "ปลาสดจ้า ปลาสด!", "รับซื้อปลาราคาดีจ้า!", "แวะมาก่อนจ้า วันนี้ปลาตัวโต!", "ใครตกปลาได้ เอามาขายลุงได้นะ!" });

        // แผงผักผลไม้ (ตกแต่ง)
        ForestLakeSceneBuilder.PlaceModel(Props + "Stall_Cart_Empty.fbx", market, Ground(51f, 70f), 200f, 3.2f, false);
        ForestLakeSceneBuilder.PlaceModel(Props + "FarmCrate_Apple.fbx", market, Ground(51.5f, 67.6f), 15f, 0.6f, true);
        ForestLakeSceneBuilder.PlaceModel(Props + "FarmCrate_Carrot.fbx", market, Ground(50.3f, 67.8f), -10f, 0.6f, true);

        // ร้านเหยื่อตกปลา + ป้าขายเหยื่อ
        const float baitYaw = 160f;
        Vector3 baitStall = Ground(69f, 71f);
        Quaternion baitRot = Quaternion.Euler(0f, baitYaw, 0f);
        GameObject baitStallGo = ForestLakeSceneBuilder.PlaceModel(Props + "Stall_Empty.fbx", market, baitStall, baitYaw, 3.2f, false);
        // แผงอยู่ด้านหลัง ป้ายืนหน้าแผง มีโต๊ะวางถังเหยื่อกั้นหน้าอีกที (แบบเดียวกับร้านปลา)
        Vector3 stallCenter = baitStallGo != null ? ForestLakeSceneBuilder.WorldBounds(baitStallGo).center : baitStall;
        stallCenter.y = baitStall.y;
        Vector3 baitNpc = stallCenter + baitRot * new Vector3(0f, 0f, 1.85f);
        Vector3 baitCounter = stallCenter + baitRot * new Vector3(0f, 0f, 2.75f);
        GameObject baitTable = ForestLakeSceneBuilder.PlaceModel(Props + "Table_Large.fbx", market, Ground(baitCounter.x, baitCounter.z), baitYaw + 180f, 1.7f, false);
        float baitTop = baitTable != null ? ForestLakeSceneBuilder.WorldBounds(baitTable).max.y : baitCounter.y + 0.8f;
        foreach (float side in new[] { -0.45f, 0f, 0.45f })
        {
            Vector3 bp = baitCounter + baitRot * new Vector3(side, 0f, 0f);
            ForestLakeSceneBuilder.PlaceModel(Props + "Bucket_Wooden_1.fbx", market, new Vector3(bp.x, baitTop, bp.z), side * 90f, 0.32f, true);
        }
        Vector3 barrelPos = baitCounter + baitRot * new Vector3(-1.6f, 0f, 0.1f);
        Vector3 cratePos = baitCounter + baitRot * new Vector3(1.5f, 0f, -0.2f);
        ForestLakeSceneBuilder.PlaceModel(Pirate + "barrel.fbx", market, Ground(barrelPos.x, barrelPos.z), 0f, 0.9f, true);
        ForestLakeSceneBuilder.PlaceModel(Props + "Crate_Wooden.fbx", market, Ground(cratePos.x, cratePos.z), 25f, 0.6f, true);
        NpcBuilder.PlaceVendor(NpcBuilder.BaitSeller, market, Ground(baitNpc.x, baitNpc.z), baitYaw, "ป้าแก้ว", "ร้านเหยื่อ", VendorShop.BaitShop,
            new[]
            {
                "เหยื่อดีปลาก็กินไวจ้า เลือกดูได้เลย",
                "ไส้เดือนป้าขุดเองเมื่อเช้า สดๆ เลยนะ",
                "อยากได้ปลาหายาก ลองเหยื่อทองคำของป้าสิ!",
            },
            new[] { "เหยื่อตกปลาจ้า!", "ไส้เดือนสดๆ จ้า!", "จะไปตกปลาเหรอ? แวะซื้อเหยื่อก่อนจ้า!" });
        Label(market, "ร้านเหยื่อ", baitStall + Vector3.up * 4f, 0.55f, new Color(0.85f, 1f, 0.75f));
        ForestLakeSceneBuilder.PlaceModel(Pirate + "barrel.fbx", market, Ground(63.3f, 70.2f), 0f, 0.9f, true);
        ForestLakeSceneBuilder.PlaceModel(Props + "Bucket_Metal.fbx", market, Ground(57f, 69.2f), 0f, 0.4f, true);
        ForestLakeSceneBuilder.PlaceModel(Pirate + "flag-pennant.fbx", market, Ground(55.5f, 73.5f), 180f, 3.5f, true);
        ForestLakeSceneBuilder.PlaceModel(Pirate + "flag-pennant.fbx", market, Ground(64.5f, 73.5f), 180f, 3.5f, true);
        if (stall == null) Debug.LogWarning("[Village] ไม่พบโมเดล Stall_Empty");
        Block(new Vector2(Center.x, 71f), 12f);
    }

    // ------------------------------------------------------------------ Notice board

    private static void BuildNoticeBoard(Transform env)
    {
        Material wood = Mat("VillageWood", new Color(0.45f, 0.3f, 0.18f), 0.1f);
        Material board = Mat("BoardWood", new Color(0.6f, 0.43f, 0.27f), 0.1f);
        Material roof = Mat("Roof_Green", new Color(0.24f, 0.42f, 0.28f), 0.15f);
        Color[] paperColors = { new Color(0.96f, 0.93f, 0.82f), new Color(1f, 0.97f, 0.9f), new Color(0.98f, 0.9f, 0.6f), new Color(0.9f, 0.95f, 0.98f) };

        // ทางเข้าลานจากทางเดินทิศใต้ หันป้ายลงใต้ให้เห็นตอนเดินเข้าหมู่บ้าน
        Vector3 p = Ground(65.5f, 48.5f);
        var root = new GameObject("Notice Board").transform;
        root.SetParent(env);
        root.SetPositionAndRotation(p, Quaternion.Euler(0f, 200f, 0f));

        Prim("Post_L", root, PrimitiveType.Cube, new Vector3(-1.1f, 1.3f, 0f), new Vector3(0.16f, 2.6f, 0.16f), wood, false);
        Prim("Post_R", root, PrimitiveType.Cube, new Vector3(1.1f, 1.3f, 0f), new Vector3(0.16f, 2.6f, 0.16f), wood, false);
        Prim("Board", root, PrimitiveType.Cube, new Vector3(0f, 1.75f, 0f), new Vector3(2.3f, 1.4f, 0.1f), board, true);
        Roof("BoardRoof", root, new Vector3(0f, 2.55f, 0f), new Vector3(2.9f, 0.55f, 0.8f), roof);
        for (int i = 0; i < 5; i++)
        {
            float x = -0.8f + i * 0.4f + (float)(rng.NextDouble() - 0.5) * 0.1f;
            float y = 1.75f + (i % 2 == 0 ? 0.22f : -0.25f);
            Prim("Paper", root, PrimitiveType.Cube, new Vector3(x, y, 0.06f), new Vector3(0.36f, 0.46f, 0.01f),
                Mat("Paper_" + (i % 4), paperColors[i % 4], 0.05f), false, Quaternion.Euler(0f, 0f, (float)(rng.NextDouble() - 0.5) * 14f));
        }

        root.gameObject.AddComponent<NoticeBoard>().EditorSetup("อ่านป้ายประกาศราคาปลา", 3f);
        Label(env, "ป้ายประกาศ", p + Vector3.up * 3.4f, 0.55f, new Color(1f, 0.95f, 0.8f));
        Block(new Vector2(p.x, p.z), 3f);
    }

    // ------------------------------------------------------------------ Houses

    private struct HouseStyle
    {
        public string name;
        public Color wall, roof, trim;
        public float width, depth, height;
    }

    private static void BuildHouses(Transform env)
    {
        HouseStyle[] styles =
        {
            new HouseStyle { name = "House_A", wall = new Color(0.93f, 0.87f, 0.72f), roof = new Color(0.62f, 0.24f, 0.18f), trim = new Color(0.35f, 0.5f, 0.38f), width = 5.5f, depth = 4.5f, height = 3f },
            new HouseStyle { name = "House_B", wall = new Color(0.84f, 0.88f, 0.9f), roof = new Color(0.27f, 0.36f, 0.5f), trim = new Color(0.6f, 0.35f, 0.2f), width = 5f, depth = 4f, height = 2.8f },
            new HouseStyle { name = "House_C", wall = new Color(0.95f, 0.82f, 0.62f), roof = new Color(0.3f, 0.45f, 0.28f), trim = new Color(0.55f, 0.25f, 0.2f), width = 6f, depth = 4.8f, height = 3.2f },
            new HouseStyle { name = "House_D", wall = new Color(0.78f, 0.6f, 0.45f), roof = new Color(0.72f, 0.46f, 0.2f), trim = new Color(0.3f, 0.32f, 0.4f), width = 4.6f, depth = 4f, height = 2.7f },
        };
        var prefabs = new List<GameObject>();
        foreach (HouseStyle s in styles) prefabs.Add(MakeHousePrefab(s));

        var houses = new GameObject("Houses").transform;
        houses.SetParent(env);
        (float angle, float radius)[] spots =
        {
            (30f, 27f), (150f, 27f), (188f, 27f), (228f, 26f), (312f, 26f), (352f, 27f), (118f, 36f), (62f, 36f),
        };

        string[] yardProps = { "Barrel.fbx", "Crate_Wooden.fbx", "FarmCrate_Empty.fbx", "Bucket_Wooden_1.fbx" };
        for (int i = 0; i < spots.Length; i++)
        {
            float rad = spots[i].angle * Mathf.Deg2Rad;
            float x = Center.x + Mathf.Cos(rad) * spots[i].radius, z = Center.y + Mathf.Sin(rad) * spots[i].radius;
            Vector3 pos = Ground(x, z);
            Vector3 toCenter = new Vector3(Center.x - x, 0f, Center.y - z);
            Quaternion rot = Quaternion.LookRotation(toCenter);

            var house = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[i % prefabs.Count], houses);
            house.transform.SetPositionAndRotation(pos - Vector3.up * 0.1f, rot);

            // ของตกแต่งหน้าบ้าน
            Vector3 front = pos + rot * new Vector3(1.8f, 0f, 3f);
            ForestLakeSceneBuilder.PlaceModel(Props + yardProps[i % yardProps.Length], houses, Ground(front.x, front.z), (float)rng.NextDouble() * 360f, 0.7f, true);
            Block(new Vector2(x, z), 6f);
        }
    }

    private static GameObject MakeHousePrefab(HouseStyle s)
    {
        Material wall = Mat(s.name + "_Wall", s.wall, 0.08f);
        Material roof = Mat(s.name + "_Roof", s.roof, 0.15f);
        Material trim = Mat(s.name + "_Trim", s.trim, 0.2f);
        Material stone = Mat("Stone", new Color(0.55f, 0.55f, 0.52f), 0.1f);
        Material door = Mat("DoorWood", new Color(0.36f, 0.22f, 0.13f), 0.15f);
        Material glass = Mat("WindowGlass", new Color(0.62f, 0.8f, 0.9f), 0.9f);
        Material brick = Mat("Chimney", new Color(0.52f, 0.33f, 0.27f), 0.1f);

        float w = s.width, d = s.depth, h = s.height, b = 0.35f;
        var root = new GameObject(s.name).transform;

        Prim("Foundation", root, PrimitiveType.Cube, new Vector3(0f, b / 2f, 0f), new Vector3(w + 0.3f, b, d + 0.3f), stone, false);
        Prim("Walls", root, PrimitiveType.Cube, new Vector3(0f, b + h / 2f, 0f), new Vector3(w, h, d), wall, false);
        Roof("Roof", root, new Vector3(0f, b + h, 0f), new Vector3(w + 0.8f, 1.8f, d + 0.9f), roof);
        Prim("Chimney", root, PrimitiveType.Cube, new Vector3(w / 2f - 1f, b + h + 1.1f, -d / 4f), new Vector3(0.55f, 1.6f, 0.55f), brick, false);

        // ประตู + บันได
        Prim("Door", root, PrimitiveType.Cube, new Vector3(0f, b + 0.95f, d / 2f + 0.03f), new Vector3(1f, 1.9f, 0.08f), door, false);
        Prim("DoorFrame", root, PrimitiveType.Cube, new Vector3(0f, b + 1.95f, d / 2f + 0.05f), new Vector3(1.25f, 0.14f, 0.1f), trim, false);
        Prim("Step", root, PrimitiveType.Cube, new Vector3(0f, b / 2f, d / 2f + 0.45f), new Vector3(1.5f, b, 0.6f), stone, false);

        // หน้าต่าง + บานเกล็ด
        foreach (float sx in new[] { -1f, 1f })
        {
            float x = sx * (w / 2f - 1.15f);
            Prim("Window", root, PrimitiveType.Cube, new Vector3(x, b + h * 0.58f, d / 2f + 0.02f), new Vector3(0.85f, 0.75f, 0.05f), glass, false);
            Prim("Shutter", root, PrimitiveType.Cube, new Vector3(x - 0.58f, b + h * 0.58f, d / 2f + 0.04f), new Vector3(0.28f, 0.85f, 0.05f), trim, false);
            Prim("Shutter", root, PrimitiveType.Cube, new Vector3(x + 0.58f, b + h * 0.58f, d / 2f + 0.04f), new Vector3(0.28f, 0.85f, 0.05f), trim, false);
            Prim("Sill", root, PrimitiveType.Cube, new Vector3(x, b + h * 0.58f - 0.43f, d / 2f + 0.08f), new Vector3(1f, 0.08f, 0.16f), trim, false);
            Prim("SideWindow", root, PrimitiveType.Cube, new Vector3(sx * (w / 2f + 0.02f), b + h * 0.58f, 0f), new Vector3(0.05f, 0.75f, 0.85f), glass, false);
        }

        var col = root.gameObject.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, (b + h) / 2f, 0f);
        col.size = new Vector3(w + 0.3f, b + h, d + 0.3f);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, Root + "/Prefabs/" + s.name + ".prefab");
        Object.DestroyImmediate(root.gameObject);
        return prefab;
    }

    // ------------------------------------------------------------------ Path & nature

    private static void BuildLakePath(Transform env)
    {
        Material wood = Mat("VillageWood", new Color(0.45f, 0.3f, 0.18f), 0.1f);
        Material sign = Mat("BoardWood", new Color(0.6f, 0.43f, 0.27f), 0.1f);

        // ป้ายทางไปทะเลสาบ (ปลายทางเดินทิศใต้)
        Vector3 p = Ground(PathX(17f) + 2.4f, 17f);
        var travel = new GameObject("To Lake").transform;
        travel.SetParent(env);
        travel.SetPositionAndRotation(p, Quaternion.Euler(0f, 180f, 0f));
        Prim("Post", travel, PrimitiveType.Cube, new Vector3(0f, 1.1f, 0f), new Vector3(0.14f, 2.2f, 0.14f), wood, false);
        Prim("Arrow", travel, PrimitiveType.Cube, new Vector3(-0.35f, 1.8f, -0.1f), new Vector3(1.2f, 0.32f, 0.06f), sign, false);
        travel.gameObject.AddComponent<SceneTravelPoint>().EditorSetup("เดินทางไปทะเลสาบ", 3.2f);
        travel.GetComponent<SceneTravelPoint>().EditorSetTarget("ForestLake", "FromVillage");
        Label(env, "ไปทะเลสาบ", p + Vector3.up * 2.7f, 0.5f, new Color(0.75f, 0.95f, 1f));

        // จุดเกิดเมื่อเดินมาจากทะเลสาบ
        var spawn = new GameObject("Spawn FromLake").AddComponent<SpawnPoint>();
        spawn.EditorSetId("FromLake");
        spawn.transform.SetParent(env);
        spawn.transform.SetPositionAndRotation(Ground(PathX(24f), 24f), Quaternion.identity);

        // รั้วไม้เตี้ยๆ ริมทางเดิน
        for (float z = 26f; z < 46f; z += 4f)
        {
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 fp = Ground(PathX(z) + side * 3.2f, z);
                Prim("FencePost", env, PrimitiveType.Cube, fp + Vector3.up * 0.45f, new Vector3(0.12f, 0.9f, 0.12f), wood, false);
                Prim("FenceRail", env, PrimitiveType.Cube, fp + new Vector3(0f, 0.65f, 2f), new Vector3(0.08f, 0.1f, 4f), wood, false);
            }
        }
    }

    private static void BuildNature(Transform env)
    {
        var trees = new GameObject("Trees").transform;
        trees.SetParent(env);
        var pines = new List<GameObject>();
        foreach (string n in new[] { "Pine_A", "Pine_B", "Pine_C" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/" + n + ".prefab");
            if (prefab != null) pines.Add(prefab);
        }
        if (pines.Count == 0) Debug.LogWarning("[Village] ไม่พบ prefab ต้นสน กรุณาสร้างฉากทะเลสาบก่อน");

        var placed = new List<Vector2>();
        for (int i = 0; i < 5000 && placed.Count < 170 && pines.Count > 0; i++)
        {
            float x = Rand(3f, TerrainSize - 3f), z = Rand(3f, TerrainSize - 3f);
            var p2 = new Vector2(x, z);
            float d = Vector2.Distance(p2, Center);
            if (d < 33f || OnPath(x, z, 9f) || IsBlocked(p2, 2f)) continue;
            float spacing = Mathf.Lerp(5f, 3.3f, Mathf.InverseLerp(33f, 45f, d));
            if (placed.Exists(o => (o - p2).sqrMagnitude < spacing * spacing)) continue;

            placed.Add(p2);
            var tree = (GameObject)PrefabUtility.InstantiatePrefab(pines[rng.Next(pines.Count)], trees);
            float s = Rand(0.75f, 1.3f);
            tree.transform.localScale = new Vector3(s, s * Rand(0.9f, 1.15f), s);
            tree.transform.SetPositionAndRotation(Ground(x, z) - Vector3.up * 0.15f, Quaternion.Euler(0f, Rand(0f, 360f), 0f));
        }

        var grass = new GameObject("Grass").transform;
        grass.SetParent(env);
        string[] grassModels = { "grass.fbx", "grass-plant.fbx", "grass-patch.fbx" };
        int count = 0;
        for (int i = 0; i < 2000 && count < 160; i++)
        {
            float x = Rand(4f, TerrainSize - 4f), z = Rand(4f, TerrainSize - 4f);
            var p2 = new Vector2(x, z);
            float d = Vector2.Distance(p2, Center);
            if (d < 17f || d > 50f || OnPath(x, z, 3.5f) || IsBlocked(p2, 0.5f)) continue;
            if (ForestLakeSceneBuilder.PlaceModel(Pirate + grassModels[rng.Next(grassModels.Length)], grass, Ground(x, z), Rand(0f, 360f), Rand(0.5f, 1f), true, 0.05f) != null) count++;
        }

        var rocks = new GameObject("Rocks").transform;
        rocks.SetParent(env);
        string[] rockModels = { "rocks-a.fbx", "rocks-b.fbx", "rocks-c.fbx" };
        for (int i = 0; i < 14; i++)
        {
            float a = Rand(0f, Mathf.PI * 2f), r = Rand(30f, 44f);
            float x = Center.x + Mathf.Cos(a) * r, z = Center.y + Mathf.Sin(a) * r;
            if (OnPath(x, z, 5f) || IsBlocked(new Vector2(x, z), 1f)) continue;
            ForestLakeSceneBuilder.PlaceModel(Pirate + rockModels[rng.Next(rockModels.Length)], rocks, Ground(x, z), Rand(0f, 360f), Rand(1f, 2.4f), false, 0.25f);
        }
    }

    // ------------------------------------------------------------------ Helpers

    private static void Block(Vector2 p, float radius)
    {
        blocked.Add(new Vector2(p.x, p.y));
        for (int i = 0; i < 8; i++)
        {
            float a = i / 8f * Mathf.PI * 2f;
            blocked.Add(p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius * 0.7f);
        }
    }

    private static bool IsBlocked(Vector2 p, float margin) => blocked.Exists(b => (b - p).sqrMagnitude < (3f + margin) * (3f + margin));

    private static float Rand(float min, float max) => (float)(min + rng.NextDouble() * (max - min));

    private static Material Mat(string name, Color color, float smoothness)
    {
        Material m = ForestLakeSceneBuilder.LoadOrCreateMaterial(name, Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", smoothness);
        return m;
    }

    private static GameObject Prim(string name, Transform parent, PrimitiveType type, Vector3 localPos, Vector3 scale, Material mat, bool keepCollider, Quaternion? rot = null)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = rot ?? Quaternion.identity;
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    /// <summary>หลังคาจั่ว (ปริซึมสามเหลี่ยม สันหลังคาตามแกน X) ฐานอยู่ที่ localPos</summary>
    private static void Roof(string name, Transform parent, Vector3 localPos, Vector3 size, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = size;
        go.AddComponent<MeshFilter>().sharedMesh = PrismMesh();
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
    }

    private static Mesh PrismMesh()
    {
        string path = Root + "/Meshes/RoofPrism.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;

        var verts = new List<Vector3>();
        var tris = new List<int>();
        void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f) (b, c) = (c, b);
            int k = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            tris.Add(k); tris.Add(k + 1); tris.Add(k + 2);
        }
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward)
        {
            Tri(a, b, c, outward);
            Tri(a, c, d, outward);
        }

        var fl = new Vector3(-0.5f, 0f, 0.5f); var fr = new Vector3(0.5f, 0f, 0.5f);
        var bl = new Vector3(-0.5f, 0f, -0.5f); var br = new Vector3(0.5f, 0f, -0.5f);
        var tl = new Vector3(-0.5f, 1f, 0f); var tr = new Vector3(0.5f, 1f, 0f);

        Quad(fl, fr, tr, tl, new Vector3(0f, 0.5f, 1f));   // ลาดหน้า
        Quad(bl, br, tr, tl, new Vector3(0f, 0.5f, -1f));  // ลาดหลัง
        Tri(fr, br, tr, Vector3.right);                    // จั่วขวา
        Tri(fl, bl, tl, Vector3.left);                     // จั่วซ้าย
        Quad(fl, fr, br, bl, Vector3.down);                // ใต้หลังคา

        var mesh = new Mesh { name = "RoofPrism" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static void Label(Transform parent, string text, Vector3 position, float height, Color color)
    {
        var go = new GameObject("Label " + text);
        go.transform.SetParent(parent);
        go.transform.position = position;
        go.AddComponent<WorldLabel>().EditorSetup(text, height, color);
    }
}
