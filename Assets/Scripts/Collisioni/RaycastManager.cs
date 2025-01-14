using UnityEngine;

public class RaycastManager : MonoBehaviour
{
    public float raycastLength = 3.7f; // Distanza massima per il raycast
    public float threshold = 3.7f; // La soglia di distanza
    public float rayHeight = 1.5f; // Altezza del raycast rispetto al robot
    public int numberOfRays =361; // Numero di raggi che simulano l'arco
    public LayerMask layerMask; // Layer da ignorare (quello del robot)

    private RaycastHit hitInfo;
    public bool sensorsEnabled = true;

    void Update()
    {
        // Calcolare la direzione dell'ostacolo solo se i sensori sono abilitati
        if (sensorsEnabled)
        {
            GetObstacleDirection();
        }
    }

    // Funzione per ottenere la direzione dell'ostacolo
    public string GetObstacleDirection()
    {
        if (!sensorsEnabled)
        {
            return "Sensori disabilitati";
        }

        int leftCount = 0, rightCount = 0;

        // Procediamo con i raggi su un arco di 90 gradi
        Vector3 origin = transform.position + Vector3.up * rayHeight; // Punto di partenza del raggio

        bool obstacleDetected = false;  // Flag per verificare se un ostacolo è stato rilevato

        // Eseguiamo un unico "raycast" simulato con un cono (SphereCast)
        for (int i = 0; i < numberOfRays; i++)
        {
            float angle = Mathf.Lerp(-45f, 45f, i / (float)(numberOfRays - 1)); // Angolo distribuito tra -45° e +45°
            Vector3 direction = Quaternion.Euler(0, angle, 0) * transform.forward;

            // Effettuiamo il SphereCast per simulare il raycast a 90 gradi
            if (Physics.SphereCast(origin, 0.2f, direction, out hitInfo, raycastLength))
            {
                if (hitInfo.distance < threshold)
                {
                    obstacleDetected = true; // Abbiamo trovato un ostacolo

                    if (angle < 0)
                        leftCount++; // Ostacolo a sinistra
                    else
                        rightCount++; // Ostacolo a destra
                }
            }
        }

        // Se non è stato rilevato alcun ostacolo, restituiamo "Nessun ostacolo"
        if (!obstacleDetected)
        {
            return "Nessun ostacolo";
        }

        // Prendere la decisione se c'è un ostacolo
        if (leftCount > rightCount)
            return "Sinistra";
        else if (rightCount > leftCount)
            return "Destra";
        else
            return Random.Range(0, 2) == 0 ? "Sinistra" : "Destra";
    }

    // Disegnare il raycast con il Gizmos
    private void OnDrawGizmos()
    {
        if (!sensorsEnabled)
        {
            return; // Non disegnare nulla se i sensori sono disabilitati
        }

        Vector3 origin = transform.position + Vector3.up * rayHeight; // Punto di partenza del raggio
        float halfAngle = 45f;

        // Disegnare un cono di raggi (SphereCast) su un arco di 90 gradi
        for (int i = 0; i < numberOfRays; i++)
        {
            float angle = Mathf.Lerp(-halfAngle, halfAngle, i / (float)(numberOfRays - 1)); // Angolo da -45° a +45°
            Vector3 direction = Quaternion.Euler(0, angle, 0) * transform.forward;

            // Eseguiamo il SphereCast per determinare la distanza e visualizzare il colore corretto
            if (Physics.SphereCast(origin, 0.2f, direction, out hitInfo, raycastLength))
            {
                // Verifica se il raggio è sotto la soglia
                if (hitInfo.distance < threshold)
                {
                    Gizmos.color = Color.red; // Se sotto soglia, visualizza il raggio in rosso
                }
                else
                {
                    Gizmos.color = Color.green; // Se sopra la soglia, visualizza il raggio in verde
                }
            }
            else
            {
                Gizmos.color = Color.green; // Nessun ostacolo trovato, colore verde
            }

            // Disegnare il raggio
            Gizmos.DrawRay(origin, direction * raycastLength);
        }
    }
}
