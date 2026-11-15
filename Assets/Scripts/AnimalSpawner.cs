using UnityEngine;

/// <summary>
/// Places cats around the island at the start and keeps randomly spawning foxes
/// (always out of the player's sight) up to a maximum.
/// </summary>
public class AnimalSpawner : MonoBehaviour
{
    public int maxFoxes = 10;
    public int initialFoxes = 6;
    public int catCount = 10;
    public Vector2 foxInterval = new Vector2(5f, 10f);
    [Tooltip("Foxes never spawn closer than this to the player (keeps them off-screen).")]
    public float minFoxSpawnDistance = 16f;
    public float foxWidth = 1.2f;
    public float catWidth = 0.9f;
    public bool foxFacesRight = true;
    public bool catsFaceRight = true;

    WorldGenerator world;
    Transform player;
    PlayerHealth playerHealth;
    Collider2D playerCollider;
    Sprite foxSprite, whiteCat, orangeCat;
    float timer;

    public void Init(WorldGenerator w, Transform playerTransform, Sprite fox, Sprite catWhite, Sprite catOrange)
    {
        world = w;
        player = playerTransform;
        playerHealth = player != null ? player.GetComponent<PlayerHealth>() : null;
        playerCollider = player != null ? player.GetComponent<Collider2D>() : null;
        foxSprite = fox; whiteCat = catWhite; orangeCat = catOrange;

        maxFoxes = Mathf.Max(1, Mathf.RoundToInt(maxFoxes * GameSettings.FoxCount));       // difficulty
        initialFoxes = Mathf.Max(1, Mathf.RoundToInt(initialFoxes * GameSettings.FoxCount));

        timer = Random.Range(foxInterval.x, foxInterval.y);

        if (whiteCat != null || orangeCat != null)
            for (int i = 0; i < catCount; i++) SpawnCat(i);
        if (foxSprite != null)
            for (int i = 0; i < initialFoxes; i++) SpawnFox();
    }

    void Update()
    {
        if (foxSprite == null) return;
        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = Random.Range(foxInterval.x, foxInterval.y);
        bool playerAlive = playerHealth == null || !playerHealth.IsDead;
        if (playerAlive && Fox.All.Count < maxFoxes) SpawnFox();
    }

    bool TryGetPoint(float minDistFromPlayer, out Vector2 point)
    {
        var cells = world.LandSpawnCells;
        point = Vector2.zero;
        if (cells.Count == 0) return false;

        for (int i = 0; i < 40; i++)
        {
            Vector2 p = world.CellCenterWorld(cells[Random.Range(0, cells.Count)])
                      + new Vector2(Random.value - 0.5f, Random.value - 0.5f);
            if (player != null && Vector2.Distance(p, player.position) < minDistFromPlayer) continue;
            if (Physics2D.OverlapCircle(p, 0.5f) != null) continue;   // not inside a tree
            point = p;
            return true;
        }
        return false;
    }

    void SpawnFox()
    {
        if (!TryGetPoint(minFoxSpawnDistance, out Vector2 p)) return;
        var go = CreateAnimal("Fox", foxSprite, p, foxWidth);
        var fox = go.AddComponent<Fox>();
        fox.facesRight = foxFacesRight;
        fox.Init(player, playerHealth);
    }

    void SpawnCat(int index)
    {
        if (!TryGetPoint(6f, out Vector2 p)) return;
        Sprite s = (index % 2 == 0) ? whiteCat : orangeCat;
        if (s == null) s = whiteCat != null ? whiteCat : orangeCat;
        var go = CreateAnimal("Cat", s, p, catWidth);
        var cat = go.AddComponent<Cat>();
        cat.facesRight = catsFaceRight;
    }

    GameObject CreateAnimal(string animalName, Sprite sprite, Vector2 pos, float width)
    {
        var go = new GameObject(animalName);
        go.transform.SetParent(transform, false);
        go.transform.position = pos;
        float s = width / sprite.bounds.size.x;
        go.transform.localScale = new Vector3(s, s, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;

        var col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.3f / s;
        col.offset = new Vector2(0f, -0.1f / s);

        // Animals shouldn't shove the player around (damage/hugging are handled by distance).
        if (playerCollider != null) Physics2D.IgnoreCollision(col, playerCollider);
        return go;
    }
}
