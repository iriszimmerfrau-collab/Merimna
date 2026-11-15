using UnityEngine;

/// <summary>
/// Player health in half-hearts (10 = 5 hearts). Fox bites remove half a heart; a cat hug restores a whole heart.
/// Owns the player's sprite colour (damage flash x fear tint).
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    public int maxHealth = 10;
    public float invulnerableTime = 0.9f;

    public int Health { get; private set; }
    public bool IsDead => Health <= 0;

    /// <summary>Set by PlayerFear each frame.</summary>
    public Color FearTint = Color.white;

    SpriteRenderer sr;
    Rigidbody2D rb;
    PlayerMovement2D movement;
    float invulnerableUntil, flashUntil;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        movement = GetComponent<PlayerMovement2D>();
        Health = maxHealth;
    }

    void Start() { GameUI.Ensure().SetHealth(Health, maxHealth); }

    public bool TakeDamage(int amount)
    {
        if (IsDead || Time.time < invulnerableUntil) return false;
        Health = Mathf.Max(0, Health - amount);
        invulnerableUntil = Time.time + invulnerableTime;
        flashUntil = Time.time + 0.25f;
        GameUI.Ensure().SetHealth(Health, maxHealth);
        SoundFx.Play("hurt");
        if (IsDead) Die();
        return true;
    }

    /// <returns>How many half-hearts were actually restored.</returns>
    public int Heal(int amount)
    {
        if (IsDead) return 0;
        int before = Health;
        Health = Mathf.Min(maxHealth, Health + amount);
        GameUI.Ensure().SetHealth(Health, maxHealth);
        return Health - before;
    }

    /// <summary>True while the player is inside the house (foxes lose track of them).</summary>
    public bool InHouse { get; set; }

    bool frozen;

    /// <summary>Stops/starts WASD movement (used while dialogue, journal or screen fades are up).</summary>
    /// <summary>While true (cutscenes) the player cannot be unfrozen by other scripts.</summary>
    public bool CutsceneLock;

    public void SetFrozen(bool value)
    {
        if (CutsceneLock) value = true;
        if (IsDead || value == frozen) return;
        frozen = value;
        if (movement != null) movement.enabled = !value;
        if (value && rb != null) GameInput.SetVelocity(rb, Vector2.zero);
    }

    /// <summary>Self-inflicted damage from the knife. Never fatal: always leaves at least half a heart.</summary>
    /// <returns>Half-hearts actually lost.</returns>
    public int HurtSelf(int amount)
    {
        if (IsDead) return 0;
        int lost = Mathf.Min(amount, Health - 1);
        if (lost <= 0) return 0;
        Health -= lost;
        flashUntil = Time.time + 0.5f;
        GameUI.Ensure().SetHealth(Health, maxHealth);
        SoundFx.Play("hurt");
        return lost;
    }

    void Die()
    {
        if (movement != null) movement.enabled = false;
        if (rb != null) GameInput.SetVelocity(rb, Vector2.zero);
        if (!Epilogue.TryBegin(this)) GameUI.Ensure().ShowGameOver(true);   // death = waking up from a bad dream
        MusicManager.SetDead(true);
        SoundFx.Play("gameover");
    }

    public void Revive()
    {
        if (Health <= 0 && Time.timeSinceLevelLoad > 1f) SoundFx.Play("revive");
        MusicManager.SetDead(false);
        Health = maxHealth;
        frozen = false;
        InHouse = false;
        invulnerableUntil = flashUntil = 0f;
        if (movement != null) movement.enabled = true;
        var ui = GameUI.Ensure();
        ui.SetHealth(Health, maxHealth);
        ui.ShowGameOver(false);
    }

    void LateUpdate()
    {
        if (sr == null) return;
        Color c = FearTint;
        if (Time.time < flashUntil) c = Color.Lerp(c, new Color(1f, 0.25f, 0.25f), 0.8f);
        if (Time.time < invulnerableUntil && !IsDead)
            c.a = (Mathf.FloorToInt(Time.time * 14f) % 2 == 0) ? 0.55f : 1f;  // blink while invulnerable
        if (IsDead) c = new Color(0.5f, 0.5f, 0.5f, 0.8f);
        sr.color = c;
    }
}
