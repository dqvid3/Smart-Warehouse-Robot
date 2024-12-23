
using UnityEngine;
using System.Collections;

public static class ForkliftCommonFunctions
{
    // Funzione comune per calcolare il punto di approccio
    public static Vector3 CalculateApproachPoint(Transform agent, Vector3 target, float distance)
    {
        Vector3 horizPos = target;
        horizPos.y = agent.position.y;
        Vector3 directionToTarget = (horizPos - agent.position).normalized;
        return horizPos - (directionToTarget * distance);
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

    public static void AttachBox(ref GameObject targetBox, Transform grabPoint, ref bool isCarryingBox)
    {
        //Configura il Rigidbody dell'intero oggetto
        Rigidbody rb = targetBox.GetComponent<Rigidbody>();
        rb.isKinematic = true; // Disabilita la fisica
        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Assegna il parent dell'intero oggetto al grab point
        targetBox.transform.SetParent(grabPoint);
        targetBox.transform.localPosition = new Vector3(0.1f, 0.1f, 0.1f);

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

        // Ripristina le propriet� fisiche dell'intero oggetto
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
        for (int i = 0; i < forkliftController.mastsLiftTransform.Length; i++)
        {
            var mast = forkliftController.mastsLiftTransform[i];

            while (mast.localPosition.y > 0)
            {
                mast.localPosition -= Vector3.up * forkliftController.liftSpeed * Time.deltaTime;
                yield return null;
            }
        }
    }
}
