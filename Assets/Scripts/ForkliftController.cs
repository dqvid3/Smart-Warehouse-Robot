using System.Collections;
using UnityEngine;

public class ForkliftController : MonoBehaviour
{
    [Header("Mast Settings")]
    public Rigidbody[] mastRigidbody; // Array di Rigidbodies per ogni mast
    public float liftForce = 1f;
    private float maxLiftHeight = 2f;
    private int currentMastIndex = 0; // Indice del mast attualmente controllato
    private float currentHeight = 0f; // Altezza totale corrente di sollevamento

    public void MoveLift(float amount)
    {
        mastRigidbody[currentMastIndex].isKinematic = false;
        Rigidbody currentMast = mastRigidbody[currentMastIndex];
        Transform mastTransform = currentMast.transform;
        Vector3 newPosition = mastTransform.localPosition;

        // Calcola la nuova altezza locale del mast corrente
        float newLocalHeight = Mathf.Clamp(mastTransform.localPosition.y + amount, 0, maxLiftHeight);

        // Calcola il delta di altezza
        float deltaHeight = newLocalHeight - mastTransform.localPosition.y;

        // Aggiorna l'altezza totale
        currentHeight += deltaHeight;
        currentHeight = Mathf.Clamp(currentHeight, 0, maxLiftHeight * mastRigidbody.Length);

        // Aggiorna la posizione locale del mast
        newPosition.y = newLocalHeight;
        mastTransform.localPosition = newPosition;

        // Calcola la posizione mondiale
        Vector3 targetWorldPosition = mastTransform.parent.TransformPoint(mastTransform.localPosition);

        // Muovi il Rigidbody
        currentMast.MovePosition(targetWorldPosition);
        mastRigidbody[currentMastIndex].isKinematic = true;
    }

    public IEnumerator LiftMastToHeight(float targetHeight)
    {
        // Calcola la differenza di altezza
        float heightDifference = targetHeight - currentHeight;
        float direction = Mathf.Sign(heightDifference); // 1 per alzare, -1 per abbassare

        while (Mathf.Abs(heightDifference) > 0.001f) // Usa un margine di tolleranza
        {
            Rigidbody currentMast = mastRigidbody[currentMastIndex];
            float currentMastLocalHeight = currentMast.transform.localPosition.y;
            float remainingMastLiftCapacity = direction > 0 ? maxLiftHeight - currentMastLocalHeight : currentMastLocalHeight;

            // Calcola quanto sollevare/abbassare il mast corrente
            float moveAmount = Mathf.Min(Mathf.Abs(heightDifference), remainingMastLiftCapacity, liftForce * Time.fixedDeltaTime);

            // Muovi il mast corrente
            MoveLift(direction * moveAmount);

            // Aggiorna l'altezza rimanente
            heightDifference -= direction * moveAmount;

            // Passa al prossimo mast se necessario
            if (Mathf.Abs(heightDifference) > 0.001f && remainingMastLiftCapacity < 0.01f)
            {
                if (direction > 0 && currentMastIndex < mastRigidbody.Length -1)
                {
                    SwitchMast(1);
                }
                else if (direction < 0 && currentMastIndex > 0)
                {
                    SwitchMast(-1);
                } else
                {
                    break; // Non ci sono più mast da muovere
                }
            }
            yield return new WaitForFixedUpdate();
        }
        // Aggiorna l'altezza totale corrente
        currentHeight = targetHeight;
    }

    private void SwitchMast(int sign = 1)
    {
        currentMastIndex += sign;
    }
}