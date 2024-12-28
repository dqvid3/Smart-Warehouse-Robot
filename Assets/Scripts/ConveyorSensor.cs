using UnityEngine;
using System.Threading.Tasks;


public class ConveyorSensor : MonoBehaviour
{
    public Transform position;
    public GameObject piano;
    private Neo4jHelper neo4jHelper;
    private bool hasTriggered = false;
    private ConveyorPhysic conveyorPhysic;

    void Start()
    {
        neo4jHelper = new Neo4jHelper("bolt://localhost:7687", "neo4j", "password");
        conveyorPhysic = piano.GetComponent<ConveyorPhysic>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (hasTriggered) return;
        _ = neo4jHelper.UpdateParcelPositionStatusAsync(position.position.z, true);
        conveyorPhysic.speed = 0;
        conveyorPhysic.meshSpeed = 0;
        hasTriggered = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (!hasTriggered) return;
        Debug.Log("ciao");
        _ = neo4jHelper.UpdateParcelPositionStatusAsync(position.position.z, false);
        conveyorPhysic.speed = 2;
        conveyorPhysic.meshSpeed = 1;
        hasTriggered = false;
    }

    void OnDestroy()
    {
        neo4jHelper?.CloseConnection();
    }
}
