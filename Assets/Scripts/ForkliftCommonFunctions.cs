
using UnityEngine;
using System.Collections;

public static class ForkliftCommonFunctions
{
    // Funzione comune per controllare se il ForkliftController è assegnato
    public static bool CheckForkliftController(ForkliftController forkliftController)
    {
        if (forkliftController == null)
        {
            Debug.LogError("ForkliftController non assegnato!");
            return false;
        }
        return true;
    }


    public static Vector3 CalculateFromPoint(Vector3 finalApproachPoint, Transform forkliftTransform, float retreatDistance)
    {
        // Calcola il punto di arretramento aggiornando solo la coordinata z
        Vector3 retreatPos = new Vector3(
            finalApproachPoint.x,                      // Mantieni la stessa x del punto di approccio
            forkliftTransform.position.y,              // Mantieni la stessa altezza del forklift
            finalApproachPoint.z - retreatDistance     // Aggiorna solo la z arretrando di 'retreatDistance'
        );

        return retreatPos;
    }




    // Funzione comune per calcolare il punto di approccio
    // Parametrizzato per aggiungere o meno una distanza extra
    public static Vector3 CalculateApproachPoint(Transform forkliftTransform, Vector3 targetPosition, float distance, float extraDistance)
    {
        Vector3 horizPos = targetPosition;
        horizPos.y = forkliftTransform.position.y;
        Vector3 directionToTarget = (horizPos - forkliftTransform.position).normalized;
        return horizPos - directionToTarget * (distance + extraDistance);
    }

    public static void AttachBox(
     ref GameObject targetBox,
     Transform grabPoint,
     ref bool isCarryingBox,
     ForkliftController forkliftController
 )
    {
        if (targetBox == null)
        {
            Debug.LogWarning("Nessuna box selezionata per il grab!");
            return;
        }

        // Configura il Rigidbody dell'intero oggetto
        Rigidbody rb = targetBox.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true; // Disabilita la fisica
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        else
        {
            Debug.LogWarning("Rigidbody mancante nella box, il grab potrebbe non funzionare correttamente.");
        }

        // Assegna il parent dell'intero oggetto al grab point
        targetBox.transform.SetParent(grabPoint);
        targetBox.transform.localPosition = Vector3.zero;
        targetBox.transform.localRotation = Quaternion.identity;

        isCarryingBox = true;
        Debug.Log($"Oggetto {targetBox.name} attaccato al grab point.");
    }



    public static void ReleaseBox(
        ref GameObject targetBox,
        ref bool isCarryingBox,
        Vector3 positionOffset,
        bool wakeRigidbody = false
    )
    {
        if (targetBox == null)
        {
            Debug.LogWarning("Nessuna box selezionata!");
            return;
        }

        // Ripristina le proprietà fisiche dell'intero oggetto
        var rb = targetBox.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            if (wakeRigidbody)
            {
                rb.WakeUp();
            }
        }

        targetBox.transform.SetParent(null);
        targetBox.transform.position += positionOffset; // Applica l'offset di rilascio
        targetBox = null;

        isCarryingBox = false;
    }


    // Funzione comune per abbassare tutti i masti, usando i targetHeights forniti
    public static IEnumerator LowerAllMasts(ForkliftController forkliftController)
    {
        float[] targetHeights = new float[] { 1.372069f, 1.525879e-07f, -7.629394e-08f };

        // Controlla che il numero di altezze specificate corrisponda al numero di masti
        if (targetHeights.Length != forkliftController.masts.Length)
        {
            Debug.LogError("Il numero di altezze specificate non corrisponde al numero di masti!");
            yield break;
        }

        for (int i = 0; i < forkliftController.masts.Length; i++)
        {
            var mast = forkliftController.masts[i];
            if (mast.liftTransform == null) continue;

            float targetHeight = targetHeights[i];

            while (mast.liftTransform.localPosition.y > targetHeight)
            {
                mast.liftTransform.localPosition -= Vector3.up * mast.liftSpeed * Time.deltaTime;
                yield return null;
            }

            // Imposta l'altezza finale esatta per evitare piccoli errori di arrotondamento
            mast.liftTransform.localPosition = new Vector3(
                mast.liftTransform.localPosition.x,
                targetHeight,
                mast.liftTransform.localPosition.z
            );
        }
    }
}
