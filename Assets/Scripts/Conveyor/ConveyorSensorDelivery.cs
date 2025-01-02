using UnityEngine;

public class ConveyorSensorDelivery : MonoBehaviour
{
    public string positionId;
    public GameObject piano;
    private Neo4jHelper neo4jHelper;
    private bool hasTriggered = false;
    private ConveyorPhysic conveyorPhysic;

    void Start()
    {
        neo4jHelper = new Neo4jHelper("bolt://localhost:7687", "neo4j", "password");
        conveyorPhysic = piano.GetComponent<ConveyorPhysic>();

        // Controlla se c'è qualcosa nel trigger allo Start
        CheckTriggerAndUpdateStatus();
    }

    void OnTriggerEnter(Collider other)
    {
        if (hasTriggered) return;
        _ = neo4jHelper.UpdateParcelPositionStatusAsync(positionId, true);
        conveyorPhysic.speed = 0;
        conveyorPhysic.meshSpeed = 0;
        hasTriggered = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (!hasTriggered) return;
        _ = neo4jHelper.UpdateParcelPositionStatusAsync(positionId, false);
        conveyorPhysic.speed = 2;
        conveyorPhysic.meshSpeed = 1;
        hasTriggered = false;
    }

    void OnDestroy()
    {
        neo4jHelper?.CloseConnection();
    }

    private void CheckTriggerAndUpdateStatus()
    {
        Collider triggerCollider = GetComponent<Collider>();
        Collider[] colliders = Physics.OverlapBox(triggerCollider.bounds.center, triggerCollider.bounds.extents, transform.rotation);
        // Se non ci sono oggetti nel trigger, imposta hasParcel a false (Ignora se stesso)
        if (colliders.Length == 1)
            _ = neo4jHelper.UpdateParcelPositionStatusAsync(positionId, false);
    }
}