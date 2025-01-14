using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(LineRenderer))]
public class MovementWithAStar : MonoBehaviour
{
    [Header("Controller")]
    public ForkliftNavController forkliftNavController;
    public bool showPath = false;

    public RaycastManager raycastManager;

    public Vector3 start;
    private Vector3 end;

    private LineRenderer lineRenderer;
    public GameObject robotToMove;
    public float moveSpeed = 2f;
    public float arrivalTolerance = 1f;
    public float deviationAngle = 45f; // Angolo massimo di deviazione

    private void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();

        lineRenderer.startWidth = 0.2f;
        lineRenderer.endWidth = 0.2f;
        lineRenderer.positionCount = 0;

        this.start = forkliftNavController.defaultPosition;
    }

    public IEnumerator MovementToPosition(Vector3 destination)
    {
        EnableSensors();
        this.end = destination;
        this.start = robotToMove.transform.position;

        PathRequestManager.RequestPath(start, end, OnPathFound);

        while (Vector3.Distance(robotToMove.transform.position, end) > 0.1f)
        {
            if (Vector3.Distance(robotToMove.transform.position, end) <= arrivalTolerance && raycastManager.sensorsEnabled)
            {
                DisableSensors();
            }

            yield return null;
        }
    }

    private void OnPathFound(Vector3[] path, bool success)
    {
        if (success)
        {
            Vector3[] fullPath = new Vector3[path.Length + 2];
            fullPath[0] = start;
            for (int i = 0; i < path.Length; i++)
            {
                fullPath[i + 1] = path[i];
            }
            fullPath[fullPath.Length - 1] = end;

            if (showPath)
            {
                DrawPath(fullPath);
            }

            StartCoroutine(MoveAlongPath(fullPath));
        }
        else
        {
            Debug.LogError("Impossibile trovare il percorso.");
        }
    }

    private void DrawPath(Vector3[] path)
    {
        lineRenderer.positionCount = path.Length;

        for (int i = 0; i < path.Length; i++)
        {
            lineRenderer.SetPosition(i, path[i]);
        }
    }

    private IEnumerator MoveAlongPath(Vector3[] path)
    {
        Vector3 initialDirection = (path[1] - path[0]).normalized;
        Quaternion initialRotation = Quaternion.LookRotation(initialDirection);

        while (Quaternion.Angle(robotToMove.transform.rotation, initialRotation) > 0.1f)
        {
            robotToMove.transform.rotation = Quaternion.Slerp(robotToMove.transform.rotation, initialRotation, Time.deltaTime * moveSpeed);
            yield return null;
        }

        for (int i = 1; i < path.Length; i++)
        {
            Vector3 startPosition = robotToMove.transform.position;
            Vector3 targetPosition = path[i];
            Vector3 direction = (targetPosition - startPosition).normalized;
            Quaternion targetRotation = Quaternion.identity;

            if (direction != Vector3.zero)
            {
                targetRotation = Quaternion.LookRotation(direction);
            }

            float journey = 0f;

            while (journey < 1f)
            {
                if (Vector3.Distance(robotToMove.transform.position, end) <= arrivalTolerance && raycastManager.sensorsEnabled)
                {
                    DisableSensors();
                }

                string obstacleDirection = raycastManager.GetObstacleDirection();
                if (obstacleDirection != null && (obstacleDirection == "Sinistra" || obstacleDirection == "Destra"))
                {
                    Debug.Log("Ostacolo rilevato. Spostamento per evitare l'ostacolo.");

                    Vector3 deviationPosition = CalculateDeviationPosition(robotToMove.transform.position, obstacleDirection, deviationAngle);

                    yield return StartCoroutine(MoveToPosition(deviationPosition));

                    Debug.Log("Ricalcolo del percorso dalla nuova posizione.");
                    start = robotToMove.transform.position;
                    PathRequestManager.RequestPath(start, end, OnPathFound);
                    yield break;
                }

                journey += Time.deltaTime * moveSpeed / Vector3.Distance(startPosition, targetPosition);

                robotToMove.transform.position = Vector3.Lerp(startPosition, targetPosition, journey);
                robotToMove.transform.rotation = Quaternion.Slerp(robotToMove.transform.rotation, targetRotation, Time.deltaTime * moveSpeed);

                yield return null;
            }

            robotToMove.transform.position = targetPosition;
            robotToMove.transform.rotation = targetRotation;
        }
    }

    private IEnumerator MoveToPosition(Vector3 targetPosition)
    {
        Vector3 startPosition = robotToMove.transform.position;
        Quaternion startRotation = robotToMove.transform.rotation;
        Quaternion targetRotation = Quaternion.LookRotation((targetPosition - startPosition).normalized);

        float journey = 0f;

        while (journey < 1f)
        {
            journey += Time.deltaTime * moveSpeed / Vector3.Distance(startPosition, targetPosition);

            robotToMove.transform.position = Vector3.Lerp(startPosition, targetPosition, journey);
            robotToMove.transform.rotation = Quaternion.Slerp(startRotation, targetRotation, journey);
            yield return null;
        }

        robotToMove.transform.position = targetPosition;
        robotToMove.transform.rotation = targetRotation;
    }

    private Vector3 CalculateDeviationPosition(Vector3 currentPosition, string direction, float angle)
    {
        Vector3 deviationDirection;

        if (direction == "Sinistra")
        {
            deviationDirection = Quaternion.Euler(0, angle, 0) * robotToMove.transform.forward;
        }
        else if (direction == "Destra")
        {
            deviationDirection = Quaternion.Euler(0, -angle, 0) * robotToMove.transform.forward;
        }
        else
        {
            Debug.LogError("Direzione ostacolo non valida.");
            return currentPosition;
        }

        return currentPosition + deviationDirection.normalized * 1.5f; // Spostarsi di 1.5 unit� nella direzione calcolata
    }

    private void DisableSensors()
    {
        raycastManager.sensorsEnabled = false;
        Debug.Log("Sensori disabilitati.");
    }

    private void EnableSensors()
    {
        raycastManager.sensorsEnabled = true;
        Debug.Log("Sensori abilitati.");
    }

    public Vector3 GetOdometry()
    {
        return robotToMove.transform.position;
    }
}
