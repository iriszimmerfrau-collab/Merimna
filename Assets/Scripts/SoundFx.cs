using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// All sound effects, synthesized at startup (spread over a few frames). Usage:
///   SoundFx.Play("hit");                      // plain
///   SoundFx.Play("fox_hurt", foxPosition);    // positional: quieter with distance, panned left/right
/// Names: step_grass step_path step_wood swing hit fox_hurt fox_die fox_growl bite hurt heal purr score
///        door_open door_close lamp page drip sleep tink yes no heartbeat shiver whale chirp gameover revive
/// </summary>
public class SoundFx : MonoBehaviour
{
    public static SoundFx Instance { get; private set; }
    public static float Volume = 0.85f;

    readonly Dictionary<string, AudioClip[]> clips = new Dictionary<string, AudioClip[]>();
    AudioSource[] pool;
    int next;

    public static SoundFx Ensure()
    {
        if (Instance == null) new GameObject("Sound FX").AddComponent<SoundFx>();
        return Instance;
    }

    public static void Play(string clipName, Vector3? position = null, float volume = 1f)
    {
        Ensure().PlayInternal(clipName, position, volume);
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        pool = new AudioSource[14];
        for (int i = 0; i < pool.Length; i++)
        {
            pool[i] = gameObject.AddComponent<AudioSource>();
            pool[i].playOnAwake = false;
            pool[i].spatialBlend = 0f;
        }
        StartCoroutine(Prepare());
    }

    void PlayInternal(string clipName, Vector3? position, float volume)
    {
        AudioClip[] set;
        if (!clips.TryGetValue(clipName, out set)) return;      // not built yet / unknown name

        float vol = volume * Volume, pan = 0f;
        if (position.HasValue && Camera.main != null)
        {
            Vector3 cam = Camera.main.transform.position;
            float d = Vector2.Distance(position.Value, cam);
            float att = Mathf.Clamp01(1f - d / 24f);
            vol *= att * att;
            pan = Mathf.Clamp((position.Value.x - cam.x) / 14f, -1f, 1f) * 0.8f;
        }
        if (vol < 0.01f) return;

        AudioSource src = pool[next++ % pool.Length];
        src.clip = set[Random.Range(0, set.Length)];
        src.volume = Mathf.Clamp01(vol);
        src.pitch = 1f + Random.Range(-0.06f, 0.06f);
        src.panStereo = pan;
        src.Play();
    }

    // ------------------------------------------------------------------ build

    IEnumerator Prepare()
    {
        var budget = new SynthBudget();
        var jobs = new List<KeyValuePair<string, System.Func<AudioClip[]>>>();
        System.Action<string, System.Func<AudioClip[]>> add =
            (n, f) => jobs.Add(new KeyValuePair<string, System.Func<AudioClip[]>>(n, f));

        add("step_grass", () => new[] { StepGrass(0.16f), StepGrass(0.2f), StepGrass(0.24f) });
        add("step_path", () => new[] { StepPath(), StepPath() });
        add("step_wood", () => new[] { StepWood(140f), StepWood(125f) });
        add("swing", () => new[] { Swing() });
        add("hit", () => new[] { HitSound() });
        add("fox_hurt", () => new[] { FoxHurt(1000f), FoxHurt(880f) });
        add("fox_die", () => new[] { FoxDie() });
        add("fox_growl", () => new[] { FoxGrowl() });
        add("bite", () => new[] { Bite() });
        add("hurt", () => new[] { PlayerHurt() });
        add("heal", () => new[] { Heal() });
        add("purr", () => new[] { Purr() });
        add("score", () => new[] { ScorePing() });
        add("door_open", () => new[] { DoorCreak() });
        add("door_close", () => new[] { DoorThud() });
        add("lamp", () => new[] { LampClick() });
        add("page", () => new[] { PageTurn() });
        add("drip", () => new[] { Drips() });
        add("sleep", () => new[] { Lullaby() });
        add("tink", () => new[] { Tink() });
        add("yes", () => new[] { Beep(true) });
        add("no", () => new[] { Beep(false) });
        add("tick", () => new[] { C("tick", Gen(0.05f, t => Sn(1320f, t) * E(t, 70f) * 0.2f)) });
        add("menu_select", () => new[] { C("menu_select", Gen(0.5f, t => (Bell(0f, Synth.Midi(76), t, 5f) + Bell(0.07f, Synth.Midi(83), t, 5f) * 0.8f) * 0.22f)) });
        add("heartbeat", () => new[] { Heartbeat() });
        add("shiver", () => new[] { Shiver() });
        add("whale", () => new[] { WhaleCall() });
        add("chirp", () => new[] { Chirp(false), Chirp(true) });
        add("gameover", () => new[] { GameOverSting() });
        add("revive", () => new[] { Revive() });

        foreach (var job in jobs)
        {
            clips[job.Key] = job.Value();
            if (budget.Over) { yield return null; budget.Reset(); }
        }
    }

