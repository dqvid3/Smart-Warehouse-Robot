using UnityEngine;
using System.Collections;
using UnityEngine.Apple;

[RequireComponent(typeof(LineRenderer))]
public class MovementWithAStar : MonoBehaviour
{
    [Header("Controller")]
    public ForkliftNavController forkliftNavController;
    public bool showPath = false;

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
}