using UnityEngine;

/// <summary>
/// Player actions: left-click a nearby fox to hit it (4 hits to kill), press E to use things
/// (house door, bed, journal, lamp...) or to hug a cat. When dead, press R for a fresh world.
/// </summary>
public class PlayerCombat : MonoBehaviour
{
    public WorldGenerator world;
    public float attackRange = 2.4f;     // how close the fox must be to the player
    public float clickRadius = 0.9f;     // how close the click must be to the fox
    public float attackCooldown = 0.28f;
    public float hugRange = 1.8f;

    PlayerHealth health;
    float nextAttack;
    Camera cam;

    public PlayerHealth Health => health;

    void Awake() { health = GetComponent<PlayerHealth>(); }

    void Update()
    {
        if (Time.timeScale == 0f) return;   // paused / on the title screen: ignore clicks and E

        var ui = GameUI.Ensure();

        if (health.IsDead)
        {
            ui.SetPrompt("");
            if (GameInput.RestartPressed() && world != null) world.Generate();
            return;
        }

        // Dialogue / journal / fades freeze the player.
        bool modal = ui.ModalOpen;
        health.SetFrozen(modal);
        if (modal) { ui.SetPrompt(""); return; }

        Interactable thing = Interactable.FindNearest(transform.position);
        if (thing != null)
        {
            ui.SetPrompt(thing.Prompt);
            if (GameInput.HugPressed()) thing.Use(this);
        }
        else
        {
            Cat cat = NearestCat();
            ui.SetPrompt(cat != null ? "Press E to hug the cat" : "");
            if (cat != null && GameInput.HugPressed()) cat.Hug(health);
        }

        if (!health.InHouse && GameInput.AttackPressed() && Time.time >= nextAttack) TryAttack();
    }

    void TryAttack()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;
        nextAttack = Time.time + attackCooldown;
        SoundFx.Play("swing", null, 0.6f);

        Vector2 mouse = GameInput.MouseWorld(cam);
        Fox best = null;
        float bestDist = clickRadius;
        foreach (Fox f in Fox.All)
        {
            float d = Vector2.Distance(mouse, f.transform.position);
            if (d < bestDist) { bestDist = d; best = f; }
        }
        if (best == null) return;

        if (Vector2.Distance(transform.position, best.transform.position) > attackRange)
        {
            GameUI.Ensure().Toast("Too far away!", 0.8f);
            return;
        }
        SoundFx.Play("hit", best.transform.position);
        best.TakeHit(transform.position);
    }

    Cat NearestCat()
    {
        Cat best = null;
        float bestDist = hugRange;
        foreach (Cat c in Cat.All)
        {
            float d = Vector2.Distance(transform.position, c.transform.position);
            if (d < bestDist) { bestDist = d; best = c; }
        }
        return best;
    }
}
