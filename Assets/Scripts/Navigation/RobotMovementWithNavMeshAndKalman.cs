using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class RobotMovementWithNavMeshAndCollisionPrevention : MonoBehaviour
{
    // --- PUBLIC PROPERTIES ---
    [Header("Parametri di movimento")]
    public float moveSpeed = 3f;
    [Range(0.1f, 50f)]
    public float raycastDistance = 20f;
    [Range(1, 360)]
    public int numberOfRays = 180;
    [Range(0.1f, 20f)]
    public float obstacleDetectionDistance = 1.5f;

    [Header("Parametri del NavMeshAgent")]
    public float angularSpeed = 120f;
    public float acceleration = 8f;
    public ObstacleAvoidanceType obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;

    // --- PRIVATE VARIABLES ---

    private NavMeshAgent agent;
    private KalmanFilter[] kalmanFilters;
    private KalmanFilter positionKalmanFilterX;
    private KalmanFilter positionKalmanFilterZ;
    private float[] distances;
    private Vector3 currentDestination;
    private bool hasArrived = false;

    private Vector3 noisyPosition;
    private Vector3 estimatedPosition;

    private bool isMovementActive = false;
    private List<Vector3> freeRays = new List<Vector3>();
    private List<Vector3> compromisedRays = new List<Vector3>();
    private List<float> compromisedAngles = new List<float>();

    // --- UNITY METHODS ---
    public void Start()
    {
        InitializeAgent();
        InitializeKalmanFilters();

        distances = new float[numberOfRays];
    }


    public void Update()
    {
        // Esegui logica solo se il movimento � attivo
        if (!isMovementActive) return;

        if (!agent.isStopped)
        {
            UpdateNoisyAndEstimatedPosition();
        }

        HandleObstacleDetectionAndAvoidance();
        CheckArrival();
        AdjustObstacleDetectionDistance();
    }

    // --- INITIALIZATION METHODS ---
    private void InitializeAgent()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = moveSpeed;
        agent.angularSpeed = angularSpeed;
        agent.acceleration = acceleration;
        agent.obstacleAvoidanceType = obstacleAvoidanceType;
    }

    private void InitializeKalmanFilters()
    {
        kalmanFilters = new KalmanFilter[numberOfRays];
        for (int i = 0; i < numberOfRays; i++)
        {
            // Inizializzazione del filtro di Kalman per ogni raggio
            kalmanFilters[i] = new KalmanFilter(
                initialEstimate: raycastDistance,
                initialError: 1f,
                processNoise: 0.1f,
                measurementNoise: 0.5f
            );
        }

        // Inizializzazione per la posizione X e Z
        positionKalmanFilterX = new KalmanFilter(
            initialEstimate: 0f,
            initialError: 1f,
            processNoise: 0.1f,
            measurementNoise: 0.5f
        );

        positionKalmanFilterZ = new KalmanFilter(
            initialEstimate: 0f,
            initialError: 1f,
            processNoise: 0.1f,
            measurementNoise: 0.5f
        );
    }


    // --- POSITION MANAGEMENT METHODS ---

    private void UpdateNoisyAndEstimatedPosition()
    {
        noisyPosition = transform.position + new Vector3(Random.Range(-0.2f, 0.2f), 0, Random.Range(-0.2f, 0.2f));
        float estimatedX = Mathf.Round(positionKalmanFilterX.Update(noisyPosition.x) * 100f) / 100f;
        float estimatedZ = Mathf.Round(positionKalmanFilterZ.Update(noisyPosition.z) * 100f) / 100f;
        estimatedPosition = new Vector3(estimatedX, transform.position.y, estimatedZ);
    }

    // --- OBSTACLE DETECTION & AVOIDANCE ---

    private void HandleObstacleDetectionAndAvoidance()
    {
        // Svuota le liste dei raggi
        freeRays.Clear();
        compromisedRays.Clear();
        compromisedAngles.Clear();

        bool obstacleDetected = false;
        Vector3 combinedAvoidanceDirection = Vector3.zero;

        // Esegui Raycast a 360 gradi
        for (int i = 0; i < numberOfRays; i++)
        {
            float angle = i * (360f / numberOfRays);
            Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;

            // Esegui il Raycast
            if (Physics.Raycast(transform.position, direction, out RaycastHit hit, raycastDistance))
            {
                // Ignora il proprio collider (oggetto stesso)
                if (hit.collider.transform.IsChildOf(transform))
                    continue;

                // Se è un "Player", considera la priorità di evitamento
                bool isPlayer = hit.collider.CompareTag("Player") || hit.collider.gameObject.layer == LayerMask.NameToLayer("Player");

                // Introduci un po' di rumore e aggiorna la distanza
                float noisyDistance = hit.distance + Random.Range(-0.2f, 0.2f);
                distances[i] = kalmanFilters[i].Update(noisyDistance);

                if (distances[i] < obstacleDetectionDistance)
                {
                    obstacleDetected = true;

                    // Salva il raggio e l'angolo come "compromessi"
                    compromisedRays.Add(direction);
                    compromisedAngles.Add(angle);

                    // Disegna il raggio compromesso in rosso
                    Debug.DrawRay(transform.position, direction * distances[i], Color.red);

                    // Calcola il vettore di evitamento: se è un Player, aumenta la priorità
                    float weight = isPlayer ? 2.0f : 1.0f; // I Player hanno un peso maggiore
                    combinedAvoidanceDirection -= (direction / distances[i]) * weight;
                }
                else
                {
                    freeRays.Add(direction);
                    Debug.DrawRay(transform.position, direction * distances[i], Color.green);
                }
            }
            else
            {
                // Nessun ostacolo su questo raggio
                distances[i] = raycastDistance;
                freeRays.Add(direction);
                Debug.DrawRay(transform.position, direction * raycastDistance, Color.green);
            }
        }

        // Se abbiamo ostacoli rilevati
        if (obstacleDetected)
        {
            // Normalizza la direzione di evitamento combinata
            if (combinedAvoidanceDirection != Vector3.zero)
            {
                combinedAvoidanceDirection.Normalize();
            }

            // Calcola una nuova destinazione basata sull'evitamento
            Vector3 mainDirection = (agent.destination - transform.position).normalized;
            Vector3 modifiedTargetDirection = mainDirection + combinedAvoidanceDirection;
            modifiedTargetDirection.Normalize();

            Vector3 avoidanceTarget = transform.position + modifiedTargetDirection * obstacleDetectionDistance;

            // Imposta una destinazione temporanea di "evasione"
            agent.SetDestination(avoidanceTarget);
        }
        else
        {
            // Nessun ostacolo, ripristina la destinazione principale
            if (currentDestination != Vector3.zero)
            {
                agent.SetDestination(currentDestination);
            }
        }

        // Abbassa la soglia di rilevamento quando ci si avvicina alla destinazione
        AdjustObstacleDetectionDistance();

        // Reinizializza la soglia di rilevamento se ci si sta allontanando dalla destinazione
        ResetObstacleDetectionDistanceIfNeeded();
    }

    private void ResetObstacleDetectionDistanceIfNeeded()
    {
        // Calcola la distanza attuale dalla destinazione
        float distanceToDestination = Vector3.Distance(estimatedPosition, currentDestination);

        // Se la distanza dalla destinazione è aumentata, reinizializza la soglia
        if (distanceToDestination > obstacleDetectionDistance)
        {
            obstacleDetectionDistance = Mathf.Min(raycastDistance, obstacleDetectionDistance + Time.deltaTime * 0.5f);  // Aumenta lentamente la soglia
        }
    }






    // --- ARRIVAL CHECK ---

    private void CheckArrival()
    {
        if (!agent.pathPending && agent.remainingDistance < 0.4f && !hasArrived)
        {
            hasArrived = true;
            StopRobot();
        }
    }

    public bool HasArrivedAtDestination()
    {
        return hasArrived;
    }


    // --- ROBOT CONTROL ---

    private void StopRobot()
    {
        agent.ResetPath();
        agent.isStopped = true;
        isMovementActive = false;
    }


    // --- ADJUST OBSTACLE DETECTION ---

    private void AdjustObstacleDetectionDistance()
    {
        float distanceToDestination = Vector3.Distance(estimatedPosition, currentDestination);

        if (distanceToDestination < obstacleDetectionDistance && obstacleDetectionDistance > 0.1f)
        {
            obstacleDetectionDistance = Mathf.Max(0.1f, obstacleDetectionDistance - Time.deltaTime * 0.5f); // Adjust rate as needed
        }
    }


    // --- MOVE FUNCTION ---

    public void MoveWithCollisionPrevention(Vector3 destination)
    {
        agent.isStopped = false;
        isMovementActive = true;
        hasArrived = false;
        currentDestination = destination;
        agent.SetDestination(currentDestination);
    }

    public Vector3 GetPosition()
    {
        return estimatedPosition;
    }
}