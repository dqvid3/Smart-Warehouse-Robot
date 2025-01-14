using UnityEngine;
using System.Collections;
using UnityEngine.Apple;
using System.Collections.Generic;

[RequireComponent(typeof(LineRenderer))]
public class MovementWithAStar : MonoBehaviour
{
    [Header("Controller")]
    public ForkliftNavController forkliftNavController;
    public bool showPath = false;

    public RaycastManager raycastmanager;

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
            fullPath[0] = start; // Primo punto è la posizione attuale
            for (int i = 0; i < path.Length; i++)
            {
                fullPath[i + 1] = path[i]; // Copia i punti intermedi
            }
            fullPath[fullPath.Length - 1] = end; // Ultimo punto è la destinazione finale

            if (showPath)
            {
                DrawPath(fullPath);
            }

            AdjustPathWithRaycast(ref fullPath, raycastmanager);
            // Inizia a muovere l'oggetto
            StartCoroutine(MoveAlongPath(fullPath));
        }
        else
        {
            Debug.LogError("Impossibile trovare il percorso.");
        }
    }

    private void DrawPath(Vector3[] path)
    {
        // Imposta il numero di punti per il LineRenderer
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

        // Rotazione iniziale
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
                // Aggiorna il tempo del movimento (0 start, 1 end)
                journey += Time.deltaTime * moveSpeed / Vector3.Distance(startPosition, targetPosition);

                robotToMove.transform.position = Vector3.Lerp(startPosition, targetPosition, journey);
                robotToMove.transform.rotation = Quaternion.Slerp(robotToMove.transform.rotation, targetRotation, Time.deltaTime * moveSpeed);
                start = robotToMove.transform.position;

                yield return null;
            }

            robotToMove.transform.position = targetPosition;
            robotToMove.transform.rotation = targetRotation;
        }
        start = end; //Aggiornamento posizione iniziale
    }

    private void ModifyPathWithDeviation(ref Vector3[] path, int startIndex, int endIndex, float deviationAmountRangeMin, float deviationAmountRangeMax, bool isLeft)
    {
        // Assicurati che il percorso abbia abbastanza nodi
        if (path.Length > endIndex)
        {
            // Per ogni segmento di percorso da startIndex a endIndex
            for (int i = startIndex; i < endIndex; i++)
            {
                // Prendi il nodo corrente e il nodo successivo
                Vector3 currentNode = path[i];
                Vector3 nextNode = path[i + 1];

                // Calcola la direzione tra il nodo corrente e il nodo successivo
                Vector3 direction = (nextNode - currentNode).normalized;

                // Trova il vettore perpendicolare a sinistra o destra rispetto alla direzione
                Vector3 deviationDirection = isLeft ? new Vector3(-direction.z, 0, direction.x) : new Vector3(direction.z, 0, -direction.x);

                // Applica una deviazione casuale nel range specificato
                float deviationAmount = Random.Range(deviationAmountRangeMin, deviationAmountRangeMax); // La deviazione può essere più o meno grande
                Vector3 deviation = deviationDirection * deviationAmount;

                // Crea un nuovo nodo deviato
                Vector3 newDeviatedNode = currentNode + deviation;

                // Inserisci il nuovo nodo nella lista modificata del percorso
                List<Vector3> modifiedPath = new List<Vector3>(path);
                modifiedPath.Insert(i + 1, newDeviatedNode); // Inserisci dopo il nodo corrente

                // Riassegna il percorso modificato
                path = modifiedPath.ToArray();
            }
        }
    }

    private void AdjustPathWithRaycast(ref Vector3[] path, RaycastManager raycastManager)
        {
            // 1. Esegui il raycast per ottenere la lista di raggi sopra e sotto soglia
            raycastManager.UpdateDirectionAndPath(ref path);  // Aggiorna la direzione principale (dirPrinc) e ostacoli

            // 2. Determina il range di deviazione in base alla situazione
            float deviationAmountRangeMin = -2f;
            float deviationAmountRangeMax = 2f;

            // Logica per determinare l'intensità della deviazione in base agli ostacoli
            if (raycastManager.raysBelowThreshold.Count > 0)
            {
                // Se ci sono ostacoli sotto soglia, possiamo aumentare la deviazione
                deviationAmountRangeMin = -3f;
                deviationAmountRangeMax = 3f;
            }

            // 3. In base alla situazione, scegli se deviare a sinistra o a destra
            bool isLeft = raycastManager.dirPrinc < raycastManager.dirOpp;

            // 4. Usa la funzione ModifyPathWithDeviation per applicare la deviazione al percorso
            ModifyPathWithDeviation(ref path, 0, path.Length - 1, deviationAmountRangeMin, deviationAmountRangeMax, isLeft);
        }



    public Vector3 GetOdometry()
    {
        return robotToMove.transform.position;
    }

}