using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cookie fountain: the machine shoots cookies high into the air, each cookie vanishes at the
/// top of its arc. While running, the machine shakes and blinks (light + emission).
/// </summary>
public class MachineFountain : MonoBehaviour
{
    [Header("Fountain")]
    [Tooltip("Optional. Leave empty to use generated brown cookie discs.")]
    public GameObject cookiePrefab;
    public bool autoStart = true;
    [Tooltip("0 = runs forever, otherwise stops after this many seconds")]
    public float runDuration = 0f;
    [Tooltip("How high above the spawn point the cookies fly before disappearing")]
    public float fountainHeight = 6f;
    public float gravity = 9.81f;
    public float cookiesPerSecond = 8f;
    [Tooltip("Sideways spread, so it looks like a fountain and not a laser")]
    public float spread = 1.2f;
    [Tooltip("Extra offset from the top of the machine (local space)")]
    public Vector3 spawnOffset = new Vector3(0f, 0.2f, 0f);
    public float cookieSize = 0.35f;

    [Header("Shake")]
    public float shakePosition = 0.04f;
    public float shakeRotation = 1.8f;
    public float shakeSpeed = 38f;
    [Tooltip("How fast the shake fades in/out")]
    public float shakeBlend = 6f;

    [Header("Blink")]
    public Color blinkColor = new Color(1f, 0.45f, 0.1f);
    public float blinkSpeed = 5f;
    public float emissionIntensity = 1.6f;
    public float lightIntensity = 6f;
    public float lightRange = 7f;
    public bool glowMaterial = true;

    private class Cookie
    {
        public Transform t;
        public Vector3 vel;
        public Vector3 spin;
        public bool popping;
        public float popTime;
    }

    private readonly List<Cookie> cookies = new List<Cookie>();
    private static Material fallbackMat;

    private Renderer[] renderers;
    private Material[] glowMats;
    private Light blinkLight;
    private Vector3 spawnLocal;

    private bool running;
    private float runTimer, spawnAcc, power; // power 0..1 = how "on" the machine is
    private Vector3 lastPosOffset;
    private Quaternion lastRotOffset = Quaternion.identity;

    private void Start()
    {
        renderers = GetComponentsInChildren<Renderer>();

        // top center of the machine, stored in local space
        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            spawnLocal = transform.InverseTransformPoint(new Vector3(b.center.x, b.max.y, b.center.z));
        }
        else spawnLocal = Vector3.up * 2f;

        // blinking light
        var lightGo = new GameObject("MachineBlinkLight");
        lightGo.transform.SetParent(transform, false);
        lightGo.transform.localPosition = spawnLocal;
        blinkLight = lightGo.AddComponent<Light>();
        blinkLight.type = LightType.Point;
        blinkLight.color = blinkColor;
        blinkLight.range = lightRange;
        blinkLight.intensity = 0f;
        blinkLight.shadows = LightShadows.None;

        // material instances (only this machine glows, not other objects with the same material)
        if (glowMaterial)
        {
            var list = new List<Material>();
            foreach (var r in renderers)
                foreach (var m in r.materials) { m.EnableKeyword("_EMISSION"); list.Add(m); }
            glowMats = list.ToArray();
        }

