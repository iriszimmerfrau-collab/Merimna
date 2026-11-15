using UnityEngine;

/// <summary>Plays footsteps while the player moves: soft on grass, crisp on paths, hollow on the house floor.</summary>
public class FootstepSfx : MonoBehaviour
{
    public WorldGenerator world;

    PlayerHealth health;
    Vector3 last;
    float stepTimer;

    void Start()
    {
        health = GetComponent<PlayerHealth>();
        last = transform.position;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        float speed = (transform.position - last).magnitude / dt;
        last = transform.position;

        if (speed > 20f) { stepTimer = 0.1f; return; }          // teleported (door), not walking
        if (speed < 0.6f) { stepTimer = 0.05f; return; }        // standing still: first step comes quickly

        stepTimer -= dt;
        if (stepTimer > 0f) return;
        stepTimer = speed > 6.5f ? 0.24f : 0.36f;               // sprinting steps faster

        string stepName;
        if (health != null && health.InHouse) stepName = "step_wood";
        else if (world != null && world.IsPathAt(transform.position)) stepName = "step_path";
        else stepName = "step_grass";
        SoundFx.Play(stepName, null, 0.7f);
    }
}
