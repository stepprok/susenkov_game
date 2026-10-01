using System.Collections.Generic;
using UnityEngine;

public class DynamicCookieSpawner : MonoBehaviour
{
    public static DynamicCookieSpawner Instance;

    [Header("Nastavení Render Distance")]
    public GameObject cookiePrefab;         // Prefab sušenky
    public Transform player;                // Odkaz na hráče
    public int maxActiveCookies = 100;      // Kolik sušenek má být max kolem hráče
    public float spawnRadius = 30.0f;       // Vzdálenost, do které se sušenky objevují
    public float minRadius = 8.0f;          // Minimální vzdálenost od hráče (aby neskákaly přímo pod nohy)
    public float despawnRadius = 40.0f;     // Vzdálenost, při které vzdálené sušenky zmizí
    public float stepThreshold = 5.0f;      // Kolik metrů musí hráč ujít, aby se načetly nové sušenky

    [Header("Stav")]
    public bool isSystemActive = false;     // Aktivuje se po úvodním dešti sušenek

    private List<GameObject> activeCookies = new List<GameObject>();
    private Vector3 lastCheckPosition;

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

    private void Start()
    {
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }

        if (player != null)
        {
            lastCheckPosition = player.position;
        }
    }

    private void Update()
    {
        if (!isSystemActive || player == null) return;

        // Kontrola: Ušel hráč zadanou vzdálenost (stepThreshold)?
        if (Vector3.Distance(player.position, lastCheckPosition) >= stepThreshold)
        {
            lastCheckPosition = player.position;
            ManageCookiesAroundPlayer();
        }
    }

    // Spustí se po sebrání první sušenky / dokončení deště
    public void ActivateSystem()
    {
        isSystemActive = true;
        if (player != null) lastCheckPosition = player.position;
        ManageCookiesAroundPlayer();
    }

    private void ManageCookiesAroundPlayer()
    {
        // 1. Despawn: Odstranění sušenek, které jsou dál než despawnRadius
        for (int i = activeCookies.Count - 1; i >= 0; i--)
        {
            if (activeCookies[i] == null)
            {
                activeCookies.RemoveAt(i);
                continue;
            }

            float distance = Vector3.Distance(player.position, activeCookies[i].transform.position);
            if (distance > despawnRadius)
            {
                Destroy(activeCookies[i]);
                activeCookies.RemoveAt(i);
            }
        }

        // 2. Spawn: Dospawnování nových sušenek na zem do limitu maxActiveCookies
        int neededCookies = maxActiveCookies - activeCookies.Count;
        for (int i = 0; i < neededCookies; i++)
        {
            Vector3 spawnPos = GetRandomGroundPosition();
            if (spawnPos != Vector3.zero)
            {
                GameObject newCookie = Instantiate(cookiePrefab, spawnPos, Quaternion.Euler(0, Random.Range(0f, 360f), 0));
                activeCookies.Add(newCookie);
            }
        }
    }

    // Najde přesné místo na zemi pomocí Raycastu z výšky dolů
    private Vector3 GetRandomGroundPosition()
    {
        Vector2 randomCircle = Random.insideUnitCircle.normalized * Random.Range(minRadius, spawnRadius);
        Vector3 origin = player.position + new Vector3(randomCircle.x, 20f, randomCircle.y);

        // Paprsek zkoumá povrch země
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 50f))
        {
            return hit.point + Vector3.up * 0.05f; // Mírný odstup nad zemí
        }

        return Vector3.zero;
    }
}