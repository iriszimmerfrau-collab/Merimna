using System.Collections.Generic;
using UnityEngine;

/// <summary>Procedural art for the title screen: handwritten logo, heart-shaped ghost, hills, moon, sky.</summary>
public static class TitleArt
{
    // ------------------------------------------------------------------ handwriting

    // Each glyph: advance width, then strokes as flat x,y lists (x 0..1 across, y 0 = baseline, ~0.56 = x-height, 1 = ascender).
    struct Glyph
    {
        public float width;
        public float[][] strokes;
        public Glyph(float w, params float[][] s) { width = w; strokes = s; }
    }

    static Glyph Get(char c)
    {
        switch (c)
        {
            case 'M':
                return new Glyph(1.1f, new[] { 0.02f, 0.05f, 0.06f, 0.98f, 0.34f, 0.55f, 0.5f, 0.25f, 0.66f, 0.58f, 0.94f, 1.0f, 0.98f, 0.0f });
            case 'e':
                return new Glyph(0.95f, new[] { 0.08f, 0.27f, 0.40f, 0.34f, 0.78f, 0.40f, 0.80f, 0.50f, 0.62f, 0.58f, 0.38f, 0.56f, 0.18f, 0.42f, 0.12f, 0.25f, 0.22f, 0.07f, 0.48f, 0.0f, 0.78f, 0.08f, 0.92f, 0.18f });
            case 'r':
                return new Glyph(0.7f, new[] { 0.05f, 0.02f, 0.05f, 0.55f, 0.06f, 0.32f, 0.2f, 0.5f, 0.42f, 0.58f, 0.62f, 0.48f });
            case 'i':
                return new Glyph(0.4f,
                    new[] { 0.1f, 0.56f, 0.1f, 0.06f, 0.2f, 0.0f, 0.32f, 0.1f },
                    new[] { 0.12f, 0.80f, 0.02f, 0.91f, 0.05f, 1.01f, 0.12f, 0.95f, 0.19f, 1.01f, 0.22f, 0.91f, 0.12f, 0.80f });   // the dot is a tiny heart
            case 'm':
                return new Glyph(0.95f, new[] { 0.05f, 0.55f, 0.05f, 0.02f, 0.06f, 0.33f, 0.2f, 0.56f, 0.36f, 0.48f, 0.38f, 0.02f, 0.4f, 0.33f, 0.56f, 0.58f, 0.72f, 0.48f, 0.74f, 0.02f, 0.84f, 0.06f });
            case 'n':
                return new Glyph(0.7f, new[] { 0.05f, 0.55f, 0.05f, 0.02f, 0.06f, 0.33f, 0.22f, 0.57f, 0.4f, 0.48f, 0.42f, 0.0f, 0.52f, 0.06f });
            case 'a':
                return new Glyph(0.95f, new[] { 0.72f, 0.50f, 0.5f, 0.58f, 0.2f, 0.5f, 0.08f, 0.28f, 0.2f, 0.06f, 0.5f, 0.04f, 0.72f, 0.3f, 0.74f, 0.58f, 0.72f, 0.1f, 0.88f, 0.0f });
            default:
                return new Glyph(0.5f);
        }
    }

    /// <summary>Draws text as loose marker handwriting with a soft pink glow. Returns a 1400x420 sprite.</summary>
    public static Sprite HandwrittenSprite(string text, int seed = 7)
    {
        const int W = 1400, H = 420;
        var a = new float[W * H];
        var rng = new System.Random(seed);
        const float ux = 185f, uy = 235f, baseY = 105f;
        float x = 70f;
        float noiseOff = seed * 3.1f;

        foreach (char ch in text)
        {
            Glyph g = Get(ch);
            float jx = ((float)rng.NextDouble() - 0.5f) * 8f;
            float jy = ((float)rng.NextDouble() - 0.5f) * 14f;
            float sx = 0.95f + (float)rng.NextDouble() * 0.12f;

            foreach (float[] stroke in g.strokes)
            {
                var pts = new List<Vector2>();
                for (int i = 0; i < stroke.Length; i += 2) pts.Add(new Vector2(stroke[i], stroke[i + 1]));
                List<Vector2> path = Spline(pts);

                // to pixels, with a forward slant
                for (int i = 0; i < path.Count; i++)
                {
                    Vector2 p = path[i];
                    path[i] = new Vector2(x + jx + (p.x * sx + 0.16f * p.y) * ux, baseY + jy + p.y * uy);
                }
                StampPath(a, W, H, path, noiseOff);
                noiseOff += 7.7f;
            }
            x += g.width * ux * sx + 26f;
        }

        // glow: blurred copy tinted pink behind the white ink
        var glow = (float[])a.Clone();
        Blur(glow, W, H, 14);
        Blur(glow, W, H, 14);

        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[W * H];
        for (int i = 0; i < px.Length; i++)
        {
            float core = Mathf.Clamp01(a[i]);
            float gl = Mathf.Clamp01(glow[i] * 2.6f) * 0.6f;
            float alpha = Mathf.Clamp01(core + gl * (1f - core));
            Color tint = Color.Lerp(new Color(1f, 0.66f, 0.86f), Color.white, core);
            px[i] = new Color(tint.r, tint.g, tint.b, alpha);
        }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
    }

