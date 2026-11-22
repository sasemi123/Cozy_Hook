using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// สร้างตัวละคร "คนตกปลา" สไตล์ low-poly จาก primitive แล้วบันทึกเป็น prefab
/// และวางลงในฉากที่เปิดอยู่ พร้อมกล้องบุคคลที่สาม
/// เมนู: Tools > Cozy Hook > Add Fisherman Player
/// </summary>
public static class FishermanBuilder
{
    private const string PrefabPath = "Assets/Prefabs/Player/Fisherman.prefab";
    private const string MaterialFolder = "Assets/Characters/Fisherman/Materials";

    [MenuItem("Tools/Cozy Hook/Add Fisherman Player")]
    public static void AddToOpenScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.isDirty)
        {
            Debug.LogError("[Fisherman] ฉากปัจจุบันยังไม่ได้บันทึก กรุณาบันทึกก่อนแล้วรันใหม่");
            return;
        }

        BuildPrefab();
        PlaceInScene();
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Fisherman] เพิ่มผู้เล่นในฉาก " + scene.path);
    }

    // ------------------------------------------------------------------ Prefab

    public static GameObject BuildPrefab()
    {
        EnsureFolder("Assets/Prefabs/Player");
        EnsureFolder(MaterialFolder);

        Material shirt = Mat("Shirt", new Color(0.68f, 0.18f, 0.15f));
        Material vest = Mat("Vest", new Color(0.36f, 0.4f, 0.22f));
        Material pocket = Mat("VestPocket", new Color(0.29f, 0.33f, 0.18f));
        Material pants = Mat("Pants", new Color(0.4f, 0.33f, 0.24f));
        Material boots = Mat("Boots", new Color(0.2f, 0.14f, 0.1f));
        Material skin = Mat("Skin", new Color(0.93f, 0.72f, 0.56f));
        Material beard = Mat("Beard", new Color(0.42f, 0.27f, 0.16f));
        Material hat = Mat("Hat", new Color(0.72f, 0.64f, 0.45f));
        Material band = Mat("HatBand", new Color(0.3f, 0.22f, 0.14f));
        Material dark = Mat("Eyes", new Color(0.1f, 0.08f, 0.07f));
        Material wicker = Mat("Wicker", new Color(0.66f, 0.5f, 0.28f));
        Material belt = Mat("Belt", new Color(0.18f, 0.12f, 0.08f));

        var root = new GameObject("Fisherman");
        var visual = Empty("Visual", root.transform, Vector3.zero);

        // ขา (pivot อยู่ที่สะโพก ให้แกว่งได้)
        Transform legL = Leg("Leg_L", visual, -0.11f, pants, boots);
        Transform legR = Leg("Leg_R", visual, 0.11f, pants, boots);

        // ลำตัวส่วนบน (ขยับขึ้นลงตอนเดิน/หายใจ)
        Transform body = Empty("Body", visual, Vector3.zero);
        Part("Torso", body, PrimitiveType.Cube, new Vector3(0f, 1.2f, 0f), new Vector3(0.46f, 0.56f, 0.26f), shirt);
        Part("Belt", body, PrimitiveType.Cube, new Vector3(0f, 0.93f, 0f), new Vector3(0.48f, 0.07f, 0.28f), belt);
        Part("Vest_L", body, PrimitiveType.Cube, new Vector3(-0.13f, 1.17f, 0f), new Vector3(0.22f, 0.46f, 0.3f), vest);
        Part("Vest_R", body, PrimitiveType.Cube, new Vector3(0.13f, 1.17f, 0f), new Vector3(0.22f, 0.46f, 0.3f), vest);
        Part("Pocket_L", body, PrimitiveType.Cube, new Vector3(-0.13f, 1.08f, 0.155f), new Vector3(0.13f, 0.11f, 0.02f), pocket);
        Part("Pocket_R", body, PrimitiveType.Cube, new Vector3(0.13f, 1.08f, 0.155f), new Vector3(0.13f, 0.11f, 0.02f), pocket);
        Part("Creel", body, PrimitiveType.Cube, new Vector3(-0.12f, 1.0f, -0.2f), new Vector3(0.26f, 0.2f, 0.13f), wicker);
        Part("CreelStrap", body, PrimitiveType.Cube, new Vector3(0f, 1.25f, 0f), new Vector3(0.05f, 0.62f, 0.32f), belt, Quaternion.Euler(0f, 0f, 38f));

        // หัว
        Part("Neck", body, PrimitiveType.Cylinder, new Vector3(0f, 1.51f, 0f), new Vector3(0.12f, 0.04f, 0.12f), skin);
        Part("Head", body, PrimitiveType.Cube, new Vector3(0f, 1.7f, 0f), new Vector3(0.3f, 0.3f, 0.28f), skin);
        Part("Beard", body, PrimitiveType.Cube, new Vector3(0f, 1.6f, 0.13f), new Vector3(0.31f, 0.14f, 0.06f), beard);
        Part("Mustache", body, PrimitiveType.Cube, new Vector3(0f, 1.665f, 0.15f), new Vector3(0.16f, 0.035f, 0.03f), beard);
        Part("Nose", body, PrimitiveType.Cube, new Vector3(0f, 1.705f, 0.155f), new Vector3(0.05f, 0.07f, 0.05f), skin);
        Part("Eye_L", body, PrimitiveType.Cube, new Vector3(-0.07f, 1.75f, 0.142f), new Vector3(0.045f, 0.045f, 0.02f), dark);
        Part("Eye_R", body, PrimitiveType.Cube, new Vector3(0.07f, 1.75f, 0.142f), new Vector3(0.045f, 0.045f, 0.02f), dark);

        // หมวกบักเก็ต
        Part("HatBrim", body, PrimitiveType.Cylinder, new Vector3(0f, 1.86f, 0f), new Vector3(0.46f, 0.012f, 0.46f), hat);
        Part("HatBand", body, PrimitiveType.Cylinder, new Vector3(0f, 1.885f, 0f), new Vector3(0.33f, 0.02f, 0.33f), band);
        Part("HatCrown", body, PrimitiveType.Cylinder, new Vector3(0f, 1.93f, 0f), new Vector3(0.32f, 0.07f, 0.32f), hat);

        // แขน (pivot อยู่ที่ไหล่)
        Transform armL = Arm("Arm_L", body, -0.3f, shirt, skin);
        Transform armR = Arm("Arm_R", body, 0.3f, shirt, skin);
        Transform holdPoint = Empty("RodHoldPoint", armR, new Vector3(0f, -0.56f, 0.07f));
        GameObject rod = FishingGearBuilder.AttachRod(holdPoint, root.transform);

        // จุดเก็บคันเบ็ดที่หลัง: เฉียงจากสะโพกขวาขึ้นไปทางไหล่ซ้าย
        Transform backSocket = Empty("RodBackSocket", body, new Vector3(0.13f, 0.98f, -0.3f));
        backSocket.localRotation = Quaternion.Euler(-6f, 0f, 20f);

        // ส่วนควบคุม
        var cc = root.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.28f;
        cc.center = new Vector3(0f, 0.92f, 0f);
        cc.stepOffset = 0.35f;
        cc.skinWidth = 0.04f;

        var movement = root.AddComponent<PlayerMovement>();
        var anim = root.AddComponent<FishermanAnimator>();
        var so = new SerializedObject(anim);
        so.FindProperty("movement").objectReferenceValue = movement;
        so.FindProperty("body").objectReferenceValue = body;
        so.FindProperty("leftLeg").objectReferenceValue = legL;
        so.FindProperty("rightLeg").objectReferenceValue = legR;
        so.FindProperty("leftArm").objectReferenceValue = armL;
        so.FindProperty("rightArm").objectReferenceValue = armR;
        so.FindProperty("rightArmHoldPitch").floatValue = -35f; // ยกแขนขวาถือคันเบ็ดไปข้างหน้า
        so.FindProperty("rightArmSwingScale").floatValue = 0.25f;
        so.ApplyModifiedPropertiesWithoutUndo();

        root.AddComponent<FishInventory>();    // กระเป๋าเก็บปลา (ข้อมูลอยู่ใน GameState)
        root.AddComponent<PlayerInteractor>(); // กด E โต้ตอบกับป้าย/ร้านค้า/ทางไปฉากอื่น
        root.AddComponent<PlayerSpawner>();    // เกิดที่ SpawnPoint เมื่อมาจากฉากอื่น

        var holster = root.AddComponent<RodHolster>();
        var hso = new SerializedObject(holster);
        hso.FindProperty("rod").objectReferenceValue = rod.transform;
        hso.FindProperty("handSocket").objectReferenceValue = holdPoint;
        hso.FindProperty("backSocket").objectReferenceValue = backSocket;
        hso.FindProperty("fishermanAnimator").objectReferenceValue = anim;
        hso.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static Transform Leg(string name, Transform parent, float x, Material pants, Material boots)
    {
        Transform pivot = Empty(name, parent, new Vector3(x, 0.88f, 0f));
        Part("Pants", pivot, PrimitiveType.Cube, new Vector3(0f, -0.4f, 0f), new Vector3(0.18f, 0.8f, 0.22f), pants);
        Part("Boot", pivot, PrimitiveType.Cube, new Vector3(0f, -0.81f, 0.03f), new Vector3(0.2f, 0.14f, 0.3f), boots);
        return pivot;
    }

    private static Transform Arm(string name, Transform parent, float x, Material sleeve, Material skin)
    {
        Transform pivot = Empty(name, parent, new Vector3(x, 1.44f, 0f));
        Part("Sleeve", pivot, PrimitiveType.Cube, new Vector3(0f, -0.24f, 0f), new Vector3(0.14f, 0.5f, 0.15f), sleeve);
        Part("Hand", pivot, PrimitiveType.Cube, new Vector3(0f, -0.54f, 0f), new Vector3(0.12f, 0.12f, 0.12f), skin);
        return pivot;
    }

    // ------------------------------------------------------------------ Scene placement

    /// <summary>วางผู้เล่นและกล้องบุคคลที่สามในฉากที่เปิดอยู่ (แทนที่ของเดิมถ้ามี)</summary>
    public static GameObject PlaceInScene(Vector3? spawnOverride = null, float spawnYaw = 0f)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) prefab = BuildPrefab();

        foreach (var old in Object.FindObjectsByType<PlayerMovement>(FindObjectsInactive.Include))
            Object.DestroyImmediate(old.gameObject);
        foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
            if (cam.CompareTag("MainCamera")) Object.DestroyImmediate(cam.gameObject);

        // ไม่ระบุจุดเกิด = ยืนที่ปลายท่าเรือ (ฉากทะเลสาบ)
        Vector3 spawn = new Vector3(70f, 4.7f, 59f);
        Bounds? deck = EnsureDockWalkway();
        if (deck.HasValue) spawn = new Vector3(deck.Value.center.x, deck.Value.max.y, deck.Value.max.z - 1.4f);
        if (spawnOverride.HasValue) spawn = spawnOverride.Value;

        var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        player.transform.SetPositionAndRotation(spawn + Vector3.up * 0.05f, Quaternion.Euler(0f, spawnYaw, 0f));

        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        var camera = camGo.AddComponent<Camera>();
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 400f;
        camera.fieldOfView = 55f;
        camGo.AddComponent<AudioListener>();
        camGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;

        Quaternion camRot = Quaternion.Euler(14f, spawnYaw, 0f);
        camGo.transform.SetPositionAndRotation(spawn + Vector3.up * 1.55f + camRot * Vector3.back * 4.5f, camRot);
        var follow = camGo.AddComponent<ThirdPersonCamera>();
        follow.Target = player.transform;
        EditorUtility.SetDirty(follow);

        // UI แรงตึงสายเบ็ด (สร้างหน้าจอเองตอนเริ่มเกม)
        foreach (var oldUi in Object.FindObjectsByType<FishingTensionUI>(FindObjectsInactive.Include))
            Object.DestroyImmediate(oldUi.gameObject);
        var tension = player.GetComponentInChildren<FishingLineTension>(true);
        var ui = new GameObject("Fishing UI").AddComponent<FishingTensionUI>();
        var uso = new SerializedObject(ui);
        uso.FindProperty("tension").objectReferenceValue = tension;
        uso.FindProperty("showCaughtMessage").boolValue = false; // มีหน้าต่างผลการจับปลาแทน
        uso.ApplyModifiedPropertiesWithoutUndo();

        PlaceFishSystems(player, tension);
        PlaceGameUI();

        var movement = player.GetComponent<PlayerMovement>();
        var so = new SerializedObject(movement);
        so.FindProperty("cameraTransform").objectReferenceValue = camGo.transform;
        so.ApplyModifiedPropertiesWithoutUndo();

        return player;
    }

    /// <summary>HUD (วัน เวลา เหรียญ [E]) + หน้าต่างป้ายประกาศ + หน้าต่างตลาด</summary>
    private static void PlaceGameUI()
    {
        foreach (var old in Object.FindObjectsByType<GameHUD>(FindObjectsInactive.Include))
            Object.DestroyImmediate(old.gameObject);

        var ui = new GameObject("Game UI");
        ui.AddComponent<GameHUD>();
        ui.AddComponent<NoticeBoardUI>();
        ui.AddComponent<MarketUI>();
        ui.AddComponent<BaitShopUI>();
    }

    /// <summary>ระบบสุ่มปลา + หน้าต่างผลการจับ + หน้าต่างกระเป๋า</summary>
    private static void PlaceFishSystems(GameObject player, FishingLineTension tension)
    {
        foreach (var old in Object.FindObjectsByType<FishCatchManager>(FindObjectsInactive.Include))
            Object.DestroyImmediate(old.gameObject);

        var systems = new GameObject("Fish Systems");
        var inventory = player.GetComponent<FishInventory>();

        var manager = systems.AddComponent<FishCatchManager>();
        var mso = new SerializedObject(manager);
        List<FishData> fish = FishDataBuilder.AllFish();
        SerializedProperty pool = mso.FindProperty("fishPool");
        pool.arraySize = fish.Count;
        for (int i = 0; i < fish.Count; i++) pool.GetArrayElementAtIndex(i).objectReferenceValue = fish[i];
        mso.FindProperty("lineTension").objectReferenceValue = tension;
        mso.FindProperty("inventory").objectReferenceValue = inventory;
        mso.ApplyModifiedPropertiesWithoutUndo();

        var result = systems.AddComponent<CatchResultUI>();
        var rso = new SerializedObject(result);
        rso.FindProperty("catchManager").objectReferenceValue = manager;
        rso.ApplyModifiedPropertiesWithoutUndo();

        var bag = systems.AddComponent<FishInventoryUI>();
        var bso = new SerializedObject(bag);
        bso.FindProperty("inventory").objectReferenceValue = inventory;
        bso.FindProperty("lineTension").objectReferenceValue = tension;
        bso.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>ใส่ collider ให้พื้นท่าเรือ (โมเดล Kenney ไม่มี collider) เพื่อให้เดินบนท่าได้</summary>
    private static Bounds? EnsureDockWalkway()
    {
        GameObject dock = GameObject.Find("Dock");
        if (dock == null) return null;

        bool found = false;
        Bounds b = default;
        foreach (Transform child in dock.transform)
        {
            if (!child.name.StartsWith("platform-planks")) continue;
            foreach (Renderer r in child.GetComponentsInChildren<Renderer>())
            {
                if (!found) { b = r.bounds; found = true; }
                else b.Encapsulate(r.bounds);
            }
        }
        if (!found) return null;

        Transform walkway = dock.transform.Find("Dock Walkway");
        if (walkway == null)
        {
            walkway = new GameObject("Dock Walkway").transform;
            walkway.SetParent(dock.transform, false);
        }
        walkway.position = new Vector3(b.center.x, b.max.y - 0.1f, b.center.z);
        walkway.rotation = Quaternion.identity;
        walkway.localScale = Vector3.one;
        var box = walkway.GetComponent<BoxCollider>();
        if (box == null) box = walkway.gameObject.AddComponent<BoxCollider>();
        box.center = Vector3.zero;
        box.size = new Vector3(b.size.x, 0.2f, b.size.z);
        return b;
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
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", 0.12f);
        EditorUtility.SetDirty(m);
        return m;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
