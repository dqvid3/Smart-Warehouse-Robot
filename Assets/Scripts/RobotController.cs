using UnityEngine;
using UnityEngine.AI;

public class RobotController : MonoBehaviour
{
    [SerializeField] private NavMeshAgent agent;
    private WarehouseManager warehouseManager;
    private const float DEFAULT_DISTANCE = 1f;
    private GameObject targetObject;
    private Vector3 targetPosition;
    private bool hasObject;
    private bool returningToBase;
    private Vector3 basePosition = new(0, 0.5f, 0);

    private void Start()
    {
        warehouseManager = FindFirstObjectByType<WarehouseManager>();
    }
    public void PickUpObject(GameObject obj, Vector3 destination)
    {
        targetObject = obj;
        targetPosition = destination;
        hasObject = false;
        returningToBase = false;
    }

    private void Update()
    {
        if (targetObject != null && !hasObject)
        {
            agent.SetDestination(targetObject.transform.position);
            if (IsCloseEnough(targetObject.transform.position, 2.5f))
            {
                PickUp();
            }
        }
        else if (hasObject)
        {
            agent.SetDestination(targetPosition);
            if (IsCloseEnough(targetPosition))
            {
                DropObject();
            }
        }
        else if (!hasObject && returningToBase)
        {
            agent.SetDestination(basePosition);
            if (IsCloseEnough(basePosition))
            {
                returningToBase = false;
            }
        }
    }

    private bool IsCloseEnough(Vector3 position, float threshold = DEFAULT_DISTANCE)
    {
        Debug.Log(Vector3.Distance(transform.position, position));
        return Vector3.Distance(transform.position, position) < threshold;
    }

    private void PickUp()
    {
        targetObject.transform.SetParent(transform);
        targetObject.transform.localPosition = Vector3.up;
        hasObject = true;
    }

    private void DropObject()
    {
        targetObject.transform.SetParent(null);
        targetObject.transform.position = targetPosition;
        hasObject = false;
        targetObject = null;
        warehouseManager.EnableDelivery();
        returningToBase = true;
    }
}