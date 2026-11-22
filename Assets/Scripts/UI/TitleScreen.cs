using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// หน้า Title ของเกม: สร้างตัวเองอัตโนมัติตอนเริ่มเกม (ครั้งแรกของการเปิดเกม) ซ้อนทับฉากแรก
/// ไม่ต้องแก้ไขฉาก — ฉากหลังคือฉากจริงพร้อมกล้องหมุนช้าๆ แบบภาพยนตร์
/// เมนู: เริ่มเกม (เซฟใหม่) / เล่นต่อ / ตั้งค่า (เสียง, เต็มจอ) / ออกจากเกม
/// คีย์บอร์ด: W/S หรือ ลูกศร เลือก, Enter/Space ยืนยัน, Esc ย้อนกลับ
/// </summary>
public class TitleScreen : MonoBehaviour
{
    public const string SkipTitlePref = "CozyHook.SkipTitleInEditor";
    public const string VolumePref = "CozyHook.Volume";
    public const string FullscreenPref = "CozyHook.Fullscreen";

    private static bool shownThisSession;
    public static bool IsShowing { get; private set; }

    private enum Mode { Menu, Settings, Confirm, Leaving }

    // ---- scene state ที่ปิดไว้ระหว่างแสดง Title
    private readonly List<Behaviour> pausedBehaviours = new List<Behaviour>();
    private readonly List<Canvas> hiddenCanvases = new List<Canvas>();
    private Camera cam;
    private Transform player;
    private float orbitAngle;

    // ---- UI
    private CanvasGroup rootGroup;
    private Image fade;
    private RectTransform logo;
    private Vector2 logoBase;
    private RectTransform coin;
    private Image coinGlow;
    private CanvasGroup menuGroup, settingsGroup, confirmGroup;
    private RectTransform selector;
    private Image selectorImage;
    private RectTransform hookLeft, hookRight;
    private readonly List<MenuItem> items = new List<MenuItem>();
    private readonly List<Firefly> fireflies = new List<Firefly>();
    private Slider volumeSlider;
    private Text volumeValue;
    private Text fullscreenLabel;
    private Mode mode = Mode.Menu;
    private int selected;
    private float time;

    // ---- เสียง
    private AudioSource audioSource;
    private AudioClip tickClip, confirmClip;

    private class MenuItem
    {
        public RectTransform rect;
        public Text label;
        public Text note;
        public bool enabled = true;
        public System.Action action;
    }

    private class Firefly
    {
        public RectTransform rect;
        public Image image;
        public Vector2 velocity;
        public float phase, size;
    }

