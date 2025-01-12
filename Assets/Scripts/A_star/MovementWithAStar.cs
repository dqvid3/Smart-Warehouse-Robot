using UnityEngine;
using System.Collections;
using UnityEngine.Apple;

[RequireComponent(typeof(LineRenderer))]
public class MovementWithAStar : MonoBehaviour
{
    [Header("Controller")]
    public ForkliftNavController forkliftNavController;

    private Vector3 start;
    private Vector3 end;

    private LineRenderer lineRenderer;
    public GameObject robotToMove; 
    public float moveSpeed = 3.5f;

    private void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();

        // Inizializza le proprietà del LineRenderer
        lineRenderer.startWidth = 0.2f;
        lineRenderer.endWidth = 0.2f;
        lineRenderer.positionCount = 0;

        // La posizione iniziale è quella corrente del GameObject
        start = forkliftNavController.defaultPosition;
    }

    public IEnumerator MovementToPosition(Vector3 destination)
    {
        // Aggiorno il campo di classe con il valore ricevuto dal metodo
        this.end = destination;

        // Se la posizione di partenza è sempre la posizione attuale del robot...
        this.start = robotToMove.transform.position;

        // Calcola il percorso
        PathRequestManager.RequestPath(start, end, OnPathFound);

        // Aspetta finché il robot non raggiunge la posizione finale
        while (Vector3.Distance(robotToMove.transform.position, end) > 0.1f)
        {
            yield return null;
        }
    }



    private void OnPathFound(Vector3[] path, bool success)
    {
        if (success)
        {
            Debug.Log("Percorso trovato!");

            Vector3[] fullPath = new Vector3[path.Length + 2];
            fullPath[0] = start; // Primo punto è la posizione attuale
            for (int i = 0; i < path.Length; i++)
            {
                fullPath[i + 1] = path[i]; // Copia i punti intermedi
            }
            fullPath[fullPath.Length - 1] = end; // Ultimo punto è la destinazione finale

            // Disegna il percorso
            DrawPath(fullPath);

            // Inizia a muovere l'oggetto
            StartCoroutine(MoveAlongPath(fullPath, true)); // Segnala che è il primo percorso
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

        // Assegna i punti al LineRenderer
        for (int i = 0; i < path.Length; i++)
        {
            lineRenderer.SetPosition(i, path[i]);
        }
    }

    private IEnumerator MoveAlongPath(Vector3[] path, bool isFirstPath)
    {
        //robotToMove.transform.position = path[0]; // Posiziona l'oggetto al punto iniziale

        for (int i = 1; i < path.Length; i++)
        {
            Vector3 startPosition = robotToMove.transform.position;
            Vector3 targetPosition = path[i];

            // Calcola la direzione verso il prossimo punto
            Vector3 direction = (targetPosition - startPosition).normalized;

            // Determina la rotazione target
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

                yield return null; // Aspetta il prossimo frame
            }

            robotToMove.transform.position = targetPosition;
            robotToMove.transform.rotation = targetRotation;
        }
        start = end; //Aggiornamento posizione iniziale
        Debug.Log("Movimento completato!");
    }
}