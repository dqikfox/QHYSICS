using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static object pool for one-shot spark and smoke particle effects.
/// Self-initializing at runtime via RuntimeInitializeOnLoadMethod.
/// No scene edits required — creates its own GameObject and particle systems programmatically.
/// </summary>
public class JuiceManager : MonoBehaviour
{
    private static JuiceManager _instance;

    [Header("Pool Settings")]
    [SerializeField] private int _poolSize = 12;

    private readonly List<ParticleSystem> _sparkPool = new List<ParticleSystem>();
    private readonly List<ParticleSystem> _smokePool = new List<ParticleSystem>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInit()
    {
        EnsureInstance();
    }

    private static void EnsureInstance()
    {
        if (_instance != null) return;
        var go = new GameObject("JuiceManager");
        _instance = go.AddComponent<JuiceManager>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        for (int i = 0; i < _poolSize; i++)
        {
            _sparkPool.Add(CreateSparkSystem());
            _smokePool.Add(CreateSmokeSystem());
        }
    }

    /// <summary>
    /// Spawn a one-shot spark burst at the given world position.
    /// </summary>
    public static void SparkAt(Vector3 pos)
    {
        EnsureInstance();
        _instance.SpawnFromPool(_instance._sparkPool, pos);
    }

    /// <summary>
    /// Spawn a one-shot smoke puff at the given world position.
    /// </summary>
    public static void SmokeAt(Vector3 pos)
    {
        EnsureInstance();
        _instance.SpawnFromPool(_instance._smokePool, pos);
    }

    private void SpawnFromPool(List<ParticleSystem> pool, Vector3 pos)
    {
        for (int i = 0; i < pool.Count; i++)
        {
            var ps = pool[i];
            if (!ps.gameObject.activeSelf)
            {
                ps.transform.position = pos;
                ps.gameObject.SetActive(true);
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Play(true);
                float lifetime = ps.main.startLifetime.constantMax + ps.main.duration;
                StartCoroutine(ReturnToPool(ps, lifetime + 0.1f));
                return;
            }
        }
    }

    private IEnumerator ReturnToPool(ParticleSystem ps, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (ps != null)
            ps.gameObject.SetActive(false);
    }

    private ParticleSystem CreateSparkSystem()
    {
        var go = new GameObject("SparkBurst_Pooled");
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.loop = false;
        main.duration = 0.3f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.01f, 0.03f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.5f, 0.1f, 1f),
            new Color(1f, 0.9f, 0.7f, 1f));
        main.gravityModifier = 1.2f;
        main.maxParticles = 40;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0;
        emission.burstCount = 1;
        emission.SetBurst(0, new ParticleSystem.Burst(0f, 30, 10, 0.05f));

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.03f;

        // Color fades from bright to dark over lifetime
        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.8f, 0.4f), 0f), new GradientColorKey(new Color(0.8f, 0.3f, 0.05f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = grad;

        // Size shrinks over lifetime
        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        var sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 1f);
        sizeCurve.AddKey(1f, 0.2f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        ConfigureRenderer(go.GetComponent<ParticleSystemRenderer>(), additive: true, baseColor: new Color(1f, 0.8f, 0.4f, 1f), stretch: true);

        go.SetActive(false);
        return ps;
    }

    private ParticleSystem CreateSmokeSystem()
    {
        var go = new GameObject("SmokePuff_Pooled");
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.loop = false;
        main.duration = 0.5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.4f, 0.4f, 0.4f, 0.6f),
            new Color(0.6f, 0.6f, 0.6f, 0.4f));
        main.gravityModifier = -0.3f; // slight upward drift
        main.maxParticles = 20;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0;
        emission.burstCount = 1;
        emission.SetBurst(0, new ParticleSystem.Burst(0f, 15, 5, 0.1f));

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.05f;

        // Smoke fades out over lifetime
        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(new Color(0.5f, 0.5f, 0.5f), 0f), new GradientColorKey(new Color(0.3f, 0.3f, 0.3f), 1f) },
            new[] { new GradientAlphaKey(0.7f, 0f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = grad;

        // Smoke grows over lifetime
        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        var sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.3f);
        sizeCurve.AddKey(1f, 2f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        ConfigureRenderer(go.GetComponent<ParticleSystemRenderer>(), additive: false, baseColor: new Color(0.5f, 0.5f, 0.5f, 1f), stretch: false);

        go.SetActive(false);
        return ps;
    }

    private void ConfigureRenderer(ParticleSystemRenderer renderer, bool additive, Color baseColor, bool stretch)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        var mat = new Material(shader);
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", baseColor);
        if (additive && mat.HasProperty("_Surface"))
        {
            mat.SetFloat("_Surface", 1f); // Transparent
            mat.SetFloat("_Blend", 2f);   // Additive
        }
        renderer.material = mat;

        if (stretch)
        {
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 3f;
            renderer.velocityScale = 0.05f;
        }
        else
        {
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
        }

        renderer.sortingFudge = -1f;
    }
}