    // ---------------------------------------------------------------- helpers

    static float E(float t, float k) { return Mathf.Exp(-t * k); }
    static float Sn(float hz, float t) { return Mathf.Sin(2f * Mathf.PI * hz * t); }
    static AudioClip C(string n, float[] d) { return Synth.Clip(n, d, Synth.SfxRate); }

    static float[] Gen(float dur, System.Func<float, float> f)
    {
        int rate = Synth.SfxRate;
        var d = new float[(int)(dur * rate)];
        for (int i = 0; i < d.Length; i++) d[i] = Mathf.Clamp(f(i / (float)rate), -1f, 1f);
        return d;
    }

    /// <summary>Music-box / glass bell partial starting at t0.</summary>
    static float Bell(float t0, float f, float t, float decay = 2.8f)
    {
        float tt = t - t0;
        if (tt < 0f) return 0f;
        return (Sn(f, tt) + 0.4f * Sn(2f * f, tt) * E(tt, 6f) + 0.2f * Sn(3.01f * f, tt) * E(tt, 9f))
             * E(tt, decay) * (1f - E(tt, 500f));
    }

    static float Thump(float tt, float f)
    {
        if (tt < 0f) return 0f;
        return Sn(f, tt) * E(tt, 18f) * (1f - E(tt, 300f));
    }

    // ------------------------------------------------------------ the sounds

    static AudioClip StepGrass(float a)
    {
        float lp = 0f;
        return C("step_grass", Gen(0.10f, t =>
        {
            lp += (Synth.Noise() - lp) * a;
            return (lp * E(t, 38f) * 1.6f + Sn(85f, t) * E(t, 55f) * 0.25f) * 0.55f;
        }));
    }

    static AudioClip StepPath()
    {
        float lp = 0f;
        return C("step_path", Gen(0.07f, t =>
        {
            float n = Synth.Noise();
            lp += (n - lp) * 0.5f;
            return ((n - lp) * E(t, 80f) * 0.7f + Sn(420f, t) * E(t, 90f) * 0.12f) * 0.5f;
        }));
    }

    static AudioClip StepWood(float hz)
    {
        float lp = 0f;
        return C("step_wood", Gen(0.12f, t =>
        {
            lp += (Synth.Noise() - lp) * 0.3f;
            return Sn(hz, t) * E(t, 30f) * 0.5f + lp * E(t, 70f) * 0.35f;
        }));
    }

    static AudioClip Swing()
    {
        float lp = 0f;
        const float dur = 0.16f;
        return C("swing", Gen(dur, t =>
        {
            float k = Mathf.Sin(Mathf.PI * t / dur);
            lp += (Synth.Noise() - lp) * Mathf.Lerp(0.05f, 0.55f, k);
            return lp * k * k * 1.6f;
        }));
    }

    static AudioClip HitSound()
    {
        float lp = 0f, ph = 0f;
        return C("hit", Gen(0.2f, t =>
        {
            lp += (Synth.Noise() - lp) * 0.5f;
            ph += 2f * Mathf.PI * Mathf.Max(60f, 220f - 700f * t) / Synth.SfxRate;
            return lp * E(t, 45f) * 0.9f + Mathf.Sin(ph) * E(t, 20f) * 0.6f;
        }));
    }

    static AudioClip FoxHurt(float startHz)
    {
        float ph = 0f;
        const float dur = 0.28f;
        return C("fox_hurt", Gen(dur, t =>
        {
            float f = startHz - 500f * (t / dur) + 40f * Sn(30f, t);
            ph += 2f * Mathf.PI * f / Synth.SfxRate;
            return (Mathf.Sin(ph) + 0.4f * Mathf.Sin(2f * ph)) * (1f - E(t, 400f)) * E(t, 9f) * 0.32f;
        }));
    }

    static AudioClip FoxDie()
    {
        float ph = 0f;
        return C("fox_die", Gen(0.7f, t =>
        {
            float f = 800f * E(t, 2.2f) + 120f + 25f * Sn(14f, t);
            ph += 2f * Mathf.PI * f / Synth.SfxRate;
            return (Mathf.Sin(ph) + 0.35f * Mathf.Sin(2f * ph)) * (1f - E(t, 200f)) * E(t, 4.5f) * 0.32f;
        }));
    }

    static AudioClip FoxGrowl()
    {
        float ph = 0f, lp = 0f;
        const float dur = 0.8f;
        return C("fox_growl", Gen(dur, t =>
        {
            ph += 2f * Mathf.PI * 62f / Synth.SfxRate;
            float saw = 2f * (ph / (2f * Mathf.PI) - Mathf.Floor(ph / (2f * Mathf.PI))) - 1f;
            float am = 0.6f + 0.4f * Sn(32f, t);
            lp += ((saw * 0.6f + Synth.Noise() * 0.4f) - lp) * 0.12f;
            return lp * am * Mathf.Pow(Mathf.Sin(Mathf.PI * t / dur), 1.5f) * 1.4f;
        }));
    }

