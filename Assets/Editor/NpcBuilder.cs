using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// สร้างตัวละคร NPC พ่อค้าแม่ค้า (โมเดลจาก primitive แบบเดียวกับตัวผู้เล่น) เป็น prefab
/// แล้ววางไว้ประจำแผงในหมู่บ้าน
/// (เรียกจาก VillageSceneBuilder ตอนสร้างฉากหมู่บ้าน)
/// </summary>
public static class NpcBuilder
{
    private const string PrefabFolder = "Assets/Prefabs/NPC";
    private const string MaterialFolder = "Assets/Characters/NPC/Materials";

    public enum HatStyle { None, Cap, StrawHat, Headscarf }

    public struct Look
    {
        public string id;
        public Color shirt, pants, skin, hair, apron, shoes, hat;
        public HatStyle hatStyle;
        public bool mustache, bun, apronOn;
        public float height;   // สเกลความสูง (1 = เท่าผู้เล่น)
    }

    public static readonly Look Fishmonger = new Look
    {
        id = "Fishmonger",
        shirt = new Color(0.27f, 0.45f, 0.65f), pants = new Color(0.25f, 0.25f, 0.3f), skin = new Color(0.86f, 0.64f, 0.48f),
        hair = new Color(0.62f, 0.6f, 0.58f), apron = new Color(0.93f, 0.93f, 0.9f), shoes = new Color(0.95f, 0.8f, 0.2f), hat = new Color(0.2f, 0.32f, 0.5f),
        hatStyle = HatStyle.Cap, mustache = true, apronOn = true, height = 1.04f,
    };

    public static readonly Look BaitSeller = new Look
    {
        id = "BaitSeller",
        shirt = new Color(0.42f, 0.6f, 0.38f), pants = new Color(0.45f, 0.28f, 0.32f), skin = new Color(0.94f, 0.75f, 0.6f),
        hair = new Color(0.18f, 0.13f, 0.1f), apron = new Color(0.55f, 0.38f, 0.24f), shoes = new Color(0.3f, 0.2f, 0.14f), hat = new Color(0.86f, 0.74f, 0.45f),
        hatStyle = HatStyle.StrawHat, bun = true, apronOn = true, height = 0.94f,
    };

    // ------------------------------------------------------------------ Placement

    /// <summary>วาง NPC พ่อค้าที่ตำแหน่ง (หันหน้าตาม yaw) พร้อมป้ายชื่อเหนือหัว</summary>
    public static VendorNPC PlaceVendor(Look look, Transform parent, Vector3 position, float yaw, string displayName, string role,
        VendorShop shop, string[] greetings, string[] callouts, float range = 3.6f)
    {
        GameObject prefab = BuildPrefab(look);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = "NPC " + displayName;
        go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

        var vendor = go.GetComponent<VendorNPC>();
        vendor.EditorSetup("คุยกับ" + displayName, range);
        vendor.EditorSetupVendor(displayName, shop, greetings, callouts);
        EditorUtility.SetDirty(vendor);

        var label = new GameObject("Name Label");
        label.transform.SetParent(go.transform, false);
        label.transform.localPosition = Vector3.up * (2.12f * look.height);
        label.AddComponent<WorldLabel>().EditorSetup($"{displayName} · {role}", 0.3f, new Color(1f, 0.92f, 0.6f));
        return vendor;
    }

    // ------------------------------------------------------------------ Character

    public static GameObject BuildPrefab(Look look)
    {
        EnsureFolder(PrefabFolder);
        EnsureFolder(MaterialFolder);
        string m = look.id + "_";
        Material shirt = Mat(m + "Shirt", look.shirt);
        Material pants = Mat(m + "Pants", look.pants);
        Material skin = Mat(m + "Skin", look.skin);
        Material hair = Mat(m + "Hair", look.hair);
        Material apron = Mat(m + "Apron", look.apron);
        Material shoes = Mat(m + "Shoes", look.shoes);
        Material hat = Mat(m + "Hat", look.hat);
        Material dark = Mat("NPC_Eyes", new Color(0.1f, 0.08f, 0.07f));
        Material cheek = Mat(m + "Cheek", Color.Lerp(look.skin, new Color(0.95f, 0.45f, 0.45f), 0.35f));

        var root = new GameObject("NPC_" + look.id);
        var col = root.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0f, 0.92f * look.height, 0f);
        col.height = 1.85f * look.height;
        col.radius = 0.3f;
        root.AddComponent<VendorNPC>();

