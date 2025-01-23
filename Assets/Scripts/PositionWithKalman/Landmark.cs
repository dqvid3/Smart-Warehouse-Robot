using UnityEngine;

public class Landmark : MonoBehaviour
{
    public int id=0; // ID univoco del Landmark

    public void OnHitByRay(RobotKalmanPosition robot)
    {
        // Invia l'ID del landmark al robot
        robot.ReceiveLandmarkID(id);
    }
}
