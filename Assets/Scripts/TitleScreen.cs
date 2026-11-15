using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Everything that happens before and around the game, all inside the same scene:
///   Splash -> Title (Play / Settings / Credits) -> Loading -> Playing, plus a pause menu (Esc).
/// The scenery behind the menu, the handwritten logo and the heart-shaped ghost are drawn in code.
/// </summary>
public class TitleScreen : MonoBehaviour
{
    public static TitleScreen Instance { get; private set; }

    enum State { Splash, Title, Settings, Credits, Loading, Playing, Paused, EndCredits }

    // ------------------------------------------------------------------ small widgets
    class Btn
    {
        public RectTransform rt;
        public Image frame, fill, cursor;
        public Text label;
        public System.Action onClick;
        public bool toggled;
        public float anim;
    }

    class Slid
    {
        public RectTransform track, fill, knob;
        public Text valueText;
        public float min, max;
        public System.Func<float> get;
        public System.Action<float> set;
        public System.Func<float, string> format;
        public bool dragging;
    }

    class Mover { public RectTransform rt; public float speed, baseY, phase, amp; }
    class Star { public Image img; public float baseA, speed, phase; }
    class Layer { public RectTransform[] copies; public float speed, width; }
    class Blinker { public Image[] eyes; public float next, until; }

    // ------------------------------------------------------------------ state
    State state = State.Splash;
    State settingsFrom = State.Title;
    float stateTime;
    int loadPhase;
    float fade;
    Vector2 lastMouse;
    int selected = -1;

    WorldGenerator world;
    RectTransform root;
    GameObject artRoot, logoRoot, titlePanel, settingsPanel, creditsPanel, pausePanel, dimPanel, splashPanel;
    Image fadeImage, brightnessImage;

    // background
    readonly List<Star> stars = new List<Star>();
    readonly List<Mover> whales = new List<Mover>();
    readonly List<Mover> clouds = new List<Mover>();
    readonly List<Mover> hearts = new List<Mover>();
    readonly List<Mover> flies = new List<Mover>();
    readonly List<Layer> layers = new List<Layer>();
    readonly List<Blinker> blinkers = new List<Blinker>();
    RectTransform moon, moonHalo;
    RawImage grain;
    float grainTimer;

    // logo
    RectTransform logoBob, logoMask, ghostRt;
    Image logoMain, logoShadowA, logoShadowB, ghostImage;
    Sprite ghostOpen, ghostClosed;
    float blinkTimer = 3f, blinkUntil;
    RectTransform[] sparkles;
    Text splashText;
    Text[] creditTexts;

    // end credits (after the epilogue)
    GameObject endPanel;
    RectTransform endScroll;
    Text endTheEnd, endPress;
    float endHeight, endPhaseTime, revealT;
    int endPhase;
    Vector2 logoBase = new Vector2(-150f, 260f), ghostBase = new Vector2(610f, 255f);

    // menus
    readonly List<Btn> titleBtns = new List<Btn>();
    readonly List<Btn> pauseBtns = new List<Btn>();
    readonly List<Btn> settingsBtns = new List<Btn>();
    readonly List<Btn> creditsBtns = new List<Btn>();
    readonly List<Slid> sliders = new List<Slid>();
    Btn[] diffBtns;
    Btn shakeBtn, fullBtn;
    Text blurb;

    public static TitleScreen Create(WorldGenerator w)
    {
        if (Instance != null) return Instance;
        var t = new GameObject("Title Screen").AddComponent<TitleScreen>();
        t.Init(w);
        return t;
    }

    // ================================================================== build

    void Init(WorldGenerator w)
    {
        Instance = this;
        world = w;
        GameSettings.Load();
        SoundFx.Ensure();
        MusicManager.Ensure();
        GameSettings.Apply();

        BuildCanvases();
        BuildBackground();
        BuildLogo();
        BuildTitleMenu();
        BuildSettings();
        BuildCredits();
        BuildEndCredits();
        BuildPause();
        BuildSplash();
        BuildOverlay();
        Go(State.Splash);
    }

    void BuildCanvases()
    {
        var cgo = new GameObject("Canvas");
        cgo.transform.SetParent(transform, false);
        var canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150;
        var scaler = cgo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        root = (RectTransform)cgo.transform;
    }

    void BuildOverlay()
    {
        // fade + brightness live on their own canvas above everything (including the in-game UI).
        var cgo = new GameObject("Overlay Canvas");
        cgo.transform.SetParent(transform, false);
        var canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;
        var scaler = cgo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        var b = NewUI("Brightness", cgo.transform);
        Stretch(b);
        brightnessImage = b.gameObject.AddComponent<Image>();
        brightnessImage.raycastTarget = false;
        brightnessImage.color = Color.clear;

        var f = NewUI("Fade", cgo.transform);
        Stretch(f);
        fadeImage = f.gameObject.AddComponent<Image>();
        fadeImage.raycastTarget = false;
        fadeImage.color = Color.clear;
    }

    // ----------------------------------------------------------------- background

