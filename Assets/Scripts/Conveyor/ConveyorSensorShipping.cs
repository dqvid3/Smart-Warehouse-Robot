using UnityEngine;

public class ConveyorSensorShipping : MonoBehaviour
{
    [SerializeField] private string positionId;
    private Neo4jHelper neo4jHelper;

    void Start()
    {
        neo4jHelper = new Neo4jHelper("bolt://localhost:7687", "neo4j", "password");
    }

    void OnTriggerEnter(Collider other)
    {
        _ = neo4jHelper.UpdateParcelPositionStatusAsync(positionId, false);

        // Distrugge il parent (sunoko) e quindi tutti i figli (incluso il parcel)
        if (other.transform.parent != null)
        {
            Destroy(other.transform.parent.gameObject);
        }
        else
        {
            // Se non c'è un parent, distruggi solo l'oggetto che ha attivato il trigger
            Destroy(other.gameObject);
        }
    }


    void OnDestroy()
    {
        neo4jHelper?.CloseConnection();
    }
}
