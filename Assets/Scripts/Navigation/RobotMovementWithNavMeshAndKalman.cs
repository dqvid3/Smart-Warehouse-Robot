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
    public int numberOfRays = 360;
    [Range(0.1f, 20f)]
    public float obstacleDetectionDistance = 2;

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
        // Esegui logica solo se il movimento è attivo
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
        freeRays.Clear();
        compromisedRays.Clear();
        compromisedAngles.Clear();

        bool obstacleDetected = false;
        Vector3 combinedAvoidanceDirection = Vector3.zero;

        for (int i = 0; i < numberOfRays; i++)
        {
            float angle = i * (360f / numberOfRays);
            Vector3 direction = Quaternion.Euler(0, angle, 0) * Vector3.forward;

            Debug.DrawRay(transform.position, direction * raycastDistance, Color.green);

            if (Physics.Raycast(transform.position, direction, out RaycastHit hit, raycastDistance))
            {
                // Ignora il proprio oggetto
                if (hit.collider.transform.IsChildOf(transform)) continue;


                float noisyDistance = hit.distance + Random.Range(-0.2f, 0.2f);
                distances[i] = kalmanFilters[i].Update(noisyDistance);

                if (distances[i] < obstacleDetectionDistance)
                {
                    obstacleDetected = true;
                    compromisedRays.Add(direction);
                    compromisedAngles.Add(angle);
                    Debug.DrawRay(transform.position, direction * distances[i], Color.red);

                    // Calcolo della direzione di evitamento pesata inversamente alla distanza
                    combinedAvoidanceDirection -= direction / distances[i];
                }
                else
                {
                    freeRays.Add(direction);
                }
            }
            else
            {
                distances[i] = raycastDistance;
                freeRays.Add(direction);
            }
        }

        if (obstacleDetected)
        {
            combinedAvoidanceDirection.Normalize();
            Vector3 modifiedTargetDirection = (agent.destination - transform.position).normalized + combinedAvoidanceDirection;
            Vector3 avoidanceTarget = transform.position + modifiedTargetDirection.normalized * obstacleDetectionDistance;
            agent.SetDestination(avoidanceTarget);
        }
        else
        {
            if (currentDestination != transform.position)
            {
                agent.SetDestination(currentDestination);
            }
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