using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NPCSpawner : MonoBehaviour
{
    [Header("Limity NPC podle Kvality")]
    public int maxNPCsLow = 10;          // Počet pro Low kvalitu
    public int maxNPCsMedium = 25;       // Počet pro Medium kvalitu
    public int maxNPCsHigh = 50;         // Počet pro High kvalitu
    public int maxNPCsUltra = 80;        // Počet pro Ultra / Very High

    [Header("Dávkování Spawnu (jako u sušenek)")]
    public int spawnPerFrame = 1;        // Kolik NPC se vytvoří za 1 frame (např. 1 nebo 2)
    public float checkInterval = 3.0f;   // Jak často se spouští kontrola/dospawnování

    [Header("Nastavení Spawneru")]
    public GameObject[] npcPrefabs;      // Prefaby NPC postaviček

    [Header("Hranice Mapy")]
    public Vector3 mapCenter = Vector3.zero;
    public float mapSizeX = 200.0f;
    public float mapSizeZ = 200.0f;

    [HideInInspector]
    public int maxNPCs = 50;             // Aktuální aktivní limit

    private List<GameObject> activeNPCs = new List<GameObject>();
    private bool isSpawning = false;

    private void Start()
    {
        // Načte správný limit podle aktuálně nastavené kvality hry
        UpdateLimitByQualityLevel(QualitySettings.GetQualityLevel());

        // Spustí opakovanou kontrolu
        InvokeRepeating(nameof(TriggerSpawnRoutine), 0.5f, checkInterval);
    }

    /// <summary>
    /// Nastaví limit podle indexu kvality (0 = Low, 1 = Medium, atd.)
    /// </summary>
    public void UpdateLimitByQualityLevel(int qualityIndex)
    {
        switch (qualityIndex)
        {
            case 0:
                SetMaxNPCs(maxNPCsLow);
                break;
            case 1:
                SetMaxNPCs(maxNPCsMedium);
                break;
            case 2:
                SetMaxNPCs(maxNPCsHigh);
                break;
            default:
                SetMaxNPCs(maxNPCsUltra);
                break;
        }
    }

    /// <summary>
    /// Ručně nastavit limit NPC a v případě potřeby promazat přebytečná NPC
    /// </summary>
    public void SetMaxNPCs(int newMax)
    {
        maxNPCs = Mathf.Max(0, newMax);

        // Pokud je na scéně více NPC než nový limit, smažeme je
        while (activeNPCs.Count > maxNPCs)
        {
            activeNPCs.RemoveAll(npc => npc == null);
            if (activeNPCs.Count <= maxNPCs) break;

            GameObject npcToRemove = activeNPCs[activeNPCs.Count - 1];
            activeNPCs.RemoveAt(activeNPCs.Count - 1);

            if (npcToRemove != null)
            {
                Destroy(npcToRemove);
            }
        }
    }

    private void TriggerSpawnRoutine()
    {
        // Pročistí neplatná/zničená NPC
        activeNPCs.RemoveAll(npc => npc == null);

        // Pokud chybí NPC do limitu a korutina zrovna neběží, spustí dávkování
        if (activeNPCs.Count < maxNPCs && !isSpawning)
        {
            StartCoroutine(SpawnNPCsGradually());
        }
    }

    /// <summary>
    /// Korutina pro plynulé spawnování po snímcích (podle vzoru sušenek)
    /// </summary>
    private IEnumerator SpawnNPCsGradually()
    {
        isSpawning = true;

        while (activeNPCs.Count < maxNPCs)
        {
            // Vytvoří dávku NPC pro tento snímek
            for (int i = 0; i < spawnPerFrame && activeNPCs.Count < maxNPCs; i++)
            {
                if (npcPrefabs == null || npcPrefabs.Length == 0) break;

                Vector3 spawnPos = GetRandomGroundPosition();
                if (spawnPos != Vector3.zero)
                {
                    GameObject randomPrefab = npcPrefabs[Random.Range(0, npcPrefabs.Length)];
                    GameObject newNPC = Instantiate(randomPrefab, spawnPos, Quaternion.Euler(0, Random.Range(0f, 360f), 0));
                    activeNPCs.Add(newNPC);
                }
            }

            // Počká na další frame – díky tomu se hra nesekne a NPC přibývají postupně
            yield return null;
        }

        isSpawning = false;
    }

    private Vector3 GetRandomGroundPosition()
    {
        float randomX = Random.Range(mapCenter.x - mapSizeX / 2f, mapCenter.x + mapSizeX / 2f);
        float randomZ = Random.Range(mapCenter.z - mapSizeZ / 2f, mapCenter.z + mapSizeZ / 2f);

        Vector3 origin = new Vector3(randomX, mapCenter.y + 50f, randomZ);

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 100f))
        {
            return hit.point;
        }

        return Vector3.zero;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(mapCenter, new Vector3(mapSizeX, 10f, mapSizeZ));
    }
}