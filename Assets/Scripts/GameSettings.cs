using UnityEngine;

/// <summary>Player-adjustable settings, saved with PlayerPrefs (IndexedDB on the web).</summary>
public static class GameSettings
{
    public enum Difficulty { Gentle = 0, Normal = 1, Nightmare = 2 }

    public static Difficulty Level = Difficulty.Normal;
    public static float Brightness = 1f;      // 0.6 .. 1.4
    public static float Music = 1f;           // 0 .. 1
    public static float Sfx = 1f;             // 0 .. 1
    public static float TextSpeed = 1f;       // 0.5 .. 2
    public static bool ScreenShake = true;

    static bool loaded;

    // ---- difficulty multipliers (read by Fox / AnimalSpawner / Cat) ----
    public static int FoxDamage { get { return Level == Difficulty.Nightmare ? 2 : 1; } }
    public static float FoxSpeed { get { return Level == Difficulty.Gentle ? 0.8f : (Level == Difficulty.Nightmare ? 1.15f : 1f); } }
    public static float FoxCooldown { get { return Level == Difficulty.Gentle ? 1.6f : (Level == Difficulty.Nightmare ? 0.8f : 1f); } }
    public static float FoxAggro { get { return Level == Difficulty.Gentle ? 0.8f : (Level == Difficulty.Nightmare ? 1.3f : 1f); } }
    public static float FoxCount { get { return Level == Difficulty.Gentle ? 0.5f : (Level == Difficulty.Nightmare ? 1.5f : 1f); } }
    public static float HealMultiplier { get { return Level == Difficulty.Gentle ? 1.5f : 1f; } }

    public static string DifficultyBlurb()
    {
        switch (Level)
        {
            case Difficulty.Gentle: return "Fewer, slower foxes. Hugs heal more. A softer world.";
            case Difficulty.Nightmare: return "More foxes, faster and sharper. Every bite costs a whole heart.";
            default: return "How the world was meant to be.";
        }
    }

    public static void Load()
    {
        if (loaded) return;
        loaded = true;
        try
        {
            Level = (Difficulty)Mathf.Clamp(PlayerPrefs.GetInt("m_difficulty", 1), 0, 2);
            Brightness = Mathf.Clamp(PlayerPrefs.GetFloat("m_brightness", 1f), 0.6f, 1.4f);
            Music = Mathf.Clamp01(PlayerPrefs.GetFloat("m_music", 1f));
            Sfx = Mathf.Clamp01(PlayerPrefs.GetFloat("m_sfx", 1f));
            TextSpeed = Mathf.Clamp(PlayerPrefs.GetFloat("m_text", 1f), 0.5f, 2f);
            ScreenShake = PlayerPrefs.GetInt("m_shake", 1) == 1;
        }
        catch { /* storage unavailable: keep defaults */ }
    }

    public static void Save()
    {
        try
        {
            PlayerPrefs.SetInt("m_difficulty", (int)Level);
            PlayerPrefs.SetFloat("m_brightness", Brightness);
            PlayerPrefs.SetFloat("m_music", Music);
            PlayerPrefs.SetFloat("m_sfx", Sfx);
            PlayerPrefs.SetFloat("m_text", TextSpeed);
            PlayerPrefs.SetInt("m_shake", ScreenShake ? 1 : 0);
            PlayerPrefs.Save();
        }
        catch { }
    }

    /// <summary>Pushes the current values into the audio and UI systems.</summary>
    public static void Apply()
    {
        SoundFx.Volume = 0.85f * Sfx;
        if (MusicManager.Instance != null) MusicManager.Instance.masterVolume = 0.55f * Music;
        if (GameUI.Instance != null) GameUI.Instance.charsPerSecond = 38f * TextSpeed;
    }
}
