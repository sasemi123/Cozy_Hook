using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// สร้างฉาก "ทะเลสาบในป่า" อัตโนมัติจากโมเดลใน Assets/model
/// เมนู: Tools > Cozy Hook > Build Forest Lake Scene (รันซ้ำได้ จะสร้างฉากใหม่ทับของเดิม)
/// </summary>
public static class ForestLakeSceneBuilder
{
    private const string Root = "Assets/Environment";
    private const string ScenePath = "Assets/Scenes/ForestLake.unity";
    private const string Pirate = "Assets/model/kenney_pirate-kit/Models/FBX format/";
    private const string Watercraft = "Assets/model/kenney_watercraft-pack/Models/FBX format/";
    private const string Props = "Assets/model/Fantasy Props MegaKit[Standard]/Exports/FBX/";
    private const string FishPack = "Assets/model/Fish Pack Animated by Quaternius/FBX/";

    private const float TerrainSize = 140f;
    private const float TerrainHeight = 30f;
    private const float WaterLevel = 4f;
    private const float DeckY = WaterLevel + 0.7f;
    private const float DockX = 70f;

    private static readonly Vector2 LakeCenter = new Vector2(70f, 74f);
    private static readonly Vector2 LakeRadius = new Vector2(30f, 22f);

    private static Terrain terrain;
    private static float dockEndZ = 60f;
    private static System.Random rng;

    [MenuItem("Tools/Cozy Hook/Build Forest Lake Scene")]
    public static void Build()
    {
        if (SceneManager.GetActiveScene().isDirty)
        {
            Debug.LogError("[ForestLake] ฉากปัจจุบันยังไม่ได้บันทึก กรุณาบันทึกก่อนแล้วรันใหม่");
            return;
        }

        rng = new System.Random(7);
        EnsureFolder(Root);
        EnsureFolder(Root + "/Materials");
        EnsureFolder(Root + "/Meshes");
        EnsureFolder(Root + "/Textures");
        EnsureFolder(Root + "/Prefabs");

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        BuildLighting();
        terrain = BuildTerrain();
        BuildWater();

        Transform env = new GameObject("Environment").transform;
        BuildDock(env);
        BuildShoreProps(env);
        BuildRocksAndGrass(env);
        BuildForest(env);
        BuildFish(env);
        BuildCamera();
        FishingGearBuilder.SetupWater();
        LakeVillageLink.AddLink();

        foreach (Transform t in env.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettings(ScenePath);
        Debug.Log("[ForestLake] สร้างฉากเสร็จ: " + ScenePath);
    }

    // ------------------------------------------------------------------ Lighting

    internal static void BuildLighting()
    {
        var sunGo = new GameObject("Sun");
        var sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.9f, 0.76f);
        sun.intensity = 1.4f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.85f;
        sunGo.transform.rotation = Quaternion.Euler(34f, 135f, 0f);
        sunGo.AddComponent<SunCycle>(); // แสงเปลี่ยนตามเวลาในเกม

        var sky = LoadOrCreateMaterial("Sky_Procedural", Shader.Find("Skybox/Procedural"));
        sky.SetFloat("_SunSize", 0.05f);
        sky.SetFloat("_AtmosphereThickness", 1.05f);
        sky.SetColor("_SkyTint", new Color(0.52f, 0.62f, 0.72f));
        sky.SetColor("_GroundColor", new Color(0.36f, 0.4f, 0.33f));
        sky.SetFloat("_Exposure", 1.25f);

        RenderSettings.skybox = sky;
        RenderSettings.sun = sun;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.58f, 0.67f, 0.76f);
        RenderSettings.ambientEquatorColor = new Color(0.46f, 0.53f, 0.45f);
        RenderSettings.ambientGroundColor = new Color(0.24f, 0.26f, 0.2f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.66f, 0.76f, 0.77f);
        RenderSettings.fogStartDistance = 70f;
        RenderSettings.fogEndDistance = 280f;

        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/SampleSceneProfile.asset");
        if (profile != null)
        {
            var volume = new GameObject("Global Volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }
    }

    // ------------------------------------------------------------------ Terrain

    private static float Ellipse(float x, float z)
    {
        float dx = (x - LakeCenter.x) / LakeRadius.x;
        float dz = (z - LakeCenter.y) / LakeRadius.y;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    private static float HeightAt(float x, float z)
    {
        float n = Mathf.PerlinNoise(x * 0.05f + 10f, z * 0.05f + 20f);
        float e = Ellipse(x, z) + (n - 0.5f) * 0.12f;

        float h;
        if (e < 1f) h = WaterLevel - 0.1f - 3.2f * (1f - e * e);
        else if (e < 1.3f) h = WaterLevel - 0.1f + (e - 1f) / 0.3f * 1.1f;
        else h = 5f + (e - 1.3f) * 2f * (0.6f + n);

        // เนินเขาล้อมรอบขอบแผนที่
        float bx = Mathf.Abs(x / TerrainSize - 0.5f) * 2f;
        float bz = Mathf.Abs(z / TerrainSize - 0.5f) * 2f;
        h += Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 1f, Mathf.Max(bx, bz))) * 12f;

        // ผิวดินขรุขระเล็กน้อยบนบก
        h += (Mathf.PerlinNoise(x * 0.15f, z * 0.15f) - 0.5f) * 0.6f * Mathf.Clamp01(e - 1f);
        return Mathf.Clamp(h, 0f, TerrainHeight);
    }

