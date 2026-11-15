using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A fox. Wanders until the player comes close, then chases and bites. Dies after 4 hits (+1 score).
/// Slower than the player's sprint, so you can run away.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class Fox : MonoBehaviour
{
    public static readonly List<Fox> All = new List<Fox>();

    public int health = 4;
    public int damage = 1;                 // half-hearts per bite
    public float wanderSpeed = 1.2f;
    public float chaseSpeed = 3.6f;
    public float aggroRange = 9f;
    public float loseRange = 16f;
    public float attackRange = 1.0f;
    public float attackCooldown = 1.2f;
    public bool facesRight = true;

    Transform target;
    PlayerHealth targetHealth;
    Rigidbody2D rb;
    SpriteRenderer sr;

    bool chasing;
    float nextAttack, stunUntil, flashUntil, wanderTimer, stuckTimer, sidestepUntil;
    Vector2 wanderDir, knock, sidestepDir, lastPos;

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        sr = GetComponent<SpriteRenderer>();
    }

    public void Init(Transform player, PlayerHealth playerHealth)
    {
        target = player;
        targetHealth = playerHealth;
        lastPos = transform.position;

        // difficulty (Settings)
        damage = Mathf.Max(1, damage * GameSettings.FoxDamage);
        chaseSpeed *= GameSettings.FoxSpeed;
        attackCooldown *= GameSettings.FoxCooldown;
        aggroRange *= GameSettings.FoxAggro;
    }

    void Update()
    {
        if (sr != null) sr.color = Time.time < flashUntil ? new Color(1f, 0.3f, 0.3f) : Color.white;
    }

    void FixedUpdate()
    {
        bool playerAlive = target != null && targetHealth != null && !targetHealth.IsDead && !targetHealth.InHouse;
        float dist = playerAlive ? Vector2.Distance(rb.position, target.position) : float.MaxValue;

        if (playerAlive)
        {
            if (!chasing && dist < aggroRange) { chasing = true; SoundFx.Play("fox_growl", transform.position, 0.9f); }
            else if (chasing && dist > loseRange) chasing = false;
        }
        else chasing = false;

        Vector2 vel;
        if (Time.time < stunUntil)
        {
            knock = Vector2.Lerp(knock, Vector2.zero, 10f * Time.fixedDeltaTime);
            vel = knock;
        }
        else if (chasing)
        {
            Vector2 dir = ((Vector2)target.position - rb.position).normalized;

            if (dist <= attackRange)
            {
                vel = Vector2.zero;
                if (Time.time >= nextAttack)
                {
                    nextAttack = Time.time + attackCooldown;
                    if (targetHealth.TakeDamage(damage)) SoundFx.Play("bite", transform.position);
                }
            }
            else
            {
                // If blocked by a tree etc., sidestep for a moment.
                float moved = (rb.position - lastPos).magnitude;
                stuckTimer = moved < chaseSpeed * Time.fixedDeltaTime * 0.3f ? stuckTimer + Time.fixedDeltaTime : 0f;
                if (stuckTimer > 0.35f)
                {
                    stuckTimer = 0f;
                    sidestepDir = new Vector2(-dir.y, dir.x) * (Random.value < 0.5f ? 1f : -1f);
                    sidestepUntil = Time.time + 0.7f;
                }
                if (Time.time < sidestepUntil) dir = (dir + sidestepDir * 1.5f).normalized;
                vel = dir * chaseSpeed;
            }
        }
        else
        {
            wanderTimer -= Time.fixedDeltaTime;
            if (wanderTimer <= 0f)
            {
                wanderTimer = Random.Range(1.5f, 4f);
                wanderDir = Random.value < 0.4f ? Vector2.zero : Random.insideUnitCircle.normalized;
            }
            vel = wanderDir * wanderSpeed;
        }

        lastPos = rb.position;
        GameInput.SetVelocity(rb, vel);

        if (sr != null && Mathf.Abs(vel.x) > 0.05f) sr.flipX = facesRight ? vel.x < 0f : vel.x > 0f;
    }

    public void TakeHit(Vector2 fromPosition)
    {
        health--;
        flashUntil = Time.time + 0.15f;
        stunUntil = Time.time + 0.2f;
        knock = ((Vector2)transform.position - fromPosition).normalized * 6f;
        chasing = true;

        SoundFx.Play(health <= 0 ? "fox_die" : "fox_hurt", transform.position);

        if (health <= 0)
        {
            GameUI.Ensure().AddScore(1);
            Destroy(gameObject);
        }
    }
}