    // ------------------------------------------------------------------ Boot

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        shownThisSession = false;
        IsShowing = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        ApplySavedSettings();
        if (shownThisSession) return;
        shownThisSession = true;
#if UNITY_EDITOR
        if (EditorPrefs.GetBool(SkipTitlePref, false)) return;
#endif
        if (FindAnyObjectByType<PlayerMovement>() == null) return;   // ฉากที่ไม่มีผู้เล่น ไม่ต้องแสดง
        new GameObject("Title Screen").AddComponent<TitleScreen>();
    }

    /// <summary>กลับมาหน้า Title ระหว่างเล่น (จากเมนูหยุดเกม)</summary>
    public static void Show()
    {
        if (IsShowing || FindAnyObjectByType<TitleScreen>() != null) return;
        new GameObject("Title Screen").AddComponent<TitleScreen>();
    }

    private static void ApplySavedSettings()
    {
        AudioListener.volume = PlayerPrefs.GetFloat(VolumePref, 1f);
        if (!Application.isEditor) Screen.fullScreen = PlayerPrefs.GetInt(FullscreenPref, 1) == 1;
    }

    // ------------------------------------------------------------------ Lifecycle

    private void Start()
    {
        IsShowing = true;
        UIFactory.EnsureEventSystem();
        PlayerInputGate.Block();

        var movement = FindAnyObjectByType<PlayerMovement>();
        player = movement != null ? movement.transform : null;
        Pause<PlayerMovement>();
        Pause<PlayerInteractor>();
        Pause<RodHolster>();
        Pause<FishInventoryUI>();
        Pause<ThirdPersonCamera>();

        foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
        {
            if (!c.isRootCanvas || !c.enabled || c.renderMode == RenderMode.WorldSpace) continue;
            c.enabled = false;
            hiddenCanvases.Add(c);
        }

        cam = Camera.main;
        if (player != null) orbitAngle = player.eulerAngles.y + 150f;

        BuildSounds();
        BuildUI();
        SelectDefault();
        StartCoroutine(FadeTo(0f, 1.4f));
    }

    private void Pause<T>() where T : Behaviour
    {
        foreach (T b in FindObjectsByType<T>(FindObjectsInactive.Exclude))
        {
            if (!b.enabled) continue;
            b.enabled = false;
            pausedBehaviours.Add(b);
        }
    }

    private void OnDestroy()
    {
        if (IsShowing) Restore();
    }

    private void Restore()
    {
        foreach (Behaviour b in pausedBehaviours) if (b != null) b.enabled = true;
        foreach (Canvas c in hiddenCanvases) if (c != null) c.enabled = true;
        pausedBehaviours.Clear();
        hiddenCanvases.Clear();
        var follow = cam != null ? cam.GetComponent<ThirdPersonCamera>() : null;
        if (follow != null) follow.SnapBehindTarget();
        PlayerInputGate.Unblock();
        IsShowing = false;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        time += dt;

        UpdateCamera(dt);
        AnimateLogo();
        AnimateFireflies(dt);
        AnimateSelector(dt);
        HandleKeyboard();
    }

    // ------------------------------------------------------------------ Camera

    /// <summary>
    /// จุดที่กล้องหมุนรอบ: ถ้าฉากมีผิวน้ำ (Tag Water) จะหมุนรอบทะเลสาบโดยมองผ่านตัวละคร
    /// ถ้าไม่มีน้ำ (เช่นหมู่บ้าน) จะหมุนรอบตัวละคร
    /// </summary>
    private void SetupCameraShot()
    {
        shotFocus = player != null ? player.position + Vector3.up * 0.9f : Vector3.zero;
        shotRadius = 13f;
        shotHeight = 5.2f;

        GameObject water = null;
        try { water = GameObject.FindGameObjectWithTag("Water"); } catch (UnityException) { }
        Collider col = water != null ? water.GetComponent<Collider>() : null;
        if (col != null && player != null)
        {
            Bounds b = col.bounds;
            Vector3 center = new Vector3(b.center.x, b.max.y, b.center.z);
            // มองจากฝั่งตรงข้ามตัวละคร ข้ามทะเลสาบ ให้เห็นท่าเรือ+น้ำ+ป่า
            shotFocus = Vector3.Lerp(center, player.position, 0.55f) + Vector3.up * 0.5f;
            shotRadius = Mathf.Clamp(Mathf.Max(b.extents.x, b.extents.z) * 0.9f, 16f, 34f);
            shotHeight = shotRadius * 0.42f;
            Vector3 fromCenter = player.position - center;
            fromCenter.y = 0f;
            orbitAngle = fromCenter.sqrMagnitude > 0.01f ? Quaternion.LookRotation(fromCenter).eulerAngles.y + 200f : orbitAngle;
        }
        hasShot = true;
    }

    private Vector3 shotFocus;
    private float shotRadius, shotHeight;
    private bool hasShot;

    private void UpdateCamera(float dt)
    {
        if (cam == null || player == null || mode == Mode.Leaving && fade.color.a > 0.99f) return;
        if (!hasShot) SetupCameraShot();
        orbitAngle += dt * 1.6f;
        Vector3 pos = shotFocus + Quaternion.Euler(0f, orbitAngle, 0f) * new Vector3(0f, 0f, -shotRadius) + Vector3.up * (shotHeight + Mathf.Sin(time * 0.15f) * 0.6f);
        cam.transform.position = pos;
        // หันกล้องเยื้องซ้ายเล็กน้อย ให้ฉากเด่นอยู่ฝั่งขวาของจอ (ฝั่งซ้ายเป็นเมนู)
        cam.transform.rotation = Quaternion.LookRotation(shotFocus - pos, Vector3.up) * Quaternion.Euler(-4f, -18f, 0f);
    }

    // ------------------------------------------------------------------ Animation

    private void AnimateLogo()
    {
        logo.anchoredPosition = logoBase + new Vector2(0f, Mathf.Sin(time * 1.1f) * 7f);
        logo.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 0.7f) * 0.8f);

        // เหรียญพลิกหนึ่งรอบทุก 7 วินาที + แสงเรืองเต้นเป็นจังหวะ
        float phase = Mathf.Repeat(time, 7f);
        float flip = phase < 0.9f ? Mathf.Cos(phase / 0.9f * Mathf.PI * 2f) : 1f;
        coin.localScale = new Vector3(Mathf.Max(0.08f, Mathf.Abs(flip)), 1f, 1f);
        coinGlow.color = new Color(1f, 0.8f, 0.35f, 0.35f + Mathf.Sin(time * 2.2f) * 0.15f);
    }

    private void AnimateFireflies(float dt)
    {
        foreach (Firefly f in fireflies)
        {
            Vector2 p = f.rect.anchoredPosition + f.velocity * dt;
            p.x += Mathf.Sin(time * 0.8f + f.phase) * 12f * dt;
            if (p.y > 580f) p = new Vector2(Random.Range(-960f, 960f), -580f);
            f.rect.anchoredPosition = p;
            float pulse = Mathf.Sin(time * 1.7f + f.phase) * 0.5f + 0.5f;
            f.image.color = new Color(1f, 0.88f, 0.5f, 0.15f + pulse * 0.6f);
            f.rect.sizeDelta = Vector2.one * f.size * (0.8f + pulse * 0.4f);
        }
    }

    private void AnimateSelector(float dt)
    {
        if (items.Count == 0) return;
        MenuItem target = items[selected];
        selector.anchoredPosition = Vector2.Lerp(selector.anchoredPosition, target.rect.anchoredPosition, 1f - Mathf.Exp(-dt * 16f));
        selectorImage.color = new Color(1f, 0.97f, 0.86f, 0.55f + Mathf.Sin(time * 3f) * 0.1f);

        float swing = Mathf.Sin(time * 3.2f) * 10f;
        float halfWidth = target.label.preferredWidth / 2f + 46f;
        hookLeft.anchoredPosition = selector.anchoredPosition + new Vector2(-halfWidth, 4f);
        hookRight.anchoredPosition = selector.anchoredPosition + new Vector2(halfWidth, 4f);
        hookLeft.localRotation = Quaternion.Euler(0f, 0f, swing);
        hookRight.localRotation = Quaternion.Euler(0f, 180f, swing);

        for (int i = 0; i < items.Count; i++)
        {
            MenuItem item = items[i];
            bool on = i == selected && mode == Mode.Menu;
            float s = Mathf.Lerp(item.rect.localScale.x, on ? 1.08f : 1f, 1f - Mathf.Exp(-dt * 14f));
            item.rect.localScale = Vector3.one * s;
            Color c = on ? UISkin.TitleRed : UISkin.Ink;
            c.a = item.enabled ? 1f : 0.35f;
            item.label.color = c;
        }
    }

    // ------------------------------------------------------------------ Input

    private void HandleKeyboard()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null || mode == Mode.Leaving) return;

        bool up = kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame;
        bool down = kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame;
        bool confirm = kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame;
        bool back = kb.escapeKey.wasPressedThisFrame;

        switch (mode)
        {
            case Mode.Menu:
                if (up) Move(-1);
                if (down) Move(1);
                if (confirm) Activate(selected);
                break;
            case Mode.Settings:
                if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) volumeSlider.value -= 0.1f;
                if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) volumeSlider.value += 0.1f;
                if (back || confirm) ShowPanel(Mode.Menu);
                break;
            case Mode.Confirm:
                if (confirm) StartCoroutine(StartGame(true));
                if (back) ShowPanel(Mode.Menu);
                break;
        }
    }

    private void Move(int dir)
    {
        int next = selected;
        for (int i = 0; i < items.Count; i++)
        {
            next = (next + dir + items.Count) % items.Count;
            if (items[next].enabled) break;
        }
        Select(next);
    }

    private void Select(int index)
    {
        if (index == selected || index < 0 || index >= items.Count || !items[index].enabled) return;
        selected = index;
        Play(tickClip, 0.5f);
    }

    private void SelectDefault()
    {
        selected = GameState.SaveExists ? 1 : 0;
        selector.anchoredPosition = items[selected].rect.anchoredPosition;
    }

    private void Activate(int index)
    {
        if (mode != Mode.Menu || index < 0 || index >= items.Count || !items[index].enabled) return;
        selected = index;
        Play(confirmClip, 0.7f);
        items[index].action?.Invoke();
    }

    // ------------------------------------------------------------------ Actions

    private void NewGame()
    {
        if (GameState.SaveExists) ShowPanel(Mode.Confirm);
        else StartCoroutine(StartGame(true));
    }

    private IEnumerator StartGame(bool fresh)
    {
        if (mode == Mode.Leaving) yield break;
        mode = Mode.Leaving;
        Play(confirmClip, 0.8f);
        yield return FadeTo(1f, 0.7f);

        if (fresh)
        {
            GameState.Instance.ResetSave();
            if (GameHUD.Instance != null) GameHUD.Instance.ShowToast("เริ่มการเดินทางใหม่! ลองเหวี่ยงเบ็ดที่ท่าเรือดูสิ", new Color(1f, 0.92f, 0.65f), 4f);
        }
        rootGroup.alpha = 0f;
        Restore();
        yield return FadeTo(0f, 0.9f);
        Destroy(gameObject);
    }

    private IEnumerator QuitGame()
    {
        mode = Mode.Leaving;
        yield return FadeTo(1f, 0.6f);
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void ShowPanel(Mode next)
    {
        mode = next;
        Play(tickClip, 0.6f);

        Fade(menuGroup, next == Mode.Menu || next == Mode.Confirm);
        Fade(settingsGroup, next == Mode.Settings);
        Fade(confirmGroup, next == Mode.Confirm);
    }

    private readonly Dictionary<CanvasGroup, Coroutine> fades = new Dictionary<CanvasGroup, Coroutine>();

    private void Fade(CanvasGroup group, bool show)
    {
        if (fades.TryGetValue(group, out Coroutine running) && running != null) StopCoroutine(running);
        fades[group] = StartCoroutine(CrossFade(group, show));
    }

    private static IEnumerator CrossFade(CanvasGroup group, bool show)
    {
        group.blocksRaycasts = show;
        group.interactable = show;
        float target = show ? 1f : 0f;
        while (!Mathf.Approximately(group.alpha, target))
        {
            group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime * 6f);
            yield return null;
        }
    }

    private IEnumerator FadeTo(float alpha, float seconds)
    {
        fade.raycastTarget = true;
        float start = fade.color.a;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            fade.color = new Color(0f, 0f, 0f, Mathf.Lerp(start, alpha, Mathf.SmoothStep(0f, 1f, t / seconds)));
            yield return null;
        }
        fade.color = new Color(0f, 0f, 0f, alpha);
        fade.raycastTarget = alpha > 0.01f;
    }

    // ------------------------------------------------------------------ Sound

    private void BuildSounds()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        tickClip = Tone("tick", new[] { 1320f }, 0.06f);
        confirmClip = Tone("confirm", new[] { 880f, 1320f }, 0.09f);
    }

    private static AudioClip Tone(string name, float[] freqs, float noteLength)
    {
        const int rate = 44100;
        int noteSamples = Mathf.RoundToInt(rate * noteLength);
        var data = new float[noteSamples * freqs.Length];
        for (int n = 0; n < freqs.Length; n++)
        for (int i = 0; i < noteSamples; i++)
        {
            float t = i / (float)rate;
            float env = Mathf.Exp(-t * 38f) * Mathf.Clamp01(i / 80f);
            data[n * noteSamples + i] = (Mathf.Sin(2f * Mathf.PI * freqs[n] * t) * 0.7f + Mathf.Sin(4f * Mathf.PI * freqs[n] * t) * 0.15f) * env * 0.35f;
        }
        var clip = AudioClip.Create(name, data.Length, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private void Play(AudioClip clip, float volume)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip, volume);
    }

    // ------------------------------------------------------------------ Build

    private void BuildUI()
    {
        Canvas canvas = UIFactory.CreateCanvas(transform, "Title Canvas", 100);
        rootGroup = canvas.gameObject.AddComponent<CanvasGroup>();
        Transform root = canvas.transform;

        // บรรยากาศ: แสงแดดอุ่นๆ มุมขวาบน + ขอบจอมืด + หิ่งห้อย
        Image sun = UISkin.Sliced("Sun Glow", root, UISkin.Glow, new Vector2(1700f, 1700f), new Vector2(760f, 520f), new Color(1f, 0.82f, 0.5f, 0.28f));
        sun.raycastTarget = false;
        Image vignette = UISkin.Sliced("Vignette", root, UISkin.Vignette, Vector2.zero, Vector2.zero, Color.white);
        UIFactory.Stretch(vignette.rectTransform);
        vignette.raycastTarget = false;
        Image leftShade = UIFactory.Panel("Left Shade", root, new Vector2(1500f, 1400f), new Vector2(-900f, 0f), new Color(0.08f, 0.05f, 0.02f, 0.28f));
        leftShade.sprite = UISkin.Glow;
        leftShade.raycastTarget = false;
        for (int i = 0; i < 38; i++) fireflies.Add(CreateFirefly(root));

        BuildLogo(root);
        BuildMenu(root);
        BuildFooter(root);
        BuildConfirm(root);

        fade = UIFactory.Panel("Fade", root, Vector2.zero, Vector2.zero, Color.black);
        UIFactory.Stretch(fade.rectTransform);
    }

    private Firefly CreateFirefly(Transform root)
    {
        var f = new Firefly
        {
            size = Random.Range(8f, 26f),
            phase = Random.Range(0f, 10f),
            velocity = new Vector2(Random.Range(-8f, 8f), Random.Range(10f, 34f)),
        };
        f.image = UISkin.Sliced("Firefly", root, UISkin.Glow, Vector2.one * f.size, new Vector2(Random.Range(-960f, 960f), Random.Range(-560f, 560f)), Color.clear);
        f.image.raycastTarget = false;
        f.rect = f.image.rectTransform;
        return f;
    }

    private void BuildLogo(Transform root)
    {
        Font logoFont = Font.CreateDynamicFontFromOSFont(new[] { "Georgia", "Cambria", "Palatino Linotype", "Leelawadee UI" }, 64);
        Vector2 size = new Vector2(960f, 230f);

        logo = UIFactory.Rect("Logo", root, size, new Vector2(-430f, 300f));
        logoBase = logo.anchoredPosition;

        UISkin.Sliced("Shadow", logo, UISkin.Rounded(30), size + new Vector2(10f, 10f), new Vector2(8f, -14f), new Color(0f, 0f, 0f, 0.45f)).raycastTarget = false;
        UISkin.Sliced("Plank", logo, UISkin.Wood(32), size, Vector2.zero, Color.white).raycastTarget = false;
        UISkin.Sliced("Inset", logo, UISkin.Wood(24), size - new Vector2(44f, 44f), Vector2.zero, new Color(0.78f, 0.7f, 0.66f)).raycastTarget = false;
        foreach (float sx in new[] { -1f, 1f })
        foreach (float sy in new[] { -1f, 1f })
        {
            Image fish = UISkin.Sliced("Fish", logo, UISkin.FishOrnament, new Vector2(120f, 60f), new Vector2(sx * (size.x / 2f - 30f), sy * (size.y / 2f - 18f)), new Color(0.78f, 0.57f, 0.34f));
            fish.rectTransform.localRotation = Quaternion.Euler(0f, sx > 0f ? 0f : 180f, sy > 0f ? -30f : 30f);
            fish.raycastTarget = false;
        }

        LogoWord(logo, logoFont, "Cozy", new Vector2(-268f, 6f));
        LogoWord(logo, logoFont, "Hook", new Vector2(276f, 6f));

        coinGlow = UISkin.Sliced("Coin Glow", logo, UISkin.Glow, new Vector2(420f, 420f), new Vector2(0f, 4f), Color.clear);
        coinGlow.raycastTarget = false;
        UISkin.Sliced("Coin Rim", logo, UISkin.GoldCoinLarge, new Vector2(238f, 238f), new Vector2(4f, -2f), new Color(0.3f, 0.17f, 0.07f, 0.9f)).raycastTarget = false;
        Image coinImage = UISkin.Sliced("Coin", logo, UISkin.GoldCoinLarge, new Vector2(222f, 222f), new Vector2(0f, 4f), Color.white);
        coinImage.raycastTarget = false;
        coin = coinImage.rectTransform;

        // ริบบิ้นกระดาษใต้โลโก้
        Vector2 ribbonPos = new Vector2(0f, -size.y / 2f - 30f);
        foreach (float side in new[] { -1f, 1f })
            UISkin.Sliced("Roll", logo, UISkin.Rounded(12), new Vector2(30f, 66f), ribbonPos + new Vector2(side * 250f, 0f), UISkin.ParchmentDark * 0.9f).raycastTarget = false;
        Image ribbon = UISkin.Sliced("Ribbon", logo, UISkin.ParchmentSheet(8), new Vector2(500f, 60f), ribbonPos, Color.white);
        ribbon.raycastTarget = false;
        UIFactory.Label("Tagline", ribbon.transform, "เกมตกปลาสุดชิลกลางป่า", 30, TextAnchor.MiddleCenter, new Vector2(480f, 56f), Vector2.zero, UISkin.Ink, FontStyle.Bold);
    }

    private static void LogoWord(Transform parent, Font font, string word, Vector2 position)
    {
        Text t = UIFactory.Label(word, parent, word, 128, TextAnchor.MiddleCenter, new Vector2(360f, 180f), position, Color.white, FontStyle.Bold);
        t.font = font;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        var gradient = t.gameObject.AddComponent<UIGradient>();
        gradient.top = new Color(1f, 0.95f, 0.75f);
        gradient.bottom = new Color(0.93f, 0.62f, 0.24f);
        var outline = t.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.24f, 0.11f, 0.04f);
        outline.effectDistance = new Vector2(4f, -4f);
        var outline2 = t.gameObject.AddComponent<Outline>();
        outline2.effectColor = new Color(0.24f, 0.11f, 0.04f);
        outline2.effectDistance = new Vector2(-3f, 3f);
        var shadow = t.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
        shadow.effectDistance = new Vector2(5f, -9f);
    }

    private void BuildMenu(Transform root)
    {
        Vector2 size = new Vector2(540f, 470f);
        Vector2 pos = new Vector2(-430f, -165f);
        UISkin.Sliced("Menu Shadow", root, UISkin.Rounded(30), size + new Vector2(10f, 10f), pos + new Vector2(8f, -12f), new Color(0f, 0f, 0f, 0.45f)).raycastTarget = false;
        Image frame = UISkin.Sliced("Menu Board", root, UISkin.Wood(30), size, pos, Color.white);
        Transform paper = UISkin.Sliced("Paper", frame.transform, UISkin.ParchmentSheet(16), size - new Vector2(52f, 52f), Vector2.zero, Color.white).transform;
        foreach (float sx in new[] { -1f, 1f })
        foreach (float sy in new[] { -1f, 1f })
        {
            Image fish = UISkin.Sliced("Fish", frame.transform, UISkin.FishOrnament, new Vector2(96f, 48f), new Vector2(sx * (size.x / 2f - 26f), sy * (size.y / 2f - 16f)), new Color(0.72f, 0.52f, 0.32f));
            fish.rectTransform.localRotation = Quaternion.Euler(0f, sx > 0f ? 0f : 180f, sy > 0f ? -35f : 35f);
            fish.raycastTarget = false;
        }

        // ---- เมนูหลัก
        menuGroup = NewGroup("Main", paper);
        Transform menu = menuGroup.transform;
        selectorImage = UISkin.Sliced("Selector", menu, UISkin.Rounded(18), new Vector2(400f, 76f), Vector2.zero, Color.white);
        selectorImage.raycastTarget = false;
        selector = selectorImage.rectTransform;
        hookLeft = UISkin.Sliced("Hook L", menu, UISkin.Hook, new Vector2(26f, 36f), Vector2.zero, UISkin.TitleRed).rectTransform;
        hookRight = UISkin.Sliced("Hook R", menu, UISkin.Hook, new Vector2(26f, 36f), Vector2.zero, UISkin.TitleRed).rectTransform;
        hookLeft.GetComponent<Image>().raycastTarget = hookRight.GetComponent<Image>().raycastTarget = false;

        bool hasSave = GameState.SaveExists;
        AddItem(menu, "เริ่มเกม", 138f, true, NewGame, hasSave ? "เซฟเดิมจะถูกแทนที่" : null);
        AddItem(menu, "เล่นต่อ", 46f, hasSave, () => StartCoroutine(StartGame(false)), hasSave ? null : "ยังไม่มีเซฟ");
        AddItem(menu, "ตั้งค่า", -46f, true, () => ShowPanel(Mode.Settings), null);
        AddItem(menu, "ออกจากเกม", -138f, true, () => StartCoroutine(QuitGame()), null);

        // ---- ตั้งค่า
        settingsGroup = NewGroup("Settings", paper);
        settingsGroup.alpha = 0f;
        settingsGroup.blocksRaycasts = settingsGroup.interactable = false;
        Transform s = settingsGroup.transform;
        UIFactory.Label("Title", s, "ตั้งค่า", 46, TextAnchor.MiddleCenter, new Vector2(400f, 60f), new Vector2(0f, 150f), UISkin.TitleRed, FontStyle.Bold);
        UIFactory.Label("VolumeLabel", s, "ความดังเสียง", 28, TextAnchor.MiddleLeft, new Vector2(400f, 40f), new Vector2(0f, 76f), UISkin.Ink, FontStyle.Bold);
        volumeSlider = BuildSlider(s, new Vector2(-40f, 30f), new Vector2(320f, 34f));
        volumeValue = UIFactory.Label("VolumeValue", s, "", 26, TextAnchor.MiddleRight, new Vector2(90f, 40f), new Vector2(170f, 30f), UISkin.Ink, FontStyle.Bold);
        volumeSlider.value = AudioListener.volume;
        volumeValue.text = $"{Mathf.RoundToInt(AudioListener.volume * 100f)}%";
        volumeSlider.onValueChanged.AddListener(v =>
        {
            AudioListener.volume = v;
            PlayerPrefs.SetFloat(VolumePref, v);
            volumeValue.text = $"{Mathf.RoundToInt(v * 100f)}%";
        });

        Button fullscreen = UISkin.WoodButton("Fullscreen", s, "", new Vector2(360f, 60f), new Vector2(0f, -50f), ToggleFullscreen, 24, false);
        fullscreenLabel = fullscreen.GetComponentInChildren<Text>();
        UpdateFullscreenLabel();
        UISkin.WoodButton("Back", s, "กลับ", new Vector2(200f, 62f), new Vector2(0f, -140f), () => ShowPanel(Mode.Menu), 28, true);
    }

    private CanvasGroup NewGroup(string name, Transform parent)
    {
        RectTransform rt = UIFactory.Rect(name, parent, Vector2.zero, Vector2.zero);
        UIFactory.Stretch(rt);
        return rt.gameObject.AddComponent<CanvasGroup>();
    }

    private void AddItem(Transform parent, string text, float y, bool enabled, System.Action action, string note)
    {
        int index = items.Count;
        Image hit = UIFactory.Panel(text, parent, new Vector2(420f, 80f), new Vector2(0f, y), new Color(1f, 1f, 1f, 0.001f));
        var item = new MenuItem { rect = hit.rectTransform, enabled = enabled, action = action };
        item.label = UIFactory.Label("Text", hit.transform, text, 50, TextAnchor.MiddleCenter, new Vector2(420f, 70f), new Vector2(0f, note != null ? 6f : 0f), UISkin.Ink, FontStyle.Bold);
        if (note != null)
            item.note = UIFactory.Label("Note", hit.transform, note, 19, TextAnchor.MiddleCenter, new Vector2(420f, 22f), new Vector2(0f, -30f), UISkin.InkSoft);

        var button = hit.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => Activate(index));
        hit.gameObject.AddComponent<PointerHover>().onEnter = () => { if (mode == Mode.Menu) Select(index); };
        items.Add(item);
    }

    public static Slider BuildSlider(Transform parent, Vector2 position, Vector2 size)
    {
        RectTransform rt = UIFactory.Rect("Volume", parent, size, position);
        UISkin.Sliced("Track", rt, UISkin.Rounded(10), new Vector2(size.x, 18f), Vector2.zero, UISkin.WoodDark);

        RectTransform fillArea = UIFactory.Rect("Fill Area", rt, Vector2.zero, Vector2.zero);
        fillArea.anchorMin = new Vector2(0f, 0.5f);
        fillArea.anchorMax = new Vector2(1f, 0.5f);
        fillArea.sizeDelta = new Vector2(-6f, 12f);
        Image fill = UISkin.Sliced("Fill", fillArea, UISkin.Rounded(6), Vector2.zero, Vector2.zero, new Color(0.95f, 0.72f, 0.3f));
        fill.rectTransform.anchorMin = new Vector2(0f, 0f);
        fill.rectTransform.anchorMax = new Vector2(0f, 1f);
        fill.rectTransform.sizeDelta = Vector2.zero;

        RectTransform handleArea = UIFactory.Rect("Handle Area", rt, Vector2.zero, Vector2.zero);
        UIFactory.Stretch(handleArea);
        handleArea.offsetMin = new Vector2(16f, 0f);
        handleArea.offsetMax = new Vector2(-16f, 0f);
        Image handle = UISkin.Sliced("Handle", handleArea, UISkin.GoldCoin, Vector2.zero, Vector2.zero, Color.white);
        handle.rectTransform.anchorMin = new Vector2(0f, 0f);
        handle.rectTransform.anchorMax = new Vector2(0f, 1f);
        handle.rectTransform.sizeDelta = new Vector2(44f, 10f);

        var slider = rt.gameObject.AddComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.transition = Selectable.Transition.None;
        return slider;
    }

    private void ToggleFullscreen()
    {
        bool on = PlayerPrefs.GetInt(FullscreenPref, 1) != 1;
        PlayerPrefs.SetInt(FullscreenPref, on ? 1 : 0);
        if (!Application.isEditor) Screen.fullScreen = on;
        UpdateFullscreenLabel();
    }

    private void UpdateFullscreenLabel()
    {
        bool on = PlayerPrefs.GetInt(FullscreenPref, 1) == 1;
        fullscreenLabel.text = on ? "โหมดเต็มจอ: เปิด" : "โหมดเต็มจอ: ปิด";
    }

    private void BuildFooter(Transform root)
    {
        Image bar = UISkin.Sliced("Footer", root, UISkin.Wood(14), new Vector2(0f, 64f), Vector2.zero, Color.white);
        bar.raycastTarget = false;
        RectTransform rt = bar.rectTransform;
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(40f, 64f);
        rt.anchoredPosition = new Vector2(0f, -8f);

        Text left = UIFactory.Label("Left", bar.transform, "เกมตกปลาสุดชิล  •  Cozy Hook", 26, TextAnchor.MiddleLeft, new Vector2(700f, 56f), Vector2.zero, UISkin.Cream, FontStyle.Bold);
        left.rectTransform.anchorMin = left.rectTransform.anchorMax = left.rectTransform.pivot = new Vector2(0f, 0.5f);
        left.rectTransform.anchoredPosition = new Vector2(48f, 4f);
        UISkin.Shadowed(left, new Color(0f, 0f, 0f, 0.6f));

        Text right = UIFactory.Label("Right", bar.transform, "W/S หรือ ลูกศร เลือก  •  Enter ยืนยัน  •  Esc ย้อนกลับ      v0.1", 22, TextAnchor.MiddleRight, new Vector2(900f, 56f), Vector2.zero, UISkin.Cream);
        right.rectTransform.anchorMin = right.rectTransform.anchorMax = right.rectTransform.pivot = new Vector2(1f, 0.5f);
        right.rectTransform.anchoredPosition = new Vector2(-48f, 4f);
        UISkin.Shadowed(right, new Color(0f, 0f, 0f, 0.6f));
    }

    private void BuildConfirm(Transform root)
    {
        confirmGroup = NewGroup("Confirm", root);
        confirmGroup.alpha = 0f;
        confirmGroup.blocksRaycasts = confirmGroup.interactable = false;
        Image dim = UIFactory.Panel("Dim", confirmGroup.transform, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.5f));
        UIFactory.Stretch(dim.rectTransform);

        Vector2 size = new Vector2(700f, 330f);
        RectTransform paper = UISkin.WoodWindow(confirmGroup.transform, size, out _);
        UIFactory.Label("Title", paper, "เริ่มเกมใหม่?", 46, TextAnchor.MiddleCenter, new Vector2(600f, 60f), new Vector2(0f, 82f), UISkin.TitleRed, FontStyle.Bold);
        UIFactory.Label("Body", paper, "เซฟเดิมทั้งหมด (เหรียญ ปลาในกระเป๋า เหยื่อ วันที่) จะถูกลบ\nแน่ใจไหม?", 24, TextAnchor.MiddleCenter, new Vector2(600f, 80f), new Vector2(0f, 10f), UISkin.Ink);
        UISkin.PaintedButton("Yes", paper, "เริ่มใหม่", new Vector2(210f, 62f), new Vector2(-120f, -86f), new Color(0.75f, 0.35f, 0.25f), () => StartCoroutine(StartGame(true)), 26);
        UISkin.WoodButton("No", paper, "ยกเลิก", new Vector2(210f, 62f), new Vector2(120f, -86f), () => ShowPanel(Mode.Menu), 26, true);
    }

    // ------------------------------------------------------------------ Helpers

    /// <summary>เรียก callback ตอนเมาส์ชี้</summary>
    private class PointerHover : MonoBehaviour, IPointerEnterHandler
    {
        public System.Action onEnter;
        public void OnPointerEnter(PointerEventData eventData) => onEnter?.Invoke();
    }
}

/// <summary>ไล่สีแนวตั้งให้ข้อความ/รูป (บนสุด → ล่างสุด)</summary>
public class UIGradient : BaseMeshEffect
{
    public Color top = Color.white;
    public Color bottom = Color.black;

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || vh.currentVertCount == 0) return;
        var v = new UIVertex();
        float min = float.MaxValue, max = float.MinValue;
        for (int i = 0; i < vh.currentVertCount; i++)
        {
            vh.PopulateUIVertex(ref v, i);
            min = Mathf.Min(min, v.position.y);
            max = Mathf.Max(max, v.position.y);
        }
        float range = Mathf.Max(0.001f, max - min);
        for (int i = 0; i < vh.currentVertCount; i++)
        {
            vh.PopulateUIVertex(ref v, i);
            Color c = Color.Lerp(bottom, top, (v.position.y - min) / range);
            v.color = (Color32)(c * (Color)v.color);
            vh.SetUIVertex(v, i);
        }
    }
}
