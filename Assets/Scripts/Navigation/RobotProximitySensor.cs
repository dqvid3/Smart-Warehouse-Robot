using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class RobotProximitySensor : MonoBehaviour
{
    // --- PUBLIC PROPERTIES ---
    [Header("Agent")]
    public NavMeshAgent agent;
    public Robot robot;

    [Header("Sensore di prossimità")]
    public float raycastDistance = 3f;
    public float emergencyDistance = 0.8f;
    public int numberOfRays = 180; 
    public float rayHeight = 0.4f; // Altezza dei raggi rispetto alla posizione del robot

    // --- PRIVATE VARIABLES ---
    private bool isObstacleDetected = false;
    private Dictionary<Collider, Vector3> previousPositions = new Dictionary<Collider, Vector3>();

    // --- UNITY METHODS ---
    public void Update()
    {
        if (robot.isPaused)
        {
            return;
        }
        else 
        { 
            HandleObstacleDetection();

            // Ferma o riattiva il movimento in base alla presenza di un ostacolo
            if (isObstacleDetected && !agent.isStopped)
            {
                agent.isStopped = true;
            }
            else if (!isObstacleDetected && agent.isStopped)
            {
                agent.isStopped = false;
            }
        }
    }

    // --- OBSTACLE DETECTION ---

    private void HandleObstacleDetection()
    {
        isObstacleDetected = false; // Resetta lo stato a ogni frame

        Vector3 rayOrigin = transform.position + new Vector3(0, rayHeight, 0);

        for (int i = 0; i < numberOfRays; i++)
        {
            float angle = i * (180f / (numberOfRays - 1)) - 90f; // Angolo da -90° a +90°
            Vector3 direction = Quaternion.Euler(0f, angle, 0f) * transform.forward;

            if (Physics.Raycast(rayOrigin, direction, out RaycastHit hit, raycastDistance))
            {
                // Considera qualsiasi oggetto rilevato come ostacolo
                if (IsObjectInMotion(hit.collider))
                {
                    isObstacleDetected = true;
                    //Ostacolo in movimento
                    Debug.DrawRay(rayOrigin, direction * hit.distance, Color.red);
                    return; // Esce subito se rileva un ostacolo in movimento
                }
                else
                {
                    // Controllo ostacoli statici a distanza di emergenza
                    if (Physics.Raycast(rayOrigin, direction, out RaycastHit hitEmergency, emergencyDistance))
                    {
                        Vector3 retreatDirection = -direction.normalized;
                        transform.position = Vector3.Lerp(transform.position, transform.position + retreatDirection * 0.1f, Time.deltaTime);
                    }
                    //Ostacolo statico
                    Debug.DrawRay(rayOrigin, direction * hit.distance, Color.yellow);
                }
            }
            else
            {
                Debug.DrawRay(rayOrigin, direction * raycastDistance, Color.green);
            }
        }
    }

    private bool IsObjectInMotion(Collider collider)
    {
        // Controlla se l'oggetto si sta muovendo rilevando cambiamenti di posizione
        if (previousPositions.TryGetValue(collider, out Vector3 previousPosition))
        {
            Vector3 currentPosition = collider.transform.position;
            previousPositions[collider] = currentPosition;
            return Vector3.SqrMagnitude(previousPosition - currentPosition) > 0.0001f; // Precisione per rilevare piccoli movimenti
        }
        else
        {
            // Salva la posizione iniziale del collider
            previousPositions[collider] = collider.transform.position;
            return false;
        }
    }
}