    void BuildBackground()
    {
        var art = NewUI("Art", root);
        Stretch(art);
        artRoot = art.gameObject;

        // sky
        var sky = NewUI("Sky", art);
        Stretch(sky);
        var skyImg = sky.gameObject.AddComponent<Image>();
        skyImg.sprite = TitleArt.SkySprite();
        skyImg.raycastTarget = false;

        // stars
        var rng = new System.Random(11);
        for (int i = 0; i < 120; i++)
        {
            var rt = NewUI("Star", art);
            float size = 3f + (float)rng.NextDouble() * 6f;
            Place(rt, new Vector2(Rnd(rng, -1500, 1500), Rnd(rng, -40, 560)), new Vector2(size, size));
            var img = rt.gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            float pick = (float)rng.NextDouble();
            Color c = pick < 0.6f ? Color.white : (pick < 0.82f ? new Color(1f, 0.8f, 0.92f) : new Color(0.75f, 0.85f, 1f));
            img.color = c;
            stars.Add(new Star { img = img, baseA = 0.4f + (float)rng.NextDouble() * 0.6f, speed = 0.6f + (float)rng.NextDouble() * 2.2f, phase = (float)rng.NextDouble() * 6.28f });
        }

        // clouds
        for (int i = 0; i < 5; i++)
        {
            var rt = NewUI("Cloud", art);
            Place(rt, new Vector2(Rnd(rng, -1400, 1400), Rnd(rng, 80, 440)), new Vector2(Rnd(rng, 700, 1100), Rnd(rng, 140, 240)));
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = PixelArt.Glow();
            img.raycastTarget = false;
            img.color = i % 2 == 0 ? new Color(1f, 0.65f, 0.85f, 0.11f) : new Color(0.7f, 0.6f, 1f, 0.12f);
            clouds.Add(new Mover { rt = rt, speed = Rnd(rng, 6, 16) });
        }

        // moon rising behind the hills
        moonHalo = NewUI("Moon Halo", art);
        Place(moonHalo, new Vector2(0, -150), new Vector2(1700, 1700));
        var halo = moonHalo.gameObject.AddComponent<Image>();
        halo.sprite = PixelArt.Glow();
        halo.raycastTarget = false;
        halo.color = new Color(1f, 0.82f, 0.85f, 0.4f);

        moon = NewUI("Moon", art);
        Place(moon, new Vector2(0, -150), new Vector2(700, 700));
        var moonImg = moon.gameObject.AddComponent<Image>();
        moonImg.sprite = TitleArt.MoonSprite();
        moonImg.raycastTarget = false;
        moonImg.color = new Color(1f, 0.96f, 0.9f, 0.95f);

        // whales drifting through the sky ("the fish in the sky")
        if (world != null && world.whaleSprite != null)
        {
            for (int i = 0; i < 3; i++)
            {
                var rt = NewUI("Sky Whale", art);
                float w = Rnd(rng, 300, 520);
                Place(rt, new Vector2(Rnd(rng, -1200, 1200), Rnd(rng, 120, 430)), new Vector2(w, w));
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = world.whaleSprite;
                img.preserveAspect = true;
                img.raycastTarget = false;
                img.color = new Color(0.8f, 0.72f, 1f, 0.3f);
                float dir = i % 2 == 0 ? 1f : -1f;
                rt.localScale = new Vector3(dir, 1f, 1f);
                whales.Add(new Mover { rt = rt, speed = dir * Rnd(rng, 14, 26), baseY = rt.anchoredPosition.y, phase = (float)rng.NextDouble() * 6.28f, amp = Rnd(rng, 18, 40) });
            }
        }

        // hills: three parallax layers, each drawn as three copies so they scroll forever
        BuildHillLayer(art, 21, new Color(0.34f, 0.18f, 0.46f), 520f, 0.55f, 0.1f, 6f, 0, rng);
        BuildHillLayer(art, 22, new Color(0.17f, 0.09f, 0.3f), 440f, 0.5f, 0.12f, 14f, 1, rng);
        BuildHillLayer(art, 23, new Color(0.07f, 0.04f, 0.16f), 330f, 0.42f, 0.14f, 30f, 2, rng);

        // hearts floating up, fireflies
        for (int i = 0; i < 26; i++)
        {
            var rt = NewUI("Heart", art);
            float s = Rnd(rng, 22, 44);
            Place(rt, new Vector2(Rnd(rng, -1100, 1100), Rnd(rng, -540, 540)), new Vector2(s, s * 0.9f));
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = HeartSprites.Full;
            img.raycastTarget = false;
            img.color = new Color(1f, Rnd(rng, 0.7f, 1f), Rnd(rng, 0.8f, 1f), Rnd(rng, 0.25f, 0.55f));
            hearts.Add(new Mover { rt = rt, speed = Rnd(rng, 18, 44), baseY = rt.anchoredPosition.x, phase = (float)rng.NextDouble() * 6.28f, amp = Rnd(rng, 14, 40) });
        }
        for (int i = 0; i < 32; i++)
        {
            var rt = NewUI("Firefly", art);
            Place(rt, new Vector2(Rnd(rng, -1000, 1000), Rnd(rng, -520, 120)), new Vector2(34, 34));
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = PixelArt.Glow();
            img.raycastTarget = false;
            img.color = new Color(1f, 0.95f, 0.6f, 0.7f);
            flies.Add(new Mover { rt = rt, speed = Rnd(rng, 0.2f, 0.7f), baseY = rt.anchoredPosition.y, phase = (float)rng.NextDouble() * 6.28f, amp = Rnd(rng, 20, 60) });
        }

        // vignette + film grain
        var vig = NewUI("Vignette", art);
        Stretch(vig);
        var vigImg = vig.gameObject.AddComponent<Image>();
        vigImg.sprite = TitleArt.VignetteSprite();
        vigImg.raycastTarget = false;

        var gr = NewUI("Grain", art);
        Stretch(gr);
        grain = gr.gameObject.AddComponent<RawImage>();
        grain.texture = TitleArt.NoiseTexture();
        grain.uvRect = new Rect(0, 0, 15f, 8.5f);
        grain.color = new Color(1f, 1f, 1f, 0.05f);
        grain.raycastTarget = false;
    }

