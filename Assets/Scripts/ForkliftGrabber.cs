using UnityEngine;

public class ForkliftGrabber : MonoBehaviour
{
    public Transform grabPoint; // Punto dove l'oggetto verrà afferrato
    private GameObject heldObject = null;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (heldObject == null)
            {
                TryGrab();
            }
            else
            {
                Release();
            }
        }
    }

    void TryGrab()
    {
        RaycastHit hit;
        Vector3 rayOrigin = transform.position + transform.forward * 1f; // Punto di partenza del raycast leggermente davanti
        Debug.DrawRay(rayOrigin, transform.forward * 2f, Color.red, 2f); // Disegna il raycast per il debug

        if (Physics.Raycast(rayOrigin, transform.forward, out hit, 2f))
        {
            Debug.Log("Raycast Hit: " + hit.collider.name);

            if (hit.collider.CompareTag("Grabbable"))
            {
                heldObject = hit.collider.gameObject;
                heldObject.transform.SetParent(grabPoint);
                heldObject.transform.localPosition = Vector3.zero;
                heldObject.GetComponent<Rigidbody>().isKinematic = true;
            }
        }
        else
        {
            Debug.Log("Raycast did not hit any object.");
        }
    }


    void Release()
    {
        if (heldObject != null)
        {
            RaycastHit hit;
            Vector3 rayOrigin = heldObject.transform.position;

            // Raycast verso il basso per verificare il livello
            if (Physics.Raycast(rayOrigin, Vector3.down, out hit, 2f))
            {
                if (hit.collider != null)
                {
                    // Posiziona la box sopra il punto colpito
                    heldObject.transform.position = hit.point + Vector3.up * 0.1f; // Piccolo offset per evitare compenetrazione
                    heldObject.GetComponent<Rigidbody>().isKinematic = false;
                    heldObject.transform.SetParent(null);
                    heldObject = null;
                    Debug.Log("Box rilasciata su: " + hit.collider.name);
                }
                else
                {
                    Debug.LogWarning("Nessun collider trovato sotto la box!");
                }
            }
            else
            {
                Debug.LogWarning("Raycast non ha trovato nulla sotto la box!");
            }
        }
    }

}
