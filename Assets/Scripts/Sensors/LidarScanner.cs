using UnityEngine;

public class LidarScanner : MonoBehaviour
{
    public int numberOfRays = 36; // Numero di raggi per scansione
    public float angleOfView = 180f; // Angolo di visuale in gradi (campo visivo orizzontale)
    public float laserRange = 50f; // Portata massima del laser
    public LayerMask obstacleLayer; // Layer degli ostacoli da rilevare

    void Update()
    {
        Scan();
    }

    void Scan()
    {
        // Calcola l'angolo di incremento tra ogni raggio
        float angleIncrement = numberOfRays > 1 ? angleOfView / (numberOfRays - 1) : 0;

        // Ciclo per ogni raggio
        for (int i = 0; i < numberOfRays; i++)
        {
            // Calcola l'angolo del raggio corrente
            float angle = transform.eulerAngles.y - angleOfView / 2 + angleIncrement * i;

            // Converte l'angolo in radianti
            float angleRad = angle * Mathf.Deg2Rad;

            // Calcola la direzione del raggio
            Vector3 direction = new Vector3(Mathf.Sin(angleRad), 0, Mathf.Cos(angleRad));

            // Spara il Raycast
            RaycastHit hit;
            if (Physics.Raycast(transform.position, direction, out hit, laserRange, obstacleLayer))
            {
                // Ostacolo rilevato
                Debug.Log("Ostacolo rilevato a " + hit.distance + " metri, angolo: " + angle);
                Debug.DrawLine(transform.position, hit.point, Color.red); // Visualizza il raggio che ha colpito
            }
            else
            {
                // Nessun ostacolo rilevato per questo raggio
                Debug.DrawLine(transform.position, transform.position + direction * laserRange, Color.green); // Visualizza il raggio che non ha colpito
            }
        }
    }
}