using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Procedurally drawn pixel hearts (no art needed).</summary>
public static class HeartSprites
{
    static Sprite full, half, empty;

    static readonly string[] Shape =
    {
        "..XX...XX..",
        ".XXXX.XXXX.",
        "XXXXXXXXXXX",
        "XXXXXXXXXXX",
        "XXXXXXXXXXX",
        ".XXXXXXXXX.",
        "..XXXXXXX..",
        "...XXXXX...",
        "....XXX....",
        ".....X.....",
    };

    public static Sprite Full => full != null ? full : full = Make(2);
    public static Sprite Half => half != null ? half : half = Make(1);
    public static Sprite Empty => empty != null ? empty : empty = Make(0);

    // fill: 2 = full, 1 = left half, 0 = empty
    static Sprite Make(int fill)
    {
        int w = Shape[0].Length, h = Shape.Length;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        Color red = new Color(0.92f, 0.16f, 0.24f), shine = new Color(1f, 0.6f, 0.65f);
        Color grey = new Color(0.22f, 0.18f, 0.2f), outline = new Color(0.28f, 0.05f, 0.1f);

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool on = Shape[h - 1 - y][x] == 'X';
                if (!on) { tex.SetPixel(x, y, Color.clear); continue; }

                bool edge = false;
                int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };
                for (int i = 0; i < 4; i++)
                {
                    int nx = x + dx[i], ny = y + dy[i];
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h || Shape[h - 1 - ny][nx] != 'X') edge = true;
                }

                bool filled = fill == 2 || (fill == 1 && x <= w / 2);
                Color c = edge ? outline : (filled ? red : grey);
                if (!edge && filled && x == 2 && y == h - 3) c = shine;
                tex.SetPixel(x, y, c);
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
    }
}

/// <summary>
/// Builds and drives all on-screen UI at runtime: hearts, score, prompts, fear vignette and game-over screen.
/// </summary>
public class GameUI : MonoBehaviour
{
    public static GameUI Instance { get; private set; }
    public int Score { get; private set; }

