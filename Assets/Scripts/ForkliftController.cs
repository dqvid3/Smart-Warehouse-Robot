using System.Collections;
using System.Data;
using UnityEngine;

public class ForkliftController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float speed = 5f;
    public float rotationSpeed = 100f;

    [Header("Mast Settings")]
    public Rigidbody[] mastRigidbody; // Array of Rigidbodies for each mast
    public float liftForce = 2f;
    public float maxLiftHeight = 2f;

    [Header("Grab Points")]
    public Transform grabPoint;  // Punto di presa per i livelli più alti

    private int currentMastIndex = 0; // Indice del mast attualmente controllato
    private float currentTotalLiftHeight = 0f; // Altezza totale corrente di sollevamento

    void Update()
    {
        HandleMovement();
        HandleLiftInput();
        if (Input.GetKeyDown(KeyCode.Tab))
            SwitchMast();
    }

    void FixedUpdate()
    {
        if (Input.GetKey(KeyCode.PageUp))
        {
            MoveLift(liftForce * Time.fixedDeltaTime);
        }
        if (Input.GetKey(KeyCode.PageDown))
        {
            MoveLift(-liftForce * Time.fixedDeltaTime);
        }
    }

    private void HandleMovement()
    {
        float move = Input.GetAxis("Vertical");   // W/S per muoversi avanti/indietro
        float turn = Input.GetAxis("Horizontal"); // A/D per ruotare
        transform.Translate(move * speed * Time.deltaTime * Vector3.forward);
        transform.Rotate(turn * rotationSpeed * Time.deltaTime * Vector3.up);
    }

    private void HandleLiftInput()
    {
        if (Input.GetKey(KeyCode.PageUp) || Input.GetKey(KeyCode.PageDown))
        {
            // Enable physics for the current mast when trying to move it
            mastRigidbody[currentMastIndex].isKinematic = false;
        }
        else
        {
            // Disable physics when not actively moving
            mastRigidbody[currentMastIndex].isKinematic = true;
        }
    }

    public void MoveLift(float amount)
    {
        Rigidbody currentMast = mastRigidbody[currentMastIndex];
        // Calculate the target position
        Transform mastTransform = currentMast.transform;
        Vector3 newPosition = mastTransform.localPosition;
        newPosition.y = Mathf.Clamp(newPosition.y + amount, 0, maxLiftHeight);

        // Update currentTotalLiftHeight
        float deltaHeight = newPosition.y - mastTransform.localPosition.y;
        currentTotalLiftHeight += deltaHeight;
        currentTotalLiftHeight = Mathf.Clamp(currentTotalLiftHeight, 0, maxLiftHeight * mastRigidbody.Length);

        mastTransform.localPosition = newPosition;

        // Calculate the target world position based on the local position
        Vector3 targetWorldPosition = mastTransform.parent.TransformPoint(mastTransform.localPosition);

        // Move the Rigidbody using MovePosition
        currentMast.MovePosition(targetWorldPosition);
    }

    public IEnumerator LiftMast(float targetHeight)
    {
        // Calcola la differenza di altezza
        float heightDifference = targetHeight - currentTotalLiftHeight;

        if (heightDifference > 0)
        {
            // Movimento verso l'alto
            while (currentMastIndex < mastRigidbody.Length && heightDifference > 0)
            {
                Rigidbody currentMast = mastRigidbody[currentMastIndex];
                float currentMastLocalHeight = currentMast.transform.localPosition.y;
                float remainingMastLiftCapacity = maxLiftHeight - currentMastLocalHeight;

                // Calcola quanto sollevare il mast corrente
                float liftAmount = Mathf.Min(heightDifference, remainingMastLiftCapacity);

                // Solleva il mast corrente
                while (currentMast.transform.localPosition.y < currentMastLocalHeight + liftAmount)
                {
                    MoveLift(liftForce * Time.fixedDeltaTime);
                    yield return new WaitForFixedUpdate();
                }

                // Aggiorna l'altezza rimanente da sollevare
                heightDifference -= liftAmount;

                // Passa al prossimo mast se necessario
                if (heightDifference > 0)
                    SwitchMast();
            }
        }
        else if (heightDifference < 0)
        {
            // Movimento verso il basso
            heightDifference = Mathf.Abs(heightDifference); // Rendi positivo per la gestione
            while (currentMastIndex >= 0 && heightDifference > 0)
            {
                Rigidbody currentMast = mastRigidbody[currentMastIndex];
                float currentMastLocalHeight = currentMast.transform.localPosition.y;

                // Calcola quanto abbassare il mast corrente
                float lowerAmount = Mathf.Min(heightDifference, currentMastLocalHeight);

                // Abbassa il mast corrente
                while (currentMast.transform.localPosition.y > currentMastLocalHeight - lowerAmount)
                {
                    MoveLift(-liftForce * Time.fixedDeltaTime);
                    yield return new WaitForFixedUpdate();
                }

                // Aggiorna l'altezza rimanente da abbassare
                heightDifference -= lowerAmount;

                // Disabilita il mast corrente
                mastRigidbody[currentMastIndex].isKinematic = true;

                // Passa al mast precedente se necessario
                if (heightDifference > 0)
                {
                    if (currentMastIndex > 0)
                    {
                        currentMastIndex--;
                        mastRigidbody[currentMastIndex].isKinematic = false; // Abilita il mast precedente
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        // Aggiorna l'altezza totale corrente
        currentTotalLiftHeight = targetHeight;
    }


    private void SwitchMast()
    {
        // Disable physics on the current mast before switching
        mastRigidbody[currentMastIndex].isKinematic = true;

        currentMastIndex = (currentMastIndex + 1) % mastRigidbody.Length;
        Debug.Log("Switched to Mast: " + currentMastIndex);
    }
}