using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Procedurally generates a mid-size top-down world at runtime:
///   - an island of flower grass surrounded by ocean
///   - winding walking paths (spawn -> forest -> west shore, spawn -> east shore, ...)
///   - a dense forest with a path running through it
///   - an ocean with randomly spawning whales (see WhaleSpawner)
///   - a camera that follows the player and can never see past the world border
///
/// Usage: add this component to an empty GameObject (Tools > Merimna > Create World Generator),
/// press Play. Sprites are auto-assigned from Assets/Assets/ when the component is added.
/// 1 tile = 1 world unit; sprites of any pixel size are scaled to fit.
/// </summary>
public class WorldGenerator : MonoBehaviour
{
    [Header("Sprites")]
    public Sprite flowerGrassSprite;
    public Sprite pathSprite;
    public Sprite waterSprite;
    public Sprite treeSprite;
    public Sprite whaleSprite;
    [Tooltip("Only used if the scene has no player; one is created from this sprite.")]
    public Sprite playerSprite;
    public Sprite foxSprite;
    public Sprite catWhiteSprite;
    public Sprite catOrangeSprite;

    [Header("World (tiles = world units)")]
    [Min(40)] public int width = 160;
    [Min(30)] public int height = 110;
    [Tooltip("Minimum thickness of ocean at the world edge, in tiles.")]
    [Min(3)] public int oceanBorder = 12;
    [Tooltip("How much of the map the island fills. Lower = more ocean.")]
    [Range(0.4f, 0.9f)] public float landExtent = 0.66f;
    public bool generateOnStart = true;
    public bool randomizeSeed = true;
    public int seed = 12345;

    [Header("Paths")]
    [Tooltip("Width of walkways in tiles.")]
    [Range(1.5f, 5f)] public float pathWidth = 3f;

    [Header("Forest")]
    [Tooltip("Radius of the forest in tiles.")]
    public float forestRadius = 26f;
    [Range(0f, 1f)] public float forestDensity = 0.6f;
    [Range(0f, 0.05f)] public float meadowTreeChance = 0.006f;
    [Tooltip("Rendered tree height in world units.")]
    public float treeHeight = 3f;
    [Tooltip("Tiles of clearance kept free of trees around paths.")]
    [Range(0, 3)] public int pathClearance = 1;

    [Header("Whales")]
    public bool spawnWhales = true;
    public int maxWhales = 10;
    public int initialWhales = 5;

    [Header("Animals")]
    public bool spawnAnimals = true;
    public bool foxSpriteFacesRight = true;
    public bool catSpriteFacesRight = true;
    public bool whaleSpriteFacesRight = true;

    [Header("House")]
    public bool buildHouse = true;

    [Header("Camera")]
    public float cameraSize = 6f;

    // ---- generated data (read by WhaleSpawner etc.) ----
    bool[,] land;
    bool[,] path;
    int[,] distFromLand;   // 0 on land, N tiles from shore on water
    int[,] distFromWater;  // 0 on water, N tiles from shore on land
    Vector2Int origin;     // cell coordinate of the bottom-left tile

    readonly List<Vector2Int> deepWater = new List<Vector2Int>();
    GameObject root;
    System.Random rng;

    public Rect WorldBounds { get; private set; }
    public Vector3 SpawnPosition { get; private set; }
    public IReadOnlyList<Vector2Int> DeepWaterCells => deepWater;
    public IReadOnlyList<Vector2Int> LandSpawnCells => landSpawn;
    public Transform Player { get; private set; }

    readonly List<Vector2Int> landSpawn = new List<Vector2Int>();

    /// <summary>True if the world position is on a walkway tile.</summary>
    public bool IsPathAt(Vector2 worldPos)
    {
        return path != null && WorldToCell(worldPos, out int x, out int y) && path[x, y];
    }

    /// <summary>Tiles to the nearest water (1 = right at the shore, 0 = in water, 99 = outside the world).</summary>
    public int DistanceToWater(Vector2 worldPos)
    {
        if (distFromWater == null || !WorldToCell(worldPos, out int x, out int y)) return 99;
        return Mathf.Min(distFromWater[x, y], 99);
    }

    // ------------------------------------------------------------------ setup

#if UNITY_EDITOR
    void Reset() { AutoAssignSprites(); }

