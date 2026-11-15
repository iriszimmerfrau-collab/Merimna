using UnityEngine;

/// <summary>Tiny pixel canvas (origin bottom-left) used to draw sprites in code.</summary>
public class PixelCanvas
{
    public readonly int w, h;
    readonly Color[] px;

    public PixelCanvas(int width, int height)
    {
        w = width; h = height;
        px = new Color[w * h];           // transparent
    }

    public void Set(int x, int y, Color c)
    {
        if (x < 0 || y < 0 || x >= w || y >= h) return;
        px[y * w + x] = c;
    }

    public void Fill(int x, int y, int rw, int rh, Color c)
    {
        for (int yy = y; yy < y + rh; yy++)
            for (int xx = x; xx < x + rw; xx++)
                Set(xx, yy, c);
    }

    public void Circle(int cx, int cy, int r, Color c)
    {
        for (int y = -r; y <= r; y++)
            for (int x = -r; x <= r; x++)
                if (x * x + y * y <= r * r) Set(cx + x, cy + y, c);
    }

    /// <summary>Recolors every opaque pixel that touches a transparent one.</summary>
    public void Outline(Color c)
    {
        var copy = (Color[])px.Clone();
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (copy[y * w + x].a < 0.5f) continue;
                if (Clear(copy, x + 1, y) || Clear(copy, x - 1, y) || Clear(copy, x, y + 1) || Clear(copy, x, y - 1))
                    px[y * w + x] = c;
            }
    }

    bool Clear(Color[] src, int x, int y)
    {
        if (x < 0 || y < 0 || x >= w || y >= h) return true;
        return src[y * w + x].a < 0.5f;
    }

    public Sprite ToSprite(Vector2 pivot, float ppu = 16f, FilterMode filter = FilterMode.Point)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = filter, wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), pivot, ppu);
    }
}

/// <summary>Procedurally drawn art for the house and its furniture (16 pixels = 1 world unit).</summary>
public static class PixelArt
{
    static readonly Vector2 Bottom = new Vector2(0.5f, 0f);
    static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
    static readonly Color Dark = new Color(0.16f, 0.1f, 0.12f);

    static Sprite house, room, bed, lamp, plant, tableKnife, deskJournal, solid, glow;

    static Color Shade(Color c, float f) { return new Color(c.r * f, c.g * f, c.b * f, c.a); }

    public static Sprite Solid()
    {
        if (solid != null) return solid;
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        return solid = Sprite.Create(tex, new Rect(0, 0, 1, 1), Center, 1f);   // exactly 1 world unit
    }

