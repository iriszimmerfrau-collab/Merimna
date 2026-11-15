using UnityEngine;

/// <summary>A small heart that floats up and fades out (used when hugging cats).</summary>
public class FloatingFx : MonoBehaviour
{
    float age, life = 1.3f, drift;
    SpriteRenderer sr;

    public static void Hearts(Vector3 pos, int count = 3)
    {
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Heart Fx");
            go.transform.position = pos + new Vector3((i - (count - 1) / 2f) * 0.45f, Random.Range(0f, 0.2f), 0f);
            go.transform.localScale = Vector3.one * 4f;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = HeartSprites.Full;
            sr.sortingOrder = 50;
            var fx = go.AddComponent<FloatingFx>();
            fx.sr = sr;
            fx.life = Random.Range(1.0f, 1.5f);
            fx.drift = Random.Range(-0.3f, 0.3f);
        }
    }

    void Update()
    {
        age += Time.deltaTime;
        transform.position += new Vector3(drift, 1.1f, 0f) * Time.deltaTime;
        sr.color = new Color(1, 1, 1, 1f - age / life);
        if (age >= life) Destroy(gameObject);
    }
}
