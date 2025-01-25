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

        // Richiede il calcolo del path
        PathRequestManager.RequestPath(start, end, OnPathFound);

        // Attende finché il robot non si avvicina alla destinazione
        while (Vector3.Distance(robotToMove.transform.position, end) > 0.1f)
        {
            if (Vector3.Distance(robotToMove.transform.position, end) <= arrivalTolerance && raycastManager.sensorsEnabled)
            {
                DisableSensors();
            }
            yield return null;
        }

        // Una volta arrivato, se vuoi che la linea scompaia del tutto, puoi azzerare il LineRenderer:
        if (showPath)
        {
            lineRenderer.positionCount = 0;
        }
    }
    private void OnPathFound(Vector3[] path, bool success)
    {
        if (success)
        {
            // Creiamo un array di punti del percorso che includa anche lo start e l'end come primi/ultimi
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

            // Avvia il movimento lungo il percorso
            StartCoroutine(MoveAlongPath(fullPath));
        }
        else
        {
            Debug.LogError("Impossibile trovare il percorso.");
        }

        // Reimposta i pesi eventualmente modificati
        ResetModifiedNodeWeights();
    }


    private Vector3[] CorrectFinalPath(Vector3[] path)
    {
        if (path.Length < 3)
            return path;

        // Calcola i vettori degli ultimi due segmenti
        Vector3 lastSegment = (path[path.Length - 1] - path[path.Length - 2]).normalized;
        Vector3 secondLastSegment = (path[path.Length - 2] - path[path.Length - 3]).normalized;

        // Se i segmenti formano un angolo acuto, rimuovi il nodo intermedio
        if (Vector3.Dot(lastSegment, secondLastSegment) < 0.95f) // Regola la soglia secondo necessità
        {
            List<Vector3> correctedPath = new List<Vector3>(path);
            correctedPath.RemoveAt(correctedPath.Count - 2); // Rimuovi il penultimo nodo
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
        // Prima rotazione verso il primo segmento
        Vector3 initialDirection = (path[1] - path[0]).normalized;
        Quaternion initialRotation = Quaternion.LookRotation(initialDirection);

        // Ruota gradualmente il robot verso la prima direzione
        while (Quaternion.Angle(robotToMove.transform.rotation, initialRotation) > 0.1f)
        {
            robotToMove.transform.rotation = Quaternion.Slerp(robotToMove.transform.rotation, initialRotation, Time.deltaTime * moveSpeed);
            yield return null;
        }

        // Percorri ogni tratto del path
        for (int i = 1; i < path.Length; i++)
        {
            Vector3 startPosition = robotToMove.transform.position;
            Vector3 targetPosition = path[i];
            Vector3 direction = (targetPosition - startPosition).normalized;
            Quaternion targetRotation = direction != Vector3.zero ? Quaternion.LookRotation(direction) : Quaternion.identity;

            float journey = 0f;
            float distance = Vector3.Distance(startPosition, targetPosition);

            while (journey < 1f)
            {
                if (raycastManager.sensorsEnabled)
                {
                    string currentObstacleDirection = raycastManager.GetObstacleDirection();

                    if (currentObstacleDirection == "Pausa")
                    {
                        Debug.Log("Ostacoli su tutti i lati - Pausa del robot");
                        yield return new WaitForSeconds(3f);
                        start = robotToMove.transform.position;
                        PathRequestManager.RequestPath(start, end, OnPathFound);
                        yield break;
                    }
                    else if (currentObstacleDirection != "Nessun ostacolo" &&
                             currentObstacleDirection != "Sensori disabilitati")
                    {
                        Debug.Log($"Ostacolo: {currentObstacleDirection}. Ricalcolo percorso.");
                        ModifyNextNodesWeight(currentObstacleDirection);
                        yield break;
                    }
                }

                // Avanza gradualmente il robot
                journey += Time.deltaTime * moveSpeed / distance;
                robotToMove.transform.position = Vector3.Lerp(startPosition, targetPosition, journey);
                robotToMove.transform.rotation = Quaternion.Slerp(robotToMove.transform.rotation, targetRotation, Time.deltaTime * moveSpeed);

                // Rilascia nodi precedenti
                if (i > 1)
                {
                    Node previousNode = grid.NodeFromWorldPoint(path[i - 2]);
                    if (Vector3.Distance(robotToMove.transform.position, path[i - 1]) < grid.nodeRadius * 1.5f)
                    {
                        grid.ReleaseNodes(new List<Node> { previousNode });
                        currentPathNodes.Remove(previousNode);
                    }
                }

                // --- AGGIORNAMENTO LINEA DURANTE IL MOVIMENTO ---
                // Se vogliamo che la linea “scompaia” dietro il robot,
                // la aggiorniamo qui, lasciando nel LineRenderer solo 
                // la parte di percorso ancora da seguire.
                if (showPath)
                {
                    UpdateLineDuringMovement(path, i, journey, startPosition, targetPosition);
                }
                // ----------------------------------------------

                yield return null;
            }

            // Una volta completato il segmento, rilasciamo il nodo che abbiamo appena lasciato
            Node currentNode = grid.NodeFromWorldPoint(path[i - 1]);
            grid.ReleaseNodes(new List<Node> { currentNode });
            currentPathNodes.Remove(currentNode);

            // Allinea posizione e rotazione finale al punto target
            robotToMove.transform.position = targetPosition;
            robotToMove.transform.rotation = targetRotation;
        }

        // Rilascia anche l’ultimo nodo se esiste
        if (path.Length > 1)
        {
            Node lastNode = grid.NodeFromWorldPoint(path[path.Length - 2]);
            grid.ReleaseNodes(new List<Node> { lastNode });
            currentPathNodes.Remove(lastNode);
        }

        // Se vogliamo rimuovere la linea del tutto alla fine (opzionale),
        // mettiamo a zero i count del LineRenderer:
        if (showPath)
        {
            lineRenderer.positionCount = 0;
        }
    }

    // --- METODO PER AGGIORNARE IL LINE RENDERER DURANTE IL MOVIMENTO ---
    private void UpdateLineDuringMovement(Vector3[] path, int currentIndex, float journey, Vector3 startPosition, Vector3 targetPosition)
    {
        // Calcoliamo la posizione attuale del robot (dove è arrivato in questa frazione di percorso)
        Vector3 currentRobotPos = Vector3.Lerp(startPosition, targetPosition, journey);

        // Esempio di strategia:
        // Vogliamo che il primo punto del line renderer sia la posizione corrente del robot
        // e che il resto rappresenti i punti futuri (ancora non percorsi) del path.

        // 1) Calcoliamo quanti punti rimangono nel path (dal currentIndex incluso in poi)
        int remainingPoints = path.Length - currentIndex;
        // 2) Se rimangono N segmenti, avremo N+1 “vertici” perché includiamo anche la posizione attuale.
        //    Ma per semplicità, mettiamo la count = remainingPoints + 1, dove +1 è la posizione corrente del robot
        //    (però se currentIndex == path.Length, evitiamo di andare out of range).
        int positionCount = Mathf.Max(0, remainingPoints + 1);

        lineRenderer.positionCount = positionCount;
        if (positionCount == 0) return; // Nessun punto da disegnare

        // Il primo punto del LineRenderer è la posizione attuale del robot
        lineRenderer.SetPosition(0, currentRobotPos);

        // I successivi punti sono i punti ancora da percorrere
        for (int i = 1; i < positionCount; i++)
        {
            lineRenderer.SetPosition(i, path[currentIndex + i - 1]);
        }
    }
    // -------------------------------------------------------------------

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

        Vector3 checkDirection = obstacleDirection == "Sinistra"
                                ? -robotToMove.transform.right
                                : robotToMove.transform.right;

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
