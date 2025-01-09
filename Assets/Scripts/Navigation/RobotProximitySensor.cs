using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class RobotProximitySensor : MonoBehaviour
{
    // --- PUBLIC PROPERTIES ---
    [Header("Agent")]
    public NavMeshAgent agent;
    public ForkliftNavController controller;
    public RobotKalmanPosition robotKalmanPosition;

    [Header("Sensore di prossimità")]
    public float raycastDistance = 20f;
    public int numberOfRays = 180;
    public float obstacleDetectionDistance = 1f;
    public float rayHeight = 0.4f;

    // --- PRIVATE VARIABLES ---
    private List<Vector3> compromisedRays = new List<Vector3>();
    private List<float> compromisedAngles = new List<float>();

    private bool isAvoidingPlayer = false;
    private Vector3 avoidanceDirection;
    private Vector3 lastKnownDestination;
    private Vector3 defaultPosition;
    private Vector3 estimatedPosition;


    private float positionTolerance = 6f; // Tolleranza per la posizione
    private bool isNearDefaultPosition = false; // Flag per verificare se si trova vicino alla posizione predefinita

    // --- UNITY METHODS ---
    public void Start()
    {
        defaultPosition = controller.defaultPosition;

        // Blocca il movimento iniziale fino a quando non viene impostata una destinazione
        agent.isStopped = true;

        // Assicurati di avere una destinazione valida
        if (agent.hasPath || agent.destination != Vector3.zero)
        {
            lastKnownDestination = agent.destination;
            agent.isStopped = false; // Riattiva il movimento solo se la destinazione è valida
        }
    }

    public void Update()
    {
        estimatedPosition = robotKalmanPosition.GetEstimatedPosition();
        isNearDefaultPosition = Vector3.Distance(defaultPosition, estimatedPosition) <= positionTolerance;
        //Debug.Log($"{defaultPosition}, {estimatedPosition}, {Vector3.Distance(defaultPosition, estimatedPosition)}");

        if (isNearDefaultPosition)
        {
            StartCoroutine(controller.SmoothRotateToDirection(Vector3.back));
            DisableSensors();
            return;
        }
        else
        {
            if (!isAvoidingPlayer)
            {
                SaveCurrentDestination();
            }

            HandleObstacleDetectionAndAvoidance();

            if (isAvoidingPlayer)
            {
                AvoidPlayer();
            }
            else if (!agent.hasPath || agent.remainingDistance < 0.1f)
            {
                RestoreLastKnownDestination();
            }
        }
    }

    // --- OBSTACLE DETECTION & AVOIDANCE ---

    private void HandleObstacleDetectionAndAvoidance()
    {
        if (isNearDefaultPosition) return; // Ignora completamente la logica se vicino alla posizione predefinita

        compromisedRays.Clear();
        compromisedAngles.Clear();

        bool playerDetected = false;
        Vector3 combinedAvoidanceDirection = Vector3.zero;

        for (int i = 0; i < numberOfRays; i++)
        {
            float angle = i * (360f / numberOfRays);
            Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            Vector3 rayOrigin = transform.position + new Vector3(0, rayHeight, 0);

            if (Physics.Raycast(rayOrigin, direction, out RaycastHit hit, raycastDistance, ~LayerMask.GetMask("Ignore Raycast")))
            {
                if ((hit.collider.CompareTag("Player") || hit.collider.CompareTag("Robot")) && hit.distance < obstacleDetectionDistance)
                {
                    playerDetected = true;
                    compromisedRays.Add(direction);
                    compromisedAngles.Add(angle);
                    combinedAvoidanceDirection -= direction / hit.distance;
                    Debug.DrawRay(rayOrigin, direction * hit.distance, Color.red);
                }
                else
                {
                    Debug.DrawRay(rayOrigin, direction * hit.distance, Color.green);
                }
            }
            else
            {
                Debug.DrawRay(rayOrigin, direction * raycastDistance, Color.green);
            }
        }

        if (playerDetected)
        {
            isAvoidingPlayer = true;
            avoidanceDirection = combinedAvoidanceDirection.normalized;
        }
        else
        {
            isAvoidingPlayer = false;
        }
    }

    private void AvoidPlayer()
    {
        if (avoidanceDirection != Vector3.zero)
        {
            // Rotazione graduale per evitare il giocatore
            Quaternion targetRotation = Quaternion.LookRotation(avoidanceDirection);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, 30f * Time.deltaTime);

            // Continua verso la destinazione originale senza spostarsi lateralmente
            Vector3 forwardMovement = transform.forward * agent.speed * Time.deltaTime;
            agent.Move(forwardMovement);
        }
    }

    private void SaveCurrentDestination()
    {
        if (agent.hasPath)
        {
            lastKnownDestination = agent.destination;
        }
    }

    private void RestoreLastKnownDestination()
    {
        if (lastKnownDestination != null && lastKnownDestination != agent.destination)
        {
            agent.SetDestination(lastKnownDestination);
        }
    }

    private void DisableSensors()
    {
        isAvoidingPlayer = false;
        compromisedRays.Clear();
        compromisedAngles.Clear();
        avoidanceDirection = Vector3.zero;
    }
}
