using UnityEngine;
using static Cinemachine.CinemachineFreeLook;

[RequireComponent(typeof(RobotExplainability))] // Assicuriamoci di avere il componente RobotExplainability
public class RaycastManager : MonoBehaviour
{
    public float raycastLength = 3.7f; // Maximum raycast distance
    public float threshold = 3.7f; // Distance threshold
    public float rayHeight = 1.5f; // Raycast height relative to the robot
    public int numberOfRays = 90; // Number of rays simulating the arc
    public int additionalRays = 20; // Additional rays for slots
    public float extendedWeight = 0.1f;
    public Robot robot;
    
    [Range(0f, 90f)]
    public float frontRayAngle = 45f; // Angle covered by the front rays (green)

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
        if (!sensorsEnabled)
        {
            return "Sensori disabilitati";
        }

        int leftCount = 0, rightCount = 0;
        int leftSlotCount = 0, rightSlotCount = 0;

        Vector3 origin = transform.position + Vector3.up * rayHeight; // Ray origin point

        bool frontObstacleDetected = false; // Flag to check if a front obstacle was detected

        // Check front rays
        for (int i = 0; i < numberOfRays; i++)
        {
            float angle = Mathf.Lerp(-frontRayAngle, frontRayAngle, i / (float)(numberOfRays - 1)); // Angle distributed between -frontRayAngle and +frontRayAngle
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
            for (int i = 0; i < additionalRays; i++)
            {
                float angle = Mathf.Lerp(-90f, -frontRayAngle, i / (float)(additionalRays - 1)); // Further left slot angles
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
                float angle = Mathf.Lerp(frontRayAngle, 90f, i / (float)(additionalRays - 1)); // Further right slot angles
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

        int totalRays = numberOfRays + 2 * additionalRays; // Total number of rays
        int totalObstacles = leftCount + rightCount + leftSlotCount + rightSlotCount; // Total obstacles detected

        // Check if 60% or more of the rays detect obstacles
        if ((float)totalObstacles / totalRays >= 0.6f)
        {
            robot.isPaused = true;
            Invoke("ResumeRobot", 3f); // Call ResumeRobot after 3 seconds
            return "Pausa: Troppi ostacoli rilevati";
        }

        if (!frontObstacleDetected && leftSlotCount == 0 && rightSlotCount == 0)
        {
            return "Nessun ostacolo";
        }
        float totalLeft = leftCount + leftSlotCount * extendedWeight;
        float totalRight = rightCount + rightSlotCount * extendedWeight;

        if (totalLeft > totalRight)
            return "Sinistra";
        else if (totalRight > totalLeft)
            return "Destra";
        else
            return Random.Range(0, 2) == 0 ? "Sinistra" : "Destra";
    }

    private void ResumeRobot()
    {
        robot.isPaused = false;
    }

    private void OnDrawGizmos()
    {
        if (!sensorsEnabled)
        {
            return; // Do not draw anything if sensors are disabled
        }

        Vector3 origin = transform.position + Vector3.up * rayHeight; // Ray origin point

        for (int i = 0; i < numberOfRays; i++)
        {
            float angle = Mathf.Lerp(-frontRayAngle, frontRayAngle, i / (float)(numberOfRays - 1)); // Angle from -frontRayAngle to +frontRayAngle
            Vector3 direction = Quaternion.Euler(0, angle, 0) * transform.forward;

            if (Physics.SphereCast(origin, 0.2f, direction, out hitInfo, raycastLength))
            {
                if (hitInfo.distance < threshold)
                {
                    Gizmos.color = Color.red; // Below threshold, show ray in red
                }
                else
                {
                    Gizmos.color = Color.green; // Above threshold, show ray in green
                }
            }
            else
            {
                Gizmos.color = Color.green; // No obstacle detected, color green
            }

            Gizmos.DrawRay(origin, direction * raycastLength);
        }

        DrawSlotGizmos(origin, -90f, -frontRayAngle, additionalRays); // Further left slot rays
        DrawSlotGizmos(origin, 90f, frontRayAngle, additionalRays); // Further right slot rays
    }

    private void DrawSlotGizmos(Vector3 origin, float minAngle, float maxAngle, int slotCount)
    {
        for (int i = 0; i < slotCount; i++)
        {
            float angle = Mathf.Lerp(minAngle, maxAngle, i / (float)(slotCount - 1)); // Slot angles
            Vector3 direction = Quaternion.Euler(0, angle, 0) * transform.forward;

            if (Physics.SphereCast(origin, 0.2f, direction, out hitInfo, raycastLength))
            {
                if (hitInfo.distance < threshold)
                {
                    Gizmos.color = Color.yellow; // Below threshold, show ray in yellow
                }
                else
                {
                    Gizmos.color = Color.blue; // Above threshold, show ray in blue
                }
            }
            else
            {
                Gizmos.color = Color.blue; // No obstacle detected, color blue
            }

            Gizmos.DrawRay(origin, direction * raycastLength);
        }
    }
}