using UnityEngine;

public class ConveyorSensorShipping : MonoBehaviour
{
    [SerializeField] private string positionId;
    private Neo4jHelper neo4jHelper;

    void Start()
    {
        neo4jHelper = new Neo4jHelper("bolt://localhost:7687", "neo4j", "password");
    }

    void OnTriggerExit(Collider other)
    {
        Debug.Log("ciao");
        _ = neo4jHelper.UpdateParcelPositionStatusAsync(positionId, false);
    }

    void OnDestroy()
    {
        neo4jHelper?.CloseConnection();
    }
}
