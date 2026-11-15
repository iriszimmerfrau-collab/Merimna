using UnityEngine;

/// <summary>
/// Near water the player gets scared: vision darkens at the edges (pulsing like a heartbeat),
/// the sprite goes pale, the camera trembles and a "!" appears overhead.
/// </summary>
public class PlayerFear : MonoBehaviour
{
    public WorldGenerator world;
    [Tooltip("Fear starts this many tiles away from the water and grows as you get closer.")]
    public float fearDistance = 5f;
    [Tooltip("Fear from a fox starts at this distance (tiles) and peaks when it's adjacent.")]
    public float foxFearDistance = 7f;
    public float maxCameraShake = 0.05f;

    public float Fear { get; private set; }

    /// <summary>Current fear (0..1) for the music mixer.</summary>
    public static float Current { get; private set; }

    float heartbeatTimer, shiverTimer = 2f;

    PlayerHealth health;
    BoundedCameraFollow camFollow;
    Transform bubble;

    void Awake()
    {
        health = GetComponent<PlayerHealth>();
        BuildBubble();
    }

    void Update()
    {
        float d = world != null ? world.DistanceToWater(transform.position) : 99f;
        float target = Mathf.Clamp01((fearDistance - d + 1f) / fearDistance); // 1 right at the shore, 0 beyond fearDistance

        // Foxes frighten the player too: 0 at foxFearDistance, 1 when a fox is right next to them.
        foreach (Fox f in Fox.All)
        {
            float fd = Vector2.Distance(transform.position, f.transform.position);
            target = Mathf.Max(target, Mathf.Clamp01((foxFearDistance - fd) / (foxFearDistance - 1f)));
        }

        // Hanging on by a thread (one heart or less) is terrifying on its own: strongest at half a heart.
        // Healing (a cat hug) or sleeping calms it again.
        if (health != null && !health.IsDead && health.Health <= 2)
            target = Mathf.Max(target, health.Health <= 1 ? 1f : 0.85f);

        if (health != null && health.IsDead) target = 0f;
        Fear = Mathf.Lerp(Fear, target, 1f - Mathf.Exp(-3f * Time.deltaTime));

        Current = Fear;

        // heartbeat speeds up as fear grows; occasional shivers
        if (Fear > 0.2f)
        {
            heartbeatTimer -= Time.deltaTime;
            if (heartbeatTimer <= 0f)
            {
                SoundFx.Play("heartbeat", null, Mathf.Clamp01(Fear * 0.9f + 0.1f));
                heartbeatTimer = Mathf.Lerp(1.15f, 0.45f, Fear);
            }
            if (Fear > 0.5f)
            {
                shiverTimer -= Time.deltaTime;
                if (shiverTimer <= 0f) { SoundFx.Play("shiver", null, Fear * 0.7f); shiverTimer = Random.Range(3f, 5.5f); }
            }
        }

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (4f + 6f * Fear));
        GameUI.Ensure().SetFear(Fear, pulse);

        if (health != null) health.FearTint = Color.Lerp(Color.white, new Color(0.72f, 0.8f, 1f), Fear);

        if (camFollow == null && Camera.main != null) camFollow = Camera.main.GetComponent<BoundedCameraFollow>();
        if (camFollow != null) camFollow.shake = GameSettings.ScreenShake ? Fear * maxCameraShake : 0f;

        if (bubble != null)
        {
            bool show = Fear > 0.35f;
            if (bubble.gameObject.activeSelf != show) bubble.gameObject.SetActive(show);
            if (show) bubble.localScale = Vector3.one * (1f + 0.15f * pulse);
        }
    }

    void BuildBubble()
    {
        var go = new GameObject("Fear Bubble");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 0.95f, 0f);
        var tm = go.AddComponent<TextMesh>();
        tm.text = "!";
        tm.font = GameUI.GetFont();
        tm.fontSize = 64;
        tm.characterSize = 0.1f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.color = new Color(1f, 0.95f, 0.6f);
        var mr = go.GetComponent<MeshRenderer>();
        if (tm.font != null) mr.sharedMaterial = tm.font.material;
        mr.sortingOrder = 60;
        bubble = go.transform;
        go.SetActive(false);
    }

    void OnDisable() { Current = 0f; if (camFollow != null) camFollow.shake = 0f; }
}
