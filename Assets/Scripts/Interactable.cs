using System.Collections.Generic;
using UnityEngine;

/// <summary>Something the player can use by standing close and pressing E (doors, bed, lamp, journal...).</summary>
public class Interactable : MonoBehaviour
{
    public static readonly List<Interactable> All = new List<Interactable>();

    public string prompt = "Press E";
    public float radius = 2f;
    public System.Func<string> promptGetter;          // optional: dynamic prompt text
    public System.Action<PlayerCombat> onUse;

    public string Prompt => promptGetter != null ? promptGetter() : prompt;

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    public void Use(PlayerCombat player) { if (onUse != null) onUse(player); }

    public static Interactable Create(GameObject go, string promptText, float useRadius, System.Action<PlayerCombat> use)
    {
        var it = go.AddComponent<Interactable>();
        it.prompt = promptText;
        it.radius = useRadius;
        it.onUse = use;
        return it;
    }

    public static Interactable FindNearest(Vector2 pos)
    {
        Interactable best = null;
        float bestDist = float.MaxValue;
        foreach (var it in All)
        {
            float d = Vector2.Distance(pos, it.transform.position);
            if (d <= it.radius && d < bestDist) { bestDist = d; best = it; }
        }
        return best;
    }
}
