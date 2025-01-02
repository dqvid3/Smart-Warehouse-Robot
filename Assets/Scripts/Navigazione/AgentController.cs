using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AgentController : MonoBehaviour
{
    public Transform[] landmarks;   // Array di landmark che l'agente deve seguire
    public float speed = 5f;        // Velocità di movimento dell'agente
    public float raycastRange = 5f; // Distanza massima per il Raycast
    public float raycastAngle = 30f; // Angolo massimo per il Raycast

    private int currentLandmarkIndex = 0; // Indice del landmark attuale
    private Rigidbody rb; // Riferimento al rigidbody per il movimento

    void Start()
    {
        rb = GetComponent<Rigidbody>(); // Inizializza il Rigidbody
        if (landmarks.Length > 0)
        {
            // Posiziona l'agente sul primo landmark
            transform.position = landmarks[currentLandmarkIndex].position;
        }
    }

    void Update()
    {
        // Muovi l'agente verso il landmark attuale
        MoveTowardsLandmark();
        
        // Controlla se ci sono ostacoli davanti all'agente con il Raycast
        CheckForObstacles();

        // Controlla se l'agente ha raggiunto il landmark attuale
        if (Vector3.Distance(transform.position, landmarks[currentLandmarkIndex].position) < 1f)
        {
            // Cambia al prossimo landmark
            currentLandmarkIndex = (currentLandmarkIndex + 1) % landmarks.Length;
        }
    }

    void MoveTowardsLandmark()
    {
        if (landmarks.Length == 0) return; // Se non ci sono landmark, non fare nulla

        Vector3 direction = (landmarks[currentLandmarkIndex].position - transform.position).normalized;
        rb.MovePosition(transform.position + direction * speed * Time.deltaTime);
    }

    void CheckForObstacles()
    {
        RaycastHit hit;
        Vector3 forward = transform.forward;
        
        // Raycast dritto davanti all'agente
        if (Physics.Raycast(transform.position, forward, out hit, raycastRange))
        {
            Debug.DrawRay(transform.position, forward * hit.distance, Color.red);
            if (hit.collider != null)
            {
                // Se c'è un ostacolo, cambia la direzione (evita l'ostacolo)
                AvoidObstacle();
            }
        }
        else
        {
            // Se non ci sono ostacoli, puoi anche fare qualcosa
            Debug.DrawRay(transform.position, forward * raycastRange, Color.green);
        }

        // Raycast a sinistra
        Vector3 left = Quaternion.Euler(0, -raycastAngle, 0) * forward;
        if (Physics.Raycast(transform.position, left, out hit, raycastRange))
        {
            Debug.DrawRay(transform.position, left * hit.distance, Color.red);
            if (hit.collider != null)
            {
                // Evita l'ostacolo a sinistra
                AvoidObstacle();
            }
        }

        // Raycast a destra
        Vector3 right = Quaternion.Euler(0, raycastAngle, 0) * forward;
        if (Physics.Raycast(transform.position, right, out hit, raycastRange))
        {
            Debug.DrawRay(transform.position, right * hit.distance, Color.red);
            if (hit.collider != null)
            {
                // Evita l'ostacolo a destra
                AvoidObstacle();
            }
        }
    }

    void AvoidObstacle()
    {
        // Cambio direzione per evitare l'ostacolo
        currentLandmarkIndex = (currentLandmarkIndex + 1) % landmarks.Length; // Cambia il landmark in modo casuale
    }
}