    void BuildHillLayer(RectTransform parent, int seed, Color tint, float height, float baseH, float amp, float speed, int propLevel, System.Random rng)
    {
        const float width = 1800f;
        Sprite hill = TitleArt.HillSprite(seed, baseH, amp);
        var layer = new Layer { copies = new RectTransform[3], speed = speed, width = width };

        for (int c = 0; c < 3; c++)
        {
            var rt = NewUI("Hills", parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(c * width, 0f);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = hill;
            img.color = tint;
            img.raycastTarget = false;
            layer.copies[c] = rt;

            if (propLevel > 0 && world != null)
            {
                var propRng = new System.Random(seed * 31);   // identical props on every copy so the loop is seamless
                AddProps(rt, seed, baseH, amp, height, tint, propLevel, propRng, c == 0);
            }
        }
        layers.Add(layer);
    }

    void AddProps(RectTransform hillRt, int seed, float baseH, float amp, float height, Color tint, int level, System.Random prng, bool registerBlink)
    {
        Color dark = new Color(tint.r * 0.75f, tint.g * 0.75f, tint.b * 0.75f, 1f);

        int trees = level == 1 ? 9 : 12;
        for (int i = 0; i < trees && world.treeSprite != null; i++)
        {
            float xn = (float)prng.NextDouble();
            float y = TitleArt.HillHeightAt(seed, baseH, amp, xn) * height - 6f;
            var rt = NewUI("Tree", hillRt);
            float th = Rnd(prng, 150, level == 1 ? 240 : 300);
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(xn * 1800f, y);
            rt.sizeDelta = new Vector2(th, th);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = world.treeSprite;
            img.preserveAspect = true;
            img.color = dark;
            img.raycastTarget = false;
        }

        if (level == 1 && world != null)
        {
            // a small house with warm lit windows
            float xn = 0.62f;
            float y = TitleArt.HillHeightAt(seed, baseH, amp, xn) * height - 4f;
            var rt = NewUI("House", hillRt);
            float s = 2.6f;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(xn * 1800f, y);
            rt.sizeDelta = new Vector2(96f * s, 80f * s);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = PixelArt.House();
            img.color = new Color(0.1f, 0.06f, 0.2f, 1f);
            img.raycastTarget = false;
            foreach (float wx in new[] { 16f, 66f })
            {
                var w = NewUI("Window", rt);
                w.anchorMin = w.anchorMax = Vector2.zero;
                w.pivot = Vector2.zero;
                w.anchoredPosition = new Vector2(wx * s, 18f * s);
                w.sizeDelta = new Vector2(15f * s, 15f * s);
                var wi = w.gameObject.AddComponent<Image>();
                wi.color = new Color(1f, 0.82f, 0.45f, 0.95f);
                wi.raycastTarget = false;
            }
        }

        if (level == 2)
        {
            // silhouettes of a fox and a cat with eyes that blink in the dark
            AddCreature(hillRt, world.foxSprite, 0.22f, 150f, seed, baseH, amp, height, registerBlink);
            AddCreature(hillRt, world.catWhiteSprite, 0.74f, 110f, seed, baseH, amp, height, registerBlink);
        }
    }

    void AddCreature(RectTransform hillRt, Sprite sprite, float xn, float size, int seed, float baseH, float amp, float height, bool registerBlink)
    {
        if (sprite == null) return;
        float y = TitleArt.HillHeightAt(seed, baseH, amp, xn) * height - 4f;
        var rt = NewUI("Creature", hillRt);
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(xn * 1800f, y);
        rt.sizeDelta = new Vector2(size, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.color = new Color(0.04f, 0.025f, 0.09f, 1f);
        img.raycastTarget = false;

        var eyes = new Image[2];
        for (int e = 0; e < 2; e++)
        {
            var er = NewUI("Eye", rt);
            er.anchorMin = er.anchorMax = new Vector2(0.5f, 0.5f);
            er.sizeDelta = new Vector2(8f, 8f);
            er.anchoredPosition = new Vector2(size * (0.2f + e * 0.12f), size * 0.12f);
            var ei = er.gameObject.AddComponent<Image>();
            ei.color = new Color(1f, 0.9f, 0.4f, 0.9f);
            ei.raycastTarget = false;
            eyes[e] = ei;
        }
        if (registerBlink) blinkers.Add(new Blinker { eyes = eyes, next = Random.Range(1f, 4f) });
        else foreach (var e in eyes) e.enabled = false;
    }

    // ----------------------------------------------------------------- logo

    void BuildLogo()
    {
        var lr = NewUI("Logo Root", root);
        Stretch(lr);
        logoRoot = lr.gameObject;

        logoBob = NewUI("Logo Bob", lr);
        Place(logoBob, new Vector2(-150f, 260f), new Vector2(10, 10));

        // the mask grows left-to-right so the name looks like it is being written
        logoMask = NewUI("Reveal Mask", logoBob);
        logoMask.anchorMin = logoMask.anchorMax = new Vector2(0.5f, 0.5f);
        logoMask.pivot = new Vector2(0f, 0.5f);
        logoMask.anchoredPosition = new Vector2(-620f, 0f);
        logoMask.sizeDelta = new Vector2(0f, 420f);
        logoMask.gameObject.AddComponent<RectMask2D>();

        Sprite logo = TitleArt.HandwrittenSprite("Merimna");
        logoShadowA = MakeLogoLayer(logoMask, logo, new Color(1f, 0.35f, 0.6f, 0.0f));
        logoShadowB = MakeLogoLayer(logoMask, logo, new Color(0.4f, 0.7f, 1f, 0.0f));
        logoMain = MakeLogoLayer(logoMask, logo, Color.white);

        ghostOpen = TitleArt.GhostSprite(false);
        ghostClosed = TitleArt.GhostSprite(true);
        var gr = NewUI("Ghost", lr);
        Place(gr, new Vector2(610f, 255f), new Vector2(320f, 320f));
        ghostRt = gr;
        ghostImage = gr.gameObject.AddComponent<Image>();
        ghostImage.sprite = ghostOpen;
        ghostImage.raycastTarget = false;

        sparkles = new RectTransform[5];
        for (int i = 0; i < sparkles.Length; i++)
        {
            var s = NewUI("Sparkle", lr);
            Place(s, Vector2.zero, new Vector2(46, 46));
            var si = s.gameObject.AddComponent<Image>();
            si.sprite = PixelArt.Glow();
            si.raycastTarget = false;
            si.color = new Color(1f, 0.9f, 1f, 0.8f);
            sparkles[i] = s;
        }
    }

    Image MakeLogoLayer(RectTransform parent, Sprite s, Color c)
    {
        var rt = NewUI("Logo", parent);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, 0f);
        rt.sizeDelta = new Vector2(1240f, 372f);        // 1400x420 sprite scaled to 0.886
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = s;
        img.color = c;
        img.raycastTarget = false;
        return img;
    }

    // ----------------------------------------------------------------- menus

    void BuildTitleMenu()
    {
        var p = NewUI("Title Menu", root);
        Stretch(p);
        titlePanel = p.gameObject;

        titleBtns.Add(MakeButton(p, "Play", new Vector2(0, -70), new Vector2(520, 96), 54, StartGame));
        titleBtns.Add(MakeButton(p, "Settings", new Vector2(0, -185), new Vector2(520, 96), 54, () => OpenSettings(State.Title)));
        titleBtns.Add(MakeButton(p, "Credits", new Vector2(0, -300), new Vector2(520, 96), 54, () => Go(State.Credits)));

        var tag = MakeText(p, "everything is fine. the walls said so.", 30, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.55f));
        Place(tag.rectTransform, new Vector2(0, -430), new Vector2(1000, 50));

        var greek = MakeText(p, "μέριμνα  ·  anxiety, care", 30, TextAnchor.MiddleLeft, new Color(1f, 0.85f, 0.95f, 0.65f));
        Place(greek.rectTransform, new Vector2(-780, -490), new Vector2(700, 50));
    }

