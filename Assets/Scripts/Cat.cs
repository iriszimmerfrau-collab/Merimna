using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A friendly cat (white or orange). Wanders lazily. Standing next to it and pressing E hugs it
/// and restores one heart; each cat needs a rest between healing hugs.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class Cat : MonoBehaviour
{
    public static readonly List<Cat> All = new List<Cat>();

    public float wanderSpeed = 0.8f;
    public int healAmount = 2;          // half-hearts = one heart
    public float healCooldown = 10f;
    public bool facesRight = true;

    Rigidbody2D rb;
    SpriteRenderer sr;
    float wanderTimer, pausedUntil, nextHeal, nextHug;
    Vector2 wanderDir;

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        sr = GetComponent<SpriteRenderer>();
    }

    void FixedUpdate()
    {
        Vector2 vel = Vector2.zero;
        if (Time.time >= pausedUntil)
        {
            wanderTimer -= Time.fixedDeltaTime;
            if (wanderTimer <= 0f)
            {
                wanderTimer = Random.Range(1.5f, 5f);
                wanderDir = Random.value < 0.55f ? Vector2.zero : Random.insideUnitCircle.normalized;
            }
            vel = wanderDir * wanderSpeed;
        }
        GameInput.SetVelocity(rb, vel);
        if (sr != null && Mathf.Abs(vel.x) > 0.05f) sr.flipX = facesRight ? vel.x < 0f : vel.x > 0f;
    }

    public void Hug(PlayerHealth player)
    {
        if (Time.time < nextHug) return;
        nextHug = Time.time + 1.2f;
        pausedUntil = Time.time + 2f;                       // stay put and enjoy it
        if (sr != null)
        {
            float dx = player.transform.position.x - transform.position.x;
            if (Mathf.Abs(dx) > 0.05f) sr.flipX = facesRight ? dx < 0f : dx > 0f;
        }

        var ui = GameUI.Ensure();
        SoundFx.Play("purr", transform.position, 0.9f);
        Vector3 above = transform.position + Vector3.up * 0.6f;

        if (player.Health >= player.maxHealth)
        {
            FloatingFx.Hearts(above, 1);
            ui.Toast("Purr~ you're already full of love");
        }
        else if (Time.time < nextHeal)
        {
            FloatingFx.Hearts(above, 1);
            ui.Toast("This cat needs a rest before healing again");
        }
        else
        {
            player.Heal(Mathf.RoundToInt(healAmount * GameSettings.HealMultiplier));
            SoundFx.Play("heal");
            nextHeal = Time.time + healCooldown;
            FloatingFx.Hearts(above, 3);
            ui.Toast("Hugged the cat  +1 heart");
        }
    }
}
