using System.Collections;
using UnityEngine;

/// <summary>
/// The soundtrack, composed as note data and rendered with Synth.
///   Meadow : "Wandering Home"  - C major, 72 bpm, music box over a warm pad, with an aching turn in the second half.
///   Home   : "Lamp Hours"      - F major, 60 bpm, felt piano lullaby.
///   Dread  : "Something Counts" - beating drones, a tritone, bells played backwards. Fades in with fear.
///   Ocean  : slow waves, looped.
/// Melody strings are "beat:midiNote:lengthInBeats" separated by spaces, one string per bar.
/// Chord strings are "bassMidi|chord notes".
/// </summary>
public static class MusicScores
{
    const int R = Synth.MusicRate;

    // ------------------------------------------------------------------ Meadow

    static readonly string[] MeadowChords =
    {
        "36|60 64 67 71", "45|57 60 64 67", "41|53 57 60 64", "43|55 59 62 64",
        "40|52 55 59 62", "45|57 60 64 67", "38|50 53 57 60", "43|55 59 62 65",
        "41|53 57 60 64", "41|53 56 60 62", "40|52 55 60 64", "45|57 60 64 67 71",
        "38|50 53 57 60", "41|53 56 60 67", "36|60 64 67 71", "43|55 60 62 65",
    };

    static readonly string[] MeadowMelody =
    {
        "0:76:1.5 1.5:79:0.5 2:83:1 3:81:1",
        "0:79:1 1:76:1 2:72:2",
        "0:81:1.5 1.5:79:0.5 2:77:1 3:76:1",
        "0:74:1 1:76:1 2:79:2",
        "0:83:1.5 1.5:81:0.5 2:79:1 3:76:1",
        "0:84:1 1:83:1 2:81:2",
        "0:77:1 1:81:1 2:86:1 3:84:1",
        "0:83:2 2:79:1 3:74:1",
        // second half: the same world, a shade sadder
        "0:84:2 2:81:1 3:84:1",
        "0:80:1.5 1.5:77:0.5 2:74:2",
        "0:76:1 1:79:1 2:84:2",
        "0:83:1 1:81:1 2:76:2",
        "0:74:1 1:77:1 2:81:1 3:79:1",
        "0:80:2 2:84:1 3:80:1",
        "0:79:1 1:76:1 2:72:2",
        "0:74:2 2:79:1 3:74:1",
    };

    public static IEnumerator Meadow(System.Action<AudioClip> done)
    {
        float beat = 60f / 72f;
        int bars = MeadowChords.Length;
        var buf = new float[(int)(bars * 4 * beat * R)];
        var budget = new SynthBudget();

        for (int bar = 0; bar < bars; bar++)
        {
            float t0 = bar * 4 * beat;
            ParseChord(MeadowChords[bar], out int bass, out int[] notes);

            foreach (int m in notes) Synth.Note(buf, R, t0, Synth.Midi(m), 4 * beat, 0.045f, Synth.Voice.Pad);
            Synth.Note(buf, R, t0, Synth.Midi(bass), beat, 0.24f, Synth.Voice.Bass);
            Synth.Note(buf, R, t0 + 2 * beat, Synth.Midi(bass), beat, 0.16f, Synth.Voice.Bass);

            // quiet music-box shimmer an octave up, off the beat
            Synth.Note(buf, R, t0 + 1.5f * beat, Synth.Midi(notes[notes.Length - 1] + 12), beat, 0.05f, Synth.Voice.MusicBox);

            AddMelody(buf, MeadowMelody[bar], t0, beat, 0.2f, Synth.Voice.MusicBox);

            if (budget.Over) { yield return null; budget.Reset(); }
        }

        Synth.Echo(buf, (int)(0.23f * R), 0.5f, 0.33f);
        Synth.LoFi(buf, 1.6f, 11);
        Synth.Normalize(buf, 0.85f);
        done(Synth.Clip("Wandering Home", buf, R));
    }

    // -------------------------------------------------------------------- Home

    static readonly string[] HomeChords =
    {
        "41|53 57 60 64", "40|52 55 59 62", "38|50 53 57 60 64", "36|55 59 60 64",
        "34|53 57 58 62", "45|55 57 60 64", "43|55 58 62 65", "36|53 55 58 60",
    };

