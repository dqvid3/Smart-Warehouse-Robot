using UnityEngine;

public class RobotController : MonoBehaviour
{
    // Oggetto da prelevare
    private GameObject targetObject;
    // Posizione di destinazione
    private Vector3 targetPosition;
    // Stato del robot
    private bool hasObject = false;
    public float speed = 2f;

    public void PickUpObject(GameObject obj, Vector3 destination)
    {
        targetObject = obj;
        targetPosition = destination;
    }

    void Update()
    {
        if (targetObject != null && !hasObject)
        {
            MoveTowards(targetObject.transform.position);

            // Se è vicino all'oggetto, lo preleva
            if (Vector3.Distance(transform.position, targetObject.transform.position) < 0.5f)
            {
                PickUp();
            }
        }
        else if (hasObject)
        {
            MoveTowards(targetPosition);

            // Se è arrivato alla destinazione, rilascia l'oggetto
            if (Vector3.Distance(transform.position, targetPosition) < 0.5f)
            {
                DropObject();
            }
        }
    }

    void MoveTowards(Vector3 destination)
    {
        transform.position = Vector3.MoveTowards(transform.position, destination, speed * Time.deltaTime);
    }

    void PickUp()
    {
        // Attacca l'oggetto al robot
        targetObject.transform.SetParent(transform);
        targetObject.transform.localPosition = Vector3.up; // Solleva l'oggetto sopra il robot
        hasObject = true;
    }

    void DropObject()
    {
        // Rilascia l'oggetto
        targetObject.transform.SetParent(null);
        targetObject.transform.position = targetPosition;
        hasObject = false;
        targetObject = null;
    }
}