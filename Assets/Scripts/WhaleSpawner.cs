using UnityEngine;

/// <summary>
/// Randomly spawns whales in deep ocean water. Each whale fades in, swims along a
/// straight line that stays in open water, then fades out and is destroyed.
/// Spawns prefer spots near the camera so they are actually seen.
/// </summary>
public class WhaleSpawner : MonoBehaviour
{
    public int maxWhales = 50;
    public int initialWhales = 10;
    public Vector2 spawnInterval = new Vector2(3f, 9f);
    public Vector2 lifetime = new Vector2(14f, 24f);
    public Vector2 speed = new Vector2(0.5f, 1.1f);
    [Tooltip("Rendered whale width in world units (randomly varied +-20%).")]
    public float whaleWidth = 4f;
    public bool spriteFacesRight = true;
    [Tooltip("Spawns are first attempted within this distance of the camera.")]
    public float nearCameraRadius = 15f;

    WorldGenerator world;
    Sprite sprite;
    float timer;
    int alive;

    public void Init(WorldGenerator w, Sprite whaleSprite)
    {
        world = w;
        sprite = whaleSprite;
        timer = Random.Range(spawnInterval.x, spawnInterval.y);
        for (int i = 0; i < initialWhales; i++) TrySpawn();
    }

    void Update()
    {
        if (world == null) return;
        timer -= Time.deltaTime;
        if (timer > 0f) return;

        timer = Random.Range(spawnInterval.x, spawnInterval.y);
        if (alive < maxWhales) TrySpawn();
    }

    public void NotifyWhaleGone() { alive--; }

    bool TrySpawn()
    {
        var cells = world.DeepWaterCells;
        if (cells.Count == 0 || sprite == null) return false;

        Camera cam = Camera.main;

        for (int attempt = 0; attempt < 30; attempt++)
        {
            Vector2 p = world.CellCenterWorld(cells[Random.Range(0, cells.Count)])
                      + new Vector2(Random.value - 0.5f, Random.value - 0.5f);

            // First 15 attempts: only accept spots near the camera.
            if (cam != null && attempt < 15 && Vector2.Distance(p, cam.transform.position) > nearCameraRadius) continue;

            float spd = Random.Range(speed.x, speed.y);
            float life = Random.Range(lifetime.x, lifetime.y);
            float travel = spd * life + 2f;

            // Pick a mostly-horizontal heading whose whole path stays in open water.
            for (int k = 0; k < 8; k++)
            {
                float angle = Random.Range(-35f, 35f) + (Random.value < 0.5f ? 180f : 0f);
                Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                if (!PathIsClear(p, dir, travel)) continue;

                SpawnWhale(p, dir, spd, life);
                return true;
            }
        }
        return false;
    }

    bool PathIsClear(Vector2 from, Vector2 dir, float length)
    {
        for (float d = 0f; d <= length; d += 1f)
            if (!world.IsOpenWater(from + dir * d, 2)) return false;
        return true;
    }

    void SpawnWhale(Vector2 pos, Vector2 dir, float spd, float life)
    {
        var go = new GameObject("Whale");
        go.transform.SetParent(transform, false);
        go.transform.position = pos;

        float s = whaleWidth * Random.Range(0.8f, 1.2f) / sprite.bounds.size.x;
        go.transform.localScale = new Vector3(s, s, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 1;                       // above the ocean tilemap
        sr.color = new Color(1f, 1f, 1f, 0f);
        bool goingRight = dir.x >= 0f;
        sr.flipX = spriteFacesRight ? !goingRight : goingRight;

        var whale = go.AddComponent<Whale>();
        whale.Init(this, world, dir, spd, life);
        alive++;
        if (Random.value < 0.7f) SoundFx.Play("whale", (Vector3)pos, 0.9f);   // a distant call as it surfaces
    }
}
