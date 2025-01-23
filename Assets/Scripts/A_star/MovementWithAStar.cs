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

    public Grid grid; // Assegna questo riferimento nell'Inspector
    private List<Node> currentPathNodes = new List<Node>();

    private List<Node> nodesWithModifiedWeight = new List<Node>();
    private float obstacleWeight = 20f; // Peso temporaneo per i nodi vicini all'ostacolo
    private float weightResetTime = 3f; // Tempo dopo il quale i pesi vengono ripristinati


    private void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();

        lineRenderer.startWidth = 0.2f;
        lineRenderer.endWidth = 0.2f;
        lineRenderer.positionCount = 0;

        this.start = forkliftNavController.defaultPosition;
    }

    public List<Node> GetNextNodes(int count)
    {
        List<Node> nextNodes = new List<Node>();
        Vector3 currentPos = robotToMove.transform.position;

        // Trova il nodo corrente nel percorso
        int currentIndex = currentPathNodes.FindIndex(n => grid.NodeFromWorldPoint(currentPos) == n);
        if (currentIndex == -1) return nextNodes;

        for (int i = currentIndex; i < Mathf.Min(currentIndex + count, currentPathNodes.Count); i++)
        {
            nextNodes.Add(currentPathNodes[i]);
        }
        return nextNodes;
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

            currentPathNodes.Clear();
            for (int i = 1; i < fullPath.Length - 1; i++)
            {
                Node node = grid.NodeFromWorldPoint(fullPath[i]);
                currentPathNodes.Add(node);
            }
            grid.OccupyNodes(currentPathNodes);

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

                    // Rilascia i nodi del percorso corrente
                    grid.ReleaseNodes(currentPathNodes);
                    currentPathNodes.Clear();

                    yield return StartCoroutine(MoveToPosition(deviationPosition));

                    Vector3 obstaclePosition = robotToMove.transform.position + robotToMove.transform.forward * 1.5f;
                    ModifyNodeWeightsNearObstacle(obstaclePosition);

                    Debug.Log("Ricalcolo del percorso dalla nuova posizione.");
                    start = robotToMove.transform.position;
                    PathRequestManager.RequestPath(start, end, OnPathFound);
                    yield break;
                }

                journey += Time.deltaTime * moveSpeed / Vector3.Distance(startPosition, targetPosition);

                robotToMove.transform.position = Vector3.Lerp(startPosition, targetPosition, journey);
                robotToMove.transform.rotation = Quaternion.Slerp(robotToMove.transform.rotation, targetRotation, Time.deltaTime * moveSpeed);

                // Rilascia il nodo precedente dopo averlo superato
                if (i > 1)
                {
                    Node previousNode = grid.NodeFromWorldPoint(path[i - 2]);
                    if (Vector3.Distance(robotToMove.transform.position, path[i - 1]) < grid.nodeRadius * 1.5f)
                    {
                        grid.ReleaseNodes(new List<Node> { previousNode });
                        currentPathNodes.Remove(previousNode);
                    }
                }

                yield return null;
            }

            // Rilascia il nodo appena raggiunto quando si arriva al punto di destinazione del segmento corrente
            Node currentNode = grid.NodeFromWorldPoint(path[i - 1]);
            grid.ReleaseNodes(new List<Node> { currentNode });
            currentPathNodes.Remove(currentNode);

            robotToMove.transform.position = targetPosition;
            robotToMove.transform.rotation = targetRotation;
        }

        // Rilascia l'ultimo nodo (punto di destinazione finale)
        if (path.Length > 1)
        {
            Node lastNode = grid.NodeFromWorldPoint(path[path.Length - 2]);
            grid.ReleaseNodes(new List<Node> { lastNode });
            currentPathNodes.Remove(lastNode);
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

    private void ModifyNodeWeightsNearObstacle(Vector3 obstaclePosition)
    {
        List<Node> nearbyNodes = grid.GetNeighbours(grid.NodeFromWorldPoint(obstaclePosition));
        foreach (Node node in nearbyNodes)
        {
            if (!nodesWithModifiedWeight.Contains(node))
            {
                nodesWithModifiedWeight.Add(node);
                node.movementPenalty += (int)obstacleWeight;
            }
        }
        StartCoroutine(ResetNodeWeights());
    }

    private IEnumerator ResetNodeWeights()
    {
        yield return new WaitForSeconds(weightResetTime);

        foreach (Node node in nodesWithModifiedWeight)
        {
            node.movementPenalty -= (int)obstacleWeight;
        }
        nodesWithModifiedWeight.Clear();
    }



    private void DisableSensors()
    {
        raycastManager.sensorsEnabled = false;
    }

    private void EnableSensors()
    {
        raycastManager.sensorsEnabled = true;
    }

    public Vector3 GetOdometry()
    {
        return robotToMove.transform.position;
    }
}
