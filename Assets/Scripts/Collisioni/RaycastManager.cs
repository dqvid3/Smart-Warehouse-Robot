using UnityEngine;
using static Cinemachine.CinemachineFreeLook;

[RequireComponent(typeof(RobotExplainability))]
public class RaycastManager : MonoBehaviour
{
    public float raycastLength = 3.7f; // Distanza massima del raycast
    public float threshold = 3.7f; // Soglia di distanza
    public float rayHeight = 1.5f; // Altezza del raycast rispetto al robot
    public int numberOfRays = 90; // Numero di raggi che simulano l'arco
    public int additionalRays = 20; // Raggi aggiuntivi per gli slot

    [Range(0f, 90f)]
    public float frontRayAngle = 45f; // Angolo coperto dai raggi frontali (raggi verdi)

    private RaycastHit hitInfo;
    public bool sensorsEnabled = true;

    void Update()
    {
        if (sensorsEnabled)
        {
            GetObstacleDirection();
        }
    }

    public string GetObstacleDirection()
    {
        if (!sensorsEnabled) return "Sensori disabilitati";

        bool frontObstacle = false;
        bool leftObstacle = false;
        bool rightObstacle = false;
        Vector3 origin = transform.position + Vector3.up * rayHeight;

        // 1. Rilevamento ostacoli frontali
        for (int i = 0; i < numberOfRays; i++)
        {
            float angle = Mathf.Lerp(-frontRayAngle, frontRayAngle, i / (float)(numberOfRays - 1));
            Vector3 dir = Quaternion.Euler(0, angle, 0) * transform.forward;

            if (Physics.SphereCast(origin, 0.2f, dir, out hitInfo, raycastLength) &&
               hitInfo.distance < threshold)
            {
                frontObstacle = true;
                break;
            }
        }

        // 2. Rilevamento laterale solo se c'è ostacolo frontale
        if (frontObstacle)
        {
            // Rilevamento sinistro
            for (int i = 0; i < additionalRays; i++)
            {
                float angle = Mathf.Lerp(-90f, -frontRayAngle, i / (float)(additionalRays - 1));
                Vector3 dir = Quaternion.Euler(0, angle, 0) * transform.forward;

                if (Physics.SphereCast(origin, 0.2f, dir, out hitInfo, raycastLength) &&
                   hitInfo.distance < threshold)
                {
                    leftObstacle = true;
                    break;
                }
            }

            // Rilevamento destro
            for (int i = 0; i < additionalRays; i++)
            {
                float angle = Mathf.Lerp(frontRayAngle, 90f, i / (float)(additionalRays - 1));
                Vector3 dir = Quaternion.Euler(0, angle, 0) * transform.forward;

                if (Physics.SphereCast(origin, 0.2f, dir, out hitInfo, raycastLength) &&
                   hitInfo.distance < threshold)
                {
                    rightObstacle = true;
                    break;
                }
            }
        }

        // 3. Logica di pausa solo per ostacoli in ogni direzione
        if (frontObstacle && leftObstacle && rightObstacle)
        {
            return "Pausa";
        }

        // 4. Logica direzioni ostacoli
        if (!frontObstacle) return "Nessun ostacolo";
        if (leftObstacle) return "Sinistra";
        if (rightObstacle) return "Destra";

        return "Nessun ostacolo"; // Solo frontale senza ostacoli laterali
    }

    private void OnDrawGizmos()
    {
        if (!sensorsEnabled) return;

        Vector3 origin = transform.position + Vector3.up * rayHeight;

        for (int i = 0; i < numberOfRays + additionalRays * 2; i++)
        {
            float angle;
            Color rayColor = Color.green;
            string rayType = "Frontale";

            // Calcola angolo e tipo di raggio
            if (i < numberOfRays)
            {
                angle = Mathf.Lerp(-frontRayAngle, frontRayAngle, i / (float)(numberOfRays - 1));
                rayType = "Frontale";
            }
            else if (i < numberOfRays + additionalRays)
            {
                float t = (i - numberOfRays) / (float)(additionalRays - 1);
                angle = Mathf.Lerp(-90f, -frontRayAngle, t);
                rayType = "Sinistra";
            }
            else
            {
                float t = (i - numberOfRays - additionalRays) / (float)(additionalRays - 1);
                angle = Mathf.Lerp(frontRayAngle, 90f, t);
                rayType = "Destra";
            }

            Vector3 dir = Quaternion.Euler(0, angle, 0) * transform.forward;
            bool hit = Physics.SphereCast(origin, 0.2f, dir, out hitInfo, raycastLength);

            // Assegna colori in base al tipo di raggio e alla distanza
            if (hit && hitInfo.distance < threshold)
            {
                switch (rayType)
                {
                    case "Frontale":
                        rayColor = Color.red;
                        break;
                    case "Sinistra":
                        rayColor = Color.magenta;
                        break;
                    case "Destra":
                        rayColor = Color.magenta;
                        break;
                }
            }
            else
            {
                switch (rayType)
                {
                    case "Frontale":
                        rayColor = Color.green;
                        break;
                    case "Sinistra":
                        rayColor = Color.yellow;
                        break;
                    case "Destra":
                        rayColor = Color.yellow;
                        break;
                }
            }

            // Disegna il raggio
            Gizmos.color = rayColor;
            Gizmos.DrawRay(origin, dir * raycastLength);

            // Disegna un cubino all'ostacolo
            if (hit)
            {
                Gizmos.color = rayColor;
                Gizmos.DrawWireCube(hitInfo.point, Vector3.one * 0.3f);
            }
        }
    }
}