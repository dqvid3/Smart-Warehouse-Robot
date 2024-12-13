using UnityEngine;
using System.Collections.Generic;

public class WarehouseManager : MonoBehaviour
{
    // Scaffali
    public GameObject shelvesParent;
    // Posizione nella zona di consegna dove lasciare l'oggetto    
    public Transform shippingPoint;
    public GameObject robot;
    private GameObject requestedObject;

    void Start()
    {
        GenerateShippingRequest();
    }

    void GenerateShippingRequest()
    {
        // Ottengo tutti i figli del genitore "Shelves"
        List<GameObject> shelves = GetChildObjects(shelvesParent);

        // Filtro le shelves per ottenere solo quelle che hanno almeno un figlio
        List<GameObject> validShelves = new();

        foreach (GameObject shelf in shelves)
        {
            List<GameObject> objectsOnShelf = GetChildObjects(shelf);
            if (objectsOnShelf.Count > 0)
            {
                validShelves.Add(shelf);
            }
        }

        if (validShelves.Count > 0)
        {
            // Seleziono una shelf a caso tra quelle valide
            GameObject randomShelf = validShelves[Random.Range(0, validShelves.Count)];
            // Ottengo tutti gli oggetti sulla shelf selezionata
            List<GameObject> objectsOnShelf = GetChildObjects(randomShelf);
            // Seleziono un oggetto casuale dallo scaffale
            requestedObject = objectsOnShelf[Random.Range(0, objectsOnShelf.Count)];
            // Chiedi al robot di prendere l'oggetto
            robot.GetComponent<RobotController>().PickUpObject(requestedObject, shippingPoint.position);
        }
        else
        {
            Debug.LogWarning("Nessun scaffale disponibile con oggetti!");
        }
    }

    private List<GameObject> GetChildObjects(GameObject parent)
    {
        List<GameObject> childObjects = new();

        foreach (Transform child in parent.transform)
        {
            childObjects.Add(child.gameObject);
        }

        return childObjects;
    }
}