    public void AutoAssignSprites()
    {
        if (flowerGrassSprite == null) flowerGrassSprite = Load("flowergrass");
        if (pathSprite == null) pathSprite = Load("walkingpath");
        if (waterSprite == null) waterSprite = Load("water");
        if (treeSprite == null) treeSprite = Load("tree");
        if (whaleSprite == null) whaleSprite = Load("whale");
        if (playerSprite == null) playerSprite = Load("player");
        if (foxSprite == null) foxSprite = Load("fox");
        if (catWhiteSprite == null) catWhiteSprite = Load("catwhite");
        if (catOrangeSprite == null) catOrangeSprite = Load("catorange");
    }

    static Sprite Load(string n) => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Assets/" + n + ".png");

    // No menu needed: if the scene has no WorldGenerator, one is created automatically on Play (editor only).
    // Set this to false if you'd rather add the component yourself.
    const bool AutoCreateOnPlay = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        if (!AutoCreateOnPlay || FindObjectOfType<WorldGenerator>() != null) return;
        var go = new GameObject("World Generator");
        go.AddComponent<WorldGenerator>(); // Start() runs Generate(), which auto-assigns sprites
    }
#endif

    [Tooltip("Show the splash / title screen (Play, Settings, Credits) before the world is generated.")]
    public bool showTitleScreen = true;

    void Start()
    {
        if (!generateOnStart) return;
        if (showTitleScreen) TitleScreen.Create(this);   // the title's Play button generates the world
        else Generate();
    }

    [ContextMenu("Regenerate (Play mode)")]
    public void Generate()
    {
        if (!Application.isPlaying) { Debug.LogWarning("WorldGenerator: enter Play mode to generate."); return; }

#if UNITY_EDITOR
        AutoAssignSprites();
#endif
        if (flowerGrassSprite == null || pathSprite == null || waterSprite == null || treeSprite == null)
        {
            Debug.LogError("WorldGenerator: flowerGrass / path / water / tree sprites must be assigned.");
            return;
        }

        if (root != null) Destroy(root);
        if (randomizeSeed) seed = Environment.TickCount;
        rng = new System.Random(seed);

        origin = new Vector2Int(-width / 2, -height / 2);
        Vector3 p = transform.position;
        WorldBounds = new Rect(p.x + origin.x, p.y + origin.y, width, height);

        root = new GameObject("Generated World");
        root.transform.SetParent(transform, false);

        BuildLandMask();
        distFromLand = DistanceFrom((x, y) => land[x, y]);
        distFromWater = DistanceFrom((x, y) => !land[x, y]);

        deepWater.Clear();
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (!land[x, y] && distFromLand[x, y] >= 3) deepWater.Add(new Vector2Int(x, y));

        landSpawn.Clear();
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (land[x, y] && distFromWater[x, y] >= 3) landSpawn.Add(new Vector2Int(x, y));

        Vector2Int spawn = NearestCell(new Vector2Int(width / 2, height / 2), (x, y) => land[x, y] && distFromWater[x, y] >= 3);
        SpawnPosition = CellCenter(spawn.x, spawn.y);

        BuildPaths(spawn, out Vector2Int forestCenter);

        bool hasHouse = buildHouse && TryPlaceHouse(spawn);
        if (hasHouse)
        {
            CarvePath(new Vector2Int(houseCell.x, houseCell.y - 3), spawn); // walkway from the door to the old spawn spot
            SpawnPosition = CellCenter(houseCell.x, houseCell.y) + new Vector3(0f, -2.3f, 0f); // start outside, in front of the house
        }

        BuildTilemaps();
        BuildTrees(spawn, forestCenter);
        BuildBorderColliders();

        if (hasHouse)
        {
            Vector3 houseBase = CellCenter(houseCell.x, houseCell.y) + new Vector3(0f, -0.5f, 0f);
            Vector3 roomOrigin = new Vector3(transform.position.x, WorldBounds.yMin - 100f, 0f); // far outside the world
            HouseBuilder.Build(this, root.transform, houseBase, roomOrigin);
        }

        if (spawnWhales && whaleSprite != null)
        {
            var spawner = root.AddComponent<WhaleSpawner>();
            spawner.maxWhales = maxWhales;
            spawner.initialWhales = initialWhales;
            spawner.spriteFacesRight = whaleSpriteFacesRight;
            spawner.Init(this, whaleSprite);
        }

        Transform player = PlacePlayer();
        Player = player;
        SetupCamera(player);

        Physics2D.SyncTransforms(); // so spawn checks can see the freshly created tree/ocean colliders
        if (spawnAnimals)
        {
            var animals = root.AddComponent<AnimalSpawner>();
            animals.foxFacesRight = foxSpriteFacesRight;
            animals.catsFaceRight = catSpriteFacesRight;
            animals.Init(this, player, foxSprite, catWhiteSprite, catOrangeSprite);
        }

        Debug.Log($"WorldGenerator: {width}x{height} world generated (seed {seed}).");
    }

    // ------------------------------------------------------------ coordinates

    public Vector3 CellCenter(int x, int y)
    {
        return transform.position + new Vector3(origin.x + x + 0.5f, origin.y + y + 0.5f, 0f);
    }

    public Vector2 CellCenterWorld(Vector2Int c) => CellCenter(c.x, c.y);

    bool WorldToCell(Vector2 w, out int x, out int y)
    {
        x = Mathf.FloorToInt(w.x - transform.position.x - origin.x);
        y = Mathf.FloorToInt(w.y - transform.position.y - origin.y);
        return x >= 0 && y >= 0 && x < width && y < height;
    }

    /// <summary>True if the world position is ocean at least minDistFromLand tiles from any shore.</summary>
    public bool IsOpenWater(Vector2 worldPos, int minDistFromLand)
    {
        if (land == null || !WorldToCell(worldPos, out int x, out int y)) return false;
        return !land[x, y] && distFromLand[x, y] >= minDistFromLand;
    }

    // -------------------------------------------------------------- land mask

    void BuildLandMask()
    {
        land = new bool[width, height];
        float ox = rng.Next(0, 10000), oy = rng.Next(0, 10000);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int edge = Mathf.Min(Mathf.Min(x, width - 1 - x), Mathf.Min(y, height - 1 - y));
                if (edge < oceanBorder) continue;

                // Rounded-rectangle falloff distorted by noise -> irregular coastline.
                float nx = (x + 0.5f - width / 2f) / (width / 2f);
                float ny = (y + 0.5f - height / 2f) / (height / 2f);
                float d = Mathf.Pow(Mathf.Pow(Mathf.Abs(nx), 3f) + Mathf.Pow(Mathf.Abs(ny), 3f), 1f / 3f);
                float n = (Mathf.PerlinNoise(ox + x * 0.07f, oy + y * 0.07f) - 0.5f)
                        + (Mathf.PerlinNoise(ox + 50f + x * 0.2f, oy + 50f + y * 0.2f) - 0.5f) * 0.3f;
                land[x, y] = d + n * 0.5f < landExtent;
            }
        }

        // Keep only the main island (connected to map centre) and fill inland lakes,
        // so every path is connected and all water is part of the open ocean.
        var main = new bool[width, height];
        Vector2Int start = NearestCell(new Vector2Int(width / 2, height / 2), (x, y) => land[x, y]);
        Flood(start, (x, y) => land[x, y], main);
        land = main;

        var ocean = new bool[width, height];
        Flood(new Vector2Int(0, 0), (x, y) => !land[x, y], ocean);
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (!land[x, y] && !ocean[x, y]) land[x, y] = true;
    }

    void Flood(Vector2Int start, Func<int, int, bool> passable, bool[,] visited)
    {
        var q = new Queue<Vector2Int>();
        visited[start.x, start.y] = true;
        q.Enqueue(start);
        int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };
        while (q.Count > 0)
        {
            var c = q.Dequeue();
            for (int i = 0; i < 4; i++)
            {
                int nx = c.x + dx[i], ny = c.y + dy[i];
                if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                if (visited[nx, ny] || !passable(nx, ny)) continue;
                visited[nx, ny] = true;
                q.Enqueue(new Vector2Int(nx, ny));
            }
        }
    }

    int[,] DistanceFrom(Func<int, int, bool> isSource)
    {
        var d = new int[width, height];
        var q = new Queue<Vector2Int>();
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                if (isSource(x, y)) { d[x, y] = 0; q.Enqueue(new Vector2Int(x, y)); }
                else d[x, y] = int.MaxValue;
            }
        while (q.Count > 0)
        {
            var c = q.Dequeue();
            for (int ax = -1; ax <= 1; ax++)
                for (int ay = -1; ay <= 1; ay++)
                {
                    if (ax == 0 && ay == 0) continue;
                    int nx = c.x + ax, ny = c.y + ay;
                    if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                    if (d[nx, ny] > d[c.x, c.y] + 1)
                    {
                        d[nx, ny] = d[c.x, c.y] + 1;
                        q.Enqueue(new Vector2Int(nx, ny));
                    }
                }
        }
        return d;
    }

    Vector2Int NearestCell(Vector2Int from, Func<int, int, bool> pred)
    {
        int max = Mathf.Max(width, height);
        for (int r = 0; r <= max; r++)
            for (int dx = -r; dx <= r; dx++)
                for (int dy = -r; dy <= r; dy++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                    int x = from.x + dx, y = from.y + dy;
                    if (x < 0 || y < 0 || x >= width || y >= height) continue;
                    if (pred(x, y)) return new Vector2Int(x, y);
                }
        return from;
    }

    // ------------------------------------------------------------------ paths

    void BuildPaths(Vector2Int spawn, out Vector2Int forestCenter)
    {
        path = new bool[width, height];

        // Forest sits in the western part of the island.
        forestCenter = NearestCell(new Vector2Int(Mathf.RoundToInt(width * 0.27f), Mathf.RoundToInt(height * 0.55f)),
                                   (x, y) => land[x, y] && distFromWater[x, y] >= 4);

        Vector2Int west = ShoreAlongRow(forestCenter.y + rng.Next(-6, 7), true);
        Vector2Int east = ShoreAlongRow(spawn.y + rng.Next(-8, 9), false);
        bool north = rng.Next(2) == 0;
        Vector2Int vertical = ShoreAlongColumn(spawn.x + rng.Next(-10, 11), north);

        // A side branch that wanders off through the forest.
        float ang = (float)(rng.NextDouble() * Math.PI * 2.0);
        Vector2Int branchEnd = NearestCell(
            new Vector2Int(forestCenter.x + Mathf.RoundToInt(Mathf.Cos(ang) * forestRadius * 0.8f),
                           forestCenter.y + Mathf.RoundToInt(Mathf.Sin(ang) * forestRadius * 0.8f)),
            (x, y) => land[x, y] && distFromWater[x, y] >= 3);

        CarvePath(spawn, forestCenter);
        CarvePath(forestCenter, west);
        CarvePath(forestCenter, branchEnd);
        CarvePath(spawn, east);
        CarvePath(spawn, vertical);
    }

    Vector2Int ShoreAlongRow(int y, bool fromLeft)
    {
        y = Mathf.Clamp(y, 0, height - 1);
        if (fromLeft) { for (int x = 0; x < width; x++) if (land[x, y]) return new Vector2Int(x, y); }
        else { for (int x = width - 1; x >= 0; x--) if (land[x, y]) return new Vector2Int(x, y); }
        return NearestCell(new Vector2Int(fromLeft ? 0 : width - 1, y), (cx, cy) => land[cx, cy]);
    }

    Vector2Int ShoreAlongColumn(int x, bool fromTop)
    {
        x = Mathf.Clamp(x, 0, width - 1);
        if (fromTop) { for (int y = height - 1; y >= 0; y--) if (land[x, y]) return new Vector2Int(x, y); }
        else { for (int y = 0; y < height; y++) if (land[x, y]) return new Vector2Int(x, y); }
        return NearestCell(new Vector2Int(x, fromTop ? height - 1 : 0), (cx, cy) => land[cx, cy]);
    }

    void CarvePath(Vector2Int a, Vector2Int b)
    {
        Vector2 pa = a, pb = b;
        float len = Vector2.Distance(pa, pb);
        if (len < 1f) return;

        Vector2 dir = (pb - pa) / len;
        Vector2 perp = new Vector2(-dir.y, dir.x);
        float amp = Mathf.Min(len * 0.2f, 6f) * (0.6f + (float)rng.NextDouble() * 0.6f);
        float waves = 1.5f + (float)rng.NextDouble() * 1.5f;
        float phase = (float)rng.NextDouble() * Mathf.PI * 2f;

        int steps = Mathf.CeilToInt(len * 2f);
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            float env = Mathf.Sin(t * Mathf.PI); // pinned to the endpoints
            Vector2 p = Vector2.Lerp(pa, pb, t) + perp * (Mathf.Sin(t * Mathf.PI * waves + phase) * amp * env);
            StampPath(p);
        }
    }

    void StampPath(Vector2 p)
    {
        float r = pathWidth * 0.5f;
        int ri = Mathf.CeilToInt(r);
        int cx = Mathf.RoundToInt(p.x), cy = Mathf.RoundToInt(p.y);
        for (int dx = -ri; dx <= ri; dx++)
            for (int dy = -ri; dy <= ri; dy++)
            {
                int x = cx + dx, y = cy + dy;
                if (x < 0 || y < 0 || x >= width || y >= height || !land[x, y]) continue;
                if (Vector2.Distance(new Vector2(x, y), p) <= r) path[x, y] = true;
            }
    }

    bool NearPath(int x, int y, int radius)
    {
        for (int dx = -radius; dx <= radius; dx++)
            for (int dy = -radius; dy <= radius; dy++)
            {
                int nx = x + dx, ny = y + dy;
                if (nx >= 0 && ny >= 0 && nx < width && ny < height && path[nx, ny]) return true;
            }
        return false;
    }

    // ------------------------------------------------------------------ house

    bool[,] houseBlock;      // footprint (+ margin) the house occupies; trees stay out of it
    Vector2Int houseCell;    // cell the house's front wall stands on

    bool TryPlaceHouse(Vector2Int spawn)
    {
        houseBlock = null;
        if (landSpawn.Count == 0) return false;

        for (int attempt = 0; attempt < 800; attempt++)
        {
            Vector2Int c = landSpawn[rng.Next(landSpawn.Count)];
            float d = Vector2.Distance(c, spawn);
            if (d < 9f || d > 22f) continue;

            // 11 x 9 clear, inland, path-free footprint.
            bool ok = true;
            for (int x = c.x - 5; x <= c.x + 5 && ok; x++)
                for (int y = c.y - 3; y <= c.y + 5 && ok; y++)
                    if (x < 0 || y < 0 || x >= width || y >= height || !land[x, y] || path[x, y] || distFromWater[x, y] < 4)
                        ok = false;
            if (!ok) continue;

            houseBlock = new bool[width, height];
            for (int x = c.x - 5; x <= c.x + 5; x++)
                for (int y = c.y - 3; y <= c.y + 5; y++)
                    houseBlock[x, y] = true;
            houseCell = c;
            return true;
        }
        return false;
    }

    // ---------------------------------------------------------------- tilemaps

    void BuildTilemaps()
    {
        var grid = root.AddComponent<Grid>();
        grid.cellSize = Vector3.one;

        Tilemap water = MakeLayer("Ocean", -30);
        Tilemap ground = MakeLayer("Flower Grass", -20);
        Tilemap paths = MakeLayer("Paths", -10);

        // Ocean is solid to the player: static composite collider over the water tilemap.
        var body = water.gameObject.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Static;
        var tc = water.gameObject.AddComponent<TilemapCollider2D>();
        var cc = water.gameObject.AddComponent<CompositeCollider2D>();
        tc.usedByComposite = true;
        cc.geometryType = CompositeCollider2D.GeometryType.Polygons;

        Tile[] waterTiles = MakeTiles(waterSprite, true, new[] { Color.white, new Color(0.96f, 0.98f, 1f) });
        Tile[] grassTiles = MakeTiles(flowerGrassSprite, false,
            new[] { Color.white, Color.white, new Color(0.94f, 0.97f, 0.94f), new Color(1f, 0.97f, 0.93f) });
        Tile[] pathTiles = MakeTiles(pathSprite, false, new[] { Color.white, new Color(0.97f, 0.96f, 0.94f) });

        var wPos = new List<Vector3Int>(); var wT = new List<TileBase>();
        var gPos = new List<Vector3Int>(); var gT = new List<TileBase>();
        var pPos = new List<Vector3Int>(); var pT = new List<TileBase>();

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                var cell = new Vector3Int(origin.x + x, origin.y + y, 0);
                if (!land[x, y]) { wPos.Add(cell); wT.Add(waterTiles[rng.Next(waterTiles.Length)]); }
                else
                {
                    gPos.Add(cell); gT.Add(grassTiles[rng.Next(grassTiles.Length)]);
                    if (path[x, y]) { pPos.Add(cell); pT.Add(pathTiles[rng.Next(pathTiles.Length)]); }
                }
            }

        water.SetTiles(wPos.ToArray(), wT.ToArray());
        ground.SetTiles(gPos.ToArray(), gT.ToArray());
        paths.SetTiles(pPos.ToArray(), pT.ToArray());
    }

    Tilemap MakeLayer(string layerName, int sortingOrder)
    {
        var go = new GameObject(layerName);
        go.transform.SetParent(root.transform, false);
        var tm = go.AddComponent<Tilemap>();
        var r = go.AddComponent<TilemapRenderer>();
        r.sortingOrder = sortingOrder;
        r.sharedMaterial = SpriteMaterial;   // explicit, so builds (WebGL) don't fall back to a missing shader (magenta)
        return tm;
    }

    static Material spriteMaterial;
    static Material SpriteMaterial
    {
        get
        {
            if (spriteMaterial == null)
            {
                Shader s = Shader.Find("Sprites/Default");
                if (s != null) spriteMaterial = new Material(s);
            }
            return spriteMaterial;
        }
    }

    /// <summary>Creates tile variants (x-flip x tint), each scaled so the sprite exactly fills one cell.</summary>
    Tile[] MakeTiles(Sprite s, bool collider, Color[] tints)
    {
        const float overscan = 1.01f; // hides hairline seams between tiles
        float sx = overscan / s.bounds.size.x, sy = overscan / s.bounds.size.y;
        var list = new List<Tile>();
        foreach (float flip in new[] { 1f, -1f })
            foreach (Color tint in tints)
            {
                var t = ScriptableObject.CreateInstance<Tile>();
                t.sprite = s;
                t.color = tint;
                t.flags = TileFlags.LockAll;
                t.transform = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(sx * flip, sy, 1f));
                t.colliderType = collider ? Tile.ColliderType.Grid : Tile.ColliderType.None;
                list.Add(t);
            }
        return list.ToArray();
    }

    // ------------------------------------------------------------------ trees

    void BuildTrees(Vector2Int spawn, Vector2Int forestCenter)
    {
        var parent = new GameObject("Trees").transform;
        parent.SetParent(root.transform, false);

        // Bottom-centre pivot so y-sorting uses the trunk base (walk behind/in front correctly).
        Sprite sprite = Sprite.Create(treeSprite.texture, treeSprite.textureRect, new Vector2(0.5f, 0f), treeSprite.pixelsPerUnit);
        sprite.name = "TreeBottomPivot";

        float noiseX = rng.Next(0, 10000), noiseY = rng.Next(0, 10000);
        var taken = new bool[width, height];
        int count = 0;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (!land[x, y] || path[x, y] || taken[x, y]) continue;
                if (houseBlock != null && houseBlock[x, y]) continue;               // keep the house site clear
                if (distFromWater[x, y] < 2) continue;                     // keep beaches clear
                if (pathClearance > 0 && NearPath(x, y, pathClearance)) continue; // keep walkways clear
                if (Vector2.Distance(new Vector2(x, y), spawn) < 5f) continue;      // clearing at spawn

                // Forest mask: circle around the forest centre with a noisy edge.
                float dd = Vector2.Distance(new Vector2(x, y), forestCenter) / forestRadius;
                dd += (Mathf.PerlinNoise(noiseX + x * 0.12f, noiseY + y * 0.12f) - 0.5f) * 0.6f;
                float chance = dd < 1f ? forestDensity * Mathf.Lerp(1f, 0.55f, Mathf.Clamp01(dd)) : meadowTreeChance;

                if (rng.NextDouble() > chance) continue;
                if (HasNeighbourTree(taken, x, y)) continue;               // minimum spacing

                taken[x, y] = true;
                PlaceTree(parent, sprite, x, y);
                count++;
            }
        }
        Debug.Log($"WorldGenerator: placed {count} trees.");
    }

    bool HasNeighbourTree(bool[,] taken, int x, int y)
    {
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                int nx = x + dx, ny = y + dy;
                if (nx >= 0 && ny >= 0 && nx < width && ny < height && taken[nx, ny]) return true;
            }
        return false;
    }

    void PlaceTree(Transform parent, Sprite sprite, int x, int y)
    {
        float h = treeHeight * (0.85f + 0.3f * (float)rng.NextDouble());
        float s = h / sprite.bounds.size.y;
        float jx = ((float)rng.NextDouble() - 0.5f) * 0.7f;
        float jy = ((float)rng.NextDouble() - 0.5f) * 0.5f;

        var go = new GameObject("Tree");
        go.transform.SetParent(parent, false);
        go.transform.position = CellCenter(x, y) + new Vector3(jx, jy - 0.3f, 0f); // origin = trunk base
        go.transform.localScale = new Vector3(s, s, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.flipX = rng.Next(2) == 0;

        // Small trunk collider (world size 0.5 x 0.4), expressed in the scaled local space.
        var box = go.AddComponent<BoxCollider2D>();
        box.size = new Vector2(0.5f / s, 0.4f / s);
        box.offset = new Vector2(0f, 0.2f / s);
    }

    // ---------------------------------------------------------- world boundary

    void BuildBorderColliders()
    {
        var parent = new GameObject("World Border").transform;
        parent.SetParent(root.transform, false);
        Rect b = WorldBounds;
        const float t = 4f;
        MakeWall(parent, new Vector2(b.center.x, b.yMax + t / 2f), new Vector2(b.width + t * 2f, t));
        MakeWall(parent, new Vector2(b.center.x, b.yMin - t / 2f), new Vector2(b.width + t * 2f, t));
        MakeWall(parent, new Vector2(b.xMin - t / 2f, b.center.y), new Vector2(t, b.height));
        MakeWall(parent, new Vector2(b.xMax + t / 2f, b.center.y), new Vector2(t, b.height));
    }

    static void MakeWall(Transform parent, Vector2 pos, Vector2 size)
    {
        var go = new GameObject("Wall");
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.AddComponent<BoxCollider2D>().size = size;
    }

    // ------------------------------------------------------- player and camera

    Transform PlacePlayer()
    {
        Transform player = null;
        var pm = FindObjectOfType<PlayerMovement2D>();
        if (pm != null) player = pm.transform;
        else
        {
            try { var go = GameObject.FindWithTag("Player"); if (go != null) player = go.transform; }
            catch (UnityException) { /* tag not defined */ }
        }

        // No player in the scene -> create one so the world is playable out of the box.
        if (player == null && playerSprite != null)
        {
            var go = new GameObject("Player");
            try { go.tag = "Player"; } catch (UnityException) { }
            go.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = playerSprite;
            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.3f / 0.8f;
            col.offset = new Vector2(0f, -0.45f);
            go.AddComponent<PlayerMovement2D>(); // also adds the Rigidbody2D
            player = go.transform;
        }

        if (player != null)
        {
            var rb = player.GetComponent<Rigidbody2D>();
            Vector3 pos = new Vector3(SpawnPosition.x, SpawnPosition.y, player.position.z);
            player.position = pos;
            if (rb != null) rb.position = pos;
        }
        if (player != null)
        {
            // Health, fighting/hugging and fear-of-water behaviours.
            var health = player.GetComponent<PlayerHealth>();
            if (health == null) health = player.gameObject.AddComponent<PlayerHealth>();
            var combat = player.GetComponent<PlayerCombat>();
            if (combat == null) combat = player.gameObject.AddComponent<PlayerCombat>();
            var fear = player.GetComponent<PlayerFear>();
            if (fear == null) fear = player.gameObject.AddComponent<PlayerFear>();
            combat.world = this;
            fear.world = this;
            var steps = player.GetComponent<FootstepSfx>();
            if (steps == null) steps = player.gameObject.AddComponent<FootstepSfx>();
            steps.world = this;
            SoundFx.Ensure();
            MusicManager.Ensure().Init(this, player);
            Epilogue.Ensure().world = this;
            health.Revive();               // full health + closes any game-over screen
            GameUI.Ensure().ResetScore();
        }
        return player;
    }

    void SetupCamera(Transform target)
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            cam = go.AddComponent<Camera>();
        }

        if (cam.GetComponent<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>(); // needed to hear the dialogue blips
        cam.orthographic = true;
        cam.orthographicSize = cameraSize;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.16f, 0.36f, 0.55f);

        // Sort sprites (player, trees, whales) by Y so they overlap correctly in top-down view.
        cam.transparencySortMode = TransparencySortMode.CustomAxis;
        cam.transparencySortAxis = new Vector3(0f, 1f, 0f);

        var follow = cam.GetComponent<BoundedCameraFollow>();
        if (follow == null) follow = cam.gameObject.AddComponent<BoundedCameraFollow>();
        follow.target = target;
        follow.worldBounds = WorldBounds;
        follow.desiredSize = cameraSize;
        follow.Snap(target != null ? target.position : SpawnPosition);
    }
}
