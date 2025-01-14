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
    public float moveSpeed = 3.5f;

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
        this.end = destination;
        this.start = robotToMove.transform.position;

        PathRequestManager.RequestPath(start, end, OnPathFound);

        while (Vector3.Distance(robotToMove.transform.position, end) > 0.1f)
        {
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
                string obstacleDirection = raycastManager.GetObstacleDirection();
                if (obstacleDirection != null && (obstacleDirection == "Sinistra" || obstacleDirection == "Destra"))
                {
                    Debug.Log("Ostacolo rilevato. Applicando correzione.");

                    yield return StartCoroutine(ApplyDeviationAndResumePath(path, i, obstacleDirection));
                    yield break;
                }

                journey += Time.deltaTime * moveSpeed / Vector3.Distance(startPosition, targetPosition);

                robotToMove.transform.position = Vector3.Lerp(startPosition, targetPosition, journey);
                robotToMove.transform.rotation = Quaternion.Slerp(robotToMove.transform.rotation, targetRotation, Time.deltaTime * moveSpeed);
                start = robotToMove.transform.position;

                yield return null;
            }

            robotToMove.transform.position = targetPosition;
            robotToMove.transform.rotation = targetRotation;
        }

        start = end;
    }

    private IEnumerator ApplyDeviationAndResumePath(Vector3[] path, int currentIndex, string obstacleDirection)
    {
        Vector3[] modifiedPath = CreateDeviatedPath(path, currentIndex, obstacleDirection, 0.5f, 2f);

        if (modifiedPath == null || modifiedPath.Length < 2)
        {
            Debug.LogError("Percorso modificato non valido.");
            yield break;
        }

        if (showPath)
        {
            DrawPath(modifiedPath);
        }

        yield return StartCoroutine(MoveAlongPath(modifiedPath));
    }

    private Vector3[] CreateDeviatedPath(Vector3[] originalPath, int currentIndex, string direction, float deviationMin, float deviationMax)
    {
        List<Vector3> modifiedPath = new List<Vector3>(originalPath);

        if (currentIndex < 0 || currentIndex >= originalPath.Length - 1)
        {
            Debug.LogError("Indice non valido per la deviazione.");
            return null;
        }

        Vector3 currentNode = originalPath[currentIndex];
        Vector3 nextNode = originalPath[currentIndex + 1];
        Vector3 directionVector = (nextNode - currentNode).normalized;

        Vector3 deviationDirection;
        if (direction == "Sinistra")
        {
            deviationDirection = new Vector3(-directionVector.z, 0, directionVector.x);
        }
        else if (direction == "Destra")
        {
            deviationDirection = new Vector3(directionVector.z, 0, -directionVector.x);
        }
        else
        {
            Debug.LogError("Direzione ostacolo non valida.");
            return null;
        }

        float deviationAmount = Random.Range(deviationMin, deviationMax);
        Vector3 deviation = deviationDirection * deviationAmount;
        Vector3 newDeviatedNode = currentNode + deviation;

        modifiedPath.Insert(currentIndex + 1, newDeviatedNode);

        return modifiedPath.ToArray();
    }

    public Vector3 GetOdometry()
    {
        return robotToMove.transform.position;
    }
}
