using System.Collections;
using UnityEngine;

public class CookieSpawner : MonoBehaviour
{
    public static CookieSpawner Instance;

    [Header("Nastavení sušenkové exploze")]
    public GameObject cookiePrefab;
    public int cookieCount = 10000;    // Počet sušenek
    public int spawnPerFrame = 150;     // Kolik sušenek se vytvoří za 1 snímek
    public float spawnRadius = 15.0f;   // Zvětšený okruh, aby se neshlukly na jedno místo
    public float spawnHeight = 12.0f;

    private bool isTriggered = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void TriggerCookieRain(Vector3 playerPosition)
    {
        if (isTriggered) return;
        isTriggered = true;

        // Spustí postupné dávkování sušenek bez zaseknutí hry
        StartCoroutine(SpawnCookieRainRoutine(playerPosition));
    }

    private IEnumerator SpawnCookieRainRoutine(Vector3 playerPosition)
    {
        int spawnedCount = 0;

        while (spawnedCount < cookieCount)
        {
            // Vytvoří dávku sušenek pro aktuální frame
            for (int i = 0; i < spawnPerFrame && spawnedCount < cookieCount; i++)
            {
                Vector2 randomPoint = Random.insideUnitCircle * spawnRadius;
                Vector3 spawnPosition = playerPosition + new Vector3(
                    randomPoint.x, 
                    spawnHeight + Random.Range(0f, 10f), 
                    randomPoint.y
                );

                Quaternion randomRotation = Random.rotation;
                GameObject newCookie = Instantiate(cookiePrefab, spawnPosition, randomRotation);

                Rigidbody rb = newCookie.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.AddTorque(Random.insideUnitSphere * 2f, ForceMode.Impulse);
                }

                spawnedCount++;
            }

            // Počká na další frame (snímek), čímž umožní hře plynule běžet
            yield return null;
        }

        // --- Aktivace průběžného načítání sušenek při chůzi po dokončení deště ---
        if (DynamicCookieSpawner.Instance != null)
        {
            DynamicCookieSpawner.Instance.ActivateSystem();
        }
    }
}