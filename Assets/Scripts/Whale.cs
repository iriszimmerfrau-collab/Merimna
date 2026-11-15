using UnityEngine;

/// <summary>A single swimming whale: fades in, drifts with a gentle bob, fades out, destroys itself.</summary>
public class Whale : MonoBehaviour
{
    const float FadeTime = 3.8f;

    WhaleSpawner spawner;
    WorldGenerator world;
    SpriteRenderer sr;
    Vector2 dir, basePos;
    float speed, life, age, phase, bobFreq, bobAmp;

    public void Init(WhaleSpawner owner, WorldGenerator w, Vector2 direction, float spd, float lifeTime)
    {
        spawner = owner;
        world = w;
        dir = direction;
        speed = spd;
        life = lifeTime;
        basePos = transform.position;
        sr = GetComponent<SpriteRenderer>();
        phase = Random.value * Mathf.PI * 2f;
        bobFreq = Random.Range(0.7f, 2.4f);
        bobAmp = Random.Range(0.8f, 0.9f);
    }

    void Update()
    {
        age += Time.deltaTime;
        basePos += dir * speed * Time.deltaTime;

        // Never swim into shallows or land: start fading out early.
        if (life - age > FadeTime && !world.IsOpenWater(basePos + dir * 1.5f, 1))
            life = age + FadeTime;

        float wave = Mathf.Sin(age * bobFreq * Mathf.PI * 2f + phase);
        transform.position = new Vector3(basePos.x, basePos.y + wave * bobAmp, 0f);
        transform.rotation = Quaternion.Euler(0f, 0f, wave * 3f);

        float a = Mathf.Clamp01(Mathf.Min(age / FadeTime, (life - age) / FadeTime));
        sr.color = new Color(1f, 1f, 1f, a);

        if (age >= life) Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (spawner != null) spawner.NotifyWhaleGone();
    }
}