    static AudioClip Bite()
    {
        float lp = 0f;
        return C("bite", Gen(0.25f, t =>
        {
            lp += (Synth.Noise() - lp) * 0.7f;
            return lp * E(t, 30f) * 0.6f + (Sn(440f, t) + Sn(466f, t)) * E(t, 12f) * 0.18f;
        }));
    }

    static AudioClip PlayerHurt()
    {
        float ph = 0f, lp = 0f;
        return C("hurt", Gen(0.45f, t =>
        {
            ph += 2f * Mathf.PI * Mathf.Lerp(130f, 55f, Mathf.Min(1f, t / 0.3f)) / Synth.SfxRate;
            lp += (Synth.Noise() - lp) * 0.4f;
            return Mathf.Sin(ph) * E(t, 7f) * 0.7f + (Bell(0f, 330f, t, 6f) + Bell(0f, 349f, t, 6f)) * 0.12f + lp * E(t, 50f) * 0.3f;
        }));
    }

    static AudioClip Heal()
    {
        int[] notes = { 72, 76, 79, 83, 84 };
        return C("heal", Gen(1.6f, t =>
        {
            float s = 0f;
            for (int i = 0; i < notes.Length; i++) s += Bell(i * 0.12f, Synth.Midi(notes[i]), t, 2.2f) * 0.22f;
            return s;
        }));
    }

    static AudioClip Purr()
    {
        float lp = 0f;
        const float dur = 1.8f;
        return C("purr", Gen(dur, t =>
        {
            lp += (Synth.Noise() - lp) * 0.06f;
            float am = 0.5f + 0.5f * Sn(24f, t);
            float env = Mathf.Pow(Mathf.Sin(Mathf.PI * t / dur), 0.6f);
            return (lp * 3f + Sn(55f, t) * 0.1f) * am * env;
        }));
    }

    static AudioClip ScorePing()
    {
        return C("score", Gen(0.9f, t => (Bell(0f, Synth.Midi(79), t, 4f) + Bell(0.09f, Synth.Midi(86), t, 4f) * 0.7f) * 0.3f));
    }

    static AudioClip DoorCreak()
    {
        float ph = 0f, lp = 0f;
        const float dur = 0.7f;
        return C("door_open", Gen(dur, t =>
        {
            float f = 150f + 90f * Mathf.Sin(Mathf.PI * t / dur) + 8f * Sn(11f, t);
            ph += 2f * Mathf.PI * f / Synth.SfxRate;
            float saw = 2f * (ph / (2f * Mathf.PI) - Mathf.Floor(ph / (2f * Mathf.PI))) - 1f;
            lp += ((saw * 0.5f + Synth.Noise() * 0.3f) - lp) * 0.1f;
            float env = Mathf.Sin(Mathf.PI * t / dur);
            return lp * env * 1.2f + Thump(t - 0.62f, 60f) * 0.4f;
        }));
    }

    static AudioClip DoorThud()
    {
        float lp = 0f;
        return C("door_close", Gen(0.35f, t =>
        {
            lp += (Synth.Noise() - lp) * 0.2f;
            return Sn(65f, t) * E(t, 14f) * 0.9f + lp * E(t, 40f) * 0.3f;
        }));
    }

    static AudioClip LampClick()
    {
        float lp = 0f;
        return C("lamp", Gen(0.06f, t =>
        {
            float n = Synth.Noise();
            lp += (n - lp) * 0.4f;
            return (n - lp) * E(t, 120f) * 0.5f + Sn(1500f, t) * E(t, 150f) * 0.2f;
        }));
    }

    static AudioClip PageTurn()
    {
        float lp = 0f;
        const float dur = 0.3f;
        return C("page", Gen(dur, t =>
        {
            float n = Synth.Noise();
            lp += (n - lp) * 0.3f;
            float env = Mathf.Pow(Mathf.Sin(Mathf.PI * t / dur), 2f);
            return (n - lp) * env * (1f + 0.8f * Sn(18f, t)) * 0.3f;
        }));
    }

    static float Drip(float t0, float t)
    {
        float tt = t - t0;
        if (tt < 0f) return 0f;
        float f = Mathf.Lerp(500f, 950f, Mathf.Min(1f, tt / 0.06f));
        return Mathf.Sin(2f * Mathf.PI * f * tt) * E(tt, 35f) * 0.3f;
    }

    static AudioClip Drips()
    {
        return C("drip", Gen(0.9f, t => Drip(0f, t) + Drip(0.22f, t) * 0.8f + Drip(0.45f, t) * 0.6f));
    }

