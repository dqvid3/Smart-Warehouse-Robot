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

    public Grid grid;
    private List<Node> currentPathNodes = new List<Node>();

    private List<Node> nodesWithModifiedWeight = new List<Node>();
    private float obstacleWeight = 100f; // Peso temporaneo per i nodi vicini all'ostacolo

    private Robot robot;

    private void Start()
    {
        robot = GetComponent<Robot>();
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

        // Richiede il calcolo del path
        PathRequestManager.RequestPath(start, end, OnPathFound);

        while (Vector3.Distance(robotToMove.transform.position, end) > 0.1f)
        {
            if (Vector3.Distance(robotToMove.transform.position, end) <= arrivalTolerance && raycastManager.sensorsEnabled)
            {
                DisableSensors();
            }
            yield return null;
        }

        if (showPath)
        {
            lineRenderer.positionCount = 0;
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

            fullPath = CorrectFinalPath(fullPath);

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
            Debug.LogError($"Impossibile trovare il percorso. Riprovo tra 2 secondi... (Start: {start}, End: {end})");
            StartCoroutine(RetryPath(2f));
        }
    }

    private IEnumerator RetryPath(float waitTime)
    {
        yield return new WaitForSeconds(waitTime);
        this.start = robotToMove.transform.position;

        PathRequestManager.RequestPath(start, end, OnPathFound);
    }

    private Vector3[] CorrectFinalPath(Vector3[] path)
    {
        if (path.Length < 3)
            return path;

        Vector3 lastSegment = (path[path.Length - 1] - path[path.Length - 2]).normalized;
        Vector3 secondLastSegment = (path[path.Length - 2] - path[path.Length - 3]).normalized;

        // Se i segmenti formano un angolo troppo "acuto", rimuovi il nodo intermedio
        if (Vector3.Dot(lastSegment, secondLastSegment) < 0.95f)
        {
            List<Vector3> correctedPath = new List<Vector3>(path);
            correctedPath.RemoveAt(correctedPath.Count - 2); // Rimuove il penultimo nodo
            return correctedPath.ToArray();
        }

        return path;
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
        if (path.Length > 1)
        {
            Vector3 initialDirection = (path[1] - path[0]).normalized;
            Quaternion initialRotation = Quaternion.LookRotation(initialDirection);

            while (Quaternion.Angle(robotToMove.transform.rotation, initialRotation) > 0.1f)
            {
                robotToMove.transform.rotation = Quaternion.Slerp(
                    robotToMove.transform.rotation,
                    initialRotation,
                    Time.deltaTime * moveSpeed
                );
                yield return null;
            }
        }

        // Percorri ogni tratto del path
        for (int i = 1; i < path.Length; i++)
        {
            Vector3 startPosition = robotToMove.transform.position;
            Vector3 targetPosition = path[i];
            Vector3 direction = (targetPosition - startPosition).normalized;

            Quaternion targetRotation = direction != Vector3.zero
                ? Quaternion.LookRotation(direction)
                : Quaternion.identity;

            float journey = 0f;
            float distance = Vector3.Distance(startPosition, targetPosition);

            // Movimento sul tratto [startPosition -> targetPosition]
            while (journey < 1f)
            {
                // Controllo sensori
                if (raycastManager.sensorsEnabled)
                {
                    string currentObstacleDirection = raycastManager.GetObstacleDirection();

                    // Se c'� un blocco su tutti i lati -> "Pausa"
                    if (currentObstacleDirection == "Pausa")
                    {
                        Debug.Log($"Robot {robot.id}: Ostacoli su tutti i lati - Pausa del robot.");
                        yield return new WaitForSeconds(3f);
                        start = robotToMove.transform.position;
                        PathRequestManager.RequestPath(start, end, OnPathFound);
                        yield break;
                    }
                    else
                    {
                        if (currentObstacleDirection != "Nessun ostacolo" &&
                                 currentObstacleDirection != "Sensori disabilitati" &&
                                 currentObstacleDirection != "Dietro")
                        {
                            Debug.Log($"Robot {robot.id}: Ostacolo: {currentObstacleDirection}. Ricalcolo percorso.");
                            ModifyNextNodesWeight(currentObstacleDirection);
                            start = robotToMove.transform.position;
                            PathRequestManager.RequestPath(start, end, OnPathFound);
                            ResetModifiedNodeWeights();
                            yield break;
                        }
                    }
                }

                // Avanza gradualmente il robot
                journey += Time.deltaTime * moveSpeed / distance;
                robotToMove.transform.position = Vector3.Lerp(startPosition, targetPosition, journey);
                robotToMove.transform.rotation = Quaternion.Slerp(
                    robotToMove.transform.rotation,
                    targetRotation,
                    Time.deltaTime * moveSpeed
                );

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

                // Aggiornamento linea durante il movimento (se vogliamo che "scompaia" dietro al robot)
                if (showPath)
                {
                    UpdateLineDuringMovement(path, i, journey, startPosition, targetPosition);
                }

                yield return null;
            }

            // Una volta completato il segmento, rilascio il nodo che ho appena lasciato
            Node currentNode = grid.NodeFromWorldPoint(path[i - 1]);
            grid.ReleaseNodes(new List<Node> { currentNode });
            currentPathNodes.Remove(currentNode);

            // Allineo posizione e rotazione
            robotToMove.transform.position = targetPosition;
            robotToMove.transform.rotation = targetRotation;
        }

        // Rilascio eventuale ultimo nodo
        if (path.Length > 1)
        {
            Node lastNode = grid.NodeFromWorldPoint(path[path.Length - 2]);
            grid.ReleaseNodes(new List<Node> { lastNode });
            currentPathNodes.Remove(lastNode);
        }

        if (showPath)
        {
            lineRenderer.positionCount = 0;
        }
    }

    private void UpdateLineDuringMovement(Vector3[] path, int currentIndex, float journey, Vector3 startPosition, Vector3 targetPosition)
    {
        Vector3 currentRobotPos = Vector3.Lerp(startPosition, targetPosition, journey);

        int remainingPoints = path.Length - currentIndex;
        int positionCount = Mathf.Max(0, remainingPoints + 1);

        lineRenderer.positionCount = positionCount;
        if (positionCount == 0) return;

        lineRenderer.SetPosition(0, currentRobotPos);

        for (int i = 1; i < positionCount; i++)
        {
            lineRenderer.SetPosition(i, path[currentIndex + i - 1]);
        }
    }

    private void ModifyNextNodesWeight(string obstacleDirection)
    {
        Vector3 currentPos = robotToMove.transform.position;

        float nodeSize = grid.nodeRadius * 2; 

        Vector3 forwardDirection = robotToMove.transform.forward;
        Vector3 rightDirection = robotToMove.transform.right;
        Vector3 leftDirection = -rightDirection;
        Vector3 backwardDirection = -forwardDirection;

        // Penalizza i 2 nodi davanti al robot
        for (int i = 1; i <= 2; i++)
        {
            Vector3 frontNodePos = currentPos + forwardDirection * nodeSize * i;
            Node frontNode = grid.NodeFromWorldPoint(frontNodePos);
            ModifyNode(frontNode);
        }

        // Se l'ostacolo è a sinistra, penalizza i 2 nodi a sinistra dei 2 nodi davanti e i 2 nodi a sinistra del nodo corrente
        if (obstacleDirection == "Sinistra")
        {
            // Penalizza i 2 nodi a sinistra dei 2 nodi davanti
            for (int i = 1; i <= 2; i++)
            {
                Vector3 frontLeftNodePos = currentPos + forwardDirection * nodeSize * i + leftDirection * nodeSize;
                Node frontLeftNode = grid.NodeFromWorldPoint(frontLeftNodePos);
                ModifyNode(frontLeftNode);
            }

            // Penalizza i 2 nodi a sinistra del nodo corrente
            for (int i = 1; i <= 2; i++)
            {
                Vector3 leftNodePos = currentPos + leftDirection * nodeSize * i;
                Node leftNode = grid.NodeFromWorldPoint(leftNodePos);
                ModifyNode(leftNode);
            }
        }

        // Se l'ostacolo è a destra, penalizza i 2 nodi a destra dei 2 nodi davanti e i 2 nodi a destra del nodo corrente
        if (obstacleDirection == "Destra")
        {
            // Penalizza i 2 nodi a destra dei 2 nodi davanti
            for (int i = 1; i <= 2; i++)
            {
                Vector3 frontRightNodePos = currentPos + forwardDirection * nodeSize * i + rightDirection * nodeSize;
                Node frontRightNode = grid.NodeFromWorldPoint(frontRightNodePos);
                ModifyNode(frontRightNode);
            }

            // Penalizza i 2 nodi a destra del nodo corrente
            for (int i = 1; i <= 2; i++)
            {
                Vector3 rightNodePos = currentPos + rightDirection * nodeSize * i;
                Node rightNode = grid.NodeFromWorldPoint(rightNodePos);
                ModifyNode(rightNode);
            }
        }

        // Se l'ostacolo è dietro, penalizza i 2 nodi dietro al robot
        if (obstacleDirection == "Dietro")
        {
            for (int i = 1; i <= 2; i++)
            {
                Vector3 backNodePos = currentPos + backwardDirection * nodeSize * i;
                Node backNode = grid.NodeFromWorldPoint(backNodePos);
                ModifyNode(backNode);
            }
        }
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
            robotToMove.transform.position.y,
            robotToMove.transform.position.z + noiseZ
        );

        return noisyPosition;
    }
}

