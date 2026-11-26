using System;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// In-game clock + day/night lighting. Put it on an empty GameObject (one per scene).
/// Time runs on scaled time, so it freezes when you pause with Time.timeScale = 0.
/// Other scripts can read ClockSystem.Instance.Hour / Minute / Day / TimeString / IsNight,
/// or subscribe to OnHourChanged / OnDayChanged.
/// </summary>
public class ClockSystem : MonoBehaviour
{
    public static ClockSystem Instance { get; private set; }

    [Header("Time")]
    [Range(0f, 24f)] public float startHour = 8f;
    [Tooltip("How many REAL minutes one full in-game day (24 h) lasts.")]
    public float dayLengthMinutes = 20f;
    [Tooltip("Extra speed multiplier (1 = normal, 2 = twice as fast).")]
    public float speed = 1f;
    public bool paused = false;
    public int startDay = 1;

    [Header("Display")]
    public bool use24Hour = true;

    [Header("Day / night lighting")]
    public bool controlLighting = true;
    [Tooltip("Directional light used as the sun. Rotated and dimmed automatically.")]
    public Light sun;
    [Tooltip("Optional second directional light used as a dim moon at night. Leave empty for no moon.")]
    public Light moon;
    [Tooltip("Sun brightness over the day. X = 0..1 of the day (0 = midnight, 0.5 = noon).")]
    public AnimationCurve sunIntensity = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.23f, 0f), new Keyframe(0.30f, 1f),
        new Keyframe(0.70f, 1f), new Keyframe(0.77f, 0f), new Keyframe(1f, 0f));
    public Gradient sunColor = DefaultSunColor();
    [Tooltip("Max moon brightness (only used when a Moon light is set).")]
    public float moonMaxIntensity = 0.12f;
    public Color moonColor = new Color(0.45f, 0.55f, 0.85f);

    [Header("Ambient (this is what makes the night dark)")]
    [Tooltip("Ambient colour over the day. Make the night end nearly black / dark blue.")]
    public Gradient ambientColor = DefaultAmbient();
    [Tooltip("Multiplies the skybox brightness (Exposure). 0 at night = black sky.")]
    public AnimationCurve skyExposure = new AnimationCurve(
        new Keyframe(0f, 0.03f), new Keyframe(0.23f, 0.03f), new Keyframe(0.30f, 1f),
        new Keyframe(0.70f, 1f), new Keyframe(0.77f, 0.03f), new Keyframe(1f, 0.03f));
    [Tooltip("Reflection brightness over the day (reflections of the bright daytime sky also light up objects at night).")]
    public AnimationCurve reflectionIntensity = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.23f, 0f), new Keyframe(0.30f, 1f),
        new Keyframe(0.70f, 1f), new Keyframe(0.77f, 0f), new Keyframe(1f, 0f));
    [Tooltip("Fog colour over the day (only used if Fog is enabled in the Lighting window).")]
    public Gradient fogColor = DefaultFog();

    /// <summary>Time of day in hours, 0..24.</summary>
    public float TimeOfDay { get; private set; }
    public int Day { get; private set; }
    public int Hour => Mathf.FloorToInt(TimeOfDay) % 24;
    public int Minute => Mathf.FloorToInt((TimeOfDay - Mathf.Floor(TimeOfDay)) * 60f) % 60;
    /// <summary>0..1 through the day (0 = midnight, 0.5 = noon).</summary>
    public float NormalizedDay => TimeOfDay / 24f;
    public bool IsNight => Hour < 6 || Hour >= 20;

    /// <summary>Set by WeatherSystem: 1 = clear sky, lower = overcast/dark (dims sun, ambient, sky).</summary>
    public float LightMultiplier { get; set; } = 1f;
    /// <summary>Set by WeatherSystem: 0..1 lightning flash.</summary>
    public float LightningFlash { get; set; }

    public event Action<int> OnHourChanged;   // new hour
    public event Action<int> OnDayChanged;    // new day number

    private int lastHour;
    private Material skyInstance; // copy of the skybox, so the asset itself is never changed
    private float baseExposure = 1f;
    private bool skyHasExposure;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        TimeOfDay = Mathf.Repeat(startHour, 24f);
        Day = Mathf.Max(1, startDay);
        lastHour = Hour;

        if (controlLighting)
        {
            // ambient comes from OUR colour, not from the (bright) skybox
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;

            if (RenderSettings.skybox != null)
            {
                skyInstance = new Material(RenderSettings.skybox);
                skyHasExposure = skyInstance.HasProperty("_Exposure");
                if (skyHasExposure) baseExposure = skyInstance.GetFloat("_Exposure");
                RenderSettings.skybox = skyInstance;
            }
            if (sun == null) sun = RenderSettings.sun;
        }
        ApplyLighting();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (skyInstance != null) Destroy(skyInstance);
    }

    private void Update()
    {
        if (paused || dayLengthMinutes <= 0f) { ApplyLighting(); return; }

        float hoursPerSecond = 24f / (dayLengthMinutes * 60f);
        TimeOfDay += Time.deltaTime * hoursPerSecond * speed;

        while (TimeOfDay >= 24f)
        {
            TimeOfDay -= 24f;
            Day++;
            OnDayChanged?.Invoke(Day);
        }

        if (Hour != lastHour)
        {
            lastHour = Hour;
            OnHourChanged?.Invoke(Hour);
        }

        ApplyLighting();
    }

    /// <summary>Jump to a time, e.g. SetTime(6, 30) for 06:30.</summary>
    public void SetTime(int hour, int minute = 0)
    {
        TimeOfDay = Mathf.Repeat(hour + minute / 60f, 24f);
        lastHour = Hour;
        ApplyLighting();
    }

    /// <summary>Skip forward (e.g. sleeping). Days are counted.</summary>
    public void AddHours(float hours)
    {
        TimeOfDay += hours;
        while (TimeOfDay >= 24f) { TimeOfDay -= 24f; Day++; OnDayChanged?.Invoke(Day); }
        lastHour = Hour;
        ApplyLighting();
    }

    public string TimeString()
    {
        if (use24Hour) return Hour.ToString("00") + ":" + Minute.ToString("00");
        int h12 = Hour % 12; if (h12 == 0) h12 = 12;
        return h12 + ":" + Minute.ToString("00") + (Hour < 12 ? " AM" : " PM");
    }

    // ---------------------------------------------------------------- Lighting

    private void ApplyLighting()
    {
        if (!controlLighting) return;
        float t = NormalizedDay;

        // sun: midnight = -90 deg (below the horizon), noon = 90 deg (straight overhead)
        if (sun != null)
        {
            sun.transform.rotation = Quaternion.Euler(t * 360f - 90f, 170f, 0f);
            float i = Mathf.Max(0f, sunIntensity.Evaluate(t)) * LightMultiplier;
            sun.intensity = i;
            sun.color = sunColor.Evaluate(t);
            sun.enabled = i > 0.01f; // fully off at night: no light and no shadows from below the ground
        }

        // moon: opposite side of the sky, only when the sun is down
        if (moon != null)
        {
            moon.transform.rotation = Quaternion.Euler(t * 360f + 90f, 170f, 0f);
            float night = 1f - Mathf.Clamp01(sunIntensity.Evaluate(t));
            moon.intensity = moonMaxIntensity * night * LightMultiplier;
            moon.color = moonColor;
            moon.enabled = moon.intensity > 0.005f;
        }

        RenderSettings.ambientLight = Color.Lerp(ambientColor.Evaluate(t) * LightMultiplier, new Color(0.75f, 0.8f, 1f), LightningFlash);
        RenderSettings.reflectionIntensity = Mathf.Clamp01(reflectionIntensity.Evaluate(t)) * LightMultiplier;
        if (skyInstance != null && skyHasExposure)
            skyInstance.SetFloat("_Exposure", baseExposure * (Mathf.Max(0f, skyExposure.Evaluate(t)) * LightMultiplier + LightningFlash * 0.6f));
        if (RenderSettings.fog) RenderSettings.fogColor = fogColor.Evaluate(t) * Mathf.Max(0.4f, LightMultiplier);
    }

    // ---------------------------------------------------------------- Default gradients (x = 0..1 of the day)

    private static Gradient Make(params (float t, Color c)[] k)
    {
        var g = new Gradient();
        var keys = new GradientColorKey[k.Length];
        var alpha = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) };
        for (int i = 0; i < k.Length; i++) keys[i] = new GradientColorKey(k[i].c, k[i].t);
        g.SetKeys(keys, alpha);
        return g;
    }

    private static Gradient DefaultSunColor() => Make(
        (0f, new Color(1f, 0.6f, 0.4f)), (0.25f, new Color(1f, 0.6f, 0.35f)), (0.35f, new Color(1f, 0.95f, 0.85f)),
        (0.65f, new Color(1f, 0.95f, 0.85f)), (0.75f, new Color(1f, 0.55f, 0.3f)), (1f, new Color(1f, 0.6f, 0.4f)));

    private static Gradient DefaultAmbient() => Make(
        (0f, new Color(0.02f, 0.03f, 0.07f)), (0.22f, new Color(0.03f, 0.04f, 0.09f)), (0.27f, new Color(0.45f, 0.32f, 0.3f)),
        (0.35f, new Color(0.5f, 0.55f, 0.6f)), (0.65f, new Color(0.5f, 0.55f, 0.6f)), (0.73f, new Color(0.45f, 0.3f, 0.28f)),
        (0.80f, new Color(0.03f, 0.04f, 0.09f)), (1f, new Color(0.02f, 0.03f, 0.07f)));

    private static Gradient DefaultFog() => Make(
        (0f, new Color(0.03f, 0.04f, 0.08f)), (0.25f, new Color(0.35f, 0.3f, 0.35f)), (0.4f, new Color(0.6f, 0.7f, 0.8f)),
        (0.6f, new Color(0.6f, 0.7f, 0.8f)), (0.75f, new Color(0.35f, 0.25f, 0.28f)), (0.82f, new Color(0.03f, 0.04f, 0.08f)), (1f, new Color(0.03f, 0.04f, 0.08f)));
}