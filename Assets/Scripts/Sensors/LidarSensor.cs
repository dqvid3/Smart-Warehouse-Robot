using UnityEngine;
using System.Collections.Generic;

public class LidarSensor : MonoBehaviour
{
    [Header("Lidar Parameters")]
    public int numberOfRays = 360; // Numero di raggi per scansione
    public float maxDistance = 10f; // Distanza massima di rilevamento
    public float fieldOfView = 360f; // Campo visivo in gradi (360 per una scansione completa)
    public float scanRate = 10f; // Scansioni al secondo
    public LayerMask obstacleLayer; // Layer su cui rilevare gli ostacoli

    [Header("Visualization")]
    public bool drawRays = true; // Disegna i raggi nella scena
    public Color rayColor = Color.red; // Colore dei raggi (quando non colpiscono nulla)
    public Color hitColor = Color.green; // Colore dei raggi (quando colpiscono un oggetto)
    public float rayDuration = 0.1f; // Durata dei raggi visualizzati

    private float angleIncrement;
    private float currentScanAngle = 0f;
    private float timeSinceLastScan = 0f;

    // Struttura per memorizzare i dati di un punto rilevato
    public struct LidarHit
    {
        public Vector3 point;
        public float distance;

        public LidarHit(Vector3 p, float d)
        {
            point = p;
            distance = d;
        }
    }

    // Lista per memorizzare i punti rilevati in una scansione
    private List<LidarHit> scanData = new List<LidarHit>();

    void Start()
    {
        angleIncrement = fieldOfView / numberOfRays;
    }

    void Update()
    {
        timeSinceLastScan += Time.deltaTime;

        if (timeSinceLastScan >= 1f / scanRate)
        {
            PerformScan();
            timeSinceLastScan = 0f;
        }
    }

    void PerformScan()
    {
        scanData.Clear(); // Pulisce i dati della scansione precedente

        for (int i = 0; i < numberOfRays; i++)
        {
            // Calcola la direzione del raggio corrente
            float angle = currentScanAngle + i * angleIncrement;
            Vector3 direction = Quaternion.Euler(0, angle, 0) * transform.forward;

            RaycastHit hit;
            if (Physics.Raycast(transform.position, direction, out hit, maxDistance, obstacleLayer))
            {
                // Ostacolo rilevato
                scanData.Add(new LidarHit(hit.point, hit.distance));

                if (drawRays)
                {
                    // Disegna il raggio in verde se colpisce un oggetto
                    Debug.DrawLine(transform.position, hit.point, hitColor, rayDuration);
                }
            }
            else
            {
                // Nessun ostacolo rilevato
                if (drawRays)
                {
                    // Disegna il raggio in rosso se non colpisce nulla
                    Debug.DrawRay(transform.position, direction * maxDistance, rayColor, rayDuration);
                }
            }
        }

        currentScanAngle += angleIncrement * numberOfRays * Time.deltaTime * scanRate; // Aggiorna l'angolo per la prossima scansione (rotazione continua)
        if (currentScanAngle >= 360f)
        {
            currentScanAngle -= 360f;
        }

        // Qui puoi utilizzare i dati della scansione (scanData) per la navigazione del robot,
        // l'evitamento degli ostacoli, la mappatura dell'ambiente, ecc.
        ProcessScanData(scanData);
    }

    // Esempio di funzione per processare i dati della scansione
    void ProcessScanData(List<LidarHit> data)
    {
        // In questo esempio, stampa semplicemente il numero di punti rilevati
        Debug.Log("Lidar Scan: " + data.Count + " points detected.");

        // Qui dovresti implementare la logica per utilizzare i dati del Lidar
        // Ad esempio, potresti:
        // - Trovare l'ostacolo più vicino
        // - Calcolare la distanza media dagli ostacoli in una certa direzione
        // - Creare una mappa dell'ambiente
        // - ecc.
    }

    // Funzione per ottenere i dati dell'ultima scansione (utile per altri script)
    public List<LidarHit> GetScanData()
    {
        return scanData;
    }
}