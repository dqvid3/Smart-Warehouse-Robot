using System.Collections;
using System.Data;
using UnityEngine;

public class ForkliftController : MonoBehaviour
{
    [Header("Mast Settings")]
    public Rigidbody[] mastRigidbody; // Array of Rigidbodies for each mast
    public float liftForce = 1f;
    public float maxLiftHeight = 2f;

    [Header("Grab Points")]
    public Transform grabPoint;  // Punto di presa per i livelli più alti

    private int currentMastIndex = 0; // Indice del mast attualmente controllato
    private float currentTotalLiftHeight = 0f; // Altezza totale corrente di sollevamento

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

    public IEnumerator LiftMastToHeight(float targetHeight)
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
    }
}