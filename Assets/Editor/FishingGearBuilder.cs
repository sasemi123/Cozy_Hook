using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// สร้างคันเบ็ด ทุ่น และเอฟเฟกต์น้ำกระจาย แล้วติดคันเบ็ดให้ตัวละครคนตกปลา
/// เมนู: Tools > Cozy Hook > Add Fishing Gear
/// </summary>
public static class FishingGearBuilder
{
    private const string PrefabFolder = "Assets/Prefabs/Fishing";
    private const string MaterialFolder = "Assets/Prefabs/Fishing/Materials";
    private const string BobberPath = PrefabFolder + "/Bobber.prefab";
    private const string SplashPath = PrefabFolder + "/WaterSplash.prefab";
    private const string WaterTag = "Water";

    [MenuItem("Tools/Cozy Hook/Add Fishing Gear")]
    public static void AddToOpenScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.isDirty)
        {
            Debug.LogError("[FishingGear] ฉากปัจจุบันยังไม่ได้บันทึก กรุณาบันทึกก่อนแล้วรันใหม่");
            return;
        }

        BuildBobberPrefab();
        FishermanBuilder.BuildPrefab(); // สร้างตัวละครใหม่พร้อมคันเบ็ดในมือ
        FishermanBuilder.PlaceInScene();
        SetupWater();
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[FishingGear] ติดคันเบ็ดและทุ่นเรียบร้อย");
    }

    // ------------------------------------------------------------------ Water

    /// <summary>ใส่ Tag "Water" และ trigger collider ให้ผิวน้ำ เพื่อให้ทุ่นตรวจจับได้</summary>
    public static void SetupWater()
    {
        EnsureTag(WaterTag);
        GameObject water = GameObject.Find("Lake Water");
        if (water == null)
        {
            Debug.LogWarning("[FishingGear] ไม่พบ GameObject ชื่อ 'Lake Water' ในฉาก");
            return;
        }

        water.tag = WaterTag;
        var box = water.GetComponent<BoxCollider>();
        if (box == null) box = water.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = new Vector3(0f, -0.5f, 0f); // ขอบบนอยู่ที่ผิวน้ำพอดี
        box.size = new Vector3(10f, 1f, 10f);    // Plane ขนาด 10x10 หน่วยก่อน scale
    }

    private static void EnsureTag(string tag)
    {
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty tags = tagManager.FindProperty("tags");
        for (int i = 0; i < tags.arraySize; i++)
            if (tags.GetArrayElementAtIndex(i).stringValue == tag) return;

        tags.InsertArrayElementAtIndex(tags.arraySize);
        tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
        tagManager.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------------ Rod

    /// <summary>สร้างคันเบ็ดใต้ holdPoint (มือขวา) พร้อมสคริปต์เหวี่ยงเบ็ดและสายเบ็ด</summary>
    public static GameObject AttachRod(Transform holdPoint, Transform aimReference)
    {
        Material cork = Mat("Cork", new Color(0.72f, 0.55f, 0.36f), 0.15f);
        Material blank = Mat("RodBlank", new Color(0.12f, 0.22f, 0.17f), 0.75f);
        Material metal = Mat("Metal", new Color(0.5f, 0.52f, 0.55f), 0.7f, 0.8f);
        Material dark = Mat("RodDark", new Color(0.08f, 0.08f, 0.09f), 0.4f);
        Material lineMat = UnlitMat("FishingLine", new Color(0.93f, 0.93f, 0.88f));

        var rod = new GameObject("FishingRod");
        rod.transform.SetParent(holdPoint, false);

        Transform pivot = new GameObject("RodPivot").transform;
        pivot.SetParent(rod.transform, false);
        pivot.localRotation = Quaternion.Euler(75f, 0f, 0f);

        // ด้ามจับ + รอก (รอกอยู่ด้าน +Z ซึ่งหันลงพื้นเมื่อคันเอียง)
        Part("ButtCap", pivot, PrimitiveType.Cylinder, new Vector3(0f, -0.26f, 0f), new Vector3(0.046f, 0.015f, 0.046f), dark);
        Part("Handle", pivot, PrimitiveType.Cylinder, new Vector3(0f, -0.065f, 0f), new Vector3(0.04f, 0.185f, 0.04f), cork);
        Part("ReelSeat", pivot, PrimitiveType.Cylinder, new Vector3(0f, 0.15f, 0f), new Vector3(0.034f, 0.04f, 0.034f), dark);
        Part("ReelFoot", pivot, PrimitiveType.Cube, new Vector3(0f, 0.15f, 0.035f), new Vector3(0.015f, 0.05f, 0.04f), dark);
        Part("Reel", pivot, PrimitiveType.Cylinder, new Vector3(0f, 0.15f, 0.075f), new Vector3(0.1f, 0.022f, 0.1f), metal, Quaternion.Euler(0f, 0f, 90f));
        Part("ReelHandle", pivot, PrimitiveType.Cube, new Vector3(0.035f, 0.15f, 0.09f), new Vector3(0.012f, 0.012f, 0.05f), dark);

        // ตัวคันเรียวลงไปที่ปลาย
        Part("Blank_1", pivot, PrimitiveType.Cylinder, new Vector3(0f, 0.6f, 0f), new Vector3(0.026f, 0.43f, 0.026f), blank);
        Part("Blank_2", pivot, PrimitiveType.Cylinder, new Vector3(0f, 1.37f, 0f), new Vector3(0.018f, 0.34f, 0.018f), blank);
        Part("Blank_3", pivot, PrimitiveType.Cylinder, new Vector3(0f, 1.97f, 0f), new Vector3(0.011f, 0.26f, 0.011f), blank);
        foreach (float y in new[] { 0.55f, 1.05f, 1.5f, 1.9f, 2.2f })
            Part("Guide", pivot, PrimitiveType.Cube, new Vector3(0f, y, 0.016f), new Vector3(0.018f, 0.018f, 0.018f), metal);

        Transform tip = new GameObject("RodTip").transform;
        tip.SetParent(pivot, false);
        tip.localPosition = new Vector3(0f, 2.24f, 0f);

        // สายเบ็ด
        var line = rod.AddComponent<LineRenderer>();
        line.sharedMaterial = lineMat;
        line.widthMultiplier = 1f;
        line.numCapVertices = 2;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.useWorldSpace = true;
        line.positionCount = 0;

        // สคริปต์เหวี่ยงเบ็ด (เขียนไว้ก่อนหน้า)
        var caster = rod.AddComponent<FishingRodCaster>();
        var bobber = AssetDatabase.LoadAssetAtPath<GameObject>(BobberPath);
        if (bobber == null) bobber = BuildBobberPrefab();
        var so = new SerializedObject(caster);
        so.FindProperty("rodTip").objectReferenceValue = tip;
        so.FindProperty("bobberPrefab").objectReferenceValue = bobber.GetComponent<Rigidbody>();
        so.FindProperty("aimReference").objectReferenceValue = aimReference;
        so.FindProperty("lineWidth").floatValue = 0.012f;
        so.ApplyModifiedPropertiesWithoutUndo();

        // ระบบแรงตึงสายตอนปลากินเบ็ด
        var tension = rod.AddComponent<FishingLineTension>();
        var tso = new SerializedObject(tension);
        tso.FindProperty("player").objectReferenceValue = aimReference;
        tso.FindProperty("splashPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ParticleSystem>(SplashPath);
        tso.ApplyModifiedPropertiesWithoutUndo();

        var anim = rod.AddComponent<RodCastAnimator>();
        var aso = new SerializedObject(anim);
        aso.FindProperty("caster").objectReferenceValue = caster;
        aso.FindProperty("rodPivot").objectReferenceValue = pivot;
        aso.FindProperty("restPitch").floatValue = 75f;
        aso.ApplyModifiedPropertiesWithoutUndo();

        return rod;
    }

    // ------------------------------------------------------------------ Bobber

    public static GameObject BuildBobberPrefab()
    {
        EnsureFolder(PrefabFolder);
        EnsureFolder(MaterialFolder);

        Material red = Mat("BobberRed", new Color(0.86f, 0.12f, 0.1f), 0.55f);
        Material white = Mat("BobberWhite", new Color(0.96f, 0.95f, 0.92f), 0.55f);
        Material dark = Mat("RodDark", new Color(0.08f, 0.08f, 0.09f), 0.4f);

        var root = new GameObject("Bobber");
        Transform visual = new GameObject("Visual").transform;
        visual.SetParent(root.transform, false);
        Part("Bottom", visual, PrimitiveType.Sphere, new Vector3(0f, -0.014f, 0f), new Vector3(0.24f, 0.24f, 0.24f), red);
        Part("Top", visual, PrimitiveType.Sphere, new Vector3(0f, 0.018f, 0f), new Vector3(0.23f, 0.23f, 0.23f), white);
        Part("Band", visual, PrimitiveType.Cylinder, new Vector3(0f, 0f, 0f), new Vector3(0.245f, 0.007f, 0.245f), dark);
        Part("Antenna", visual, PrimitiveType.Cylinder, new Vector3(0f, 0.18f, 0f), new Vector3(0.022f, 0.08f, 0.022f), white);
        Part("AntennaTip", visual, PrimitiveType.Sphere, new Vector3(0f, 0.27f, 0f), new Vector3(0.045f, 0.045f, 0.045f), red);
        Part("Stem", visual, PrimitiveType.Cylinder, new Vector3(0f, -0.16f, 0f), new Vector3(0.018f, 0.05f, 0.018f), dark);

        var col = root.AddComponent<SphereCollider>();
        col.radius = 0.12f;

        // แรงเหวี่ยงใน FishingRodCaster ปรับไว้สำหรับมวล 1
        var rb = root.AddComponent<Rigidbody>();
        rb.mass = 1f;
        rb.linearDamping = 0.1f;
        rb.angularDamping = 0.5f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        var audio = root.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0.6f;
        audio.minDistance = 8f;
        audio.maxDistance = 120f;

        var bobber = root.AddComponent<FishingBobber>();
        var so = new SerializedObject(bobber);
        so.FindProperty("splashPrefab").objectReferenceValue = BuildSplashPrefab().GetComponent<ParticleSystem>();
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, BobberPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // ------------------------------------------------------------------ Splash VFX

    public static GameObject BuildSplashPrefab()
    {
        EnsureFolder(PrefabFolder);
        Material particleMat = ParticleMaterial();

        var root = new GameObject("WaterSplash");
        var droplets = root.AddComponent<ParticleSystem>();
        droplets.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = droplets.main;
        main.duration = 1f;
        main.loop = false;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
        main.startColor = new Color(0.86f, 0.95f, 1f, 0.9f);
        main.gravityModifier = 1.2f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 80;

        var emission = droplets.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 30) });

        var shape = droplets.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 28f;
        shape.radius = 0.07f;
        shape.rotation = new Vector3(-90f, 0f, 0f); // พุ่งขึ้นด้านบน

        var color = droplets.colorOverLifetime;
        color.enabled = true;
        color.color = Fade(new Color(0.9f, 0.97f, 1f));

        var size = droplets.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.4f));

        root.GetComponent<ParticleSystemRenderer>().sharedMaterial = particleMat;

        // วงน้ำกระเพื่อมแผ่ออกบนผิวน้ำ
        var rippleGo = new GameObject("Ripple");
        rippleGo.transform.SetParent(root.transform, false);
        rippleGo.transform.localPosition = new Vector3(0f, 0.01f, 0f);
        var ripple = rippleGo.AddComponent<ParticleSystem>();
        ripple.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var rmain = ripple.main;
        rmain.duration = 1f;
        rmain.loop = false;
        rmain.startLifetime = 1.1f;
        rmain.startSpeed = 0f;
        rmain.startSize = 0.3f;
        rmain.startColor = new Color(0.85f, 0.95f, 1f, 0.55f);
        rmain.simulationSpace = ParticleSystemSimulationSpace.World;
        rmain.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var remission = ripple.emission;
        remission.rateOverTime = 0f;
        remission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1), new ParticleSystem.Burst(0.18f, 1) });
        var rshape = ripple.shape;
        rshape.enabled = false;
        var rsize = ripple.sizeOverLifetime;
        rsize.enabled = true;
        rsize.size = new ParticleSystem.MinMaxCurve(5f, AnimationCurve.EaseInOut(0f, 0.2f, 1f, 1f));
        var rcolor = ripple.colorOverLifetime;
        rcolor.enabled = true;
        rcolor.color = Fade(Color.white);
        var rrenderer = rippleGo.GetComponent<ParticleSystemRenderer>();
        rrenderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        rrenderer.sharedMaterial = particleMat;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, SplashPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static ParticleSystem.MinMaxGradient Fade(Color c)
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });
        return new ParticleSystem.MinMaxGradient(g);
    }

    private static Material ParticleMaterial()
    {
        var urp = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat");
        if (urp != null) return urp;
        return UnlitMat("SplashParticle", Color.white);
    }

    // ------------------------------------------------------------------ Helpers

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

    private static Material Mat(string name, Color color, float smoothness, float metallic = 0f)
    {
        EnsureFolder(MaterialFolder);
        string path = MaterialFolder + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Metallic", metallic);
        EditorUtility.SetDirty(m);
        return m;
    }

    private static Material UnlitMat(string name, Color color)
    {
        EnsureFolder(MaterialFolder);
        string path = MaterialFolder + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = name };
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", color);
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
