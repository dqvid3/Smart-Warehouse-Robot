using UnityEngine;

public class Controller : MonoBehaviour
{
    public WheelCollider[] wheels = new WheelCollider[4];
    public float motorPower = 1000f;
    public float steerAngle = 30f;
    
    void FixedUpdate()
    {
        float motor = Input.GetAxis("Vertical") * motorPower;
        float steering = Input.GetAxis("Horizontal") * steerAngle;
        
        // Potenza motore (ruote anteriori)
        wheels[0].motorTorque = motor;
        wheels[1].motorTorque = motor;
        
        // Sterzo (ruote posteriori)
        wheels[2].steerAngle = steering;
        wheels[3].steerAngle = steering;
    }
}