using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// What happens when the player dies: it was a bad dream. A scripted scene in a bedroom
/// (built in code, far outside the world): Merimna wakes up, talks it through, gets out of bed
/// and walks out the door into the light. Then the end credits and the game logo play.
/// </summary>
public class Epilogue : MonoBehaviour
{
    public static Epilogue Instance { get; private set; }
    public static bool Active { get; private set; }

    public WorldGenerator world;
    Image overlay;

    // "@Name|text" = speaker line, otherwise narration.
    static readonly string[] WakeLines =
    {
        "You wake up. The ceiling is exactly where you left it.",
        "@Merimna|...Okay. Okay. It was a dream.",
        "@Merimna|There were foxes. And water. So much water, and something behind me, counting.",
        "@Merimna|I couldn't make it stop. I tried to hide, I tried to fight, and it just kept coming closer, until everything went quiet.",
        "Your heart is still running. Nobody has told it yet that it's safe.",
    };

    static readonly string[] WindowLines =
    {
        "Pink-gray light slides across the floor. Somewhere outside, a bird tries one note, then another.",
        "@Merimna|There was a cat in the dream. It purred. I think that part was real.",
        "@Merimna|The lamp is still here. The house is still here. The plant is... greener than yesterday. Is that normal?",
        "@Merimna|I'm not going to pretend I'm not scared of tomorrow.",
        "@Merimna|But it's morning. And mornings have never once been the thing that hurts.",
        "@Merimna|I'm still here. That counts, too.",
    };

    static readonly string[] DoorLines =
    {
        "@Merimna|Okay.",
        "The door is heavier than it looks. It lets the light in anyway.",
    };

    public static Epilogue Ensure()
    {
        if (Instance == null) new GameObject("Epilogue").AddComponent<Epilogue>();
        return Instance;
    }

    void Awake()
    {
        Instance = this;
        var cgo = new GameObject("Epilogue Canvas");
        cgo.transform.SetParent(transform, false);
        var canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 260;
        var go = new GameObject("Fade", typeof(RectTransform));
        go.transform.SetParent(cgo.transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        overlay = go.AddComponent<Image>();
        overlay.raycastTarget = false;
        overlay.color = Color.clear;
    }

    /// <summary>Starts the epilogue. Returns false (so the old game-over screen is used) if it can't run.</summary>
    public static bool TryBegin(PlayerHealth hp)
    {
        Epilogue e = Instance;
        if (e == null || Active || TitleScreen.Instance == null) return false;
        Active = true;
        e.StartCoroutine(e.Run(hp));
        return true;
    }

    // ================================================================== the scene

    IEnumerator Run(PlayerHealth hp)
    {
        GameUI ui = GameUI.Ensure();
        Transform player = hp.transform;
        var sr = player.GetComponent<SpriteRenderer>();
        var rb = player.GetComponent<Rigidbody2D>();
        hp.CutsceneLock = true;

        yield return new WaitForSeconds(2.4f);                       // the game-over sting plays
        yield return Fade(new Color(0f, 0f, 0f, 1f), 1.6f);
        ui.SetHudVisible(false);

        // -------- build the bedroom far outside the world and put the player in the bed
        Vector3 origin = new Vector3(world != null ? world.transform.position.x : 0f,
                                     (world != null ? world.WorldBounds.yMin : 0f) - 260f, 0f);
        SpriteRenderer dark, beam, door, doorGlow;
        GameObject stage = BuildBedroom(origin, out dark, out beam, out door, out doorGlow);
        Rect room = new Rect(origin.x - 11f, origin.y - 6f, 22f, 12f);

        int baseOrder = sr.sortingOrder;
        sr.sortingOrder = 5;
        if (rb != null) { rb.simulated = false; }                    // scripted movement: no physics
        player.rotation = Quaternion.Euler(0f, 0f, 90f);             // lying down, head to the left
        sr.flipX = false;
        Place(player, origin + new Vector3(-7.0f, 1.7f, 0f));

        hp.InHouse = true;
        hp.Revive();                                                 // plays the wake-up chime, closes any game-over state
        hp.InHouse = true;
        hp.CutsceneLock = true;
        hp.SetFrozen(true);
        MusicManager.SetLamp(true);

        Camera cam = Camera.main;
        var follow = cam != null ? cam.GetComponent<BoundedCameraFollow>() : null;
        if (follow != null) { follow.worldBounds = room; follow.Snap(player.position); }

        StartCoroutine(Dawn(dark, beam, 32f));
        yield return Fade(Color.clear, 2.4f);

        SoundFx.Play("heartbeat", null, 0.8f);
        yield return new WaitForSeconds(0.9f);
        SoundFx.Play("heartbeat", null, 0.6f);
        yield return new WaitForSeconds(1.6f);

        // -------- wake up
        SoundFx.Play("shiver", null, 0.5f);
        player.rotation = Quaternion.identity;                       // sits bolt upright
        Place(player, origin + new Vector3(-7.0f, 1.95f, 0f));
        yield return new WaitForSeconds(0.9f);
        yield return Say(WakeLines);

        // -------- out of bed, to the window
        sr.sortingOrder = baseOrder;
        yield return Walk(player, origin + new Vector3(-6.4f, 0.3f, 0f), 2.4f);
        yield return Walk(player, origin + new Vector3(-1.5f, 0.3f, 0f), 2.6f);
        yield return Walk(player, origin + new Vector3(0f, 2.3f, 0f), 2.4f);
        yield return new WaitForSeconds(0.8f);

        int n = ui.Score;
        string count = n > 0 ? "@Merimna|In the dream I kept a count. I think it got to " + n + "."
                             : "@Merimna|In the dream I kept a count. It never got past zero.";
        var lines = new System.Collections.Generic.List<string>(WindowLines);
        lines.Insert(1, count);
        lines.Insert(2, "@Merimna|Funny. Even there, I was counting.");
        yield return Say(lines.ToArray());

        // -------- to the door
        yield return Walk(player, origin + new Vector3(6.5f, 2.3f, 0f), 2.8f);
        yield return new WaitForSeconds(0.5f);
        SoundFx.Play("door_open");
        door.sprite = PixelArt.Door(true);
        float g = 0f;
        while (g < 1f) { g += Time.deltaTime * 1.2f; doorGlow.color = new Color(1f, 0.95f, 0.8f, g * 0.85f); yield return null; }
        yield return Say(DoorLines);

        // -------- walk out into the light
        StartCoroutine(Fade(new Color(1f, 0.97f, 0.9f, 1f), 3.2f));
        yield return Walk(player, origin + new Vector3(6.5f, 3.6f, 0f), 1.4f);
        yield return new WaitForSeconds(2.4f);

        // -------- clean up, credits
        Destroy(stage);
        sr.sortingOrder = baseOrder;
        if (rb != null) rb.simulated = true;
        hp.CutsceneLock = false;
        ui.SetHudVisible(true);
        Active = false;
        TitleScreen.Instance.BeginEndCredits();
        yield return Fade(Color.clear, 1.4f);
    }

    // ================================================================== helpers

    IEnumerator Say(string[] lines)
    {
        bool done = false;
        GameUI.Ensure().ShowDialogue(lines, () => done = true);
        while (!done) yield return null;
        yield return new WaitForSeconds(0.2f);
    }

    IEnumerator Walk(Transform t, Vector3 target, float speed)
    {
        var sr = t.GetComponent<SpriteRenderer>();
        target.z = t.position.z;
        while ((t.position - target).sqrMagnitude > 0.0025f)
        {
            Vector3 next = Vector3.MoveTowards(t.position, target, speed * Time.deltaTime);
            if (sr != null && Mathf.Abs(next.x - t.position.x) > 0.0001f) sr.flipX = next.x < t.position.x;
            t.position = next;
            yield return null;
        }
    }

    static void Place(Transform t, Vector3 pos)
    {
        pos.z = t.position.z;
        t.position = pos;
        var rb = t.GetComponent<Rigidbody2D>();
        if (rb != null) rb.position = pos;
    }

    IEnumerator Fade(Color target, float seconds)
    {
        Color from = overlay.color;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            overlay.color = Color.Lerp(from, target, t / seconds);
            yield return null;
        }
        overlay.color = target;
    }

