using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NPCWander : MonoBehaviour
{
    [Header("Limity chůze a čekání")]
    public float minWalkTime = 3.0f;   // Minimální doba chůze (v sekundách)
    public float maxWalkTime = 8.0f;   // Maximální doba chůze (v sekundách)
    
    public float minWaitTime = 2.0f;   // Minimální doba stání (v sekundách)
    public float maxWaitTime = 6.0f;   // Maximální doba stání (v sekundách)

    [Header("Okruh hledání cíle")]
    public float wanderRadius = 15.0f; // V jaké vzdálenosti hledá další bod

    private NavMeshAgent agent;
    private float stateTimer;
    private bool isWaiting = false;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    private void OnEnable()
    {
        SetNewDestination();
    }

    private void Update()
    {
        // Kontrola, zda je agent platný a aktivní na NavMeshi
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            return;
        }

        stateTimer -= Time.deltaTime;

        // Pokud vypršel časovač aktuálního stavu
        if (stateTimer <= 0f)
        {
            if (isWaiting)
            {
                // NPC stálo, teď se znova rozchází
                SetNewDestination();
            }
            else
            {
                // NPC chodilo, teď zastaví a odpočívá
                StartWaiting();
            }
        }
        
        // Pokud NPC chodí, ale dorazí do cíle dříve, než vyprší walkTime, začne stát hned
        if (!isWaiting && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            StartWaiting();
        }
    }

    private void SetNewDestination()
    {
        isWaiting = false;
        stateTimer = Random.Range(minWalkTime, maxWalkTime);

        Vector3 newPos = RandomNavSphere(transform.position, wanderRadius, NavMesh.AllAreas);
        if (newPos != Vector3.zero)
        {
            agent.isStopped = false;
            agent.SetDestination(newPos);
        }
    }

    private void StartWaiting()
    {
        isWaiting = true;
        stateTimer = Random.Range(minWaitTime, maxWaitTime);

        if (agent.isOnNavMesh)
        {
            agent.isStopped = true; // Zastaví agenta na místě
            agent.velocity = Vector3.zero;
        }
    }

    public static Vector3 RandomNavSphere(Vector3 origin, float dist, int layermask)
    {
        Vector3 randDirection = Random.insideUnitSphere * dist;
        randDirection += origin;

        NavMeshHit navHit;
        if (NavMesh.SamplePosition(randDirection, out navHit, dist, layermask))
        {
            return navHit.position;
        }

        return Vector3.zero;
    }
}