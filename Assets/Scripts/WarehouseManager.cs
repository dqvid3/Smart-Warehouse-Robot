using UnityEngine;

public class WarehouseManager : MonoBehaviour
{
    [SerializeField] private Transform shippingPoint;
    [SerializeField] private GameObject robot;
    public bool canDeliver = true;

    void Update()
    {
        if (Input.GetMouseButtonDown(0) && canDeliver)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                GameObject requestedObject = hit.collider.gameObject;
                if (IsItem(requestedObject))
                {
                    canDeliver = false;
                    robot.GetComponent<RobotController>().PickUpObject(requestedObject, shippingPoint.position);
                }
            }
        }
    }

    private bool IsItem(GameObject obj)
    {
        Transform parent = obj.transform.parent;
        return parent != null && parent.name.StartsWith("item");
    }

    public void EnableDelivery()
    {
        canDeliver = true;
    }
}