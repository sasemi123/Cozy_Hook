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
/// เมนูหยุดเกม (กด Esc ระหว่างเล่น): หยุดเวลาในเกม แล้วเลือก
/// เล่นต่อ / ตั้งค่า (เสียง, เต็มจอ) / กลับหน้าแรก / ออกจากเกม
/// สร้างตัวเองอัตโนมัติ อยู่ข้ามฉาก ไม่ต้องใส่ในฉาก
/// ถ้ามีหน้าต่างอื่นเปิดอยู่ (กระเป๋า ตลาด ป้ายประกาศ) Esc จะปิดหน้าต่างนั้นก่อน ไม่เปิดเมนูนี้
/// </summary>
public class PauseMenu : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    private enum Mode { Closed, Menu, Settings, ConfirmTitle, ConfirmQuit }

    private Mode mode = Mode.Closed;
    private bool blockedLastFrame;
    private float savedTimeScale = 1f;
    private float time;

    private GameObject root;
    private CanvasGroup rootGroup, menuGroup, settingsGroup, confirmGroup;
    private RectTransform window;
    private Text infoText, confirmTitle, confirmBody;
    private RectTransform selector;
    private Image selectorImage;
    private RectTransform hookLeft, hookRight;
    private readonly List<Item> items = new List<Item>();
    private readonly Dictionary<CanvasGroup, Coroutine> fades = new Dictionary<CanvasGroup, Coroutine>();
    private int selected;
    private Slider volumeSlider;
    private Text volumeValue, fullscreenLabel;
    private AudioSource audioSource;
    private AudioClip tick;

    private class Item
    {
        public RectTransform rect;
        public Text label;
        public System.Action action;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => IsOpen = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (FindAnyObjectByType<PauseMenu>() != null) return;
        var go = new GameObject("Pause Menu");
        DontDestroyOnLoad(go);
        go.AddComponent<PauseMenu>();
    }

    private void Awake()
    {
        UIFactory.EnsureEventSystem();
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.ignoreListenerPause = true;
        tick = MakeTick();
        Build();
        root.SetActive(false);
    }

    private void OnDestroy()
    {
        if (IsOpen) Time.timeScale = savedTimeScale;
        IsOpen = false;
    }

    // ------------------------------------------------------------------ Update

    private void Update()
    {
        time += Time.unscaledDeltaTime;
        Keyboard kb = Keyboard.current;

        if (mode == Mode.Closed)
        {
            // เปิดได้เฉพาะตอนไม่มีหน้าต่างอื่นค้างอยู่ตั้งแต่เฟรมก่อน (กัน Esc ที่ใช้ปิดหน้าต่างอื่น)
            if (kb != null && kb.escapeKey.wasPressedThisFrame && !blockedLastFrame && !TitleScreen.IsShowing && FindAnyObjectByType<PlayerMovement>() != null)
                Open();
            return;
        }

        AnimateSelector();
        infoText.text = InfoLine();
        if (kb == null) return;

        bool up = kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame;
        bool down = kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame;
        bool confirm = kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame;
        bool back = kb.escapeKey.wasPressedThisFrame;

        switch (mode)
        {
            case Mode.Menu:
                if (up) Select((selected + items.Count - 1) % items.Count);
                if (down) Select((selected + 1) % items.Count);
                if (confirm) Activate(selected);
                if (back) Close();
                break;
            case Mode.Settings:
                if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) volumeSlider.value -= 0.1f;
                if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) volumeSlider.value += 0.1f;
                if (back || confirm) Show(Mode.Menu);
                break;
            case Mode.ConfirmTitle:
            case Mode.ConfirmQuit:
                if (confirm) ConfirmYes();
                if (back) Show(Mode.Menu);
                break;
        }
    }

    private void LateUpdate()
    {
        if (mode == Mode.Closed) blockedLastFrame = PlayerInputGate.IsBlocked;
    }

    private static string InfoLine()
    {
        if (!GameState.Exists) return "";
        GameState s = GameState.Instance;
        return $"วันที่ {s.Day}  •  {s.ClockText}  •  {s.Coins:N0} เหรียญ  •  ปลาในกระเป๋า {s.Fish.Count} ตัว";
    }

    // ------------------------------------------------------------------ Open / close

    public void Open()
    {
        if (IsOpen) return;
        IsOpen = true;
        savedTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        PlayerInputGate.Block();
        root.SetActive(true);
        selected = 0;
        selector.anchoredPosition = items[0].rect.anchoredPosition;
        Show(Mode.Menu, false);
        StartCoroutine(Pop(true));
        Play(0.6f);
    }

    public void Close()
    {
        if (!IsOpen) return;
        if (GameState.Exists) GameState.Instance.Save();
        StartCoroutine(Pop(false));
    }

    private IEnumerator Pop(bool show)
    {
        float from = show ? 0f : 1f, to = show ? 1f : 0f;
        for (float t = 0f; t < 0.18f; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(from, to, t / 0.18f);
            rootGroup.alpha = k;
            window.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, k);
            yield return null;
        }
        rootGroup.alpha = to;
        window.localScale = Vector3.one;
        if (!show) FinishClose();
    }

    private void FinishClose()
    {
        root.SetActive(false);
        mode = Mode.Closed;
        IsOpen = false;
        Time.timeScale = savedTimeScale <= 0f ? 1f : savedTimeScale;
        PlayerInputGate.Unblock();
        blockedLastFrame = true;   // กัน Esc เฟรมเดียวกันเปิดซ้ำ
    }

    // ------------------------------------------------------------------ Actions

    private void Activate(int index)
    {
        if (mode != Mode.Menu) return;
        selected = index;
        Play(0.8f);
        items[index].action?.Invoke();
    }

    private void ConfirmYes()
    {
        Play(0.8f);
        if (mode == Mode.ConfirmTitle)
        {
            if (GameState.Exists) GameState.Instance.Save();
            FinishClose();
            TitleScreen.Show();
        }
        else
        {
            if (GameState.Exists) GameState.Instance.Save();
            Time.timeScale = 1f;
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }

    private void Show(Mode next, bool sound = true)
    {
        mode = next;
        if (sound) Play(0.5f);
        Fade(menuGroup, next == Mode.Menu || next == Mode.ConfirmTitle || next == Mode.ConfirmQuit, next == Mode.Menu);
        Fade(settingsGroup, next == Mode.Settings, next == Mode.Settings);
        Fade(confirmGroup, next == Mode.ConfirmTitle || next == Mode.ConfirmQuit, true);
        if (next == Mode.ConfirmTitle)
        {
            confirmTitle.text = "กลับหน้าแรก?";
            confirmBody.text = "เกมจะบันทึกให้อัตโนมัติ\nกลับมาเล่นต่อได้จากปุ่ม \"เล่นต่อ\"";
        }
        else if (next == Mode.ConfirmQuit)
        {
            confirmTitle.text = "ออกจากเกม?";
            confirmBody.text = "เกมจะบันทึกให้อัตโนมัติก่อนปิด\nแล้วพบกันใหม่ที่ทะเลสาบนะ";
        }
    }

    private void Fade(CanvasGroup group, bool visible, bool interactive)
    {
        if (fades.TryGetValue(group, out Coroutine running) && running != null) StopCoroutine(running);
        group.blocksRaycasts = group.interactable = interactive;
        fades[group] = StartCoroutine(FadeGroup(group, visible ? 1f : 0f));
    }

    private static IEnumerator FadeGroup(CanvasGroup group, float target)
    {
        while (!Mathf.Approximately(group.alpha, target))
        {
            group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime * 7f);
            yield return null;
        }
    }

    // ------------------------------------------------------------------ Selector

    private void Select(int index)
    {
        if (index == selected) return;
        selected = index;
        Play(0.4f);
    }

    private void AnimateSelector()
    {
        float dt = Time.unscaledDeltaTime;
        Item target = items[selected];
        selector.anchoredPosition = Vector2.Lerp(selector.anchoredPosition, target.rect.anchoredPosition, 1f - Mathf.Exp(-dt * 16f));
        selectorImage.color = new Color(1f, 0.97f, 0.86f, 0.6f + Mathf.Sin(time * 3f) * 0.1f);
        float half = target.label.preferredWidth / 2f + 40f;
        float swing = Mathf.Sin(time * 3.2f) * 10f;
        hookLeft.anchoredPosition = selector.anchoredPosition + new Vector2(-half, 3f);
        hookRight.anchoredPosition = selector.anchoredPosition + new Vector2(half, 3f);
        hookLeft.localRotation = Quaternion.Euler(0f, 0f, swing);
        hookRight.localRotation = Quaternion.Euler(0f, 180f, swing);
        for (int i = 0; i < items.Count; i++)
        {
            bool on = i == selected && mode == Mode.Menu;
            float s = Mathf.Lerp(items[i].rect.localScale.x, on ? 1.07f : 1f, 1f - Mathf.Exp(-dt * 14f));
            items[i].rect.localScale = Vector3.one * s;
            items[i].label.color = on ? UISkin.TitleRed : UISkin.Ink;
        }
    }

    // ------------------------------------------------------------------ Build

    private void Build()
    {
        Canvas canvas = UIFactory.CreateCanvas(transform, "Pause Canvas", 90);
        root = canvas.gameObject;
        rootGroup = root.AddComponent<CanvasGroup>();

        Image dim = UIFactory.Panel("Dim", canvas.transform, Vector2.zero, Vector2.zero, new Color(0.03f, 0.03f, 0.05f, 0.6f));
        UIFactory.Stretch(dim.rectTransform);
        Image vignette = UISkin.Sliced("Vignette", canvas.transform, UISkin.Vignette, Vector2.zero, Vector2.zero, Color.white);
        UIFactory.Stretch(vignette.rectTransform);
        vignette.raycastTarget = false;

        Vector2 size = new Vector2(620f, 700f);
        window = UIFactory.Rect("Window", canvas.transform, size, Vector2.zero);
        RectTransform paper = UISkin.WoodWindow(window, size, out Image frame);
        float top = paper.sizeDelta.y / 2f;

        // ป้ายหัว "พักเกม" บนคานไม้
        Image plank = UISkin.Sliced("Title Plank", frame.transform, UISkin.Wood(22), new Vector2(360f, 84f), new Vector2(0f, size.y / 2f + 4f), Color.white);
        Text title = UIFactory.Label("Title", plank.transform, "พักเกม", 46, TextAnchor.MiddleCenter, new Vector2(340f, 80f), new Vector2(0f, 2f), new Color(1f, 0.88f, 0.55f), FontStyle.Bold);
        UISkin.Shadowed(title, new Color(0.15f, 0.06f, 0.02f, 0.9f), 3f);
        UISkin.Sliced("Coin", plank.transform, UISkin.GoldCoin, new Vector2(56f, 56f), new Vector2(-150f, 0f), Color.white);
        UISkin.Sliced("Coin", plank.transform, UISkin.GoldCoin, new Vector2(56f, 56f), new Vector2(150f, 0f), Color.white);

        // แถบสถานะ
        Image info = UISkin.Sliced("Info", paper, UISkin.ParchmentSheet(8), new Vector2(paper.sizeDelta.x - 50f, 48f), new Vector2(0f, top - 70f), new Color(0.96f, 0.9f, 0.78f));
        infoText = UIFactory.Label("Text", info.transform, "", 21, TextAnchor.MiddleCenter, new Vector2(paper.sizeDelta.x - 70f, 44f), Vector2.zero, UISkin.Ink);

        // เมนูหลัก
        menuGroup = Group("Menu", paper);
        Transform m = menuGroup.transform;
        selectorImage = UISkin.Sliced("Selector", m, UISkin.Rounded(18), new Vector2(380f, 72f), Vector2.zero, Color.white);
        selectorImage.raycastTarget = false;
        selector = selectorImage.rectTransform;
        hookLeft = UISkin.Sliced("Hook L", m, UISkin.Hook, new Vector2(24f, 34f), Vector2.zero, UISkin.TitleRed).rectTransform;
        hookRight = UISkin.Sliced("Hook R", m, UISkin.Hook, new Vector2(24f, 34f), Vector2.zero, UISkin.TitleRed).rectTransform;

        AddItem(m, "เล่นต่อ", 120f, Close);
        AddItem(m, "ตั้งค่า", 30f, () => Show(Mode.Settings));
        AddItem(m, "กลับหน้าแรก", -60f, () => Show(Mode.ConfirmTitle));
        AddItem(m, "ออกจากเกม", -150f, () => Show(Mode.ConfirmQuit));
        Text hint = UIFactory.Label("Hint", m, "Esc กลับเข้าเกม  •  W/S เลือก  •  Enter ยืนยัน", 19, TextAnchor.MiddleCenter, new Vector2(520f, 30f), new Vector2(0f, -top + 34f), UISkin.InkSoft);
        hint.raycastTarget = false;

        // ตั้งค่า
        settingsGroup = Group("Settings", paper);
        settingsGroup.alpha = 0f;
        Transform s = settingsGroup.transform;
        UIFactory.Label("Title", s, "ตั้งค่า", 44, TextAnchor.MiddleCenter, new Vector2(400f, 60f), new Vector2(0f, 140f), UISkin.TitleRed, FontStyle.Bold);
        UIFactory.Label("VolumeLabel", s, "ความดังเสียง", 28, TextAnchor.MiddleLeft, new Vector2(420f, 40f), new Vector2(0f, 70f), UISkin.Ink, FontStyle.Bold);
        volumeSlider = TitleScreen.BuildSlider(s, new Vector2(-45f, 24f), new Vector2(330f, 34f));
        volumeValue = UIFactory.Label("VolumeValue", s, "", 26, TextAnchor.MiddleRight, new Vector2(90f, 40f), new Vector2(175f, 24f), UISkin.Ink, FontStyle.Bold);
        volumeSlider.value = AudioListener.volume;
        volumeValue.text = $"{Mathf.RoundToInt(AudioListener.volume * 100f)}%";
        volumeSlider.onValueChanged.AddListener(v =>
        {
            AudioListener.volume = v;
            PlayerPrefs.SetFloat(TitleScreen.VolumePref, v);
            volumeValue.text = $"{Mathf.RoundToInt(v * 100f)}%";
        });
        Button fs = UISkin.WoodButton("Fullscreen", s, "", new Vector2(380f, 62f), new Vector2(0f, -60f), ToggleFullscreen, 24, false);
        fullscreenLabel = fs.GetComponentInChildren<Text>();
        UpdateFullscreenLabel();
        UISkin.WoodButton("Back", s, "กลับ", new Vector2(200f, 62f), new Vector2(0f, -160f), () => Show(Mode.Menu), 28, true);
        settingsGroup.blocksRaycasts = settingsGroup.interactable = false;

        // ยืนยัน
        confirmGroup = Group("Confirm", paper);
        confirmGroup.alpha = 0f;
        Transform c = confirmGroup.transform;
        Image cover = UISkin.Sliced("Cover", c, UISkin.ParchmentSheet(16), paper.sizeDelta - new Vector2(10f, 100f), new Vector2(0f, -45f), Color.white);
        confirmTitle = UIFactory.Label("Title", cover.transform, "", 44, TextAnchor.MiddleCenter, new Vector2(500f, 60f), new Vector2(0f, 120f), UISkin.TitleRed, FontStyle.Bold);
        confirmBody = UIFactory.Label("Body", cover.transform, "", 25, TextAnchor.MiddleCenter, new Vector2(500f, 100f), new Vector2(0f, 30f), UISkin.Ink);
        UISkin.PaintedButton("Yes", cover.transform, "ตกลง", new Vector2(190f, 62f), new Vector2(-110f, -90f), new Color(0.75f, 0.35f, 0.25f), ConfirmYes, 26);
        UISkin.WoodButton("No", cover.transform, "ยกเลิก", new Vector2(190f, 62f), new Vector2(110f, -90f), () => Show(Mode.Menu), 26, true);
        confirmGroup.blocksRaycasts = confirmGroup.interactable = false;

        UISkin.WoodButton("Close", frame.transform, "Esc เล่นต่อ", new Vector2(180f, 56f), new Vector2(size.x / 2f - 110f, -size.y / 2f + 8f), Close, 22, true)
            .transform.SetAsLastSibling();
    }

    private static CanvasGroup Group(string name, Transform parent)
    {
        RectTransform rt = UIFactory.Rect(name, parent, Vector2.zero, Vector2.zero);
        UIFactory.Stretch(rt);
        return rt.gameObject.AddComponent<CanvasGroup>();
    }

    private void AddItem(Transform parent, string text, float y, System.Action action)
    {
        int index = items.Count;
        Image hit = UIFactory.Panel(text, parent, new Vector2(400f, 78f), new Vector2(0f, y), new Color(1f, 1f, 1f, 0.001f));
        var item = new Item { rect = hit.rectTransform, action = action };
        item.label = UIFactory.Label("Text", hit.transform, text, 44, TextAnchor.MiddleCenter, new Vector2(400f, 70f), Vector2.zero, UISkin.Ink, FontStyle.Bold);
        var button = hit.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => Activate(index));
        hit.gameObject.AddComponent<Hover>().onEnter = () => { if (mode == Mode.Menu) Select(index); };
        items.Add(item);
    }

    private void ToggleFullscreen()
    {
        bool on = PlayerPrefs.GetInt(TitleScreen.FullscreenPref, 1) != 1;
        PlayerPrefs.SetInt(TitleScreen.FullscreenPref, on ? 1 : 0);
        if (!Application.isEditor) Screen.fullScreen = on;
        UpdateFullscreenLabel();
    }

    private void UpdateFullscreenLabel()
    {
        fullscreenLabel.text = PlayerPrefs.GetInt(TitleScreen.FullscreenPref, 1) == 1 ? "โหมดเต็มจอ: เปิด" : "โหมดเต็มจอ: ปิด";
    }

    private static AudioClip MakeTick()
    {
        const int rate = 44100;
        int n = rate / 16;
        var data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            data[i] = Mathf.Sin(2f * Mathf.PI * 1100f * t) * Mathf.Exp(-t * 40f) * Mathf.Clamp01(i / 80f) * 0.3f;
        }
        var clip = AudioClip.Create("pause_tick", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private void Play(float volume) => audioSource.PlayOneShot(tick, volume);

    private class Hover : MonoBehaviour, IPointerEnterHandler
    {
        public System.Action onEnter;
        public void OnPointerEnter(PointerEventData eventData) => onEnter?.Invoke();
    }
}