    static List<Vector2> Spline(List<Vector2> p)
    {
        var result = new List<Vector2>();
        if (p.Count < 3) { result.AddRange(p); return result; }
        for (int i = 0; i < p.Count - 1; i++)
        {
            Vector2 p0 = p[Mathf.Max(i - 1, 0)], p1 = p[i], p2 = p[i + 1], p3 = p[Mathf.Min(i + 2, p.Count - 1)];
            for (int s = 0; s < 14; s++)
            {
                float t = s / 14f, t2 = t * t, t3 = t2 * t;
                result.Add(0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
            }
        }
        result.Add(p[p.Count - 1]);
        return result;
    }

    static void StampPath(float[] a, int W, int H, List<Vector2> path, float noiseOff)
    {
        float total = 0f;
        for (int i = 1; i < path.Count; i++) total += Vector2.Distance(path[i - 1], path[i]);
        float dist = 0f;

        for (int i = 1; i < path.Count; i++)
        {
            Vector2 p0 = path[i - 1], p1 = path[i];
            float seg = Vector2.Distance(p0, p1);
            int steps = Mathf.Max(1, Mathf.CeilToInt(seg / 1.5f));
            for (int s = 0; s <= steps; s++)
            {
                Vector2 p = Vector2.Lerp(p0, p1, s / (float)steps);
                float d = dist + seg * s / steps;
                float taper = Mathf.SmoothStep(0.45f, 1f, Mathf.Min(d, total - d) / 22f);          // pen lifts at both ends
                float pressure = 0.78f + 0.42f * Mathf.PerlinNoise(d * 0.018f, noiseOff);              // marker pressure wobble
                float r = 10.5f * pressure * Mathf.Lerp(0.45f, 1f, taper);
                Stamp(a, W, H, p.x, p.y, r);
            }
            dist += seg;
        }
    }

    static void Stamp(float[] a, int W, int H, float cx, float cy, float r)
    {
        int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - r - 1)), x1 = Mathf.Min(W - 1, Mathf.CeilToInt(cx + r + 1));
        int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - r - 1)), y1 = Mathf.Min(H - 1, Mathf.CeilToInt(cy + r + 1));
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                float v = Mathf.Clamp01(r - d + 0.5f);
                int idx = y * W + x;
                if (v > a[idx]) a[idx] = v;
            }
    }

    static void Blur(float[] src, int W, int H, int radius)
    {
        var tmp = new float[src.Length];
        float inv = 1f / (radius * 2 + 1);
        for (int y = 0; y < H; y++)                                  // horizontal
        {
            float sum = 0f;
            for (int x = -radius; x <= radius; x++) sum += src[y * W + Mathf.Clamp(x, 0, W - 1)];
            for (int x = 0; x < W; x++)
            {
                tmp[y * W + x] = sum * inv;
                sum += src[y * W + Mathf.Min(x + radius + 1, W - 1)] - src[y * W + Mathf.Max(x - radius, 0)];
            }
        }
        for (int x = 0; x < W; x++)                                  // vertical
        {
            float sum = 0f;
            for (int y = -radius; y <= radius; y++) sum += tmp[Mathf.Clamp(y, 0, H - 1) * W + x];
            for (int y = 0; y < H; y++)
            {
                src[y * W + x] = sum * inv;
                sum += tmp[Mathf.Min(y + radius + 1, H - 1) * W + x] - tmp[Mathf.Max(y - radius, 0) * W + x];
            }
        }
    }

    // ------------------------------------------------------------------ the ghost

    /// <summary>A white, heart-shaped ghost with a scalloped hem. blink = closed eyes.</summary>
    public static Sprite GhostSprite(bool blink)
    {
        const int N = 256, SS = 3;
        var px = new Color[N * N];

        for (int py = 0; py < N; py++)
            for (int pxl = 0; pxl < N; pxl++)
            {
                float inside = 0f, glow = 0f;
                float r = 0f, g = 0f, b = 0f;
                for (int sy = 0; sy < SS; sy++)
                    for (int sx = 0; sx < SS; sx++)
                    {
                        float u = ((pxl + (sx + 0.5f) / SS) / N - 0.5f) * 3.1f;       // -1.55 .. 1.55
                        float v = ((py + (sy + 0.5f) / SS) / N - 0.5f) * 3.1f + 0.1f;
                        float f = Heart(u, v);
                        float hem = -0.34f - 0.13f * Mathf.Abs(Mathf.Sin(u * 5.2f));
                        bool body = f <= 0f && v > hem;
                        if (body)
                        {
                            inside += 1f;
                            float shade = Mathf.Clamp01((v + 0.5f) / 1.5f);           // slightly lavender toward the hem
                            r += Mathf.Lerp(0.86f, 1f, shade);
                            g += Mathf.Lerp(0.84f, 1f, shade);
                            b += 1f;
                        }
                        else
                        {
                            float outside = Mathf.Max(f, 0f);
                            glow += Mathf.Exp(-outside * 5f) * (v > hem - 0.12f ? 1f : 0.4f);
                        }
                    }
                int count = SS * SS;
                float cov = inside / count;
                float gl = Mathf.Clamp01(glow / count) * 0.45f;
                Color body2 = inside > 0 ? new Color(r / inside, g / inside, b / inside) : new Color(0.85f, 0.8f, 1f);
                float alpha = cov + gl * (1f - cov);
                Color col = Color.Lerp(new Color(0.85f, 0.8f, 1f), body2, cov);
                px[py * N + pxl] = new Color(col.r, col.g, col.b, alpha);
            }

        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var canvas = px;

        // face (drawn in texture space)
        System.Action<float, float, float, float, Color> ellipse = (cx, cy, rx, ry, c) =>
        {
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = ((x + 0.5f) / N - 0.5f) * 3.1f, v = ((y + 0.5f) / N - 0.5f) * 3.1f + 0.1f;
                    float d = ((u - cx) * (u - cx)) / (rx * rx) + ((v - cy) * (v - cy)) / (ry * ry);
                    float k = Mathf.Clamp01((1f - d) * 4f);
                    if (k > 0f)
                    {
                        Color o = canvas[y * N + x];
                        Color m = Color.Lerp(o, new Color(c.r, c.g, c.b, 1f), k * c.a);
                        m.a = Mathf.Max(o.a, k * c.a);
                        canvas[y * N + x] = m;
                    }
                }
        };

        ellipse(-0.52f, -0.02f, 0.2f, 0.12f, new Color(1f, 0.62f, 0.76f, 0.6f));   // cheeks
        ellipse(0.52f, -0.02f, 0.2f, 0.12f, new Color(1f, 0.62f, 0.76f, 0.6f));

        Color ink = new Color(0.09f, 0.06f, 0.18f, 1f);
        if (!blink)
        {
            ellipse(-0.3f, 0.2f, 0.115f, 0.17f, ink);
            ellipse(0.3f, 0.2f, 0.115f, 0.17f, ink);
            ellipse(-0.26f, 0.27f, 0.04f, 0.05f, new Color(1f, 1f, 1f, 1f));       // eye shine
            ellipse(0.34f, 0.27f, 0.04f, 0.05f, new Color(1f, 1f, 1f, 1f));
        }
        else
        {
            for (int s = -1; s <= 1; s += 2)                                       // closed eyes: little arcs
                for (int k = -8; k <= 8; k++)
                {
                    float t = k / 8f;
                    ellipse(s * 0.3f + t * 0.13f, 0.17f + (1f - t * t) * 0.05f, 0.03f, 0.022f, ink);
                }
        }
        ellipse(0f, -0.06f, 0.05f, 0.07f, ink);                                    // small "o" mouth

        tex.SetPixels(canvas);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
    }

    static float Heart(float x, float y)
    {
        float a = x * x + y * y - 1f;
        return a * a * a - x * x * y * y * y;
    }

    // ----------------------------------------------------------- scenery pieces

    /// <summary>Seamlessly tileable hill silhouette (white; tint it). Whole-number frequencies make it wrap.</summary>
    public static Sprite HillSprite(int seed, float baseHeight, float amplitude)
    {
        const int W = 1024, H = 256;
        var rng = new System.Random(seed);
        var px = new Color[W * H];
        int[] freqs = { 1, 2, 3, 5, 8 };
        float[] amps = { 1f, 0.55f, 0.35f, 0.18f, 0.08f };
        float[] phase = new float[freqs.Length];
        for (int i = 0; i < phase.Length; i++) phase[i] = (float)rng.NextDouble() * 6.283f;

        for (int x = 0; x < W; x++)
        {
            float h = baseHeight * H;
            for (int i = 0; i < freqs.Length; i++) h += Mathf.Sin(6.283f * freqs[i] * x / W + phase[i]) * amps[i] * amplitude * H;
            for (int y = 0; y < H; y++)
            {
                float edge = Mathf.Clamp01(h - y + 0.5f);
                px[y * W + x] = new Color(1f, 1f, 1f, edge);
            }
        }
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Repeat };
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0f), 100f);
    }

    /// <summary>The height (0..1 of the sprite) of a hill silhouette at column x, to stand things on it.</summary>
    public static float HillHeightAt(int seed, float baseHeight, float amplitude, float xNorm)
    {
        var rng = new System.Random(seed);
        int[] freqs = { 1, 2, 3, 5, 8 };
        float[] amps = { 1f, 0.55f, 0.35f, 0.18f, 0.08f };
        float h = baseHeight;
        for (int i = 0; i < freqs.Length; i++)
        {
            float ph = (float)rng.NextDouble() * 6.283f;
            h += Mathf.Sin(6.283f * freqs[i] * xNorm + ph) * amps[i] * amplitude;
        }
        return h;
    }

    public static Sprite SkySprite()
    {
        var tex = new Texture2D(4, 256, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        Color top = new Color(0.04f, 0.03f, 0.14f), mid = new Color(0.22f, 0.1f, 0.36f), low = new Color(0.62f, 0.28f, 0.5f), bottom = new Color(0.95f, 0.6f, 0.6f);
        for (int y = 0; y < 256; y++)
        {
            float t = 1f - y / 255f;                         // 0 at top
            Color c = t < 0.45f ? Color.Lerp(top, mid, t / 0.45f)
                    : t < 0.8f ? Color.Lerp(mid, low, (t - 0.45f) / 0.35f)
                    : Color.Lerp(low, bottom, (t - 0.8f) / 0.2f);
            for (int x = 0; x < 4; x++) tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 4, 256), new Vector2(0.5f, 0.5f), 100f);
    }

    public static Sprite MoonSprite()
    {
        const int N = 256;
        var px = new Color[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = (x + 0.5f) / N - 0.5f, v = (y + 0.5f) / N - 0.5f;
                float d = Mathf.Sqrt(u * u + v * v);
                float disc = Mathf.Clamp01((0.36f - d) * 90f);
                float crater = 0f;
                crater += Mathf.Clamp01((0.07f - Mathf.Sqrt((u + 0.1f) * (u + 0.1f) + (v - 0.1f) * (v - 0.1f))) * 40f) * 0.14f;
                crater += Mathf.Clamp01((0.05f - Mathf.Sqrt((u - 0.12f) * (u - 0.12f) + (v + 0.05f) * (v + 0.05f))) * 40f) * 0.12f;
                crater += Mathf.Clamp01((0.035f - Mathf.Sqrt((u + 0.05f) * (u + 0.05f) + (v + 0.15f) * (v + 0.15f))) * 40f) * 0.1f;
                Color c = new Color(1f, 0.96f - crater, 0.82f - crater * 1.2f);
                px[y * N + x] = new Color(c.r, c.g, c.b, disc);
            }
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
    }

    public static Sprite VignetteSprite()
    {
        const int N = 128;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = (x + 0.5f) / N - 0.5f, v = (y + 0.5f) / N - 0.5f;
                float r = Mathf.Sqrt(u * u + v * v) / 0.5f;
                tex.SetPixel(x, y, new Color(0.02f, 0f, 0.06f, 0.8f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.3f, r))));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
    }

    public static Texture2D NoiseTexture()
    {
        const int N = 128;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float v = Synth.Noise() * 0.5f + 0.5f;
                tex.SetPixel(x, y, new Color(v, v, v, 1f));
            }
        tex.Apply();
        return tex;
    }
}
