using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public enum WeatherType { Clear, LightRain, Rain, Fog, Storm }

/// <summary>
/// Weather: clear / light rain / rain / fog / storm (with lightning + thunder).
/// Put it on an empty GameObject (one per scene) next to the ClockSystem.
/// Rain falls in a box right above the player. Changes blend smoothly.
/// Use WeatherSystem.Instance.SetWeather(WeatherType.Storm) from other scripts.
/// </summary>
public class WeatherSystem : MonoBehaviour
{
    public static WeatherSystem Instance { get; private set; }

    [Serializable]
    public class Profile
    {
        public WeatherType type;
        [Range(0f, 1f)] public float rain;
        [Tooltip("Fog density (Exponential Squared). 0 = no fog. 0.02 = thick.")]
        public float fogDensity;
        [Tooltip("1 = full light, lower = darker overcast sky.")]
        [Range(0.1f, 1f)] public float light = 1f;
        [Range(0f, 1f)] public float wind;
        public bool lightning;
        [Tooltip("Random chance weight when the weather changes by itself.")]
        public float chance = 1f;
        public Profile(WeatherType t, float rain, float fog, float light, float wind, bool lightning, float chance)
        { type = t; this.rain = rain; fogDensity = fog; this.light = light; this.wind = wind; this.lightning = lightning; this.chance = chance; }
    }

    [Header("State")]
    public WeatherType startWeather = WeatherType.Clear;
    [Tooltip("Seconds to blend from one weather to the next.")]
    public float transitionSeconds = 12f;

    [Header("Random changes")]
    public bool autoChange = true;
    [Tooltip("Real minutes before the weather changes by itself (random between min and max).")]
    public Vector2 changeEveryMinutes = new Vector2(3f, 8f);

    [Header("Weather types")]
    public Profile[] profiles =
    {
        new Profile(WeatherType.Clear,     0f,    0f,     1f,   0.1f, false, 4f),
        new Profile(WeatherType.LightRain, 0.35f, 0.002f, 0.8f, 0.25f, false, 2f),
        new Profile(WeatherType.Rain,      0.7f,  0.005f, 0.6f, 0.5f, false, 2f),
        new Profile(WeatherType.Fog,       0f,    0.02f,  0.7f, 0.05f, false, 1.5f),
        new Profile(WeatherType.Storm,     1f,    0.008f, 0.4f, 1f,   true,  1f),
    };

    [Header("Rain")]
    [Tooltip("X = width, Z = depth of the rain area (Y is ignored).")]
    public Vector3 rainArea = new Vector3(14f, 1f, 14f);
    [Tooltip("Player (or camera) the rain follows. If empty, the script looks for the Player tag, then Camera.main.")]
    public Transform player;
    [Tooltip("Height above the player where raindrops spawn.")]
    public float rainSpawnHeight = 6f;
    public int maxRainPerSecond = 6000;
    public Color rainColor = new Color(0.8f, 0.85f, 1f, 0.35f);
    [Tooltip("Optional. If empty a simple default material is created. In a BUILD, assign one here (the default shader can be stripped).")]
    public Material rainMaterial;
    public bool controlFog = true;

    [Header("Lightning (storm)")]
    public Vector2 secondsBetweenLightning = new Vector2(6f, 18f);

    [Header("Audio (optional)")]
    public AudioClip rainLoop;
    [Range(0f, 1f)] public float rainVolume = 0.6f;
    public AudioClip[] thunderClips;

    public WeatherType Current { get; private set; }
    /// <summary>Current rain strength 0..1 (use it for gameplay, e.g. slippery ground).</summary>
    public float Rain01 => cur.rain;
    public bool IsRaining => cur.rain > 0.05f;
    public event Action<WeatherType> OnWeatherChanged;

    private struct Vals { public float rain, fog, light, wind; }
    private Vals from, cur, target;
    private float blend = 1f, nextChange, nextLightning;
    private Profile targetProfile;

    private ParticleSystem rain;
    private Transform rainT;
    private AudioSource rainSrc, thunderSrc;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        BuildRain();
        rainSrc = gameObject.AddComponent<AudioSource>();
        rainSrc.clip = rainLoop; rainSrc.loop = true; rainSrc.spatialBlend = 0f; rainSrc.volume = 0f; rainSrc.playOnAwake = false;
        thunderSrc = gameObject.AddComponent<AudioSource>();
        thunderSrc.spatialBlend = 0f; thunderSrc.playOnAwake = false;