    /// <summary>Night slowly becomes morning.</summary>
    IEnumerator Dawn(SpriteRenderer dark, SpriteRenderer beam, float seconds)
    {
        for (float t = 0f; t < seconds && dark != null; t += Time.deltaTime)
        {
            float k = t / seconds;
            dark.color = new Color(0.03f, 0.03f, 0.18f, Mathf.Lerp(0.62f, 0.05f, k));
            beam.color = new Color(1f, 0.82f, 0.62f, Mathf.Lerp(0f, 0.35f, k));
            yield return null;
        }
    }

    GameObject BuildBedroom(Vector3 o, out SpriteRenderer dark, out SpriteRenderer beam, out SpriteRenderer door, out SpriteRenderer doorGlow)
    {
        var root = new GameObject("Bedroom");
        root.transform.position = o;

        var floor = Spr(root, "Floor", PixelArt.Room(), o, -5);
        floor.color = new Color(0.8f, 0.86f, 1f);                     // cool, early-morning tint
        Spr(root, "Bed", PixelArt.Bed(), o + new Vector3(-6.5f, 1.0f), 0);
        Spr(root, "Lamp", PixelArt.Lamp(), o + new Vector3(-8.9f, 1.8f), 0);
        Spr(root, "Plant", PixelArt.Plant(), o + new Vector3(-9.8f, -1.8f), 0);

        door = Spr(root, "Door", PixelArt.Door(false), o + new Vector3(6.5f, 3.0f), -4);

        doorGlow = Spr(root, "Door Light", PixelArt.Glow(), o + new Vector3(6.5f, 4.4f), 41);
        doorGlow.transform.localScale = new Vector3(0.5f, 0.55f, 1f);
        doorGlow.color = new Color(1f, 0.95f, 0.8f, 0f);

        beam = Spr(root, "Window Light", PixelArt.Glow(), o + new Vector3(0f, 2.3f), 41);
        beam.transform.localScale = new Vector3(0.7f, 0.5f, 1f);
        beam.color = Color.clear;

        dark = Spr(root, "Darkness", PixelArt.Solid(), o, 40);
        dark.transform.localScale = new Vector3(22f, 12f, 1f);
        dark.color = new Color(0.03f, 0.03f, 0.18f, 0.62f);
        return root;
    }

    static SpriteRenderer Spr(GameObject parent, string objName, Sprite sprite, Vector3 pos, int order)
    {
        var go = new GameObject(objName);
        go.transform.SetParent(parent.transform, false);
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        return sr;
    }
}
