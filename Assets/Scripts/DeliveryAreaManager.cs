using UnityEngine;

public class DeliveryAreaManager : MonoBehaviour
{
    // Array di prefabs degli oggetti da generare
    [SerializeField] private GameObject[] deliveryObjectPrefabs;

    // Posizione nella zona di consegna dove generare l'oggetto
    [SerializeField] private Transform deliveryPoint;

    // Altezza di generazione dell'oggetto sopra il deliveryPoint
    [SerializeField] private float deliveryHeight = 5f;

    // Timer per simulare una consegna periodica
    [SerializeField] private float deliveryInterval = 10f;
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
        // Considero DeliveryPoint all'interno
        return transform.childCount == 1;
    }

    private void GenerateDeliveryObject()
    {
        if (deliveryObjectPrefabs.Length > 0 && deliveryPoint != null)
        {
            // Seleziono un prefab casuale dall'array
            GameObject selectedPrefab = deliveryObjectPrefabs[Random.Range(0, deliveryObjectPrefabs.Length)];

            // Genero un offset casuale per la posizione (intorno al deliveryPoint)
            float randomOffsetX = Random.Range(-4f, 4f);
            float randomOffsetZ = Random.Range(-4f, 4f);

            // Calcolo la posizione sopra il deliveryPoint con il random offset
            Vector3 spawnPosition = deliveryPoint.position + Vector3.up * deliveryHeight
                                    + new Vector3(randomOffsetX, 0f, randomOffsetZ);

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