        Transform body = Empty("Body", root.transform, Vector3.zero);
        body.localScale = Vector3.one;
        Transform model = Empty("Model", body, Vector3.zero);
        model.localScale = Vector3.one * look.height;

        // ขา
        foreach (float x in new[] { -0.11f, 0.11f })
        {
            Part("Leg", model, PrimitiveType.Cube, new Vector3(x, 0.42f, 0f), new Vector3(0.18f, 0.8f, 0.22f), pants);
            Part("Shoe", model, PrimitiveType.Cube, new Vector3(x, 0.06f, 0.03f), new Vector3(0.2f, 0.13f, 0.3f), shoes);
        }

        // ลำตัว + ผ้ากันเปื้อน
        Part("Torso", model, PrimitiveType.Cube, new Vector3(0f, 1.15f, 0f), new Vector3(0.48f, 0.62f, 0.27f), shirt);
        Part("Hips", model, PrimitiveType.Cube, new Vector3(0f, 0.86f, 0f), new Vector3(0.44f, 0.12f, 0.25f), pants);
        if (look.apronOn)
        {
            Part("Apron", model, PrimitiveType.Cube, new Vector3(0f, 0.92f, 0.142f), new Vector3(0.42f, 0.7f, 0.02f), apron);
            Part("ApronBib", model, PrimitiveType.Cube, new Vector3(0f, 1.27f, 0.142f), new Vector3(0.28f, 0.22f, 0.02f), apron);
            Part("ApronPocket", model, PrimitiveType.Cube, new Vector3(0f, 0.85f, 0.155f), new Vector3(0.22f, 0.12f, 0.01f), Mat(m + "ApronDark", look.apron * 0.82f));
        }

        // แขน (pivot ที่หัวไหล่)
        foreach (float side in new[] { -1f, 1f })
        {
            Transform arm = Empty(side < 0f ? "Arm_L" : "Arm_R", model, new Vector3(side * 0.31f, 1.42f, 0f));
            Part("Sleeve", arm, PrimitiveType.Cube, new Vector3(0f, -0.2f, 0f), new Vector3(0.14f, 0.42f, 0.15f), shirt);
            Part("Forearm", arm, PrimitiveType.Cube, new Vector3(0f, -0.46f, 0f), new Vector3(0.12f, 0.14f, 0.13f), skin);
            Part("Hand", arm, PrimitiveType.Cube, new Vector3(0f, -0.58f, 0f), new Vector3(0.12f, 0.11f, 0.12f), skin);
        }

        // หัว (pivot ที่คอ)
        Part("Neck", model, PrimitiveType.Cylinder, new Vector3(0f, 1.5f, 0f), new Vector3(0.12f, 0.04f, 0.12f), skin);
        Transform head = Empty("Head Pivot", model, new Vector3(0f, 1.53f, 0f));
        Part("Head", head, PrimitiveType.Cube, new Vector3(0f, 0.17f, 0f), new Vector3(0.31f, 0.31f, 0.29f), skin);
        Part("Nose", head, PrimitiveType.Cube, new Vector3(0f, 0.16f, 0.155f), new Vector3(0.05f, 0.07f, 0.05f), skin);
        Part("Eye_L", head, PrimitiveType.Cube, new Vector3(-0.07f, 0.21f, 0.147f), new Vector3(0.045f, 0.05f, 0.02f), dark);
        Part("Eye_R", head, PrimitiveType.Cube, new Vector3(0.07f, 0.21f, 0.147f), new Vector3(0.045f, 0.05f, 0.02f), dark);
        Part("Cheek_L", head, PrimitiveType.Cube, new Vector3(-0.1f, 0.13f, 0.147f), new Vector3(0.06f, 0.035f, 0.015f), cheek);
        Part("Cheek_R", head, PrimitiveType.Cube, new Vector3(0.1f, 0.13f, 0.147f), new Vector3(0.06f, 0.035f, 0.015f), cheek);
        Part("Smile", head, PrimitiveType.Cube, new Vector3(0f, 0.085f, 0.147f), new Vector3(0.09f, 0.02f, 0.015f), dark);