    private static Terrain BuildTerrain()
    {
        string dataPath = Root + "/ForestLakeTerrain.asset";
        AssetDatabase.DeleteAsset(dataPath);

        var data = new TerrainData { heightmapResolution = 257, alphamapResolution = 256 };
        data.size = new Vector3(TerrainSize, TerrainHeight, TerrainSize);

        int res = data.heightmapResolution;
        var heights = new float[res, res];
        for (int zi = 0; zi < res; zi++)
        for (int xi = 0; xi < res; xi++)
        {
            float x = xi / (float)(res - 1) * TerrainSize;
            float z = zi / (float)(res - 1) * TerrainSize;
            heights[zi, xi] = HeightAt(x, z) / TerrainHeight;
        }
        data.SetHeights(0, 0, heights);

        data.terrainLayers = new[]
        {
            MakeLayer("Grass", new Color(0.36f, 0.55f, 0.24f), new Color(0.3f, 0.47f, 0.2f)),
            MakeLayer("ForestFloor", new Color(0.27f, 0.37f, 0.18f), new Color(0.33f, 0.3f, 0.2f)),
            MakeLayer("Sand", new Color(0.76f, 0.68f, 0.5f), new Color(0.66f, 0.58f, 0.42f)),
        };

        int ares = data.alphamapResolution;
        var alpha = new float[ares, ares, 3];
        for (int zi = 0; zi < ares; zi++)
        for (int xi = 0; xi < ares; xi++)
        {
            float x = xi / (float)(ares - 1) * TerrainSize;
            float z = zi / (float)(ares - 1) * TerrainSize;
            float h = HeightAt(x, z);
            float e = Ellipse(x, z);
            float sand = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(4.5f, 5.3f, h));
            float forest = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.45f, 2.1f, e))
                           * Mathf.Lerp(0.55f, 1f, Mathf.PerlinNoise(x * 0.08f, z * 0.08f));
            forest *= 1f - sand;
            float grass = Mathf.Max(0f, 1f - sand - forest);
            alpha[zi, xi, 0] = grass;
            alpha[zi, xi, 1] = forest;
            alpha[zi, xi, 2] = sand;
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

    internal static TerrainLayer MakeLayer(string name, Color a, Color b)
    {
        string texPath = Root + "/Textures/" + name + ".asset";
        AssetDatabase.DeleteAsset(texPath);
        var tex = new Texture2D(128, 128, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Repeat };
        for (int y = 0; y < 128; y++)
        for (int x = 0; x < 128; x++)
        {
            // noise ที่ต่อกันได้ไร้รอยต่อ (tileable) โดยใช้ sin/cos
            float u = x / 128f * Mathf.PI * 2f, v = y / 128f * Mathf.PI * 2f;
            float n = Mathf.PerlinNoise(Mathf.Cos(u) * 2f + 5f + Mathf.Sin(v), Mathf.Sin(u) * 2f + 5f + Mathf.Cos(v));
            float fine = Mathf.PerlinNoise(x * 0.35f, y * 0.35f) * 0.25f;
            tex.SetPixel(x, y, Color.Lerp(a, b, Mathf.Clamp01(n + fine - 0.1f)));
        }
        tex.Apply(true);
        AssetDatabase.CreateAsset(tex, texPath);

        string layerPath = Root + "/Textures/" + name + ".terrainlayer";
        AssetDatabase.DeleteAsset(layerPath);
        var layer = new TerrainLayer { diffuseTexture = tex, tileSize = new Vector2(6f, 6f), smoothness = 0.05f };
        AssetDatabase.CreateAsset(layer, layerPath);
        return layer;
    }

    private static float GroundY(float x, float z) => terrain.SampleHeight(new Vector3(x, 0f, z));

    // ------------------------------------------------------------------ Water

    private static void BuildWater()
    {
        var water = GameObject.CreatePrimitive(PrimitiveType.Plane);
        water.name = "Lake Water";
        Object.DestroyImmediate(water.GetComponent<Collider>());
        water.transform.position = new Vector3(LakeCenter.x, WaterLevel, LakeCenter.y);
        water.transform.localScale = new Vector3(8f, 1f, 6.5f);

        var mat = LoadOrCreateMaterial("Water", Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", new Color(0.09f, 0.3f, 0.34f, 0.9f));
        mat.SetFloat("_Smoothness", 0.8f);
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = (int)RenderQueue.Transparent;
        water.GetComponent<MeshRenderer>().sharedMaterial = mat;
        water.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
    }

    // ------------------------------------------------------------------ Dock & props

    private static void BuildDock(Transform env)
    {
        var dock = new GameObject("Dock").transform;
        dock.SetParent(env);

        var wood = LoadOrCreateMaterial("DockPost", Shader.Find("Universal Render Pipeline/Lit"));
        wood.SetColor("_BaseColor", new Color(0.4f, 0.27f, 0.16f));
        wood.SetFloat("_Smoothness", 0.1f);

        float z = 47f;
        for (int i = 0; i < 6; i++)
        {
            var tile = PlaceModel(Pirate + "platform-planks.fbx", dock, new Vector3(DockX, DeckY, z), 0f, 2.2f, false);
            if (tile == null) break;
            Bounds b = WorldBounds(tile);
            tile.transform.position += Vector3.up * (DeckY - b.max.y);
            b = WorldBounds(tile);

            for (int c = 0; c < 4; c++)
            {
                float px = c % 2 == 0 ? b.min.x + 0.12f : b.max.x - 0.12f;
                float pz = c < 2 ? b.min.z + 0.12f : b.max.z - 0.12f;
                var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                post.name = "Post";
                Object.DestroyImmediate(post.GetComponent<Collider>());
                post.transform.SetParent(dock);
                post.transform.localScale = new Vector3(0.16f, 1.6f, 0.16f);
                post.transform.position = new Vector3(px, DeckY - 1.4f, pz);
                post.GetComponent<MeshRenderer>().sharedMaterial = wood;
            }
            z += b.size.z;
        }
        Debug.Log($"[ForestLake] Dock ends at z={z:F2}, deck y={DeckY}");

        // ของบนท่า
        dockEndZ = z;
        PlaceModel(Props + "Bucket_Wooden_1.fbx", dock, new Vector3(DockX - 0.7f, DeckY, z - 1.6f), 30f, 0.45f, true);
        PlaceModel(Props + "Stool.fbx", dock, new Vector3(DockX + 0.65f, DeckY, z - 2.6f), -20f, 0.5f, true);
        PlaceModel(Props + "Crate_Wooden.fbx", dock, new Vector3(DockX + 0.6f, DeckY, 48.2f), 12f, 0.6f, true);
        PlaceModel(Props + "Lantern_Wall.fbx", dock, new Vector3(DockX + 0.6f, DeckY + 0.6f, 48.2f), 0f, 0.4f, true);

        // เรือพายผูกข้างท่า
        var boat = PlaceModel(Pirate + "boat-row-small.fbx", dock, new Vector3(DockX + 3.4f, WaterLevel - 0.3f, z - 4.5f), 0f, 3.2f, false);
        if (boat != null) AlignLongAxisToZ(boat);
    }

    private static void BuildShoreProps(Transform env)
    {
        var shore = new GameObject("Shore Props").transform;
        shore.SetParent(env);

        Ground(PlaceModel(Pirate + "barrel.fbx", shore, new Vector3(67.6f, 0f, 46.5f), 20f, 0.9f, true));
        Ground(PlaceModel(Props + "Barrel_Apples.fbx", shore, new Vector3(66.6f, 0f, 45.6f), -15f, 0.9f, true));
        Ground(PlaceModel(Props + "Bench.fbx", shore, new Vector3(64.2f, 0f, 47.8f), 70f, 1.8f, false));
        Ground(PlaceModel(Props + "Crate_Wooden.fbx", shore, new Vector3(73.2f, 0f, 46.2f), 35f, 0.7f, true));
        Ground(PlaceModel(Props + "Rope_1.fbx", shore, new Vector3(72.6f, 0f, 47.6f), 0f, 0.6f, false));

        // เรือตกปลาลำเล็กจอดอยู่ฝั่งตรงข้าม
        PlaceModel(Watercraft + "boat-fishing-small.fbx", shore, new Vector3(86f, WaterLevel - 0.35f, 88f), 120f, 5f, false);
        PlaceModel(Watercraft + "buoy.fbx", shore, new Vector3(62f, WaterLevel - 0.3f, 70f), 0f, 0.9f, true);
    }

    private static void BuildRocksAndGrass(Transform env)
    {
        var rocks = new GameObject("Rocks").transform;
        rocks.SetParent(env);
        string[] rockModels = { "rocks-a.fbx", "rocks-b.fbx", "rocks-c.fbx" };
        int placed = 0;
        for (int i = 0; i < 200 && placed < 26; i++)
        {
            float ang = Rand(0f, Mathf.PI * 2f);
            float e = Rand(0.96f, 1.18f);
            float x = LakeCenter.x + Mathf.Cos(ang) * LakeRadius.x * e;
            float z = LakeCenter.y + Mathf.Sin(ang) * LakeRadius.y * e;
            if (NearDock(x, z, 5f)) continue;
            var r = PlaceModel(Pirate + rockModels[rng.Next(rockModels.Length)], rocks,
                new Vector3(x, Mathf.Max(GroundY(x, z), WaterLevel - 0.6f), z), Rand(0f, 360f), Rand(1.2f, 3f), false, 0.35f);
            if (r != null) placed++;
        }

        var grass = new GameObject("Grass").transform;
        grass.SetParent(env);
        string[] grassModels = { "grass.fbx", "grass-plant.fbx", "grass-patch.fbx" };
        placed = 0;
        for (int i = 0; i < 1500 && placed < 220; i++)
        {
            float x = Rand(4f, TerrainSize - 4f), z = Rand(4f, TerrainSize - 4f);
            float e = Ellipse(x, z);
            if (e < 1.12f || e > 3.2f) continue;
            if (NearDock(x, z, 1.6f)) continue;
            var g = PlaceModel(Pirate + grassModels[rng.Next(grassModels.Length)], grass,
                new Vector3(x, GroundY(x, z), z), Rand(0f, 360f), Rand(0.5f, 1.1f), true, 0.05f);
            if (g != null) placed++;
        }
    }

    // ------------------------------------------------------------------ Forest

    private static void BuildForest(Transform env)
    {
        var trunk = LoadOrCreateMaterial("TreeTrunk", Shader.Find("Universal Render Pipeline/Lit"));
        trunk.SetColor("_BaseColor", new Color(0.36f, 0.24f, 0.15f));
        trunk.SetFloat("_Smoothness", 0.05f);

        GameObject[] variants =
        {
            MakePine("Pine_A", trunk, LeafMaterial("Leaves_A", new Color(0.16f, 0.36f, 0.2f)), 3, 7f, 2.1f, 7),
            MakePine("Pine_B", trunk, LeafMaterial("Leaves_B", new Color(0.22f, 0.43f, 0.22f)), 4, 9f, 2.3f, 8),
            MakePine("Pine_C", trunk, LeafMaterial("Leaves_C", new Color(0.18f, 0.34f, 0.29f)), 2, 5.5f, 2f, 6),
        };

        var forest = new GameObject("Forest").transform;
        forest.SetParent(env);
        var positions = new List<Vector2>();
        for (int i = 0; i < 6000 && positions.Count < 280; i++)
        {
            float x = Rand(3f, TerrainSize - 3f), z = Rand(3f, TerrainSize - 3f);
            float e = Ellipse(x, z);
            if (e < 1.4f) continue;
            if (NearDock(x, z, 8f)) continue;

            // ใกล้ทะเลสาบโปรง ไกลออกไปหนาแน่น
            float spacing = Mathf.Lerp(5f, 3.2f, Mathf.InverseLerp(1.4f, 2.4f, e));
            bool tooClose = false;
            foreach (var p in positions)
                if ((p - new Vector2(x, z)).sqrMagnitude < spacing * spacing) { tooClose = true; break; }
            if (tooClose) continue;

            positions.Add(new Vector2(x, z));
            var prefab = variants[rng.Next(variants.Length)];
            var tree = (GameObject)PrefabUtility.InstantiatePrefab(prefab, forest);
            float s = Rand(0.75f, 1.35f);
            tree.transform.localScale = new Vector3(s * Rand(0.9f, 1.1f), s, s * Rand(0.9f, 1.1f));
            tree.transform.SetPositionAndRotation(new Vector3(x, GroundY(x, z) - 0.15f, z), Quaternion.Euler(0f, Rand(0f, 360f), 0f));
        }
        Debug.Log($"[ForestLake] Trees placed: {positions.Count}");
    }

    private static Material LeafMaterial(string name, Color c)
    {
        var m = LoadOrCreateMaterial(name, Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", 0.08f);
        return m;
    }

    private static GameObject MakePine(string name, Material trunkMat, Material leafMat, int tiers, float height, float radius, int sides)
    {
        Mesh cone = ConeMesh(sides);
        var root = new GameObject(name);

        var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.name = "Trunk";
        Object.DestroyImmediate(trunk.GetComponent<Collider>());
        trunk.transform.SetParent(root.transform);
        trunk.transform.localPosition = new Vector3(0f, height * 0.15f, 0f);
        trunk.transform.localScale = new Vector3(radius * 0.17f, height * 0.15f, radius * 0.17f);
        trunk.GetComponent<MeshRenderer>().sharedMaterial = trunkMat;

        float tierHeight = height * 0.45f;
        for (int i = 0; i < tiers; i++)
        {
            float t = tiers == 1 ? 0f : i / (float)(tiers - 1);
            var tier = new GameObject("Leaves_" + i);
            tier.transform.SetParent(root.transform);
            tier.transform.localPosition = new Vector3(0f, Mathf.Lerp(height * 0.2f, height - tierHeight, t), 0f);
            tier.transform.localRotation = Quaternion.Euler(0f, i * 23f, 0f);
            float r = radius * Mathf.Lerp(1f, 0.5f, t);
            tier.transform.localScale = new Vector3(r, tierHeight, r);
            tier.AddComponent<MeshFilter>().sharedMesh = cone;
            tier.AddComponent<MeshRenderer>().sharedMaterial = leafMat;
        }

        var col = root.AddComponent<CapsuleCollider>();
        col.radius = radius * 0.2f;
        col.height = height;
        col.center = new Vector3(0f, height * 0.5f, 0f);

        string path = Root + "/Prefabs/" + name + ".prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static Mesh ConeMesh(int sides)
    {
        string path = Root + "/Meshes/Cone" + sides + ".asset";
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

        Vector3 tip = Vector3.up;
        for (int i = 0; i < sides; i++)
        {
            float a0 = i / (float)sides * Mathf.PI * 2f, a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
            var p0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
            var p1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
            Vector3 mid = (p0 + p1) * 0.5f;
            Tri(tip, p0, p1, mid + Vector3.up * 0.5f);
            Tri(Vector3.zero, p0, p1, Vector3.down);
        }

        var mesh = new Mesh { name = "Cone" + sides };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    // ------------------------------------------------------------------ Fish & camera

    private static void BuildFish(Transform env)
    {
        var fish = new GameObject("Fish").transform;
        fish.SetParent(env);
        PlaceModel(FishPack + "Fish1.fbx", fish, new Vector3(DockX - 2.2f, WaterLevel - 0.7f, 58f), 70f, 0.5f, false);
        PlaceModel(FishPack + "Fish2.fbx", fish, new Vector3(DockX + 1.8f, WaterLevel - 0.9f, 60.5f), 200f, 0.6f, false);
        PlaceModel(FishPack + "Fish3.fbx", fish, new Vector3(DockX - 0.5f, WaterLevel - 1.1f, 62f), 130f, 0.45f, false);
    }

    private static void BuildCamera()
    {
        // ผู้เล่น (คนตกปลา) ยืนที่ปลายท่า พร้อมกล้องบุคคลที่สาม
        FishDataBuilder.CreateDefaultFish();
        FishermanBuilder.BuildPrefab();
        FishermanBuilder.PlaceInScene();
    }

    // ------------------------------------------------------------------ Helpers

    internal static GameObject PlaceModel(string path, Transform parent, Vector3 pos, float yaw, float targetSize, bool byHeight, float sink = 0f)
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (src == null)
        {
            Debug.LogWarning("[ForestLake] ไม่พบโมเดล: " + path);
            return null;
        }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(src, parent);
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        go.transform.localScale = Vector3.one;

        Bounds b = WorldBounds(go);
        float current = byHeight ? b.size.y : Mathf.Max(b.size.x, b.size.z);
        if (current > 0.0001f && targetSize > 0f) go.transform.localScale = Vector3.one * (targetSize / current);

        b = WorldBounds(go);
        go.transform.position += new Vector3(pos.x - b.center.x, pos.y - b.min.y - sink, pos.z - b.center.z);
        return go;
    }

    private static void Ground(GameObject go)
    {
        if (go == null) return;
        Bounds b = WorldBounds(go);
        go.transform.position += Vector3.up * (GroundY(b.center.x, b.center.z) - b.min.y - 0.03f);
    }

    private static void AlignLongAxisToZ(GameObject go)
    {
        Bounds b = WorldBounds(go);
        if (b.size.x > b.size.z)
        {
            Vector3 c = b.center;
            go.transform.RotateAround(c, Vector3.up, 90f);
        }
    }

    internal static Bounds WorldBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    private static bool NearDock(float x, float z, float margin) =>
        x > DockX - 1.5f - margin && x < DockX + 4.5f + margin && z > 44f - margin && z < 62f + margin;

    private static float Rand(float min, float max) => (float)(min + rng.NextDouble() * (max - min));

    internal static Material LoadOrCreateMaterial(string name, Shader shader)
    {
        string path = Root + "/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(m, path);
        }
        else if (m.shader != shader) m.shader = shader;
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return m;
    }

    internal static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }

    internal static void AddToBuildSettings(string path)
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (scenes.Exists(s => s.path == path)) return;
        scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