    static readonly string[] HomeMelody =
    {
        "0:72:2 2:69:1 3:72:1",
        "0:71:2 2:67:2",
        "0:69:1 1:74:1 2:77:2",
        "0:76:2 2:74:1 3:72:1",
        "0:74:1.5 1.5:77:0.5 2:81:2",
        "0:79:1 1:76:1 2:72:2",
        "0:70:1 1:74:1 2:79:1 3:77:1",
        "0:77:2 2:76:2",
    };

    public static IEnumerator Home(System.Action<AudioClip> done)
    {
        float beat = 1f;
        int bars = HomeChords.Length;
        var buf = new float[(int)(bars * 4 * beat * R)];
        var budget = new SynthBudget();

        for (int bar = 0; bar < bars; bar++)
        {
            float t0 = bar * 4 * beat;
            ParseChord(HomeChords[bar], out int bass, out int[] notes);

            foreach (int m in notes) Synth.Note(buf, R, t0, Synth.Midi(m), 4 * beat, 0.04f, Synth.Voice.Pad);
            Synth.Note(buf, R, t0, Synth.Midi(bass), beat, 0.22f, Synth.Voice.Bass);

            // slow broken-chord piano underneath, eighth notes, very soft
            for (int k = 0; k < 8; k++)
            {
                int m = notes[k % notes.Length] + ((k % 4 == 3) ? 12 : 0);
                Synth.Note(buf, R, t0 + k * 0.5f * beat, Synth.Midi(m), 0.5f * beat, 0.05f, Synth.Voice.Piano);
            }

            AddMelody(buf, HomeMelody[bar], t0, beat, 0.2f, Synth.Voice.Piano);

            if (budget.Over) { yield return null; budget.Reset(); }
        }

        Synth.Echo(buf, (int)(0.3f * R), 0.5f, 0.3f);
        Synth.LowPass(buf, 0.55f);                       // felt-muted warmth
        Synth.LoFi(buf, 1.4f, 11);
        Synth.Normalize(buf, 0.8f);
        done(Synth.Clip("Lamp Hours", buf, R));
    }

    // ------------------------------------------------------------------- Title

    static readonly string[] TitleChords =
    {
        "45|57 60 64 67 71", "41|53 57 60 64", "36|60 64 67 71", "43|55 59 62 64",
        "45|57 60 64 67 71", "41|53 57 60 64", "38|50 53 57 60", "40|52 56 59 62",
    };

    static readonly string[] TitleMelody =
    {
        "0:76:2 2:72:1 3:76:1",
        "0:77:1.5 1.5:76:0.5 2:72:2",
        "0:79:2 2:76:1 3:72:1",
        "0:74:1 1:76:1 2:79:2",
        "0:81:2 2:79:1 3:76:1",
        "0:77:1 1:81:1 2:84:2",
        "0:81:1.5 1.5:77:0.5 2:74:2",
        "0:68:1 1:71:1 2:76:2",
    };

    /// <summary>"Wake Up Slowly": A minor, 60 bpm, glassy music box over a ghostly pad.</summary>
    public static IEnumerator Title(System.Action<AudioClip> done)
    {
        float beat = 1f;
        int bars = TitleChords.Length;
        var buf = new float[(int)(bars * 4 * beat * R)];
        var budget = new SynthBudget();

        for (int bar = 0; bar < bars; bar++)
        {
            float t0 = bar * 4 * beat;
            ParseChord(TitleChords[bar], out int bass, out int[] notes);

            foreach (int m in notes)
            {
                Synth.Note(buf, R, t0, Synth.Midi(m), 4 * beat, 0.04f, Synth.Voice.Pad);
                Synth.Note(buf, R, t0, Synth.Midi(m + 12), 4 * beat, 0.018f, Synth.Voice.Pad);   // airy upper voice
            }
            Synth.Note(buf, R, t0, Synth.Midi(bass), 2 * beat, 0.22f, Synth.Voice.Bass);

            // glassy sparkle arpeggio
            for (int k = 0; k < 4; k++)
                Synth.Note(buf, R, t0 + (k + 0.5f) * beat, Synth.Midi(notes[(k + 1) % notes.Length] + 24), beat, 0.04f, Synth.Voice.MusicBox);

            AddMelody(buf, TitleMelody[bar], t0, beat, 0.2f, Synth.Voice.MusicBox);

            if (budget.Over) { yield return null; budget.Reset(); }
        }

        Synth.Echo(buf, (int)(0.37f * R), 0.55f, 0.4f);
        Synth.LoFi(buf, 1.5f, 11);
        Synth.Normalize(buf, 0.85f);
        done(Synth.Clip("Wake Up Slowly", buf, R));
    }

