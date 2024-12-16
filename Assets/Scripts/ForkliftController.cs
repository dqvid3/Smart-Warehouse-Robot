using UnityEngine;

[System.Serializable]
public class MastSettings
{
    public Transform liftTransform;
    public float liftSpeed = 2f;
    public float minLiftHeight = 0f;
    public float maxLiftHeight = 5f;
}

public class ForkliftController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float speed = 5f;
    public float rotationSpeed = 100f;

    [Header("Mast Settings")]
    public MastSettings[] masts; // Array di masti con parametri personalizzabili

    [Header("Wheel Settings")]
    public Transform[] wheels; // Array dei Transform delle ruote
    public float wheelRotationSpeed = 300f; // Velocità di rotazione delle ruote

    private int currentMastIndex = 0; // Indice del mast attualmente controllato

    void Update()
    {
        HandleMovement();
        HandleLift();
        SwitchMast();
    }

    void HandleMovement()
    {
        float move = Input.GetAxis("Vertical");   // W/S per muoversi avanti/indietro
        float turn = Input.GetAxis("Horizontal"); // A/D per ruotare

        if (Mathf.Abs(move) > 0.1f) // Se si sta muovendo avanti/indietro
        {
            transform.Translate(Vector3.forward * move * speed * Time.deltaTime);
            RotateWheels(move);
        }

        transform.Rotate(Vector3.up * turn * rotationSpeed * Time.deltaTime);
    }

    void HandleLift()
    {
        if (masts.Length == 0) return;

        MastSettings currentMast = masts[currentMastIndex];

        if (currentMast.liftTransform != null)
        {
            if (Input.GetKey(KeyCode.PageUp))
            {
                MoveLift(currentMast, currentMast.liftSpeed * Time.deltaTime);
            }
            if (Input.GetKey(KeyCode.PageDown))
            {
                MoveLift(currentMast, -currentMast.liftSpeed * Time.deltaTime);
            }
        }
    }

    void MoveLift(MastSettings mast, float amount)
    {
        Vector3 liftPosition = mast.liftTransform.localPosition;
        liftPosition.y = Mathf.Clamp(liftPosition.y + amount, mast.minLiftHeight, mast.maxLiftHeight);
        mast.liftTransform.localPosition = liftPosition;
    }

    void SwitchMast()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            currentMastIndex = (currentMastIndex + 1) % masts.Length;
            Debug.Log("Switched to Mast: " + currentMastIndex);
        }
    }

    void RotateWheels(float moveDirection)
    {
        foreach (Transform wheel in wheels)
        {
            wheel.Rotate(Vector3.right * moveDirection * wheelRotationSpeed * Time.deltaTime);
        }
    }
}
