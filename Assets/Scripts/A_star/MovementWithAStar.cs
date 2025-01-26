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

    /// <summary>
    /// Restituisce i prossimi 'count' nodi del percorso rispetto alla posizione corrente del robot.
    /// </summary>
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

    /// <summary>
    /// Coroutine principale: attende il calcolo del path e poi sposta il robot nella destinazione.
    /// </summary>
    public IEnumerator MovementToPosition(Vector3 destination)
    {
        EnableSensors();
        this.end = destination;
        this.start = robotToMove.transform.position;

        // Richiede il calcolo del path
        PathRequestManager.RequestPath(start, end, OnPathFound);

        // Attende finch� il robot non si avvicina alla destinazione
        while (Vector3.Distance(robotToMove.transform.position, end) > 0.1f)
        {
            // Se siamo vicinissimi, disabilitiamo i sensori (non � questo il problema specifico, ma la logica resta)
            if (Vector3.Distance(robotToMove.transform.position, end) <= arrivalTolerance && raycastManager.sensorsEnabled)
            {
                DisableSensors();
            }
            yield return null;
        }

        // Una volta arrivato, se vogliamo far sparire la linea:
        if (showPath)
        {
            lineRenderer.positionCount = 0;
        }
    }

    /// <summary>
    /// Callback chiamata da PathRequestManager dopo il calcolo del path.
    /// </summary>
    private void OnPathFound(Vector3[] path, bool success)
    {
        if (success)
        {
            // Creiamo un array di punti del percorso che includa anche lo start e l'end
            Vector3[] fullPath = new Vector3[path.Length + 2];
            fullPath[0] = start;
            for (int i = 0; i < path.Length; i++)
            {
                fullPath[i + 1] = path[i];
            }
            fullPath[fullPath.Length - 1] = end;

            // Correggi il percorso per eliminare piccole deviazioni finali
            fullPath = CorrectFinalPath(fullPath);

            // Disegna inizialmente il path completo
            if (showPath)
            {
                DrawPath(fullPath);
            }

            // Aggiorna la lista di nodi occupati
            currentPathNodes.Clear();
            for (int i = 1; i < fullPath.Length - 1; i++)
            {
                Node node = grid.NodeFromWorldPoint(fullPath[i]);
                currentPathNodes.Add(node);
            }
            grid.OccupyNodes(currentPathNodes);

            // Avvia la Coroutine che muove il robot lungo il percorso
            StartCoroutine(MoveAlongPath(fullPath));
        }
        else
        {
            Debug.LogError($"Impossibile trovare il percorso. Riprovo tra 2 secondi... (Start: {start}, End: {end})");
            StartCoroutine(RetryPath(2f));
        }

        // Reimposta i pesi eventualmente modificati
        ResetModifiedNodeWeights();
    }

    /// <summary>
    /// Se il path non � stato trovato, aspetta un attimo e riprova.
    /// </summary>
    private IEnumerator RetryPath(float waitTime)
    {
        yield return new WaitForSeconds(waitTime);
        // Reinviamo la richiesta di path dalla posizione attuale (start) all'end
        // Nota: aggiorniamo "start" in caso il robot si sia mosso un minimo (o se stava gi� l�)
        this.start = robotToMove.transform.position;

        PathRequestManager.RequestPath(start, end, OnPathFound);
    }

    /// <summary>
    /// Rimuove eventuale "nodo di mezzo" se l'angolo formato con l'ultimo tratto � troppo stretto.
    /// </summary>
    private Vector3[] CorrectFinalPath(Vector3[] path)
    {
        if (path.Length < 3)
            return path;

        // Calcola i vettori degli ultimi due segmenti
        Vector3 lastSegment = (path[path.Length - 1] - path[path.Length - 2]).normalized;
        Vector3 secondLastSegment = (path[path.Length - 2] - path[path.Length - 3]).normalized;

        // Se i segmenti formano un angolo troppo "acuto", rimuovi il nodo intermedio
        if (Vector3.Dot(lastSegment, secondLastSegment) < 0.95f) // Soglia regolabile
        {
            List<Vector3> correctedPath = new List<Vector3>(path);
            correctedPath.RemoveAt(correctedPath.Count - 2); // Rimuove il penultimo nodo
            return correctedPath.ToArray();
        }

        return path;
    }

    /// <summary>
    /// Disegna il percorso nel LineRenderer.
    /// </summary>
    private void DrawPath(Vector3[] path)
    {
        lineRenderer.positionCount = path.Length;
        for (int i = 0; i < path.Length; i++)
        {
            lineRenderer.SetPosition(i, path[i]);
        }
    }

    /// <summary>
    /// Coroutine che gestisce il movimento lungo un vettore di posizioni (fullPath).
    /// </summary>
    private IEnumerator MoveAlongPath(Vector3[] path)
    {
        // Ruota gradualmente il robot verso la prima direzione (se esiste un prossimo punto)
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

            // Calcoliamo la rotazione target
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

                    // Verifica se il robot sta andando in retromarcia
                    float dotProduct = Vector3.Dot(robotToMove.transform.forward, direction);
                    bool isReversing = dotProduct < 0f;

                    // Se c'� un blocco su tutti i lati -> "Pausa"
                    if (currentObstacleDirection == "Pausa")
                    {
                        Debug.Log("Ostacoli su tutti i lati - Pausa del robot.");
                        yield return new WaitForSeconds(3f);
                        start = robotToMove.transform.position;
                        PathRequestManager.RequestPath(start, end, OnPathFound);
                        yield break;
                    }
                    else
                    {
                        // Se stiamo andando in retromarcia, controlliamo SOLO se l'ostacolo � dietro
                        if (isReversing && currentObstacleDirection == "Dietro")
                        {
                            Debug.Log("Ostacolo dietro in retromarcia. Ricalcolo percorso o fermo il robot.");
                            ModifyNextNodesWeight("Dietro");
                            yield break;
                        }
                        // Se non � "Dietro", ignoriamo la presenza di ostacoli dietro
                        // e continuiamo eventualmente a gestire gli altri ostacoli
                        else if (currentObstacleDirection != "Nessun ostacolo" &&
                                 currentObstacleDirection != "Sensori disabilitati" &&
                                 currentObstacleDirection != "Dietro")
                        {
                            Debug.Log($"Ostacolo: {currentObstacleDirection}. Ricalcolo percorso.");
                            ModifyNextNodesWeight(currentObstacleDirection);
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

        // Svuoto il line renderer alla fine, se lo desideri
        if (showPath)
        {
            lineRenderer.positionCount = 0;
        }
    }

    /// <summary>
    /// Aggiornamento dinamico del LineRenderer durante il movimento, per far �scomparire� il tragitto gi� percorso.
    /// </summary>
    private void UpdateLineDuringMovement(Vector3[] path, int currentIndex, float journey, Vector3 startPosition, Vector3 targetPosition)
    {
        Vector3 currentRobotPos = Vector3.Lerp(startPosition, targetPosition, journey);

        int remainingPoints = path.Length - currentIndex;
        int positionCount = Mathf.Max(0, remainingPoints + 1);

        lineRenderer.positionCount = positionCount;
        if (positionCount == 0) return;

        // Il primo punto � la posizione corrente del robot
        lineRenderer.SetPosition(0, currentRobotPos);

        // I punti successivi sono i "futuri" del path
        for (int i = 1; i < positionCount; i++)
        {
            lineRenderer.SetPosition(i, path[currentIndex + i - 1]);
        }
    }

    /// <summary>
    /// Penalizza i nodi prossimi al robot (o a un ostacolo) per forzare un ricalcolo del percorso.
    /// </summary>
    private void ModifyNextNodesWeight(string obstacleDirection)
    {
        Vector3 currentPos = robotToMove.transform.position;
        Node currentNode = grid.NodeFromWorldPoint(currentPos);
        int currentIndex = currentPathNodes.IndexOf(currentNode);

        if (currentIndex == -1 || currentIndex >= currentPathNodes.Count - 1) return;

        // Resetta i pesi precedenti
        ResetModifiedNodeWeights();

        // Penalizza primo nodo frontale (o corrispondente all'ostacoloDirection)
        Node firstFrontNode = currentPathNodes[currentIndex + 1];
        ModifyNode(firstFrontNode);

        Vector3 directionVector = Vector3.zero;

        if (obstacleDirection == "Sinistra")
            directionVector = -robotToMove.transform.right;
        else if (obstacleDirection == "Destra")
            directionVector = robotToMove.transform.right;
        else if (obstacleDirection == "Dietro")
            directionVector = -robotToMove.transform.forward;

        float nodeSize = grid.nodeRadius * 2; // Diametro del nodo

        // 1. Penalizza nodo adiacente alla posizione corrente
        Vector3 currentPenaltyPos = currentPos + directionVector * nodeSize;
        Node currentPenaltyNode = grid.NodeFromWorldPoint(currentPenaltyPos);
        ModifyNode(currentPenaltyNode);

        // 2. Penalizza nodo adiacente al prossimo nodo nel percorso
        Node frontNode = currentPathNodes[currentIndex + 1];
        if (frontNode != null)
        {
            Vector3 frontPenaltyPos = frontNode.worldPosition + directionVector * nodeSize;
            Node frontPenaltyNode = grid.NodeFromWorldPoint(frontPenaltyPos);
            ModifyNode(frontPenaltyNode);
        }

        // Richiedi nuovo percorso
        start = currentPos;
        PathRequestManager.RequestPath(start, end, OnPathFound);
    }

    /// <summary>
    /// Aumenta la penalit� di un nodo walkable, se non � gi� stato modificato.
    /// </summary>
    private void ModifyNode(Node node)
    {
        if (node != null && node.walkable && !nodesWithModifiedWeight.Contains(node))
        {
            node.movementPenalty += (int)obstacleWeight;
            nodesWithModifiedWeight.Add(node);
        }
    }

    /// <summary>
    /// Reset di tutti i nodi precedentemente modificati.
    /// </summary>
    private void ResetModifiedNodeWeights()
    {
        foreach (Node node in nodesWithModifiedWeight)
        {
            node.movementPenalty -= (int)obstacleWeight;
        }
        nodesWithModifiedWeight.Clear();
    }

    /// <summary>
    /// Disabilita i sensori (sulla RaycastManager).
    /// </summary>
    private void DisableSensors()
    {
        raycastManager.sensorsEnabled = false;
    }

    /// <summary>
    /// Abilita i sensori (sulla RaycastManager).
    /// </summary>
    private void EnableSensors()
    {
        raycastManager.sensorsEnabled = true;
    }

    /// <summary>
    /// Restituisce la posizione del robot con un piccolo rumore simulato.
    /// </summary>
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