    static AudioClip Lullaby()
    {
        int[] notes = { 76, 74, 72, 69, 67, 60 };
        float[] times = { 0f, 0.7f, 1.4f, 2.1f, 2.9f, 3.8f };
        var d = Gen(6.5f, t =>
        {
            float s = 0f;
            for (int i = 0; i < notes.Length; i++) s += Bell(times[i], Synth.Midi(notes[i]), t, 1.3f) * 0.25f;
            return s;
        });
        Synth.Echo(d, (int)(0.3f * Synth.SfxRate), 0.5f, 0.35f);
        Synth.Normalize(d, 0.6f);
        return C("sleep", d);
    }

    static AudioClip Tink()
    {
        return C("tink", Gen(0.5f, t =>
            (Sn(1800f, t) + 0.6f * Sn(1800f * 2.76f, t) * E(t, 20f) + 0.4f * Sn(1800f * 5.4f, t) * E(t, 30f)) * E(t, 14f) * 0.15f));
    }

    static AudioClip Beep(bool yes)
    {
        if (yes) return C("yes", Gen(0.16f, t => (t < 0.07f ? Sn(660f, t) : Sn(880f, t)) * E(t % 0.07f, 25f) * 0.25f));
        return C("no", Gen(0.14f, t => Sn(220f, t) * E(t, 22f) * 0.3f));
    }

    static AudioClip Heartbeat()
    {
        return C("heartbeat", Gen(0.6f, t => Thump(t, 58f) * 0.9f + Thump(t - 0.23f, 50f) * 0.6f));
    }

    static AudioClip Shiver()
    {
        float lp = 0f;
        const float dur = 0.9f;
        return C("shiver", Gen(dur, t =>
        {
            float n = Synth.Noise();
            lp += (n - lp) * 0.35f;
            float trem = 0.5f + 0.5f * Sn(15f, t);
            float env = Mathf.Sin(Mathf.PI * t / dur);
            return ((n - lp) * 0.3f + Sn(2250f, t) * 0.03f) * trem * env;
        }));
    }

    static AudioClip WhaleCall()
    {
        float ph = 0f;
        const float dur = 5f;
        var d = Gen(dur, t =>
        {
            float f = 90f + 110f * Mathf.Sin(Mathf.PI * t / dur) + 6f * Sn(5f, t);
            ph += 2f * Mathf.PI * f / Synth.SfxRate;
            float env = Mathf.Pow(Mathf.Sin(Mathf.PI * t / dur), 2f);
            return (Mathf.Sin(ph) + 0.5f * Mathf.Sin(2f * ph + 0.5f) + 0.25f * Mathf.Sin(3f * ph)) * env * 0.35f;
        });
        Synth.Echo(d, (int)(0.35f * Synth.SfxRate), 0.5f, 0.4f);
        Synth.Normalize(d, 0.6f);
        return C("whale", d);
    }

    static AudioClip Chirp(bool twice)
    {
        const float one = 0.16f;
        return C("chirp", Gen(twice ? 0.4f : one, t =>
        {
            float tt = t % 0.24f;
            if (tt > one) return 0f;
            float f = 3200f + 1200f * Mathf.Sin(Mathf.PI * tt / one) + 150f * Sn(40f, tt);
            return Mathf.Sin(2f * Mathf.PI * f * tt) * Mathf.Sin(Mathf.PI * tt / one) * 0.1f;
        }));
    }

    static AudioClip GameOverSting()
    {
        int[] notes = { 64, 62, 59, 55 };
        float[] times = { 0f, 0.6f, 1.2f, 1.9f };
        float ph = 0f;
        var d = Gen(4f, t =>
        {
            float s = 0f;
            for (int i = 0; i < notes.Length; i++)
            {
                float f = Synth.Midi(notes[i]);
                s += (Bell(times[i], f, t, 1.2f) + Bell(times[i], f * 1.02f, t, 1.2f)) * 0.14f;   // detuned = sour
            }
            ph += 2f * Mathf.PI * Mathf.Lerp(110f, 48f, Mathf.Min(1f, t / 3.5f)) / Synth.SfxRate;
            return s + Mathf.Sin(ph) * 0.22f * E(t, 0.6f);
        });
        Synth.Echo(d, (int)(0.4f * Synth.SfxRate), 0.5f, 0.3f);
        Synth.Normalize(d, 0.7f);
        return C("gameover", d);
    }

    static AudioClip Revive()
    {
        int[] notes = { 67, 72, 76, 79 };
        return C("revive", Gen(1.8f, t =>
        {
            float s = 0f;
            for (int i = 0; i < notes.Length; i++) s += Bell(i * 0.15f, Synth.Midi(notes[i]), t, 2.2f) * 0.22f;
            return s;
        }));
    }
}
