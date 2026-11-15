using System.Collections;
using UnityEngine;

/// <summary>
/// Builds the soundtrack at startup and mixes it live:
///   title screen -> "Wake Up Slowly"
///   outdoors     -> "Wandering Home" (music box), birds, and ocean waves near the shore
///   in house     -> "Lamp Hours" (piano lullaby); lamp off = slower, darker, with dread creeping in
///   fear         -> "Something Counts" fades in over everything and the cheerful music ducks
///   game over    -> everything fades out
///   paused       -> everything ducks
/// </summary>
public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    /// <summary>True while the title/menu screen is showing (plays the title theme only).</summary>
    public static bool TitleActive;
    public static bool Paused;

    public float masterVolume = 0.55f;

    AudioSource title, meadow, home, dread, ocean;
    float vTitle, vMeadow, vHome, vDread, vOcean;
    bool lampOn = true, dead;

    WorldGenerator world;
    Transform player;
    PlayerHealth health;
    float chirpTimer = 4f;

    public static MusicManager Ensure()
    {
        if (Instance == null) new GameObject("Music").AddComponent<MusicManager>();
        return Instance;
    }

    public static void SetLamp(bool on) { if (Instance != null) Instance.lampOn = on; }
    public static void SetDead(bool value) { if (Instance != null) Instance.dead = value; }

    public void Init(WorldGenerator w, Transform playerTransform)
    {
        world = w;
        player = playerTransform;
        health = player != null ? player.GetComponent<PlayerHealth>() : null;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        title = NewSource();
        meadow = NewSource();
        home = NewSource();
        dread = NewSource();
        ocean = NewSource();
        masterVolume = 0.55f * GameSettings.Music;
        StartCoroutine(Build());
    }

    AudioSource NewSource()
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = true;
        s.volume = 0f;
        s.spatialBlend = 0f;
        return s;
    }

    IEnumerator Build()
    {
        yield return StartCoroutine(MusicScores.Title(c => Begin(title, c)));     // first, so the menu has music right away
        yield return StartCoroutine(MusicScores.Ocean(c => Begin(ocean, c)));
        yield return StartCoroutine(MusicScores.Meadow(c => Begin(meadow, c)));
        yield return StartCoroutine(MusicScores.Home(c => Begin(home, c)));
        yield return StartCoroutine(MusicScores.Dread(c => Begin(dread, c)));
    }

    static void Begin(AudioSource src, AudioClip clip)
    {
        src.clip = clip;
        src.Play();
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;     // keeps fading while the game is paused (timeScale 0)

        bool inHouse = health != null && health.InHouse;
        float fear = PlayerFear.Current;
        float shore = (world != null && player != null) ? world.DistanceToWater(player.position) : 99f;

        float tTitle = TitleActive ? 1f : 0f;
        float tMeadow = inHouse ? 0f : (1f - 0.75f * fear);
        float tHome = inHouse ? (lampOn ? 1f : 0.5f) : 0f;
        float tDread = Mathf.Clamp01(fear * 1.15f + (inHouse && !lampOn ? 0.4f : 0f));
        float tOcean = inHouse ? 0f : Mathf.Clamp01((7f - shore) / 6f) * 0.9f;

        if (TitleActive || dead || player == null) { tMeadow = tHome = tDread = tOcean = 0f; }
        if (Paused) { tMeadow *= 0.35f; tHome *= 0.35f; tDread *= 0.35f; tOcean *= 0.35f; }

        float step = dt * 0.5f;                                  // ~2 second crossfades
        vTitle = Mathf.MoveTowards(vTitle, tTitle, step);
        vMeadow = Mathf.MoveTowards(vMeadow, tMeadow, step);
        vHome = Mathf.MoveTowards(vHome, tHome, step);
        vDread = Mathf.MoveTowards(vDread, tDread, step);
        vOcean = Mathf.MoveTowards(vOcean, tOcean, step);

        title.volume = vTitle * masterVolume * 1.1f;
        meadow.volume = vMeadow * masterVolume;
        home.volume = vHome * masterVolume;
        dread.volume = vDread * masterVolume * 0.9f;
        ocean.volume = vOcean * masterVolume * 0.8f;
        home.pitch = Mathf.MoveTowards(home.pitch, lampOn ? 1f : 0.86f, dt * 0.3f);   // lamp off: slower, lower

        // birds, only when it's calm outside
        if (!TitleActive && !Paused && !inHouse && !dead && fear < 0.2f && player != null)
        {
            chirpTimer -= Time.deltaTime;
            if (chirpTimer <= 0f)
            {
                chirpTimer = Random.Range(3.5f, 9f);
                Vector3 at = player.position + new Vector3(Random.Range(-9f, 9f), Random.Range(-4f, 4f), 0f);
                SoundFx.Play("chirp", at, 0.5f);
            }
        }
    }
}
