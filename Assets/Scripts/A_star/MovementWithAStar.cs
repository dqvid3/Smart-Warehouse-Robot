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
    public float arrivalTolerance = .5f;

    public Grid grid; // Assegna questo riferimento nell'Inspector
    private List<Node> currentPathNodes = new List<Node>();

    private List<Node> nodesWithModifiedWeight = new List<Node>();
    private float obstacleWeight = 100f; // Peso temporaneo per i nodi vicini all'ostacolo


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
        ResetModifiedNodeWeights();
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
            Quaternion targetRotation = direction != Vector3.zero ? Quaternion.LookRotation(direction) : Quaternion.identity;

            float journey = 0f;

            while (journey < 1f)
            {
                if(raycastManager.sensorsEnabled)
                {
                    string currentObstacleDirection = raycastManager.GetObstacleDirection();

                    if(currentObstacleDirection == "Pausa")
                    {
                        Debug.Log("Ostacoli su tutti i lati - Pausa del robot");
                        yield return new WaitForSeconds(3f);
                        start = robotToMove.transform.position;
                        PathRequestManager.RequestPath(start, end, OnPathFound);
                        yield break;
                    }
                    else if(currentObstacleDirection != "Nessun ostacolo" && 
                        currentObstacleDirection != "Sensori disabilitati")
                    {
                        Debug.Log($"Ostacolo: {currentObstacleDirection}. Ricalcolo percorso.");
                        ModifyNextNodesWeight(currentObstacleDirection);
                        yield break;
                    }
                }

                // Movimento regolare
                journey += Time.deltaTime * moveSpeed / Vector3.Distance(startPosition, targetPosition);
                robotToMove.transform.position = Vector3.Lerp(startPosition, targetPosition, journey);
                robotToMove.transform.rotation = Quaternion.Slerp(robotToMove.transform.rotation, targetRotation, Time.deltaTime * moveSpeed);

                // Rilascio nodi precedenti
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

            Node currentNode = grid.NodeFromWorldPoint(path[i - 1]);
            grid.ReleaseNodes(new List<Node> { currentNode });
            currentPathNodes.Remove(currentNode);
            robotToMove.transform.position = targetPosition;
            robotToMove.transform.rotation = targetRotation;
        }

        if (path.Length > 1)
        {
            Node lastNode = grid.NodeFromWorldPoint(path[path.Length - 2]);
            grid.ReleaseNodes(new List<Node> { lastNode });
            currentPathNodes.Remove(lastNode);
        }
    }

    private void ModifyNextNodesWeight(string obstacleDirection)
    {
        Vector3 currentPos = robotToMove.transform.position;
        Node currentNode = grid.NodeFromWorldPoint(currentPos);
        int currentIndex = currentPathNodes.IndexOf(currentNode);

        if (currentIndex == -1 || currentIndex >= currentPathNodes.Count - 1) return;

        // Resetta tutti i pesi precedenti
        ResetModifiedNodeWeights();

        // Penalizza primo nodo frontale
        Node firstFrontNode = currentPathNodes[currentIndex + 1];
        ModifyNode(firstFrontNode);

        Vector3 directionVector = Vector3.zero;

        Vector3 checkDirection = obstacleDirection == "Sinistra" ? 
                                -robotToMove.transform.right : 
                                robotToMove.transform.right;       

        // Calcoliamo le posizioni dei nodi da penalizzare
        float nodeSize = grid.nodeRadius * 2; // Diametro del nodo

        // 1. Penalizza nodo adiacente alla posizione corrente
        Vector3 currentPenaltyPos = currentPos + directionVector * nodeSize;
        Node currentPenaltyNode = grid.NodeFromWorldPoint(currentPenaltyPos);
        ModifyNode(currentPenaltyNode);

        // 2. Penalizza nodo adiacente al prossimo nodo nel percorso
        Node frontNode = currentPathNodes[currentIndex + 1];
        Vector3 frontPenaltyPos = frontNode.worldPosition + directionVector * nodeSize;
        Node frontPenaltyNode = grid.NodeFromWorldPoint(frontPenaltyPos);
        ModifyNode(frontPenaltyNode);

        // Richiedi nuovo percorso immediatamente
        start = currentPos;
        PathRequestManager.RequestPath(start, end, OnPathFound);
    }


    private void ModifyNode(Node node)
    {
        if (node != null && node.walkable && !nodesWithModifiedWeight.Contains(node))
        {
            node.movementPenalty += (int)obstacleWeight;
            nodesWithModifiedWeight.Add(node);
        }
    }

    private void ResetModifiedNodeWeights()
    {
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
        float noiseX = Random.Range(-0.1f, 0.1f);
        float noiseZ = Random.Range(-0.1f, 0.1f);

        Vector3 noisyPosition = new Vector3(
            robotToMove.transform.position.x + noiseX,
            robotToMove.transform.position.y, // Nessun rumore per Y
            robotToMove.transform.position.z + noiseZ
        );

        return noisyPosition;
    }

}