    // ------------------------------------------------------------------- Dread

    public static IEnumerator Dread(System.Action<AudioClip> done)
    {
        const float Length = 40f;                         // all frequencies are whole cycles in 40s => seamless loop
        int n = (int)(Length * R);
        var buf = new float[n];
        var budget = new SynthBudget();
        const float tp = Mathf.PI * 2f;
        float lp = 0f;

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)R;

            // drones: 55Hz against 58.25Hz (3.25Hz beating), a tritone pair above
            float s = 0.30f * Mathf.Sin(tp * 55f * t)
                    + 0.26f * Mathf.Sin(tp * 58.25f * t)
                    + 0.14f * Mathf.Sin(tp * 82.5f * t)
                    + 0.12f * Mathf.Sin(tp * 116.5f * t);

            // slow throbbing like breathing, and a thin whistle that comes and goes
            s *= 0.65f + 0.35f * Mathf.Sin(tp * 0.25f * t);
            float whistle = Mathf.Pow(0.5f - 0.5f * Mathf.Cos(tp * t / Length * 2f), 3f);
            s += 0.05f * whistle * Mathf.Sin(tp * 1760f * t + 3f * Mathf.Sin(tp * 5f * t));

            // dull rumble of noise
            lp += (Synth.Noise() - lp) * 0.02f;
            s += lp * 0.5f;

            buf[i] = s;
            if ((i & 0x7FFF) == 0 && budget.Over) { yield return null; budget.Reset(); }
        }

        // bells played backwards: a long swell that ends in sudden silence
        foreach (float at in new[] { 8f, 19f, 30f, 37f })
        {
            for (int k = 0; k < 2; k++)
            {
                float f = Synth.Midi(k == 0 ? 70 : 76);   // Bb and E: a tritone
                int len = (int)(3f * R), start = (int)(at * R);
                for (int i = 0; i < len; i++)
                {
                    float t = i / (float)R;
                    float env = Mathf.Pow(t / 3f, 3f);
                    float s = Mathf.Sin(tp * f * t) + 0.4f * Mathf.Sin(tp * 2f * f * t) + 0.2f * Mathf.Sin(tp * 3.01f * f * t);
                    buf[(start + i) % n] += s * env * 0.07f;
                }
            }
            if (budget.Over) { yield return null; budget.Reset(); }
        }

        Synth.Echo(buf, (int)(0.41f * R), 0.55f, 0.3f);
        Synth.Normalize(buf, 0.8f);
        done(Synth.Clip("Something Counts", buf, R));
    }

    // ------------------------------------------------------------------- Ocean

    public static IEnumerator Ocean(System.Action<AudioClip> done)
    {
        const float Length = 12f;
        int n = (int)(Length * R);
        var buf = new float[n];
        float lp = 0f, hp = 0f;
        const float tp = Mathf.PI * 2f;

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)R;
            float w1 = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(tp * t / Length - 1.5708f), 2f);      // one slow swell per loop
            float w2 = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(tp * 2f * t / Length - 0.6f), 3f);    // a smaller one
            float swell = 0.3f + 0.7f * Mathf.Max(w1, w2 * 0.8f);

            float nz = Synth.Noise();
            lp += (nz - lp) * 0.07f;                       // low rumble
            hp += (nz - hp) * 0.45f;                       // foam hiss
            buf[i] = (lp * 2.2f + (nz - hp) * 0.25f * swell) * swell;
        }

        Synth.Normalize(buf, 0.7f);
        done(Synth.Clip("Ocean", buf, R));
        yield return null;
    }

    // ----------------------------------------------------------------- helpers

    static void ParseChord(string s, out int bass, out int[] notes)
    {
        string[] parts = s.Split('|');
        bass = int.Parse(parts[0]);
        string[] toks = parts[1].Split(' ');
        notes = new int[toks.Length];
        for (int i = 0; i < toks.Length; i++) notes[i] = int.Parse(toks[i]);
    }

    static void AddMelody(float[] buf, string bar, float barStart, float beat, float amp, Synth.Voice voice)
    {
        foreach (string tok in bar.Split(' '))
        {
            string[] p = tok.Split(':');
            float b = float.Parse(p[0], System.Globalization.CultureInfo.InvariantCulture);
            int midi = int.Parse(p[1]);
            float len = float.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture);
            Synth.Note(buf, R, barStart + b * beat, Synth.Midi(midi), len * beat, amp, voice);
        }
    }
}