    static Font font;
    public static Font GetFont()
    {
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 16);
        }
        return font;
    }

    public static GameUI Ensure()
    {
        if (Instance == null) new GameObject("Game UI").AddComponent<GameUI>();
        return Instance;
    }

    Transform canvasRoot, heartsRoot;
    Image[] hearts = new Image[0];
    Image vignette;
    Text scoreText, promptText, toastText, gameOverText;
    GameObject gameOverPanel;
    float toastUntil;

    // dialogue / journal / fade state
    enum Mode { None, Dialogue, Choice, Journal }
    Mode mode;
    int openFrame = -10, modalClosedFrame = -10;
    string[] lines, pages;
    int lineIdx, pageIdx;
    System.Action onDone, onYes, onNo;
    GameObject dialoguePanel, journalPanel;
    Text dialogueText, dialogueHint, journalText, journalNav, fadeLabel;
    Image fadeImage;
    bool fading;

    // typewriter + blip sound
    AudioSource audioSrc;
    AudioClip blipClip;
    GameObject nameTab;
    Text nameText;
    string fullText = "", pendingHint = "";
    int visibleChars;
    float typeTimer, blipPitch = 1f;
    bool typing;
    public float charsPerSecond = 38f;

    /// <summary>True while a dialogue, journal or screen fade is up (the player is frozen and can't act).</summary>
    public bool ModalOpen => mode != Mode.None || fading || Time.frameCount <= modalClosedFrame;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Build();
        charsPerSecond = 38f * GameSettings.TextSpeed;
    }

    // -------------------------------------------------------------- public API

    public void SetHealth(int current, int max)
    {
        int count = Mathf.CeilToInt(max / 2f);
        if (hearts.Length != count) RebuildHearts(count);
        for (int i = 0; i < hearts.Length; i++)
        {
            int v = current - i * 2;
            hearts[i].sprite = v >= 2 ? HeartSprites.Full : (v == 1 ? HeartSprites.Half : HeartSprites.Empty);
        }
    }

    /// <summary>Hides/shows hearts and score (used for cutscenes).</summary>
    public void SetHudVisible(bool visible)
    {
        heartsRoot.gameObject.SetActive(visible);
        scoreText.gameObject.SetActive(visible);
    }

    public void AddScore(int n) { Score += n; scoreText.text = "Score: " + Score; SoundFx.Play("score"); }
    public void ResetScore() { Score = 0; scoreText.text = "Score: 0"; }

    public void SetPrompt(string s) { if (promptText.text != s) promptText.text = s; }

    public void Toast(string s, float seconds = 1.8f)
    {
        toastText.text = s;
        toastUntil = Time.time + seconds;
    }

    /// <param name="fear">0..1 strength</param><param name="pulse">0..1 heartbeat</param>
    public void SetFear(float fear, float pulse)
    {
        float a = Mathf.Clamp01(fear * (0.8f + 0.2f * pulse));
        vignette.color = new Color(0f, 0f, 0f, a * 0.95f);
    }

    public void ShowGameOver(bool show)
    {
        gameOverPanel.SetActive(show);
        if (show) gameOverText.text = "You were caught!\n\nScore: " + Score + "\n\nPress R to try again";
    }

    void Update()
    {
        if (toastText.text.Length > 0 && Time.time > toastUntil) toastText.text = "";
        TickTyping();
        if (mode != Mode.None && Time.frameCount != openFrame) HandleModalInput();
    }

    // ------------------------------------------------- dialogue, journal, fade

    /// <summary>
    /// Shows lines one at a time with a typewriter effect and a blip per letter.
    /// A line may start with "@Name|" to show a speaker tag (e.g. "@Merimna|...."); otherwise it's narration.
    /// </summary>
    public void ShowDialogue(string[] dialogueLines, System.Action done = null)
    {
        lines = dialogueLines; lineIdx = 0; onDone = done;
        mode = Mode.Dialogue; openFrame = Time.frameCount;
        journalPanel.SetActive(false);
        dialoguePanel.SetActive(true);
        ApplyLine(lines[0], "[E] next");
    }

    public void ShowChoice(string question, System.Action yes, System.Action no = null)
    {
        onYes = yes; onNo = no;
        mode = Mode.Choice; openFrame = Time.frameCount;
        journalPanel.SetActive(false);
        dialoguePanel.SetActive(true);
        ApplyLine(question, "[E] Yes      [N] No");
    }

    void ApplyLine(string raw, string hint)
    {
        string speaker = null, text = raw;
        if (raw.StartsWith("@") && raw.IndexOf('|') > 0)
        {
            int bar = raw.IndexOf('|');
            speaker = raw.Substring(1, bar - 1);
            text = raw.Substring(bar + 1);
        }
        if (string.IsNullOrEmpty(text)) text = " ";
        nameTab.SetActive(speaker != null);
        if (speaker != null) nameText.text = speaker;
        blipPitch = speaker != null ? 1.2f : 0.8f;     // character voice vs narration

        fullText = text;
        pendingHint = hint;
        visibleChars = 0;
        typeTimer = 0f;
        typing = true;
        dialogueText.text = "";
        dialogueHint.text = "";
    }

    void FinishTyping()
    {
        typing = false;
        dialogueText.text = fullText;
        dialogueHint.text = pendingHint;
    }

    void TickTyping()
    {
        if (!typing) return;
        typeTimer -= Time.deltaTime;
        while (typing && typeTimer <= 0f)
        {
            visibleChars++;
            char ch = fullText[visibleChars - 1];
            dialogueText.text = fullText.Substring(0, visibleChars);
            if (!char.IsWhiteSpace(ch) && ch != '.' && ch != ',') Blip();

            float delay = 1f / charsPerSecond;
            if (ch == ',') delay += 0.12f;
            else if (ch == '.' || ch == '?' || ch == '!') delay += 0.22f;
            typeTimer += delay;

            if (visibleChars >= fullText.Length) { FinishTyping(); }
        }
    }

    void Blip()
    {
        if (audioSrc == null || blipClip == null) return;
        audioSrc.pitch = blipPitch + Random.Range(-0.08f, 0.08f);
        audioSrc.PlayOneShot(blipClip, 0.6f);
    }

    /// <summary>Short soft square-ish blip, generated in code (no audio asset needed).</summary>
    static AudioClip MakeBlipClip()
    {
        const int rate = 44100;
        int n = (int)(rate * 0.055f);
        var data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float s = Mathf.Sin(2f * Mathf.PI * 520f * t);
            float wave = Mathf.Sign(s) * 0.35f + s * 0.65f;      // softened square
            data[i] = wave * Mathf.Exp(-t * 55f) * 0.4f;
        }
        var clip = AudioClip.Create("blip", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    public void ShowJournal(string[] journalPages)
    {
        pages = journalPages; pageIdx = 0;
        mode = Mode.Journal; openFrame = Time.frameCount;
        SoundFx.Play("page");
        dialoguePanel.SetActive(false);
        journalPanel.SetActive(true);
        RenderPage();
    }

    void RenderPage()
    {
        journalText.text = pages[pageIdx];
        journalText.rectTransform.localRotation = Quaternion.Euler(0f, 0f, ((pageIdx * 37) % 7 - 3) * 0.7f);
        journalText.color = (pageIdx % 3 == 2) ? new Color(0.5f, 0.08f, 0.08f) : new Color(0.2f, 0.13f, 0.08f);
        journalNav.text = "<  A      page " + (pageIdx + 1) + " / " + pages.Length + "      D  >          [E] close";
    }

    void CloseModal()
    {
        mode = Mode.None;
        typing = false;
        dialoguePanel.SetActive(false);
        journalPanel.SetActive(false);
        modalClosedFrame = Time.frameCount;
    }

    void HandleModalInput()
    {
        switch (mode)
        {
            case Mode.Dialogue:
                if (!GameInput.AdvancePressed()) break;
                if (typing) { FinishTyping(); break; }          // first press completes the line
                lineIdx++;
                if (lineIdx >= lines.Length) { var d = onDone; CloseModal(); if (d != null) d(); }
                else ApplyLine(lines[lineIdx], "[E] next");
                break;

            case Mode.Choice:
                if (typing)
                {
                    if (GameInput.AdvancePressed() || GameInput.YesPressed() || GameInput.NoPressed()) FinishTyping();
                    break;
                }
                if (GameInput.YesPressed()) { var y = onYes; CloseModal(); SoundFx.Play("yes"); if (y != null) y(); }
                else if (GameInput.NoPressed()) { var n = onNo; CloseModal(); SoundFx.Play("no"); if (n != null) n(); }
                break;

            case Mode.Journal:
                if (GameInput.RightPressed() && pageIdx < pages.Length - 1) { pageIdx++; RenderPage(); SoundFx.Play("page"); }
                else if (GameInput.LeftPressed() && pageIdx > 0) { pageIdx--; RenderPage(); SoundFx.Play("page"); }
                else if (GameInput.CloseJournalPressed()) CloseModal();
                break;
        }
    }

    /// <summary>Fade to black, run atBlack, hold, fade back in, then run after.</summary>
    public void FadeTransition(System.Action atBlack, System.Action after = null, float hold = 0.2f, string label = null)
    {
        if (fading) return;
        StartCoroutine(FadeRoutine(atBlack, after, hold, label));
    }

    IEnumerator FadeRoutine(System.Action atBlack, System.Action after, float hold, string label)
    {
        const float fadeTime = 0.35f;
        fading = true;
        fadeLabel.text = label ?? "";
        for (float t = 0f; t < fadeTime; t += Time.deltaTime) { SetFade(t / fadeTime); yield return null; }
        SetFade(1f);
        if (atBlack != null) atBlack();
        yield return new WaitForSeconds(hold);
        for (float t = 0f; t < fadeTime; t += Time.deltaTime) { SetFade(1f - t / fadeTime); yield return null; }
        SetFade(0f);
        fading = false;
        modalClosedFrame = Time.frameCount;
        if (after != null) after();
    }

    void SetFade(float a)
    {
        fadeImage.color = new Color(0f, 0f, 0f, a);
        fadeLabel.color = new Color(1f, 1f, 1f, a);
    }

    void BuildModals()
    {
        // Omori-style dialogue box: white border, black fill.
        var outer = NewUI("Dialogue", canvasRoot);
        outer.anchorMin = outer.anchorMax = outer.pivot = new Vector2(0.5f, 0f);
        outer.anchoredPosition = new Vector2(0, 50);
        outer.sizeDelta = new Vector2(1500, 280);
        outer.gameObject.AddComponent<Image>().color = Color.white;
        var inner = NewUI("Inner", outer);
        Stretch(inner);
        inner.offsetMin = new Vector2(6, 6); inner.offsetMax = new Vector2(-6, -6);
        inner.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.94f);

        dialogueText = MakeText("Text", inner, "", 44, TextAnchor.UpperLeft);
        Stretch(dialogueText.rectTransform);
        dialogueText.rectTransform.offsetMin = new Vector2(40, 60);
        dialogueText.rectTransform.offsetMax = new Vector2(-40, -30);
        dialogueText.horizontalOverflow = HorizontalWrapMode.Wrap;

        dialogueHint = MakeText("Hint", inner, "", 32, TextAnchor.LowerRight);
        Stretch(dialogueHint.rectTransform);
        dialogueHint.rectTransform.offsetMin = new Vector2(30, 14);
        dialogueHint.rectTransform.offsetMax = new Vector2(-36, -14);
        dialogueHint.color = new Color(1f, 1f, 1f, 0.7f);
        dialoguePanel = outer.gameObject;
        dialoguePanel.SetActive(false);

        // Speaker name tab (sits on the top-left edge of the box).
        var tab = NewUI("Name Tab", outer);
        tab.anchorMin = tab.anchorMax = new Vector2(0f, 1f);
        tab.pivot = new Vector2(0f, 0f);
        tab.anchoredPosition = new Vector2(40, -4);
        tab.sizeDelta = new Vector2(360, 70);
        tab.gameObject.AddComponent<Image>().color = Color.white;
        var tabInner = NewUI("Inner", tab);
        Stretch(tabInner);
        tabInner.offsetMin = new Vector2(6, 6); tabInner.offsetMax = new Vector2(-6, -6);
        tabInner.gameObject.AddComponent<Image>().color = Color.black;
        nameText = MakeText("Name", tabInner, "", 40, TextAnchor.MiddleCenter);
        Stretch(nameText.rectTransform);
        nameTab = tab.gameObject;
        nameTab.SetActive(false);

        audioSrc = gameObject.AddComponent<AudioSource>();
        audioSrc.playOnAwake = false;
        audioSrc.spatialBlend = 0f;
        blipClip = MakeBlipClip();

        // Journal: parchment page.
        var jOuter = NewUI("Journal", canvasRoot);
        jOuter.anchorMin = jOuter.anchorMax = jOuter.pivot = new Vector2(0.5f, 0.5f);
        jOuter.sizeDelta = new Vector2(1300, 820);
        jOuter.gameObject.AddComponent<Image>().color = new Color(0.32f, 0.2f, 0.14f);
        var jInner = NewUI("Page", jOuter);
        Stretch(jInner);
        jInner.offsetMin = new Vector2(18, 18); jInner.offsetMax = new Vector2(-18, -18);
        jInner.gameObject.AddComponent<Image>().color = new Color(0.92f, 0.86f, 0.7f);

        journalText = MakeText("Text", jInner, "", 46, TextAnchor.MiddleCenter);
        Stretch(journalText.rectTransform);
        journalText.rectTransform.offsetMin = new Vector2(90, 90);
        journalText.rectTransform.offsetMax = new Vector2(-90, -60);
        journalText.horizontalOverflow = HorizontalWrapMode.Wrap;
        journalText.GetComponent<Outline>().effectColor = new Color(0, 0, 0, 0);

        journalNav = MakeText("Nav", jInner, "", 30, TextAnchor.LowerCenter);
        Stretch(journalNav.rectTransform);
        journalNav.rectTransform.offsetMin = new Vector2(20, 20);
        journalNav.rectTransform.offsetMax = new Vector2(-20, -20);
        journalNav.color = new Color(0.3f, 0.2f, 0.14f);
        journalNav.GetComponent<Outline>().effectColor = new Color(0, 0, 0, 0);
        journalPanel = jOuter.gameObject;
        journalPanel.SetActive(false);

        // Full-screen fade (always last = on top).
        var f = NewUI("Fade", canvasRoot);
        Stretch(f);
        fadeImage = f.gameObject.AddComponent<Image>();
        fadeImage.color = new Color(0, 0, 0, 0);
        fadeImage.raycastTarget = false;
        fadeLabel = MakeText("Label", f, "", 72, TextAnchor.MiddleCenter);
        Stretch(fadeLabel.rectTransform);
        fadeLabel.color = new Color(1, 1, 1, 0);
    }

    // ------------------------------------------------------------------ build

    void Build()
    {
        var canvasGo = new GameObject("Canvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasRoot = canvasGo.transform;

        // Fear vignette (behind everything else).
        var vgo = NewUI("Fear Vignette", canvasRoot);
        Stretch(vgo);
        vignette = vgo.gameObject.AddComponent<Image>();
        vignette.sprite = MakeVignetteSprite();
        vignette.color = new Color(0, 0, 0, 0);
        vignette.raycastTarget = false;

        heartsRoot = NewUI("Hearts", canvasRoot);
        var hr = (RectTransform)heartsRoot;
        hr.anchorMin = hr.anchorMax = hr.pivot = new Vector2(0, 1);
        hr.anchoredPosition = new Vector2(30, -26);
        hr.sizeDelta = new Vector2(400, 60);

        scoreText = MakeText("Score", canvasRoot, "Score: 0", 46, TextAnchor.UpperRight);
        var sr = scoreText.rectTransform;
        sr.anchorMin = sr.anchorMax = sr.pivot = new Vector2(1, 1);
        sr.anchoredPosition = new Vector2(-34, -24);
        sr.sizeDelta = new Vector2(500, 70);

        promptText = MakeText("Prompt", canvasRoot, "", 38, TextAnchor.MiddleCenter);
        var pr = promptText.rectTransform;
        pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(0.5f, 0);
        pr.anchoredPosition = new Vector2(0, 90);
        pr.sizeDelta = new Vector2(1000, 60);

        toastText = MakeText("Toast", canvasRoot, "", 42, TextAnchor.MiddleCenter);
        toastText.color = new Color(1f, 0.85f, 0.9f);
        var tr = toastText.rectTransform;
        tr.anchorMin = tr.anchorMax = tr.pivot = new Vector2(0.5f, 0);
        tr.anchoredPosition = new Vector2(0, 160);
        tr.sizeDelta = new Vector2(1200, 70);

        var panel = NewUI("Game Over", canvasRoot);
        Stretch(panel);
        var bg = panel.gameObject.AddComponent<Image>();
        bg.color = new Color(0, 0, 0, 0.7f);
        gameOverText = MakeText("Text", panel, "", 64, TextAnchor.MiddleCenter);
        Stretch(gameOverText.rectTransform);
        gameOverPanel = panel.gameObject;
        gameOverPanel.SetActive(false);

        BuildModals();
    }

    void RebuildHearts(int count)
    {
        foreach (var h in hearts) if (h != null) Destroy(h.gameObject);
        hearts = new Image[count];
        for (int i = 0; i < count; i++)
        {
            var rt = NewUI("Heart " + i, heartsRoot);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(52, 48);
            rt.anchoredPosition = new Vector2(i * 60, 0);
            var img = rt.gameObject.AddComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = false;
            hearts[i] = img;
        }
    }

    static RectTransform NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static void Stretch(Transform t) { Stretch((RectTransform)t); }
    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static Text MakeText(string name, Transform parent, string content, int size, TextAnchor anchor)
    {
        var rt = NewUI(name, parent);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = GetFont();
        t.text = content;
        t.fontSize = size;
        t.alignment = anchor;
        t.color = Color.white;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        var o = rt.gameObject.AddComponent<Outline>();
        o.effectColor = new Color(0, 0, 0, 0.85f);
        o.effectDistance = new Vector2(2, -2);
        return t;
    }

    /// <summary>Radial gradient: clear in the middle, dark at the edges.</summary>
    static Sprite MakeVignetteSprite()
    {
        const int n = 128;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n - 0.5f, dy = (y + 0.5f) / n - 0.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy) / 0.5f;           // 1 at the middle of each edge
                float a = 0.15f + 0.85f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.4f, 1.25f, r));
                tex.SetPixel(x, y, new Color(0, 0, 0, a));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }
}
