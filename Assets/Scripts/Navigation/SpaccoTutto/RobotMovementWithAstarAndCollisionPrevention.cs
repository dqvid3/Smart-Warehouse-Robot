using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class RobotMovementWithAstarAndCollisionPrevention : MonoBehaviour
{
    [Header("Parametri di movimento")]
    public float moveSpeed = 3f;
    [Range(0.1f, 50f)]
    public float raycastDistance = 20f;
    [Range(1, 360)]
    public int numberOfRays = 72;
    [Range(0.1f, 20f)]
    public float obstacleDetectionDistance = 2f;
    [Header("Pathfinder")]
    public AStarPathfinder pathfinder;
    // Kalman per X e Z
    private KalmanFilter positionKalmanFilterX;
    private KalmanFilter positionKalmanFilterZ;

    private CharacterController controller; // Oppure rigidbody, come preferisci

    private List<Vector3> currentPath = new List<Vector3>(); // Waypoints da seguire
    private int currentWaypointIndex = 0;
    private bool hasArrived = false;
    private bool isMovementActive = false;

    private Vector3 currentDestination; // Destinazione finale
    private Vector3 estimatedPosition;
    private Vector3 noisyPosition;

    private float[] distances;
    private KalmanFilter[] kalmanFilters;

    // EVENTUALI: per gestire repath
    private float repathInterval = 2f;
    private float lastRepathTime = 0f;

    private void Start()
    {
        controller = GetComponent<CharacterController>();

        // Inizializza Kalman Filter per i raggi
        kalmanFilters = new KalmanFilter[numberOfRays];
        distances = new float[numberOfRays];
        for (int i = 0; i < numberOfRays; i++)
        {
            kalmanFilters[i] = new KalmanFilter(
                initialEstimate: raycastDistance,
                initialError: 1f,
                processNoise: 0.1f,
                measurementNoise: 0.5f
            );
        }

        // Inizializza Kalman Filter per la posizione X e Z
        positionKalmanFilterX = new KalmanFilter(0f, 1f, 0.1f, 0.5f);
        positionKalmanFilterZ = new KalmanFilter(0f, 1f, 0.1f, 0.5f);
    }

    private void Update()
    {
        if (!isMovementActive) return;

        UpdateNoisyAndEstimatedPosition();

        // Se abbiamo un path
        if (currentPath.Count > 0 && currentWaypointIndex < currentPath.Count)
        {
            // Avanzare verso il waypoint corrente
            Vector3 waypoint = currentPath[currentWaypointIndex];
            float distanceToWaypoint = Vector3.Distance(estimatedPosition, waypoint);

            // Se siamo vicini al waypoint, passiamo al prossimo
            if (distanceToWaypoint < 0.5f)
            {
                currentWaypointIndex++;
                if (currentWaypointIndex >= currentPath.Count)
                {
                    // Destinazione finale raggiunta
                    hasArrived = true;
                    isMovementActive = false;
                    return;
                }
            }
            else
            {
                // Muoviti verso il waypoint
                MoveTowardsWaypoint(waypoint);
            }
        }

        // Evita ostacoli dinamici
        HandleDynamicObstacleDetection();

        // Se rilevi un ostacolo insormontabile davanti, ricalcola il path ogni tot secondi
        if (Time.time - lastRepathTime > repathInterval)
        {
            lastRepathTime = Time.time;
            if (ShouldRepath())
            {
                ComputeNewPath();
            }
        }
    }

    private void MoveTowardsWaypoint(Vector3 waypoint)
    {
        Vector3 direction = (waypoint - transform.position).normalized;
        // Eventuale rotazione
        if (direction != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 5f);
        }

        // Muovi il robot in avanti
        Vector3 move = direction * moveSpeed * Time.deltaTime;
        controller.Move(move);
    }

    // Verifica se c’è un ostacolo “grosso” davanti che blocca la strada
    private bool ShouldRepath()
    {
        // Esempio: se davanti a noi c’è un ostacolo molto vicino, rifacciamo path
        float forwardCheckDist = 1.5f;

        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, forwardCheckDist))
        {
            return true;
        }
        return false;
    }

    private void ComputeNewPath()
    {
        // Ricomputo A*
        currentPath = pathfinder.ComputePath(transform.position, currentDestination);
        currentWaypointIndex = 0;
    }

    // Gestione di ostacoli dinamici con i ray. Se ci sono, modifichiamo leggermente la direzione
    private void HandleDynamicObstacleDetection()
    {
        bool obstacleDetected = false;
        Vector3 combinedAvoidanceDir = Vector3.zero;

        for (int i = 0; i < numberOfRays; i++)
        {
            float angle = i * (360f / numberOfRays);
            Vector3 dir = Quaternion.Euler(0, angle, 0) * Vector3.forward;

            if (Physics.Raycast(transform.position, dir, out RaycastHit hit, raycastDistance))
            {
                float noisyDist = hit.distance + Random.Range(-0.2f, 0.2f);
                distances[i] = kalmanFilters[i].Update(noisyDist);

                if (distances[i] < obstacleDetectionDistance)
                {
                    obstacleDetected = true;
                    // Riduci la direzione (repulsione) in base alla distanza
                    combinedAvoidanceDir -= (dir / distances[i]);
                    Debug.DrawRay(transform.position, dir * distances[i], Color.red);
                }
                else
                {
                    Debug.DrawRay(transform.position, dir * distances[i], Color.green);
                }
            }
            else
            {
                distances[i] = raycastDistance;
            }
        }

        if (obstacleDetected)
        {
            // Forza di repulsione
            combinedAvoidanceDir.Normalize();
            // Spinta rapida per evitare
            Vector3 avoidanceForce = combinedAvoidanceDir * (moveSpeed * 0.5f);

            // Muovi leggermente il robot lateralmente
            controller.Move(avoidanceForce * Time.deltaTime);
        }
    }

    private void UpdateNoisyAndEstimatedPosition()
    {
        // Aggiungiamo rumore
        noisyPosition = transform.position + new Vector3(Random.Range(-0.2f, 0.2f), 0, Random.Range(-0.2f, 0.2f));

        float estX = positionKalmanFilterX.Update(noisyPosition.x);
        float estZ = positionKalmanFilterZ.Update(noisyPosition.z);
        estimatedPosition = new Vector3(estX, transform.position.y, estZ);
    }

    // --- METODI PUBBLICI ---

    public void MoveWithAstar(Vector3 destination)
    {
        currentDestination = destination;
        hasArrived = false;
        isMovementActive = true;

        // Computa subito un path con A*
        currentPath = pathfinder.ComputePath(transform.position, destination);
        currentWaypointIndex = 0;
    }

    public bool HasArrived()
    {
        return hasArrived;
    }

    public Vector3 GetPosition()
    {
        return estimatedPosition;
    }

    // (Opzionale) per fermare il robot
    public void StopRobot()
    {
        isMovementActive = false;
        currentPath.Clear();
    }
}