        // ผม
        Part("HairTop", head, PrimitiveType.Cube, new Vector3(0f, 0.33f, -0.01f), new Vector3(0.33f, 0.06f, 0.31f), hair);
        Part("HairBack", head, PrimitiveType.Cube, new Vector3(0f, 0.2f, -0.15f), new Vector3(0.33f, 0.3f, 0.04f), hair);
        Part("HairSide_L", head, PrimitiveType.Cube, new Vector3(-0.16f, 0.24f, -0.03f), new Vector3(0.03f, 0.18f, 0.24f), hair);
        Part("HairSide_R", head, PrimitiveType.Cube, new Vector3(0.16f, 0.24f, -0.03f), new Vector3(0.03f, 0.18f, 0.24f), hair);
        if (look.mustache)
        {
            Part("Mustache", head, PrimitiveType.Cube, new Vector3(0f, 0.115f, 0.16f), new Vector3(0.17f, 0.04f, 0.03f), hair);
            Part("Brow_L", head, PrimitiveType.Cube, new Vector3(-0.07f, 0.255f, 0.15f), new Vector3(0.07f, 0.02f, 0.02f), hair);
            Part("Brow_R", head, PrimitiveType.Cube, new Vector3(0.07f, 0.255f, 0.15f), new Vector3(0.07f, 0.02f, 0.02f), hair);
        }
        if (look.bun)
        {
            Part("Bun", head, PrimitiveType.Sphere, new Vector3(0f, 0.3f, -0.17f), new Vector3(0.15f, 0.14f, 0.14f), hair);
            Part("Fringe", head, PrimitiveType.Cube, new Vector3(0f, 0.3f, 0.14f), new Vector3(0.32f, 0.05f, 0.03f), hair);
        }

        // หมวก
        switch (look.hatStyle)
        {
            case HatStyle.Cap:
                Part("CapCrown", head, PrimitiveType.Cube, new Vector3(0f, 0.37f, -0.01f), new Vector3(0.34f, 0.1f, 0.32f), hat);
                Part("CapBill", head, PrimitiveType.Cube, new Vector3(0f, 0.33f, 0.2f), new Vector3(0.3f, 0.025f, 0.14f), hat, Quaternion.Euler(-8f, 0f, 0f));
                break;
            case HatStyle.StrawHat:
                Part("HatBrim", head, PrimitiveType.Cylinder, new Vector3(0f, 0.37f, 0f), new Vector3(0.62f, 0.012f, 0.62f), hat);
                Part("HatMid", head, PrimitiveType.Cylinder, new Vector3(0f, 0.41f, 0f), new Vector3(0.38f, 0.035f, 0.38f), hat);
                Part("HatTop", head, PrimitiveType.Cylinder, new Vector3(0f, 0.46f, 0f), new Vector3(0.18f, 0.03f, 0.18f), hat);
                Part("HatBand", head, PrimitiveType.Cylinder, new Vector3(0f, 0.385f, 0f), new Vector3(0.39f, 0.012f, 0.39f), Mat(m + "HatBand", new Color(0.7f, 0.25f, 0.22f)));
                break;
            case HatStyle.Headscarf:
                Part("Scarf", head, PrimitiveType.Cube, new Vector3(0f, 0.34f, -0.02f), new Vector3(0.35f, 0.1f, 0.34f), hat);
                break;
        }

        string path = $"{PrefabFolder}/NPC_{look.id}.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // ------------------------------------------------------------------ Helpers

    private static Transform Empty(string name, Transform parent, Vector3 localPos)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = localPos;
        return t;
    }

    private static void Part(string name, Transform parent, PrimitiveType type, Vector3 localPos, Vector3 scale, Material mat, Quaternion? rot = null)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = rot ?? Quaternion.identity;
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    private static Material Mat(string name, Color color)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            AssetDatabase.CreateAsset(mat, path);
        }
        color.a = 1f;
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", 0.12f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
