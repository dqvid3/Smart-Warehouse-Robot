using UnityEngine;

public class DeliveryAreaTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // Verifica se l'oggetto che entra � una box con il tag "Grabbable"
        if (other.CompareTag("Grabbable"))
        {
            Debug.Log($"{other.name} � entrata nella Delivery Area (zona rossa).");

            // Aggiorna lo stato della box
            BoxState boxState = other.GetComponent<BoxState>();
            if (boxState != null)
            {
                boxState.isInDeliveryArea = true;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // Verifica se l'oggetto che esce � una box con il tag "Grabbable"
        if (other.CompareTag("Grabbable"))
        {
            Debug.Log($"{other.name} � uscita dalla Delivery Area (zona rossa).");

            // Aggiorna lo stato della box
            BoxState boxState = other.GetComponent<BoxState>();
            if (boxState != null)
            {
                boxState.isInDeliveryArea = false;
            }
        }
    }
}
