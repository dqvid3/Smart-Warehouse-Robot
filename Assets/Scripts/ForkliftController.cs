using UnityEngine;

public class ForkliftController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float speed = 5f;
    public float rotationSpeed = 100f;

    [Header("Mast Settings")]
    public Transform[] mastsLiftTransform;
    public float liftSpeed = 2f;
    public float liftHeight = 2f;

    [Header("Grab Points")]
    public Transform grabPoint;  // Punto di presa per i livelli pi� alti

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
        transform.Translate(move * speed * Time.deltaTime * Vector3.forward);
        transform.Rotate(turn * rotationSpeed * Time.deltaTime * Vector3.up);
    }

    void HandleLift()
    {
        Transform currentMast = mastsLiftTransform[currentMastIndex];
        if (Input.GetKey(KeyCode.PageUp))
        {
            MoveLift(currentMast, liftSpeed * Time.deltaTime);
        }
        if (Input.GetKey(KeyCode.PageDown))
        {
            MoveLift(currentMast, -liftSpeed * Time.deltaTime);
        }
    }

    void MoveLift(Transform mast, float amount)
    {
        Vector3 liftPosition = mast.localPosition;
        liftPosition.y = Mathf.Clamp(liftPosition.y + amount, 0, liftHeight);
        mast.localPosition = liftPosition;
    }

    void SwitchMast()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            currentMastIndex = (currentMastIndex + 1) % mastsLiftTransform.Length;
            Debug.Log("Switched to Mast: " + currentMastIndex);
        }
    }
}
