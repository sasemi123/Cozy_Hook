using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// เรนเดอร์โมเดลปลาเป็นภาพสำหรับ UI:
/// - ไอคอนนิ่ง (เก็บแคชต่อชนิดปลา) ใช้ในช่องกระเป๋า
/// - ภาพสด (หมุนช้าๆ) ใช้ในหน้าต่างผลการจับปลา
/// ทำงานบน "เวที" ที่อยู่ใต้ฉากไกลๆ (y = -1000) ซึ่งกล้องหลักมองไม่เห็น
/// </summary>
public class FishPreviewRenderer : MonoBehaviour
{
    private static FishPreviewRenderer instance;

    public static FishPreviewRenderer Instance
    {
        get
        {
            if (instance == null) instance = new GameObject("Fish Preview Renderer").AddComponent<FishPreviewRenderer>();
            return instance;
        }
    }

    private static readonly Vector3 LiveStage = new Vector3(0f, -1000f, 0f);
    private static readonly Vector3 IconStage = new Vector3(60f, -1000f, 0f);
    private static readonly Color Background = new Color(0.11f, 0.15f, 0.16f, 0f);

    private readonly Dictionary<FishData, Texture2D> icons = new Dictionary<FishData, Texture2D>();
    private Camera liveCamera;
    private Camera iconCamera;
    private Transform livePivot;
    private RenderTexture liveTexture;
    private GameObject liveModel;

    private void Awake()
    {
        instance = this;
        liveCamera = CreateStage("Live", LiveStage, out livePivot);
        iconCamera = CreateStage("Icon", IconStage, out _);
        liveTexture = new RenderTexture(512, 384, 24, RenderTextureFormat.ARGB32) { name = "FishLivePreview" };
        liveCamera.targetTexture = liveTexture;
        liveCamera.enabled = false;
    }

    private void OnDestroy()
    {
        if (liveTexture != null) liveTexture.Release();
        foreach (Texture2D icon in icons.Values) Destroy(icon);
    }

    private void Update()
    {
        if (liveModel != null) livePivot.Rotate(0f, 35f * Time.unscaledDeltaTime, 0f, Space.World);
    }

    /// <summary>เริ่มแสดงภาพสดของปลา คืนค่า RenderTexture ไว้ใส่ RawImage</summary>
    public RenderTexture StartLive(FishData data)
    {
        StopLive();
        if (data == null || data.Model == null) return liveTexture;

        livePivot.rotation = Quaternion.Euler(0f, -30f, 0f);
        liveModel = SpawnFitted(data, livePivot);
        liveCamera.enabled = true;
        return liveTexture;
    }

    public void StopLive()
    {
        if (liveModel != null) Destroy(liveModel);
        liveModel = null;
        if (liveCamera != null) liveCamera.enabled = false;
    }

    /// <summary>สร้างไอคอนถ้ายังไม่มี คืนค่า true ถ้าเพิ่งเรนเดอร์ใหม่</summary>
    public bool EnsureIcon(FishData data)
    {
        if (data == null || (icons.TryGetValue(data, out Texture2D cached) && cached != null)) return false;
        GetIcon(data);
        return true;
    }

    /// <summary>ไอคอนนิ่งของปลา (สร้างครั้งแรกแล้วเก็บแคช)</summary>
    public Texture2D GetIcon(FishData data)
    {
        if (data == null) return null;
        if (icons.TryGetValue(data, out Texture2D cached) && cached != null) return cached;

        const int width = 256, height = 192;
        var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
        Transform pivot = new GameObject("IconPivot").transform;
        pivot.position = IconStage;
        pivot.rotation = Quaternion.Euler(0f, -25f, 0f);
        GameObject model = SpawnFitted(data, pivot);

        iconCamera.targetTexture = rt;
        var request = new RenderPipeline.StandardRequest { destination = rt };
        if (RenderPipeline.SupportsRenderRequest(iconCamera, request)) RenderPipeline.SubmitRenderRequest(iconCamera, request);
        else iconCamera.Render();

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        var icon = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "Icon_" + data.name };
        icon.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        icon.Apply();
        RenderTexture.active = previous;

