using UnityEngine;
using UnityEngine.AI;

public class RandomMovement : MonoBehaviour
{
    public float moveRadius = 10.0f; // Raggio dell'area in cui può muoversi
    public float waitTime = 2.0f;   // Tempo di attesa prima di cambiare destinazione

    private NavMeshAgent _agent;
    private float _waitTimer;

    void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
        SetRandomDestination();
    }

    void Update()
    {
        // Controlla se l'agente ha raggiunto la destinazione
        if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance)
        {
            _waitTimer += Time.deltaTime;

            if (_waitTimer >= waitTime)
            {
                SetRandomDestination();
                _waitTimer = 0;
            }
        }
    }

    void SetRandomDestination()
    {
        // Calcola una posizione random nell'area definita
        Vector3 randomDirection = Random.insideUnitSphere * moveRadius;
        randomDirection += transform.position;

        // Trova un punto valido sul NavMesh
        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDirection, out hit, moveRadius, NavMesh.AllAreas))
        {
            _agent.SetDestination(hit.position);
        }
    }
}
