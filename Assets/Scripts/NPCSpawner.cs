using System.Collections.Generic;
using UnityEngine;

public class NPCSpawner : MonoBehaviour
{
    [Header("Nastavení Spawneru")]
    public GameObject[] npcPrefabs;      // Prefaby NPC postaviček
    public Transform player;             // Hráč
    public int maxNPCs = 15;             // Maximální počet NPC ve městě najednou
    public float spawnRadius = 50.0f;    // Okruh kolem hráče pro spawn
    public float minSpawnRadius = 10.0f; // Minimální vzdálenost od hráče
    public float checkInterval = 5.0f;   // Jak často kontrolovat a dospawnovávat NPC

    private List<GameObject> activeNPCs = new List<GameObject>();

    private void Start()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        InvokeRepeating(nameof(ManageNPCSpawns), 1.0f, checkInterval);
    }

    private void ManageNPCSpawns()
    {
        if (player == null || npcPrefabs.Length == 0) return;

        // Odebere ze seznamu zničené NPC
        activeNPCs.RemoveAll(npc => npc == null);

        // Odstraní NPC, které jsou moc daleko od hráče
        for (int i = activeNPCs.Count - 1; i >= 0; i--)
        {
            if (Vector3.Distance(player.position, activeNPCs[i].transform.position) > spawnRadius * 1.5f)
            {
                Destroy(activeNPCs[i]);
                activeNPCs.RemoveAt(i);
            }
        }

        // Dospawnuje nové NPC do limitu maxNPCs
        while (activeNPCs.Count < maxNPCs)
        {
            Vector3 spawnPos = GetRandomGroundPosition();
            if (spawnPos != Vector3.zero)
            {
                GameObject randomPrefab = npcPrefabs[Random.Range(0, npcPrefabs.Length)];
                GameObject newNPC = Instantiate(randomPrefab, spawnPos, Quaternion.Euler(0, Random.Range(0f, 360f), 0));
                activeNPCs.Add(newNPC);
            }
            else
            {
                break; // Nenalezeno vhodné místo na zemi
            }
        }
    }

    private Vector3 GetRandomGroundPosition()
    {
        Vector2 randomPoint = Random.insideUnitCircle.normalized * Random.Range(minSpawnRadius, spawnRadius);
        Vector3 origin = player.position + new Vector3(randomPoint.x, 30f, randomPoint.y);

        // Pomocí Raycastu najde povrch země
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 60f))
        {
            return hit.point;
        }

        return Vector3.zero;
    }
}