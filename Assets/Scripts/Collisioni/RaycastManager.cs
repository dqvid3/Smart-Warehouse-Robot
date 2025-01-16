using UnityEngine;

[RequireComponent(typeof(RobotExplainability))] // Assicuriamoci di avere il componente RobotExplainability
public class RaycastManager : MonoBehaviour
{
    public float raycastLength = 3.7f; // Maximum raycast distance
    public float threshold = 3.7f;     // Distance threshold
    public float rayHeight = 1.5f;     // Raycast height relative to the robot
    public int numberOfRays = 90;      // Number of rays simulating the arc
    public int additionalRays = 20;    // Additional rays for slots
    public float extendedWeight = 0.1f;
    public LayerMask layerMask;        // Layer to ignore (robot's Layer)

    private RaycastHit hitInfo;
    public bool sensorsEnabled = true;

    // Aggiungiamo un riferimento al RobotExplainability
    private RobotExplainability explainability;

    void Awake()
    {
        // Cerchiamo il componente RobotExplainability sullo stesso GameObject
        explainability = GetComponent<RobotExplainability>();
    }

    void Update()
    {
        if (sensorsEnabled)
        {
            GetObstacleDirection();
        }
    }

    public string GetObstacleDirection()
    {
        if (!sensorsEnabled)
        {
            // Spiegazione aggiuntiva se i sensori sono disabilitati
            if (explainability != null)
            {
                explainability.ShowExplanation("Sensori disabilitati, non posso rilevare ostacoli.");
            }
            return "Sensori disabilitati";
        }

        int leftCount = 0, rightCount = 0;
        int leftSlotCount = 0, rightSlotCount = 0;

        Vector3 origin = transform.position + Vector3.up * rayHeight; // Ray origin point

        bool frontObstacleDetected = false; // Flag to check if a front obstacle was detected

        // Check front rays
        for (int i = 0; i < numberOfRays; i++)
        {
            float angle = Mathf.Lerp(-45f, 45f, i / (float)(numberOfRays - 1)); // Angle between -45° and +45°
            Vector3 direction = Quaternion.Euler(0, angle, 0) * transform.forward;

            if (Physics.SphereCast(origin, 0.2f, direction, out hitInfo, raycastLength))
            {
                if (hitInfo.distance < threshold)
                {
                    frontObstacleDetected = true; // Obstacle found on the front

                    if (angle < 0)
                        leftCount++; // Obstacle on the left
                    else
                        rightCount++; // Obstacle on the right
                }
            }
        }

        // Evaluate side slots only if a front obstacle is detected
        if (frontObstacleDetected)
        {
            // Se abbiamo rilevato un ostacolo frontale, forniamo una spiegazione
            if (explainability != null)
            {
                explainability.ShowExplanation("Rilevato ostacolo frontale. Calcolo possibili direzioni alternative.");
            }

            for (int i = 0; i < additionalRays; i++)
            {
                float angle = Mathf.Lerp(-90f, -45f, i / (float)(additionalRays - 1)); // Further left slot angles
                Vector3 direction = Quaternion.Euler(0, angle, 0) * transform.forward;

                if (Physics.SphereCast(origin, 0.2f, direction, out hitInfo, raycastLength))
                {
                    if (hitInfo.distance < threshold)
                    {
                        leftSlotCount++; // Obstacle in the left slot
                    }
                }
            }

            for (int i = 0; i < additionalRays; i++)
            {
                float angle = Mathf.Lerp(45f, 90f, i / (float)(additionalRays - 1)); // Further right slot angles
                Vector3 direction = Quaternion.Euler(0, angle, 0) * transform.forward;

                if (Physics.SphereCast(origin, 0.2f, direction, out hitInfo, raycastLength))
                {
                    if (hitInfo.distance < threshold)
                    {
                        rightSlotCount++; // Obstacle in the right slot
                    }
                }
            }
        }

        if (!frontObstacleDetected && leftSlotCount == 0 && rightSlotCount == 0)
        {
            // Nessun ostacolo complessivo
            return "Nessun ostacolo";
        }

        float totalLeft = leftCount + leftSlotCount * extendedWeight;
        float totalRight = rightCount + rightSlotCount * extendedWeight;

        if (totalLeft > totalRight)
        {
            if (explainability != null)
            {
                explainability.ShowExplanation("Vado a sinistra per evitare l'ostacolo a destra.");
            }
            return "Sinistra";
        }
        else if (totalRight > totalLeft)
        {
            if (explainability != null)
            {
                explainability.ShowExplanation("Vado a destra per evitare l'ostacolo a sinistra.");
            }
            return "Destra";
        }
        else
        {
            // Se c'è pareggio, scegli una direzione casuale
            string direction = Random.Range(0, 2) == 0 ? "Sinistra" : "Destra";
            if (explainability != null)
            {
                explainability.ShowExplanation($"Situazione equilibrata, scelgo casualmente la direzione: {direction}.");
            }
            return direction;
        }
    }

    private void OnDrawGizmos()
    {
        if (!sensorsEnabled)
        {
            return; // Do not draw anything if sensors are disabled
        }

        Vector3 origin = transform.position + Vector3.up * rayHeight; // Ray origin point
        float halfAngle = 45f;

        for (int i = 0; i < numberOfRays; i++)
        {
            float angle = Mathf.Lerp(-halfAngle, halfAngle, i / (float)(numberOfRays - 1));
            Vector3 direction = Quaternion.Euler(0, angle, 0) * transform.forward;

            if (Physics.SphereCast(origin, 0.2f, direction, out hitInfo, raycastLength))
            {
                if (hitInfo.distance < threshold)
                {
                    Gizmos.color = Color.red; // Below threshold
                }
                else
                {
                    Gizmos.color = Color.green; // Above threshold
                }
            }
            else
            {
                Gizmos.color = Color.green; // No obstacle
            }

            Gizmos.DrawRay(origin, direction * raycastLength);
        }

        DrawSlotGizmos(origin, -90f, -45f, additionalRays); // Further left slot rays
        DrawSlotGizmos(origin, 90f, 45f, additionalRays);  // Further right slot rays
    }

    private void DrawSlotGizmos(Vector3 origin, float minAngle, float maxAngle, int slotCount)
    {
        for (int i = 0; i < slotCount; i++)
        {
            float angle = Mathf.Lerp(minAngle, maxAngle, i / (float)(slotCount - 1));
            Vector3 direction = Quaternion.Euler(0, angle, 0) * transform.forward;

            if (Physics.SphereCast(origin, 0.2f, direction, out hitInfo, raycastLength))
            {
                if (hitInfo.distance < threshold)
                {
                    Gizmos.color = Color.yellow;
                }
                else
                {
                    Gizmos.color = Color.blue;
                }
            }
            else
            {
                Gizmos.color = Color.blue;
            }
            Gizmos.DrawRay(origin, direction * raycastLength);
        }
    }
}
