using UnityEngine;

public class DeliveryAreaManager : MonoBehaviour
{
    // Array di prefabs degli oggetti da generare
    public GameObject[] deliveryObjectPrefabs;

    // Posizione nella zona di consegna dove generare l'oggetto
    public Transform deliveryPoint;

    // Altezza di generazione dell'oggetto sopra il deliveryPoint
    public float deliveryHeight = 5f;

    // Timer per simulare una consegna periodica
    public float deliveryInterval = 10f;
    private float deliveryTimer;

    // Forza applicata per far cadere il pacco
    public float dropForce = 10f;
    public float bounciness = 0.5f;
    public float dynamicFriction = 0.4f;
    public float staticFriction = 0.4f;

    void Start()
    {
        deliveryTimer = deliveryInterval;
    }

    void Update()
    {
        deliveryTimer -= Time.deltaTime;

        if (deliveryTimer <= 0f && IsDeliveryAreaEmpty())
        {
            GenerateDeliveryObject();
            deliveryTimer = deliveryInterval;
        }
    }

    private bool IsDeliveryAreaEmpty()
    {
        return transform.childCount == 0;
    }

    private void GenerateDeliveryObject()
    {
        if (deliveryObjectPrefabs.Length > 0 && deliveryPoint != null)
        {
            // Seleziono un prefab casuale dall'array
            GameObject selectedPrefab = deliveryObjectPrefabs[Random.Range(0, deliveryObjectPrefabs.Length)];

            // Calcolo la posizione sopra il deliveryPoint
            Vector3 spawnPosition = deliveryPoint.position + Vector3.up * deliveryHeight;

            // Instanzio il prefab selezionato
            GameObject spawnedObject = Instantiate(selectedPrefab, spawnPosition, deliveryPoint.rotation, transform);

            Rigidbody rb = spawnedObject.GetComponent<Rigidbody>();

            // Configuro un materiale fisico per il rimbalzo
            PhysicsMaterial bounceMaterial = new()
            {
                bounciness = bounciness,
                dynamicFriction = dynamicFriction,
                staticFriction = staticFriction,
                bounceCombine = PhysicsMaterialCombine.Maximum
            };

            // Sostituisco il Collider con il materiale fisico
            Collider col = spawnedObject.GetComponent<Collider>();
            col.material = bounceMaterial;
            // Applico una forza verso il basso per simulare la caduta
            rb.AddForce(Vector3.down * dropForce, ForceMode.Impulse);
        }
    }
}
