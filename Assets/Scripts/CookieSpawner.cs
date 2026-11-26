using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CookieSpawner : MonoBehaviour
{
    public static CookieSpawner Instance;

    [Header("Nastavení sušenkové exploze")]
    public GameObject cookiePrefab;
    public int cookieCount = 500;
    [Tooltip("Kolik sušenek se aktivuje za 1 snímek. 20-30 = plynulé, 150 = sekání.")]
    public int spawnPerFrame = 25;
    public float spawnRadius = 15.0f;
    public float spawnHeight = 12.0f;

    [Header("Optimalizace")]
    [Tooltip("Kolik sušenek se předvytvoří za snímek při startu hry (skryté).")]
    public int prewarmPerFrame = 20;
    [Tooltip("Po kolika sekundách od spawnu se sušenka 'zamrzne' (přestane se simulovat fyzika). 0 = nikdy.")]
    public float freezeAfterSeconds = 2.5f;
    [Tooltip("Menší = levnější fyzika. Default Unity je 6.")]
    public int solverIterations = 3;

    private bool isTriggered = false;
    private readonly List<GameObject> pool = new List<GameObject>();
    // fronta sušenek čekajících na zamrznutí (jedna fronta místo 500 coroutin)
    private readonly Queue<(Rigidbody rb, float time)> freezeQueue = new Queue<(Rigidbody, float)>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    private void Start()
    {
        // Sušenky se předvytvoří dopředu (skryté) a po částech, takže při spuštění deště se už neinstancuje.
        StartCoroutine(PrewarmRoutine());
    }

    private void Update()
    {
        while (freezeQueue.Count > 0 && freezeQueue.Peek().time <= Time.time)
        {
            var rb = freezeQueue.Dequeue().rb;
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }
        }
    }

    private IEnumerator PrewarmRoutine()
    {
        int created = 0;
        while (created < cookieCount)
        {
            for (int i = 0; i < prewarmPerFrame && created < cookieCount; i++)
            {
                var c = Instantiate(cookiePrefab, new Vector3(0, -1000f, 0), Quaternion.identity);
                c.SetActive(false);
                pool.Add(c);
                created++;
            }
            yield return null;
        }
    }

    private GameObject GetCookie()
    {
        if (pool.Count > 0)
        {
            int last = pool.Count - 1;
            var c = pool[last];
            pool.RemoveAt(last);
            return c;
        }
        // záloha, kdyby déšť začal dřív než doběhl prewarm
        var n = Instantiate(cookiePrefab);
        n.SetActive(false);
        return n;
    }

    public void TriggerCookieRain(Vector3 playerPosition)
    {
        if (isTriggered) return;
        isTriggered = true;
        StartCoroutine(SpawnCookieRainRoutine(playerPosition));
    }

    private IEnumerator SpawnCookieRainRoutine(Vector3 playerPosition)
    {
        int spawnedCount = 0;

        while (spawnedCount < cookieCount)
        {
            for (int i = 0; i < spawnPerFrame && spawnedCount < cookieCount; i++)
            {
                Vector2 randomPoint = Random.insideUnitCircle * spawnRadius;
                Vector3 spawnPosition = playerPosition + new Vector3(
                    randomPoint.x,
                    spawnHeight + Random.Range(0f, 10f),
                    randomPoint.y
                );

                var cookie = GetCookie();
                cookie.transform.SetPositionAndRotation(spawnPosition, Random.rotation);
                cookie.SetActive(true);

                if (cookie.TryGetComponent(out Rigidbody rb))
                {
                    rb.solverIterations = solverIterations;
                    rb.solverVelocityIterations = 1;
                    rb.sleepThreshold = 0.5f;
                    rb.angularDamping = 1f;
                    rb.AddTorque(Random.insideUnitSphere * 2f, ForceMode.Impulse);
                    if (freezeAfterSeconds > 0f)
                        freezeQueue.Enqueue((rb, Time.time + freezeAfterSeconds));
                }

                spawnedCount++;
            }
            yield return null;
        }

        if (DynamicCookieSpawner.Instance != null)
            DynamicCookieSpawner.Instance.ActivateSystem();
    }
}