        iconCamera.targetTexture = null;
        RenderTexture.ReleaseTemporary(rt);
        if (model != null) DestroyImmediate(model);
        DestroyImmediate(pivot.gameObject);

        icons[data] = icon;
        return icon;
    }

    private Camera CreateStage(string name, Vector3 stage, out Transform pivot)
    {
        pivot = new GameObject(name + "Pivot").transform;
        pivot.SetParent(transform, false);
        pivot.position = stage;

        var camGo = new GameObject(name + "PreviewCamera");
        camGo.transform.SetParent(transform, false);
        camGo.transform.position = stage + new Vector3(0f, 0.35f, 2.6f);
        camGo.transform.LookAt(stage);
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Background;
        cam.fieldOfView = 30f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 20f;
        cam.enabled = false;

        // ไฟส่องปลาจากด้านกล้อง ไม่ขึ้นกับทิศแดดในฉาก
        var key = new GameObject(name + "Light").AddComponent<Light>();
        key.transform.SetParent(transform, false);
        key.transform.position = stage + new Vector3(-1.2f, 1.5f, 2.2f);
        key.type = LightType.Point;
        key.range = 8f;
        key.intensity = 6f;
        key.color = new Color(1f, 0.96f, 0.9f);
        return cam;
    }

    /// <summary>สร้างโมเดลปลา หันด้านข้างเข้ากล้อง และย่อ/ขยายให้พอดีกรอบ</summary>
    private static GameObject SpawnFitted(FishData data, Transform pivot)
    {
        GameObject model = Instantiate(data.Model, pivot);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one;

        foreach (var animator in model.GetComponentsInChildren<Animator>()) animator.enabled = false;
        foreach (var r in model.GetComponentsInChildren<Renderer>())
        {
            r.shadowCastingMode = ShadowCastingMode.Off;
            if (r is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen = true;
        }

        Bounds b = LocalBounds(model, pivot);
        if (b.size.z > b.size.x) model.transform.localRotation = Quaternion.Euler(0f, 90f, 0f); // ให้ความยาวปลาอยู่แนวนอน
        b = LocalBounds(model, pivot);

        // ปลาตัวแบน (เช่นกระเบน) มองด้านข้างจะเห็นเป็นเส้นบาง จึงเอียงให้เห็นหลัง
        if (b.size.y < 0.25f * Mathf.Max(b.size.x, b.size.z))
        {
            model.transform.localRotation = Quaternion.Euler(60f, 0f, 0f) * model.transform.localRotation;
            b = LocalBounds(model, pivot);
        }

        float scale = 1.6f / Mathf.Max(0.0001f, Mathf.Max(b.size.x, b.size.y * 1.6f, b.size.z));
        model.transform.localScale = Vector3.one * scale;
        b = LocalBounds(model, pivot);
        model.transform.localPosition -= b.center;
        return model;
    }

    /// <summary>
    /// ขอบเขตจริงของเมช: SkinnedMeshRenderer มักมี bounds หลวมเกินจริง (เผื่อแอนิเมชัน)
    /// จึงคำนวณจากเมชต้นฉบับแทน
    /// </summary>
    private static Bounds RendererBounds(Renderer r)
    {
        if (r is SkinnedMeshRenderer skinned && skinned.sharedMesh != null)
        {
            Bounds mesh = skinned.sharedMesh.bounds;
            Matrix4x4 m = skinned.transform.localToWorldMatrix;
            var b = new Bounds(m.MultiplyPoint3x4(mesh.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? mesh.min.x : mesh.max.x,
                    (i & 2) == 0 ? mesh.min.y : mesh.max.y,
                    (i & 4) == 0 ? mesh.min.z : mesh.max.z);
                b.Encapsulate(m.MultiplyPoint3x4(corner));
            }
            return b;
        }
        return r.bounds;
    }

    private static Bounds LocalBounds(GameObject model, Transform pivot)
    {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.one);
        Quaternion saved = pivot.rotation;
        pivot.rotation = Quaternion.identity;
        Bounds b = RendererBounds(renderers[0]);
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(RendererBounds(renderers[i]));
        pivot.rotation = saved;
        b.center -= pivot.position;
        return b;
    }
}