    void BuildSettings()
    {
        var panel = NewUI("Settings", root);
        Stretch(panel);
        settingsPanel = panel.gameObject;

        var frame = MakeFrame(panel, new Vector2(0, 0), new Vector2(1180, 980), 0.9f);

        var title = MakeText(frame, "Settings", 80, TextAnchor.MiddleCenter, Color.white);
        Place(title.rectTransform, new Vector2(0, 420), new Vector2(800, 110));

        // difficulty
        Row(frame, "Difficulty", 320);
        diffBtns = new Btn[3];
        string[] names = { "Gentle", "Normal", "Nightmare" };
        for (int i = 0; i < 3; i++)
        {
            int level = i;
            diffBtns[i] = MakeButton(frame, names[i], new Vector2(30 + i * 212, 320), new Vector2(200, 70), 34, () =>
            {
                GameSettings.Level = (GameSettings.Difficulty)level;
                GameSettings.Save();
                RefreshSettings();
            }, false);
            settingsBtns.Add(diffBtns[i]);
        }
        blurb = MakeText(frame, "", 30, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.95f, 0.8f));
        Place(blurb.rectTransform, new Vector2(0, 255), new Vector2(1000, 44));

        // sliders
        Row(frame, "Brightness", 170);
        MakeSlider(frame, new Vector2(170, 170), 0.6f, 1.4f, () => GameSettings.Brightness, v => GameSettings.Brightness = v, v => Mathf.RoundToInt(v * 100f) + "%");
        Row(frame, "Music", 80);
        MakeSlider(frame, new Vector2(170, 80), 0f, 1f, () => GameSettings.Music, v => { GameSettings.Music = v; GameSettings.Apply(); }, v => Mathf.RoundToInt(v * 100f) + "%");
        Row(frame, "Sound effects", -10);
        MakeSlider(frame, new Vector2(170, -10), 0f, 1f, () => GameSettings.Sfx, v => { GameSettings.Sfx = v; GameSettings.Apply(); }, v => Mathf.RoundToInt(v * 100f) + "%");
        Row(frame, "Text speed", -100);
        MakeSlider(frame, new Vector2(170, -100), 0.5f, 2f, () => GameSettings.TextSpeed, v => { GameSettings.TextSpeed = v; GameSettings.Apply(); }, v => v.ToString("0.0") + "x");

        // toggles
        Row(frame, "Screen shake", -190);
        shakeBtn = MakeButton(frame, "On", new Vector2(170, -190), new Vector2(260, 70), 34, () =>
        {
            GameSettings.ScreenShake = !GameSettings.ScreenShake;
            GameSettings.Save();
            RefreshSettings();
        }, false);
        settingsBtns.Add(shakeBtn);

        Row(frame, "Fullscreen", -280);
        fullBtn = MakeButton(frame, "Enter", new Vector2(170, -280), new Vector2(260, 70), 34, () =>
        {
            Screen.fullScreen = !Screen.fullScreen;
            RefreshSettings();
        }, false);
        settingsBtns.Add(fullBtn);

