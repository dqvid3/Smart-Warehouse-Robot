using UnityEngine;

public class ForkliftMovement : MonoBehaviour
{
    // Riferimenti ai WheelCollider delle ruote
    public WheelCollider frontWheel;
    public WheelCollider backRightWheel;
    public WheelCollider backLeftWheel;

    // Riferimenti ai GameObject delle ruote per la rotazione visiva
    public Transform frontWheelTransform;
    public Transform backRightWheelTransform;
    public Transform backLeftWheelTransform;

    // Parametri di velocità e rotazione
    public float motorTorque = 200f;      // Potenza del motore
    public float steerAngle = 30f;        // Angolo massimo di sterzata
    public float brakeTorque = 500f;      // Potenza dei freni

    void Update()
    {
        HandleMovement();
        UpdateWheelVisuals();
    }

    void HandleMovement()
    {
        float verticalInput = Input.GetAxis("Vertical");
        float horizontalInput = Input.GetAxis("Horizontal");

        Debug.Log($"Vertical Input: {verticalInput}, Horizontal Input: {horizontalInput}");

        // Applicare la potenza del motore
        if (Mathf.Abs(verticalInput) > 0.1f)
        {
            frontWheel.motorTorque = verticalInput * motorTorque;
            backRightWheel.motorTorque = verticalInput * motorTorque;
            backLeftWheel.motorTorque = verticalInput * motorTorque;
        }
        else
        {
            frontWheel.motorTorque = 0;
            backRightWheel.motorTorque = 0;
            backLeftWheel.motorTorque = 0;
        }

        // Sterzata
        frontWheel.steerAngle = horizontalInput * steerAngle;
    }


    // Metodo per aggiornare visivamente le ruote in base ai WheelCollider
    void UpdateWheelVisuals()
    {
        UpdateSingleWheel(frontWheel, frontWheelTransform);
        UpdateSingleWheel(backRightWheel, backRightWheelTransform);
        UpdateSingleWheel(backLeftWheel, backLeftWheelTransform);
    }

    void UpdateSingleWheel(WheelCollider collider, Transform wheelTransform)
    {
        Vector3 pos;
        Quaternion rot;
        collider.GetWorldPose(out pos, out rot);
        wheelTransform.position = pos;
        wheelTransform.rotation = rot;
    }
}