        if (autoStart) StartFountain();
    }

    public void StartFountain()
    {
        running = true;
        runTimer = 0f;
    }

    public void StopFountain() => running = false;

    [ContextMenu("Toggle Fountain")]
    public void Toggle() { if (running) StopFountain(); else StartFountain(); }

    private void Update()
    {
        float dt = Time.deltaTime;

        if (running && runDuration > 0f)
        {
            runTimer += dt;
            if (runTimer >= runDuration) running = false;
        }

        power = Mathf.MoveTowards(power, running ? 1f : 0f, shakeBlend * dt);

        if (running) SpawnCookies(dt);
        UpdateCookies(dt);
        UpdateShake();
        UpdateBlink();
    }

    // ------------------------------------------------------------------

    private void SpawnCookies(float dt)
    {
        spawnAcc += cookiesPerSecond * dt;
        while (spawnAcc >= 1f)
        {
            spawnAcc -= 1f;
            SpawnCookie();
        }
    }

    private void SpawnCookie()
    {
        Vector3 pos = transform.TransformPoint(spawnLocal + spawnOffset);
        GameObject go = cookiePrefab != null ? Instantiate(cookiePrefab) : CreateFallbackCookie();
        go.transform.position = pos;
        go.transform.rotation = Random.rotation;
        if (cookiePrefab == null) go.transform.localScale = new Vector3(cookieSize, cookieSize * 0.3f, cookieSize);

        // v0 chosen so the apex is exactly fountainHeight above the spawn point
        float v0 = Mathf.Sqrt(2f * gravity * Mathf.Max(0.1f, fountainHeight));
        Vector2 side = Random.insideUnitCircle * spread;

        cookies.Add(new Cookie
        {
            t = go.transform,
            vel = new Vector3(side.x, v0 * Random.Range(0.97f, 1.03f), side.y),
            spin = Random.insideUnitSphere * 540f
        });
    }

    private void UpdateCookies(float dt)
    {
        for (int i = cookies.Count - 1; i >= 0; i--)
        {
            var c = cookies[i];
            if (c.t == null) { cookies.RemoveAt(i); continue; }

            if (c.popping)
            {
                // quick shrink right at the highest point, then it's removed
                c.popTime += dt;
                float k = Mathf.Clamp01(c.popTime / 0.15f);
                c.t.localScale *= Mathf.Lerp(1f, 0.6f, k);
                if (k >= 1f) { Destroy(c.t.gameObject); cookies.RemoveAt(i); }
                continue;
            }

            c.vel.y -= gravity * dt;
            c.t.position += c.vel * dt;
            c.t.Rotate(c.spin * dt, Space.World);

            // top of the arc reached -> remove the cookie
            if (c.vel.y <= 0f) c.popping = true;
        }
    }

    private void UpdateShake()
    {
        // remove last frame's offset, add the new one (works even if the machine gets moved)
        transform.position -= lastPosOffset;
        transform.localRotation = transform.localRotation * Quaternion.Inverse(lastRotOffset);

        Vector3 posOff = Vector3.zero;
        Quaternion rotOff = Quaternion.identity;

        if (power > 0.001f)
        {
            float t = Time.time * shakeSpeed;
            float n(float seed) => Mathf.PerlinNoise(t, seed) * 2f - 1f;

            posOff = new Vector3(n(1.3f), n(7.7f) * 0.5f, n(13.1f)) * (shakePosition * power);
            rotOff = Quaternion.Euler(n(21.5f) * shakeRotation * power, n(33.9f) * shakeRotation * 0.5f * power, n(45.2f) * shakeRotation * power);
        }

        transform.position += posOff;
        transform.localRotation = transform.localRotation * rotOff;
        lastPosOffset = posOff;
        lastRotOffset = rotOff;
    }

    private void UpdateBlink()
    {
        // snappy blink: smoothed square wave
        float wave = Mathf.Sin(Time.time * blinkSpeed * Mathf.PI * 2f);
        float pulse = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(wave * 3f + 0.5f)) * power;

        if (blinkLight != null) blinkLight.intensity = lightIntensity * pulse;

        if (glowMats != null)
        {
            Color e = blinkColor * (emissionIntensity * pulse);
            foreach (var m in glowMats) if (m != null) m.SetColor("_EmissionColor", e);
        }
    }

    private void OnDisable()
    {
        // clean up leftover shake offset
        transform.position -= lastPosOffset;
        transform.localRotation = transform.localRotation * Quaternion.Inverse(lastRotOffset);
        lastPosOffset = Vector3.zero;
        lastRotOffset = Quaternion.identity;
    }

    private void OnDestroy()
    {
        foreach (var c in cookies) if (c.t != null) Destroy(c.t.gameObject);
        if (glowMats != null) foreach (var m in glowMats) if (m != null) Destroy(m);
    }

    // ------------------------------------------------------------------

    private GameObject CreateFallbackCookie()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "FountainCookie";
        Destroy(go.GetComponent<Collider>());

        if (fallbackMat == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            fallbackMat = new Material(sh) { color = new Color(0.85f, 0.58f, 0.28f) };
        }
        go.GetComponent<Renderer>().sharedMaterial = fallbackMat;
        return go;
    }
}