        SetWeather(startWeather, true);
        ScheduleNextChange();
    }

    private void OnDestroy()
    {
        if (rainT != null) Destroy(rainT.gameObject);
        if (Instance == this) Instance = null;
        if (ClockSystem.Instance != null) { ClockSystem.Instance.LightMultiplier = 1f; ClockSystem.Instance.LightningFlash = 0f; }
    }

    private Profile Get(WeatherType t)
    {
        foreach (var p in profiles) if (p.type == t) return p;
        return profiles.Length > 0 ? profiles[0] : new Profile(t, 0, 0, 1, 0, false, 1);
    }

    /// <summary>Change the weather. instant = no blending.</summary>
    public void SetWeather(WeatherType t, bool instant = false)
    {
        var p = Get(t);
        targetProfile = p;
        target = new Vals { rain = p.rain, fog = p.fogDensity, light = p.light, wind = p.wind };
        from = cur;
        blend = instant || transitionSeconds <= 0f ? 1f : 0f;
        if (instant) cur = target;
        bool changed = Current != t;
        Current = t;
        nextLightning = UnityEngine.Random.Range(secondsBetweenLightning.x, secondsBetweenLightning.y) * 0.5f;
        if (changed) OnWeatherChanged?.Invoke(t);
    }

    private void ScheduleNextChange()
    {
        nextChange = Time.time + UnityEngine.Random.Range(changeEveryMinutes.x, changeEveryMinutes.y) * 60f;
    }

    private void PickRandomWeather()
    {
        float total = 0f;
        foreach (var p in profiles) if (p.type != Current) total += Mathf.Max(0f, p.chance);
        if (total <= 0f) return;
        float r = UnityEngine.Random.value * total;
        foreach (var p in profiles)
        {
            if (p.type == Current) continue;
            r -= Mathf.Max(0f, p.chance);
            if (r <= 0f) { SetWeather(p.type); return; }
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        // random weather
        if (autoChange && Time.time >= nextChange) { PickRandomWeather(); ScheduleNextChange(); }

        // blend
        if (blend < 1f)
        {
            blend = Mathf.Min(1f, blend + dt / Mathf.Max(0.01f, transitionSeconds));
            float k = Mathf.SmoothStep(0f, 1f, blend);
            cur.rain = Mathf.Lerp(from.rain, target.rain, k);
            cur.fog = Mathf.Lerp(from.fog, target.fog, k);
            cur.light = Mathf.Lerp(from.light, target.light, k);
            cur.wind = Mathf.Lerp(from.wind, target.wind, k);
        }

        // lightning
        if (targetProfile != null && targetProfile.lightning && cur.rain > 0.5f)
        {
            nextLightning -= dt;
            if (nextLightning <= 0f)
            {
                nextLightning = UnityEngine.Random.Range(secondsBetweenLightning.x, secondsBetweenLightning.y);
                StartCoroutine(Lightning());
            }
        }

        ApplyRain();
        ApplyFog();
        ApplyAudio();
        if (ClockSystem.Instance != null) ClockSystem.Instance.LightMultiplier = cur.light;
    }

    // ---------------------------------------------------------------- Rain

    private void BuildRain()
    {
        var go = new GameObject("Rain");
        go.transform.SetParent(null);
        rainT = go.transform;
        rain = go.AddComponent<ParticleSystem>();
        rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = rain.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = 0.5f;
        main.startSpeed = 28f;
        main.startSize = 0.03f;
        main.startColor = rainColor;
        main.maxParticles = 10000;

        var shape = rain.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(rainArea.x, rainArea.z, 0.1f);

        var em = rain.emission;
        em.rateOverTime = 0f;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.lengthScale = 3f;
        r.velocityScale = 0.02f;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.sharedMaterial = rainMaterial != null ? rainMaterial : DefaultRainMaterial();

        rain.Play();
    }

    private static Material DefaultRainMaterial()
    {
        string[] shaders = { "Sprites/Default", "Particles/Standard Unlit", "Universal Render Pipeline/Particles/Unlit" };
        foreach (var n in shaders)
        {
            var sh = Shader.Find(n);
            if (sh != null) return new Material(sh);
        }
        Debug.LogWarning("WeatherSystem: no default particle shader found - assign a Rain Material.");
        return null;
    }

    private void ApplyRain()
    {
        if (player == null)
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null) player = p.transform;
            else if (Camera.main != null) player = Camera.main.transform;
        }
        if (player != null)
        {
            // directly above the player; emitter points down, wind tilts it
            rainT.position = player.position + Vector3.up * rainSpawnHeight;
            rainT.rotation = Quaternion.Euler(90f - cur.wind * 18f, 0f, 0f);
        }

        // emitter is rotated 90 deg: shape X = width, shape Y = depth (horizontal), shape Z = height (small)
        var shape = rain.shape;
        shape.scale = new Vector3(rainArea.x, rainArea.z, 0.1f);

        var em = rain.emission;
        em.rateOverTime = maxRainPerSecond * cur.rain;
    }

    // ---------------------------------------------------------------- Fog

    private void ApplyFog()
    {
        if (!controlFog) return;
        bool on = cur.fog > 0.0001f;
        RenderSettings.fog = on;
        if (!on) return;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = cur.fog;
        if (ClockSystem.Instance == null) RenderSettings.fogColor = Color.Lerp(new Color(0.04f, 0.05f, 0.08f), new Color(0.65f, 0.7f, 0.75f), cur.light);
    }

    // ---------------------------------------------------------------- Audio + lightning

    private void ApplyAudio()
    {
        if (rainLoop == null) return;
        rainSrc.volume = rainVolume * cur.rain;
        if (cur.rain > 0.02f && !rainSrc.isPlaying) rainSrc.Play();
        else if (cur.rain <= 0.02f && rainSrc.isPlaying) rainSrc.Stop();
    }

    private void SetFlash(float v)
    {
        if (ClockSystem.Instance != null) ClockSystem.Instance.LightningFlash = v;
    }

    private IEnumerator Lightning()
    {
        StartCoroutine(Thunder());
        SetFlash(1f);
        yield return new WaitForSeconds(0.07f);
        SetFlash(0.25f);
        yield return new WaitForSeconds(0.06f);
        SetFlash(0.9f);
        float t = 0f;
        while (t < 0.35f)
        {
            t += Time.deltaTime;
            SetFlash(Mathf.Lerp(0.9f, 0f, t / 0.35f));
            yield return null;
        }
        SetFlash(0f);
    }

    private IEnumerator Thunder()
    {
        yield return new WaitForSeconds(UnityEngine.Random.Range(0.4f, 3f)); // light is faster than sound
        if (thunderClips != null && thunderClips.Length > 0)
            thunderSrc.PlayOneShot(thunderClips[UnityEngine.Random.Range(0, thunderClips.Length)], UnityEngine.Random.Range(0.6f, 1f));
    }
}