using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class RobotMovementWithNavMeshAndCollisionPrevention : MonoBehaviour
{
    // --- PUBLIC PROPERTIES ---

    [Header("Destinazioni")]
    public Vector3 pointA;
    public Vector3 pointB;

    [Header("Parametri di movimento")]
    public float moveSpeed = 3f;
    [Range(0.1f, 50f)]
    public float raycastDistance = 5f;
    [Range(1, 360)]
    public int numberOfRays = 360;
    [Range(0.1f, 20f)]
    public float obstacleDetectionDistance = 2f;

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

    private bool isMoving = true;
    private List<Vector3> freeRays = new List<Vector3>();
    private List<Vector3> compromisedRays = new List<Vector3>();
    private List<float> compromisedAngles = new List<float>();

    // --- UNITY METHODS ---

    public void Start()
    {
        InitializeAgent();
        InitializeKalmanFilters();
        currentDestination = pointB;
        agent.SetDestination(currentDestination);
        distances = new float[numberOfRays];
    }

    public void Update()
    {
        if (!agent.isStopped)
        {
            UpdateNoisyAndEstimatedPosition();
        }

        HandleObstacleDetectionAndAvoidance();
        CheckArrival();

        AdjustObstacleDetectionDistance();

        if (isMoving)
        {
            DebugPositionInfo();
        }
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
        if (!isMoving) return;

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
                float noisyDistance = hit.distance + Random.Range(-0.2f, 0.2f);
                distances[i] = kalmanFilters[i].Update(noisyDistance);

                if (distances[i] < obstacleDetectionDistance)
                {
                    obstacleDetected = true;
                    compromisedRays.Add(direction);
                    compromisedAngles.Add(angle);
                    Debug.DrawRay(transform.position, direction * distances[i], Color.red);

                    combinedAvoidanceDirection -= direction / distances[i]; // Weight inversely to distance
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

            Debug.Log("[INFO] Ostacolo rilevato, direzione modificata.");
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
            Debug.Log("[INFO] Robot troppo vicino alla destinazione, fermato e ricalcolando.");
            hasArrived = true;
            StopRobot();
            RecalculatePath();
        }
    }

    private void RecalculatePath()
    {
        currentDestination = pointB;
        agent.SetDestination(currentDestination);
        StartRobot();
    }

    // --- ROBOT CONTROL ---

    private void StopRobot()
    {
        agent.isStopped = true;
        isMoving = false;
    }

    private void StartRobot()
    {
        agent.isStopped = false;
        isMoving = true;
    }

    // --- ADJUST OBSTACLE DETECTION ---

    private void AdjustObstacleDetectionDistance()
    {
        float distanceToDestination = Vector3.Distance(estimatedPosition, pointB);

        if (distanceToDestination < obstacleDetectionDistance && obstacleDetectionDistance > 0.1f)
        {
            obstacleDetectionDistance = Mathf.Max(0.1f, obstacleDetectionDistance - Time.deltaTime * 0.5f); // Adjust rate as needed
            Debug.Log($"[INFO] Distanza a pointB: {distanceToDestination}, riducendo obstacleDetectionDistance a: {obstacleDetectionDistance}");
        }
    }

    // --- DEBUGGING ---

    private void DebugPositionInfo()
    {
        Debug.Log($"[DEBUG] Free Rays: {freeRays.Count}, Compromised Rays: {compromisedRays.Count}");
        Debug.Log($"[DEBUG] Noisy Position: {noisyPosition}, Estimated Position: {estimatedPosition}");
    }
}
