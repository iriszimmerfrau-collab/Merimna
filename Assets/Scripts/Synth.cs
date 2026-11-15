using UnityEngine;

/// <summary>Tiny software synthesizer used to build all music and sound effects in code (no audio files).</summary>
public static class Synth
{
    public const int MusicRate = 16000;   // lo-fi on purpose
    public const int SfxRate = 22050;
    const float TwoPi = Mathf.PI * 2f;

    public enum Voice { MusicBox, Piano, Pad, Bass }

    static uint seed = 2463534242u;

    /// <summary>White noise in -1..1 (deterministic).</summary>
    public static float Noise()
    {
        seed = seed * 1664525u + 1013904223u;
        return ((seed >> 8) & 0xFFFF) / 32768f - 1f;
    }

    public static float Midi(int m) { return 440f * Mathf.Pow(2f, (m - 69) / 12f); }

    public static AudioClip Clip(string clipName, float[] data, int rate)
    {
        var c = AudioClip.Create(clipName, data.Length, 1, rate, false);
        c.SetData(data, 0);
        return c;
    }

    /// <summary>Adds one note into buf. Tails wrap around the buffer end so loops are seamless.</summary>
    public static void Note(float[] buf, int rate, float start, float freq, float dur, float amp, Voice voice)
    {
        float renderLen;
        switch (voice)
        {
            case Voice.MusicBox: renderLen = 2.8f; break;
            case Voice.Piano: renderLen = dur + 0.7f; break;
            case Voice.Pad: renderLen = dur + 1.2f; break;
            default: renderLen = 1.6f; break;
        }

        int n = (int)(renderLen * rate);
        int startIdx = (int)(start * rate);
        int len = buf.Length;

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float p = TwoPi * freq * t;
            float s, env;

            switch (voice)
            {
                case Voice.MusicBox:
                    s = Mathf.Sin(p) + 0.4f * Mathf.Sin(2f * p) * Mathf.Exp(-t * 6f) + 0.2f * Mathf.Sin(3.01f * p) * Mathf.Exp(-t * 9f);
                    env = Mathf.Exp(-t * 2.6f) * (1f - Mathf.Exp(-t * 600f));
                    break;

                case Voice.Piano:
                    s = Mathf.Sin(p) + 0.5f * Mathf.Sin(2f * p) * Mathf.Exp(-t * 3f)
                      + 0.25f * Mathf.Sin(3f * p) * Mathf.Exp(-t * 5f) + 0.35f * Mathf.Sin(p * 1.003f);
                    env = (1f - Mathf.Exp(-t * 400f)) * Mathf.Exp(-t * 1.25f);
                    if (t > dur) env *= Mathf.Exp(-(t - dur) * 7f);
                    break;

                case Voice.Pad:
                    s = 0.5f * (Mathf.Sin(p) + Mathf.Sin(p * 1.0045f)) + 0.15f * Mathf.Sin(2f * p);
                    env = Mathf.SmoothStep(0f, 1f, t / 0.6f);
                    if (t > dur) env *= Mathf.Exp(-(t - dur) * 3.2f);
                    break;

                default: // Bass pluck
                    s = Mathf.Sin(p) + 0.3f * Mathf.Sin(2f * p) * Mathf.Exp(-t * 4f);
                    env = (1f - Mathf.Exp(-t * 300f)) * Mathf.Exp(-t * 2.2f);
                    break;
            }

            buf[(startIdx + i) % len] += s * env * amp;
        }
    }

    /// <summary>Two-tap circular echo (a cheap "room").</summary>
    public static void Echo(float[] b, int delay, float fb, float mix)
    {
        int n = b.Length;
        var src = (float[])b.Clone();
        for (int i = 0; i < n; i++)
        {
            b[i] += src[(i - delay % n + n) % n] * mix;
            b[i] += src[(i - (2 * delay) % n + n) % n] * mix * fb;
        }
    }

    public static void LowPass(float[] b, float a)
    {
        float y = 0f;
        for (int i = 0; i < b.Length; i++) { y += (b[i] - y) * a; b[i] = y; }
    }

    public static void Normalize(float[] b, float peak)
    {
        float max = 0.0001f;
        for (int i = 0; i < b.Length; i++) { float a = Mathf.Abs(b[i]); if (a > max) max = a; }
        float k = peak / max;
        for (int i = 0; i < b.Length; i++) b[i] *= k;
    }

    /// <summary>Soft saturation + slight bit reduction for a warm, dusty "old cartridge" feel.</summary>
    public static void LoFi(float[] b, float drive, int bits)
    {
        float levels = Mathf.Pow(2f, bits);
        for (int i = 0; i < b.Length; i++)
        {
            float x = (float)System.Math.Tanh(b[i] * drive);
            b[i] = Mathf.Round(x * levels) / levels;
        }
    }
}

/// <summary>Time budget so heavy generation is spread over frames (keeps the game smooth, esp. in WebGL).</summary>
public class SynthBudget
{
    readonly System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
    public bool Over { get { return sw.ElapsedMilliseconds > 8; } }
    public void Reset() { sw.Restart(); }
}