        var back = MakeButton(frame, "Back", new Vector2(0, -400), new Vector2(360, 84), 46, BackFromSettings);
        settingsBtns.Add(back);
    }

    void BuildCredits()
    {
        var panel = NewUI("Credits", root);
        Stretch(panel);
        creditsPanel = panel.gameObject;

        var dim = NewUI("Dim", panel);
        Stretch(dim);
        var di = dim.gameObject.AddComponent<Image>();
        di.color = new Color(0.02f, 0f, 0.07f, 0.5f);
        di.raycastTarget = false;

        var heading = MakeText(panel, "Credits", 96, TextAnchor.MiddleCenter, Color.white);
        Place(heading.rectTransform, new Vector2(0, 300), new Vector2(900, 130));

        var by = MakeText(panel, "made with care by", 38, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.95f, 0.8f));
        Place(by.rectTransform, new Vector2(0, 190), new Vector2(900, 60));

        var n1 = MakeText(panel, "Biri Zimmerfrau", 104, TextAnchor.MiddleCenter, Color.white);
        Place(n1.rectTransform, new Vector2(0, 80), new Vector2(1400, 140));
        var heart = NewUI("Heart", panel);
        Place(heart, new Vector2(0, -20), new Vector2(54, 48));
        var hi = heart.gameObject.AddComponent<Image>();
        hi.sprite = HeartSprites.Full;
        hi.raycastTarget = false;
        var n2 = MakeText(panel, "Iris Zimmerfrau", 104, TextAnchor.MiddleCenter, Color.white);
        Place(n2.rectTransform, new Vector2(0, -120), new Vector2(1400, 140));

        var thanks = MakeText(panel, "thank you for staying a while.", 36, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.6f));
        Place(thanks.rectTransform, new Vector2(0, -240), new Vector2(1000, 60));

        creditTexts = new[] { heading, by, n1, n2, thanks };

        creditsBtns.Add(MakeButton(panel, "Back", new Vector2(0, -380), new Vector2(360, 84), 46, () => Go(State.Title)));
    }

    void BuildEndCredits()
    {
        var panel = NewUI("End Credits", root);
        Stretch(panel);
        endPanel = panel.gameObject;

        var dim = NewUI("Dim", panel);
        Stretch(dim);
        var di = dim.gameObject.AddComponent<Image>();
        di.color = new Color(0.02f, 0f, 0.07f, 0.5f);
        di.raycastTarget = false;

        endScroll = NewUI("Scroll", panel);
        Place(endScroll, Vector2.zero, new Vector2(1400, 10));

        // text, size, alpha, gap after
        object[][] items =
        {
            new object[] { "Merimna", 130, 1f, 130f },
            new object[] { "created by", 42, 0.75f, 70f },
            new object[] { "Biri Zimmerfrau", 94, 1f, 110f },
            new object[] { "Iris Zimmerfrau", 94, 1f, 190f },
            new object[] { "music, sound effects and every pixel", 42, 0.7f, 60f },
            new object[] { "were made in code", 42, 0.7f, 190f },
            new object[] { "built with Unity", 40, 0.6f, 190f },
            new object[] { "thank you for staying a while.", 66, 1f, 0f },
        };

        float cursor = 0f;
        foreach (object[] it in items)
        {
            int size = (int)it[1];
            var t = MakeText(endScroll, (string)it[0], size, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, (float)it[2]));
            Place(t.rectTransform, new Vector2(0f, -cursor), new Vector2(1400, size * 1.4f));
            cursor += (float)it[3] + size * 0.9f;
        }
        endHeight = cursor;

        endTheEnd = MakeText(panel, "The End", 84, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0f));
        Place(endTheEnd.rectTransform, new Vector2(0f, -210f), new Vector2(900, 120));
        endPress = MakeText(panel, "press any key", 34, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0f));
        Place(endPress.rectTransform, new Vector2(0f, -320f), new Vector2(900, 60));
    }

    /// <summary>Called by the epilogue once Merimna has walked out of the bedroom.</summary>
    public void BeginEndCredits() { Go(State.EndCredits); }

    void BuildPause()
    {
        var dim = NewUI("Pause Dim", root);
        Stretch(dim);
        var di = dim.gameObject.AddComponent<Image>();
        di.color = new Color(0f, 0f, 0.04f, 0.6f);
        dimPanel = dim.gameObject;

        var panel = NewUI("Pause", root);
        Stretch(panel);
        pausePanel = panel.gameObject;
        var frame = MakeFrame(panel, Vector2.zero, new Vector2(700, 640), 0.92f);
        var t = MakeText(frame, "Paused", 80, TextAnchor.MiddleCenter, Color.white);
        Place(t.rectTransform, new Vector2(0, 220), new Vector2(600, 110));

        pauseBtns.Add(MakeButton(frame, "Resume", new Vector2(0, 80), new Vector2(460, 90), 50, Resume));
        pauseBtns.Add(MakeButton(frame, "Settings", new Vector2(0, -35), new Vector2(460, 90), 50, () => OpenSettings(State.Paused)));
        pauseBtns.Add(MakeButton(frame, "Title Screen", new Vector2(0, -150), new Vector2(460, 90), 50, () => Go(State.Title)));
    }

    void BuildSplash()
    {
        var s = NewUI("Splash", root);
        Stretch(s);
        splashPanel = s.gameObject;
        var bg = s.gameObject.AddComponent<Image>();
        bg.color = new Color(0.02f, 0.01f, 0.05f, 1f);

        var g = NewUI("Ghost", s);
        Place(g, new Vector2(0, 90), new Vector2(180, 180));
        var gi = g.gameObject.AddComponent<Image>();
        gi.sprite = ghostOpen != null ? ghostOpen : TitleArt.GhostSprite(false);
        gi.color = new Color(1f, 1f, 1f, 0.55f);
        gi.raycastTarget = false;

        splashText = MakeText(s, "click anywhere to wake up", 44, TextAnchor.MiddleCenter, Color.white);
        Place(splashText.rectTransform, new Vector2(0, -90), new Vector2(1000, 70));
    }

    // ----------------------------------------------------------------- widget helpers

    static RectTransform NewUI(string n, Transform parent)
    {
        var go = new GameObject(n, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static void Place(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static float Rnd(System.Random r, float a, float b) { return a + (float)r.NextDouble() * (b - a); }

    static Text MakeText(Transform parent, string s, int size, TextAnchor anchor, Color color)
    {
        var rt = NewUI("Text", parent);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = GameUI.GetFont();
        t.text = s;
        t.fontSize = size;
        t.alignment = anchor;
        t.color = color;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        var o = rt.gameObject.AddComponent<Outline>();
        o.effectColor = new Color(0f, 0f, 0f, 0.8f);
        o.effectDistance = new Vector2(2, -2);
        return t;
    }

    RectTransform MakeFrame(Transform parent, Vector2 pos, Vector2 size, float alpha)
    {
        var outer = NewUI("Frame", parent);
        Place(outer, pos, size);
        var oi = outer.gameObject.AddComponent<Image>();
        oi.color = Color.white;
        oi.raycastTarget = false;
        var inner = NewUI("Inner", outer);
        Stretch(inner);
        inner.offsetMin = new Vector2(6, 6); inner.offsetMax = new Vector2(-6, -6);
        var ii = inner.gameObject.AddComponent<Image>();
        ii.color = new Color(0.02f, 0.01f, 0.06f, alpha);
        ii.raycastTarget = false;
        return outer;
    }

    void Row(RectTransform parent, string label, float y)
    {
        var t = MakeText(parent, label, 42, TextAnchor.MiddleLeft, Color.white);
        Place(t.rectTransform, new Vector2(-300, y), new Vector2(500, 60));
    }

    Btn MakeButton(Transform parent, string text, Vector2 pos, Vector2 size, int fontSize, System.Action click, bool withCursor = true)
    {
        var rt = NewUI("Button", parent);
        Place(rt, pos, size);
        var b = new Btn { rt = rt, onClick = click };
        b.frame = rt.gameObject.AddComponent<Image>();
        b.frame.color = Color.white;
        b.frame.raycastTarget = false;

        var inner = NewUI("Fill", rt);
        Stretch(inner);
        inner.offsetMin = new Vector2(5, 5); inner.offsetMax = new Vector2(-5, -5);
        b.fill = inner.gameObject.AddComponent<Image>();
        b.fill.color = new Color(0f, 0f, 0f, 0.85f);
        b.fill.raycastTarget = false;

        b.label = MakeText(rt, text, fontSize, TextAnchor.MiddleCenter, Color.white);
        Stretch(b.label.rectTransform);
        b.label.GetComponent<Outline>().effectColor = Color.clear;

        if (withCursor)
        {
            var c = NewUI("Cursor", rt);
            Place(c, new Vector2(-size.x / 2f - 54f, 0f), new Vector2(46, 42));
            b.cursor = c.gameObject.AddComponent<Image>();
            b.cursor.sprite = HeartSprites.Full;
            b.cursor.raycastTarget = false;
            b.cursor.color = new Color(1f, 1f, 1f, 0f);
        }
        return b;
    }

    void MakeSlider(RectTransform parent, Vector2 pos, float min, float max, System.Func<float> get, System.Action<float> set, System.Func<float, string> fmt)
    {
        const float W = 440f;
        var s = new Slid { min = min, max = max, get = get, set = set, format = fmt };

        var track = NewUI("Slider", parent);
        Place(track, pos, new Vector2(W, 26));
        var ti = track.gameObject.AddComponent<Image>();
        ti.color = Color.white;
        ti.raycastTarget = false;
        s.track = track;

        var inner = NewUI("Inner", track);
        Stretch(inner);
        inner.offsetMin = new Vector2(3, 3); inner.offsetMax = new Vector2(-3, -3);
        inner.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.9f);

        var fill = NewUI("Fill", inner);
        fill.anchorMin = new Vector2(0, 0); fill.anchorMax = new Vector2(0, 1);
        fill.pivot = new Vector2(0, 0.5f);
        fill.offsetMin = Vector2.zero; fill.offsetMax = Vector2.zero;
        fill.sizeDelta = new Vector2(100, 0);
        fill.gameObject.AddComponent<Image>().color = new Color(1f, 0.72f, 0.88f, 1f);
        s.fill = fill;

        var knob = NewUI("Knob", track);
        knob.anchorMin = knob.anchorMax = new Vector2(0, 0.5f);
        knob.pivot = new Vector2(0.5f, 0.5f);
        knob.sizeDelta = new Vector2(48, 44);
        var ki = knob.gameObject.AddComponent<Image>();
        ki.sprite = HeartSprites.Full;
        ki.raycastTarget = false;
        s.knob = knob;

        s.valueText = MakeText(parent, "", 36, TextAnchor.MiddleLeft, Color.white);
        Place(s.valueText.rectTransform, new Vector2(pos.x + W / 2f + 105f, pos.y), new Vector2(180, 50));
        sliders.Add(s);
    }

    // ================================================================== flow

    void Go(State s)
    {
        State previous = state;
        state = s;
        stateTime = 0f;
        selected = -1;

        bool fromPause = settingsFrom == State.Paused;
        bool artOn = s == State.Title || s == State.Credits || s == State.Loading || s == State.EndCredits || (s == State.Settings && !fromPause);

        artRoot.SetActive(artOn);
        logoRoot.SetActive(s == State.Title || s == State.Loading);
        endPanel.SetActive(s == State.EndCredits);
        if (s == State.EndCredits)
        {
            endPhase = 0;
            endPhaseTime = 0f;
            endScroll.anchoredPosition = new Vector2(0f, -650f);
            endScroll.gameObject.SetActive(true);
            endTheEnd.color = new Color(1f, 1f, 1f, 0f);
            endPress.color = new Color(1f, 1f, 1f, 0f);
        }
        if (s == State.Title || s == State.Loading) { logoBase = new Vector2(-150f, 260f); ghostBase = new Vector2(610f, 255f); }
        titlePanel.SetActive(s == State.Title);
        settingsPanel.SetActive(s == State.Settings);
        creditsPanel.SetActive(s == State.Credits);
        dimPanel.SetActive(s == State.Paused || (s == State.Settings && fromPause));
        pausePanel.SetActive(s == State.Paused);
        splashPanel.SetActive(s == State.Splash);

        MusicManager.TitleActive = artOn || s == State.Splash;
        MusicManager.Paused = s == State.Paused || (s == State.Settings && fromPause);
        Time.timeScale = (s == State.Playing) ? 1f : 0f;

        if (s == State.Title && previous != State.Settings) SoundFx.Play("page");
        if (s == State.Credits)
            foreach (var t in creditTexts) t.color = new Color(t.color.r, t.color.g, t.color.b, 0f);
        if (s == State.Settings) RefreshSettings();
    }

    void OpenSettings(State from)
    {
        settingsFrom = from;
        Go(State.Settings);
    }

    void BackFromSettings()
    {
        State back = settingsFrom;
        Go(back);
    }

    void StartGame()
    {
        loadPhase = 0;
        Go(State.Loading);
        Time.timeScale = 0f;
    }

    void Resume() { Go(State.Playing); }

    // ================================================================== update

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float t = Time.unscaledTime;
        stateTime += dt;

        if (artRoot.activeSelf) AnimateBackground(dt, t);
        revealT = state == State.EndCredits ? endPhaseTime : stateTime;
        if (logoRoot.activeSelf) AnimateLogo(dt, t);

        // brightness overlay
        float b = GameSettings.Brightness;
        brightnessImage.color = b < 1f ? new Color(0f, 0f, 0f, (1f - b) * 1.1f) : new Color(1f, 1f, 1f, (b - 1f) * 0.4f);

        switch (state)
        {
            case State.Splash:
                splashText.color = new Color(1f, 1f, 1f, 0.35f + 0.45f * (0.5f + 0.5f * Mathf.Sin(t * 2f)));
                if (stateTime > 0.5f && GameInput.AnyPressed()) { SoundFx.Play("menu_select"); Go(State.Title); }
                break;

            case State.Title:
                UpdateButtons(titleBtns, true);
                break;

            case State.Settings:
                UpdateButtons(settingsBtns, false);
                UpdateSliders();
                if (GameInput.EscapePressed()) BackFromSettings();
                break;

            case State.Credits:
                UpdateButtons(creditsBtns, false);
                for (int i = 0; i < creditTexts.Length; i++)
                {
                    float a = Mathf.Clamp01((stateTime - 0.4f - i * 0.45f) / 0.9f);
                    var c = creditTexts[i].color;
                    float baseA = (i == 1 || i == 4) ? 0.7f : 1f;
                    creditTexts[i].color = new Color(c.r, c.g, c.b, a * baseA);
                }
                if (GameInput.EscapePressed()) Go(State.Title);
                break;

            case State.EndCredits:
                endPhaseTime += dt;
                if (endPhase == 0)
                {
                    endScroll.anchoredPosition += new Vector2(0f, 105f * dt);
                    if (endScroll.anchoredPosition.y > endHeight + 120f || (endPhaseTime > 1f && GameInput.EscapePressed()))
                    {
                        // credits done: the logo is written out one last time
                        endPhase = 1;
                        endPhaseTime = 0f;
                        endScroll.gameObject.SetActive(false);
                        logoBase = new Vector2(-150f, 110f);
                        ghostBase = new Vector2(610f, 105f);
                        logoRoot.SetActive(true);
                        SoundFx.Play("page");
                    }
                }
                else
                {
                    endTheEnd.color = new Color(1f, 1f, 1f, Mathf.Clamp01((endPhaseTime - 3f) / 1.5f));
                    endPress.color = new Color(1f, 1f, 1f, endPhaseTime > 5f ? 0.35f + 0.35f * Mathf.Sin(t * 2.5f) : 0f);
                    if (endPhaseTime > 5f && GameInput.AnyPressed()) { SoundFx.Play("menu_select"); Go(State.Title); }
                }
                break;

            case State.Loading:
                UpdateLoading(dt);
                break;

            case State.Playing:
                if (GameInput.EscapePressed() && !Epilogue.Active && !(GameUI.Instance != null && GameUI.Instance.ModalOpen))
                {
                    Go(State.Paused);
                    SoundFx.Play("tick");
                }
                break;

            case State.Paused:
                UpdateButtons(pauseBtns, true);
                if (GameInput.EscapePressed()) Resume();
                break;
        }
    }

    void UpdateLoading(float dt)
    {
        if (loadPhase == 0)
        {
            fade = Mathf.MoveTowards(fade, 1f, dt / 0.9f);
            if (fade >= 1f) loadPhase = 1;
        }
        else if (loadPhase == 1)
        {
            // fully black: build the world (this takes a moment)
            artRoot.SetActive(false);
            logoRoot.SetActive(false);
            titlePanel.SetActive(false);
            MusicManager.TitleActive = false;
            Time.timeScale = 1f;
            if (world != null) world.Generate();
            GameSettings.Apply();
            if (GameUI.Instance != null)
                GameUI.Instance.Toast("WASD move  ·  Shift run  ·  click foxes  ·  E interact  ·  Esc pause", 7f);
            loadPhase = 2;
        }
        else
        {
            fade = Mathf.MoveTowards(fade, 0f, dt / 0.9f);
            if (fade <= 0f) Go(State.Playing);
        }
        fadeImage.color = new Color(0f, 0f, 0f, fade);
    }

    // ---------------------------------------------------------------- buttons & sliders

    void UpdateButtons(List<Btn> list, bool keyboard)
    {
        Vector2 m = GameInput.MouseScreen();
        bool moved = (m - lastMouse).sqrMagnitude > 1f;
        lastMouse = m;

        int hovered = -1;
        for (int i = 0; i < list.Count; i++)
            if (list[i].rt.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(list[i].rt, m, null)) hovered = i;

        if (keyboard)
        {
            int old = selected;
            if (moved && hovered >= 0) selected = hovered;
            if (GameInput.DownPressed()) selected = (selected + 1) % list.Count;
            if (GameInput.UpPressed()) selected = selected <= 0 ? list.Count - 1 : selected - 1;
            if (selected < 0 && stateTime > 0.2f) selected = 0;
            if (selected != old && old != -1) SoundFx.Play("tick");
        }
        else
        {
            if (hovered != selected && hovered >= 0) SoundFx.Play("tick");
            selected = hovered;
        }

        if (GameInput.AttackPressed() && hovered >= 0) { selected = hovered; Activate(list[hovered]); }
        else if (keyboard && GameInput.ConfirmPressed() && selected >= 0) Activate(list[selected]);

        float dt = Time.unscaledDeltaTime;
        for (int i = 0; i < list.Count; i++)
        {
            Btn b = list[i];
            bool hot = (i == selected) || b.toggled;
            b.anim = Mathf.MoveTowards(b.anim, hot ? 1f : 0f, dt * 9f);
            b.fill.color = Color.Lerp(new Color(0f, 0f, 0f, 0.85f), Color.white, b.anim);
            b.label.color = Color.Lerp(Color.white, new Color(0.05f, 0.02f, 0.1f), b.anim);
            b.rt.localScale = Vector3.one * (1f + 0.04f * (i == selected ? 1f : 0f));
            if (b.cursor != null) b.cursor.color = new Color(1f, 1f, 1f, (i == selected) ? 1f : 0f);
        }
    }

    void Activate(Btn b)
    {
        SoundFx.Play("menu_select");
        if (b.onClick != null) b.onClick();
    }

    void UpdateSliders()
    {
        Vector2 m = GameInput.MouseScreen();
        foreach (Slid s in sliders)
        {
            Rect r = s.track.rect;
            if (GameInput.AttackPressed())
            {
                Vector2 local;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(s.track, m, null, out local);
                if (local.x > r.xMin - 30f && local.x < r.xMax + 30f && Mathf.Abs(local.y) < 34f) s.dragging = true;
            }
            if (s.dragging)
            {
                if (GameInput.MouseHeld())
                {
                    Vector2 local;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(s.track, m, null, out local);
                    float k = Mathf.Clamp01((local.x - r.xMin) / r.width);
                    s.set(Mathf.Lerp(s.min, s.max, k));
                }
                else { s.dragging = false; GameSettings.Save(); SoundFx.Play("tick"); }
            }

            float v = s.get();
            float t = Mathf.InverseLerp(s.min, s.max, v);
            s.fill.sizeDelta = new Vector2((r.width - 6f) * t, 0f);
            s.knob.anchoredPosition = new Vector2(3f + (r.width - 6f) * t, 0f);
            s.valueText.text = s.format(v);
        }
    }

    void RefreshSettings()
    {
        for (int i = 0; i < diffBtns.Length; i++) diffBtns[i].toggled = (int)GameSettings.Level == i;
        blurb.text = GameSettings.DifficultyBlurb();
        shakeBtn.label.text = GameSettings.ScreenShake ? "On" : "Off";
        shakeBtn.toggled = GameSettings.ScreenShake;
        fullBtn.label.text = Screen.fullScreen ? "Exit" : "Enter";
    }

    // ================================================================== animation

    void AnimateBackground(float dt, float t)
    {
        foreach (var s in stars)
            s.img.color = new Color(s.img.color.r, s.img.color.g, s.img.color.b, s.baseA * (0.45f + 0.55f * Mathf.Sin(t * s.speed + s.phase)));

        foreach (var c in clouds)
        {
            Vector2 p = c.rt.anchoredPosition;
            p.x += c.speed * dt;
            if (p.x > 1700f) p.x = -1700f;
            c.rt.anchoredPosition = p;
        }

        foreach (var w in whales)
        {
            Vector2 p = w.rt.anchoredPosition;
            p.x += w.speed * dt;
            p.y = w.baseY + Mathf.Sin(t * 0.4f + w.phase) * w.amp;
            if (p.x > 1400f) p.x = -1400f;
            if (p.x < -1400f) p.x = 1400f;
            w.rt.anchoredPosition = p;
            w.rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.5f + w.phase) * 3f);
        }

        foreach (var l in layers)
            for (int c = 0; c < l.copies.Length; c++)
            {
                float x = (c * l.width - (t * l.speed) % l.width);
                l.copies[c].anchoredPosition = new Vector2(x, 0f);
            }

        foreach (var h in hearts)
        {
            Vector2 p = h.rt.anchoredPosition;
            p.y += h.speed * dt;
            if (p.y > 600f) p.y = -600f;
            p.x = h.baseY + Mathf.Sin(t * 0.7f + h.phase) * h.amp;
            h.rt.anchoredPosition = p;
        }

        foreach (var f in flies)
        {
            f.rt.anchoredPosition = new Vector2(f.rt.anchoredPosition.x + Mathf.Cos(t * f.speed + f.phase) * 14f * dt,
                                                f.baseY + Mathf.Sin(t * f.speed * 1.3f + f.phase) * f.amp);
            var img = f.rt.GetComponent<Image>();
            img.color = new Color(1f, 0.95f, 0.6f, 0.25f + 0.55f * (0.5f + 0.5f * Mathf.Sin(t * 2.2f + f.phase * 3f)));
        }

        // moon breathes
        float pulse = 1f + 0.015f * Mathf.Sin(t * 0.6f);
        moon.localScale = Vector3.one * pulse;
        moonHalo.localScale = Vector3.one * (1f + 0.04f * Mathf.Sin(t * 0.45f));

        // eyes in the dark
        foreach (var bl in blinkers)
        {
            if (t > bl.next) { bl.until = t + 0.18f; bl.next = t + Random.Range(2f, 6f); }
            bool open = t > bl.until;
            foreach (var e in bl.eyes) e.color = new Color(1f, 0.9f, 0.4f, open ? 0.9f : 0f);
        }

        // film grain
        grainTimer -= dt;
        if (grainTimer <= 0f)
        {
            grainTimer = 0.06f;
            grain.uvRect = new Rect(Random.value, Random.value, 15f, 8.5f);
        }
    }

    void AnimateLogo(float dt, float t)
    {
        // the name is "written" left to right
        float reveal = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(revealT / 2.6f));
        logoMask.sizeDelta = new Vector2(Mathf.Lerp(0f, 1300f, reveal), 420f);

        logoBob.anchoredPosition = new Vector2(logoBase.x, logoBase.y + Mathf.Sin(t * 0.9f) * 7f);
        logoBob.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.55f) * 0.8f);

        // occasional glitch: coloured echoes split apart and the ink shivers
        bool glitch = (t % 7f) > 6.8f || (t % 11f) > 10.85f;
        float g = glitch ? 1f : 0f;
        float jitter = glitch ? Random.Range(-6f, 6f) : 0f;
        logoShadowA.color = new Color(1f, 0.35f, 0.6f, 0.18f + 0.45f * g);
        logoShadowB.color = new Color(0.4f, 0.7f, 1f, 0.15f + 0.45f * g);
        logoShadowA.rectTransform.anchoredPosition = new Vector2(-5f - 10f * g, 3f);
        logoShadowB.rectTransform.anchoredPosition = new Vector2(5f + 10f * g, -3f);
        logoMain.rectTransform.anchoredPosition = new Vector2(jitter, 0f);

        // the ghost: bobbing, tilting, blinking
        ghostRt.anchoredPosition = new Vector2(ghostBase.x + Mathf.Sin(t * 0.8f) * 12f, ghostBase.y + Mathf.Sin(t * 1.4f) * 18f);
        ghostRt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 1.1f) * 5f);
        blinkTimer -= dt;
        if (blinkTimer <= 0f) { blinkUntil = t + 0.14f; blinkTimer = Random.Range(2.5f, 6f); }
        ghostImage.sprite = t < blinkUntil ? ghostClosed : ghostOpen;

        for (int i = 0; i < sparkles.Length; i++)
        {
            float a = t * (0.7f + i * 0.13f) + i * 1.9f;
            float r = 190f + 25f * Mathf.Sin(t * 0.9f + i);
            sparkles[i].anchoredPosition = ghostRt.anchoredPosition + new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.75f);
            float s = 0.5f + 0.5f * Mathf.Sin(t * 2.5f + i * 2f);
            sparkles[i].localScale = Vector3.one * (0.4f + 0.7f * s);
        }
    }
}