    public static Sprite Glow()
    {
        if (glow != null) return glow;
        var c = new PixelCanvas(64, 64);
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float r = Mathf.Sqrt((x - 31.5f) * (x - 31.5f) + (y - 31.5f) * (y - 31.5f)) / 32f;
                float a = Mathf.Pow(Mathf.Clamp01(1f - r), 2f);
                c.Set(x, y, new Color(1, 1, 1, a));
            }
        return glow = c.ToSprite(Center, 8f, FilterMode.Bilinear);          // 8 world units wide
    }

    // ------------------------------------------------------------ exterior

    public static Sprite House()
    {
        if (house != null) return house;
        var c = new PixelCanvas(96, 80);
        Color wall = new Color(0.94f, 0.86f, 0.72f), roofA = new Color(0.66f, 0.26f, 0.28f), roofB = new Color(0.56f, 0.2f, 0.24f);
        Color wood = new Color(0.45f, 0.28f, 0.18f), glass = new Color(0.62f, 0.82f, 0.95f), brick = new Color(0.6f, 0.32f, 0.28f);

        c.Fill(8, 1, 80, 44, wall);
        for (int y = 6; y < 44; y += 6) c.Fill(8, y, 80, 1, Shade(wall, 0.9f));

        for (int y = 45; y < 77; y++)                       // roof
        {
            float inset = (y - 45) * 0.72f;
            int x0 = Mathf.RoundToInt(1 + inset), x1 = Mathf.RoundToInt(94 - inset);
            c.Fill(x0, y, x1 - x0 + 1, 1, ((y / 3) % 2 == 0) ? roofA : roofB);
        }
        c.Fill(68, 62, 9, 16, brick);                       // chimney
        c.Fill(67, 76, 11, 3, Shade(brick, 0.8f));

        c.Fill(39, 1, 18, 30, Shade(wood, 0.7f));           // door + frame
        c.Fill(40, 1, 16, 28, wood);
        c.Fill(47, 1, 1, 28, Shade(wood, 0.8f));
        c.Fill(52, 14, 2, 2, new Color(0.95f, 0.8f, 0.3f)); // knob

        foreach (int wx in new[] { 15, 65 })                // windows
        {
            c.Fill(wx, 17, 18, 18, wood);
            c.Fill(wx + 1, 18, 16, 16, glass);
            c.Fill(wx + 8, 18, 2, 16, wood);
            c.Fill(wx + 1, 25, 16, 2, wood);
        }
        c.Fill(36, 0, 24, 1, new Color(0.6f, 0.6f, 0.62f)); // step
        c.Outline(Dark);
        return house = c.ToSprite(Bottom);
    }

    // ------------------------------------------------------------ interior

    /// <summary>22 x 12 unit room: wooden floor, wallpapered back wall with a night window, door mat.</summary>
    public static Sprite Room()
    {
        if (room != null) return room;
        const int W = 352, H = 192, wallStart = 144;
        var c = new PixelCanvas(W, H);
        Color floorA = new Color(0.78f, 0.6f, 0.42f), floorB = new Color(0.72f, 0.54f, 0.38f);

        for (int row = 0; row < wallStart / 12; row++)
        {
            int y0 = row * 12;
            c.Fill(0, y0, W, 12, (row % 2 == 0) ? floorA : floorB);
            c.Fill(0, y0, W, 1, Shade(floorA, 0.8f));
            for (int x = (row * 23) % 64; x < W; x += 64) c.Fill(x, y0, 1, 12, Shade(floorA, 0.75f));
        }

        Color paper = new Color(0.86f, 0.88f, 0.96f);        // back wall
        c.Fill(0, wallStart, W, H - wallStart, paper);
        for (int x = 0; x < W; x += 16) c.Fill(x, wallStart + 4, 4, H - wallStart - 4, Shade(paper, 0.94f));
        c.Fill(0, wallStart, W, 4, new Color(0.4f, 0.26f, 0.2f));

        c.Fill(148, 156, 56, 30, new Color(0.45f, 0.3f, 0.22f)); // window
        c.Fill(151, 159, 50, 24, new Color(0.1f, 0.1f, 0.28f));
        c.Fill(175, 159, 2, 24, new Color(0.45f, 0.3f, 0.22f));
        c.Circle(190, 168, 4, new Color(1f, 0.95f, 0.6f));
        foreach (var s in new[] { new Vector2Int(156, 176), new Vector2Int(164, 164), new Vector2Int(182, 178), new Vector2Int(196, 180) })
            c.Set(s.x, s.y, Color.white);

        c.Fill(144, 6, 64, 20, new Color(0.62f, 0.25f, 0.32f)); // door mat
        c.Fill(147, 9, 58, 14, new Color(0.75f, 0.35f, 0.42f));
        return room = c.ToSprite(Center);
    }

    static Sprite doorClosed, doorOpen;

    /// <summary>A bedroom door (2 x 3.25 units). Open = warm light pouring through.</summary>
    public static Sprite Door(bool open)
    {
        if (open ? doorOpen != null : doorClosed != null) return open ? doorOpen : doorClosed;
        var c = new PixelCanvas(32, 52);
        Color frame = new Color(0.3f, 0.19f, 0.13f), wood = new Color(0.55f, 0.35f, 0.22f);
        c.Fill(0, 0, 32, 52, frame);
        if (!open)
        {
            c.Fill(3, 0, 26, 48, wood);
            c.Fill(7, 5, 18, 17, Shade(wood, 0.85f));
            c.Fill(7, 27, 18, 17, Shade(wood, 0.85f));
            c.Fill(23, 23, 3, 3, new Color(0.95f, 0.8f, 0.3f));
        }
        else
        {
            c.Fill(3, 0, 26, 48, new Color(1f, 0.97f, 0.85f));      // morning light
            c.Fill(8, 0, 21, 8, new Color(1f, 0.9f, 0.7f));
            c.Fill(3, 0, 5, 48, wood);                             // the door leaf, swung open
        }
        c.Outline(Dark);
        var s = c.ToSprite(Bottom);
        if (open) doorOpen = s; else doorClosed = s;
        return s;
    }

    public static Sprite Bed()
    {
        if (bed != null) return bed;
        var c = new PixelCanvas(40, 28);
        Color wood = new Color(0.45f, 0.28f, 0.18f);
        c.Fill(1, 1, 38, 4, wood);                                         // frame
        c.Fill(1, 1, 3, 26, wood);                                         // headboard
        c.Fill(4, 4, 34, 18, new Color(0.96f, 0.94f, 0.92f));              // mattress
        c.Fill(14, 4, 24, 16, new Color(0.7f, 0.6f, 0.86f));               // blanket
        c.Fill(14, 17, 24, 2, new Color(0.82f, 0.74f, 0.94f));
        c.Fill(5, 12, 9, 8, Color.white);                                  // pillow
        c.Outline(Dark);
        return bed = c.ToSprite(Bottom);
    }

    public static Sprite Lamp()
    {
        if (lamp != null) return lamp;
        var c = new PixelCanvas(14, 30);
        c.Fill(3, 1, 8, 2, new Color(0.3f, 0.2f, 0.16f));
        c.Fill(6, 3, 2, 14, new Color(0.3f, 0.2f, 0.16f));
        for (int y = 17; y < 28; y++)
        {
            int half = 3 + (y - 17) / 2;
            c.Fill(7 - half, y, half * 2, 1, new Color(1f, 0.85f, 0.45f));
        }
        c.Outline(Dark);
        return lamp = c.ToSprite(Bottom);
    }

    public static Sprite Plant()
    {
        if (plant != null) return plant;
        var c = new PixelCanvas(22, 32);
        Color pot = new Color(0.78f, 0.4f, 0.28f), leaf = new Color(0.3f, 0.62f, 0.32f);
        c.Fill(5, 1, 12, 8, pot);
        c.Fill(4, 9, 14, 2, Shade(pot, 1.1f));
        c.Fill(10, 11, 2, 6, new Color(0.2f, 0.45f, 0.24f));
        c.Circle(11, 21, 5, leaf);
        c.Circle(6, 17, 4, Shade(leaf, 0.9f));
        c.Circle(16, 17, 4, Shade(leaf, 1.1f));
        c.Circle(11, 27, 3, Shade(leaf, 1.15f));
        c.Outline(Dark);
        return plant = c.ToSprite(Bottom);
    }

    public static Sprite TableWithKnife()
    {
        if (tableKnife != null) return tableKnife;
        var c = new PixelCanvas(34, 24);
        Color wood = new Color(0.55f, 0.36f, 0.22f);
        c.Fill(1, 1, 3, 11, Shade(wood, 0.8f));
        c.Fill(30, 1, 3, 11, Shade(wood, 0.8f));
        c.Fill(0, 11, 34, 5, wood);
        c.Fill(8, 16, 13, 2, new Color(0.82f, 0.85f, 0.9f));             // blade
        c.Fill(9, 17, 10, 1, Color.white);
        c.Fill(21, 16, 6, 2, new Color(0.3f, 0.18f, 0.14f));             // handle
        c.Outline(Dark);
        return tableKnife = c.ToSprite(Bottom);
    }

    public static Sprite DeskWithJournal()
    {
        if (deskJournal != null) return deskJournal;
        var c = new PixelCanvas(42, 28);
        Color wood = new Color(0.5f, 0.33f, 0.22f);
        c.Fill(1, 1, 3, 11, Shade(wood, 0.8f));
        c.Fill(0, 11, 42, 5, wood);
        c.Fill(26, 1, 14, 10, Shade(wood, 0.9f));                          // drawers
        c.Fill(31, 5, 4, 2, new Color(0.9f, 0.75f, 0.3f));
        c.Fill(9, 16, 15, 9, new Color(0.45f, 0.25f, 0.55f));            // journal cover
        c.Fill(9, 16, 15, 1, new Color(0.95f, 0.9f, 0.78f));             // page edge
        c.Fill(19, 16, 2, 7, new Color(0.8f, 0.15f, 0.2f));              // ribbon
        c.Set(13, 21, new Color(1f, 0.9f, 0.4f));
        c.Outline(Dark);
        return deskJournal = c.ToSprite(Bottom);
    }
}
