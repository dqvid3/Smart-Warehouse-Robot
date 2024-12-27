using UnityEngine;
using System.Collections;
using Neo4j.Driver;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine.AI;

public class ForkliftNavController : MonoBehaviour
{
    [Header("NavMesh Settings")]
    public NavMeshAgent agent;
    public Transform shippingPoint;
    public ForkliftController forkliftController;
    [SerializeField] private LayerMask layerMask;
    private bool isCarryingBox = false;
    private float approachDistance = 3.2f;
    private float takeBoxDistance = 1.2f;
    private Neo4jHelper neo4jHelper;
    private QRCodeReader qrReader;

    [Header("Sensor Settings")]
    public GameObject sensor;
    private Collider sensorCollider;

    void Start()
    {
        neo4jHelper = new Neo4jHelper("bolt://localhost:7687", "neo4j", "password");
        qrReader = GetComponent<QRCodeReader>();

        if (sensor == null)
        {
            Debug.LogError("Sensore non collegato! Assicurati di collegarlo nello script.");
        }
        else
        {
            sensorCollider = sensor.GetComponent<Collider>();
            if (sensorCollider == null || !sensorCollider.isTrigger)
            {
                Debug.LogError("Il sensore deve avere un Collider con Is Trigger abilitato.");
            }
        }
    }


    void Update()
    {
        if (sensorCollider != null && !isCarryingBox)
        {
            Collider[] detectedObjects = Physics.OverlapBox(sensorCollider.bounds.center, sensorCollider.bounds.extents, Quaternion.identity, layerMask);
            Debug.Log($"Oggetti rilevati: {detectedObjects.Length}");

            foreach (var obj in detectedObjects)
            {
                Debug.Log($"Oggetto rilevato: {obj.name}");

                if ((layerMask & (1 << obj.gameObject.layer)) != 0)
                {
                    GameObject parcel = obj.gameObject;

                    if (parcel != null)
                    {
                        Debug.Log($"Pacco trovato: {parcel.name}");

                        // Inizia il processo per prendere il pacco
                        StartCoroutine(PickParcel(parcel));
                        break;
                    }
                    else
                    {
                        Debug.LogWarning("Il pacco è null");
                    }
                }
                else
                {
                    Debug.Log($"Oggetto non corrisponde al layer: {obj.name}");
                }
            }
        }
        else
        {
            Debug.Log("Sensore non valido o si sta già trasportando una scatola");
        }
    }

    private async Task UpdateParcelArrival(string timestamp, string category)
    {
        string query = @"
        MATCH (p:Parcel {timestamp: $timestamp})
        SET p.status = 'Arrived', p.category = $category
        RETURN p";

        var parameters = new Dictionary<string, object>
        {
            { "timestamp", timestamp },
            { "category", category }
        };

        await neo4jHelper.ExecuteWriteAsync(query, parameters);
    }

    private IEnumerator PickParcel(GameObject parcel)
    {
        Vector3 parcelPosition = parcel.transform.position;
        Vector3 qrCodeDirection = parcelPosition.y < 0.5f ? Vector3.left : Vector3.forward;
        Vector3 approachPosition = parcelPosition + qrCodeDirection * approachDistance;
        approachPosition.y = transform.position.y;

        yield return MoveToPosition(approachPosition);
        yield return SmoothRotateToDirection(-qrCodeDirection);
        yield return LiftMastToHeight(parcelPosition.y);
        yield return MoveToPosition(approachPosition - qrCodeDirection * takeBoxDistance);
        yield return LiftMastToHeight(parcelPosition.y + 0.1f);
        AttachParcel(parcel);
    }

    private IEnumerator MoveToPosition(Vector3 position)
    {
        agent.SetDestination(position);
        yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance);
        agent.ResetPath();
    }

    private IEnumerator SmoothRotateToDirection(Vector3 targetForward, float rotationSpeed = 1f)
    {
        Quaternion startRotation = transform.rotation;
        Quaternion finalRotation = Quaternion.LookRotation(targetForward, Vector3.up);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * rotationSpeed;
            transform.rotation = Quaternion.Slerp(startRotation, finalRotation, t);
            yield return new WaitForFixedUpdate();
        }
        transform.rotation = finalRotation;
    }

    private IEnumerator LiftMastToHeight(float height)
    {
        yield return forkliftController.LiftMastToHeight(height);
    }

    private void AttachParcel(GameObject parcel)
    {
        parcel.transform.SetParent(forkliftController.grabPoint);
        isCarryingBox = true;
    }
}
