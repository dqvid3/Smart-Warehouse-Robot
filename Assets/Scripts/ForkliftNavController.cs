using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using Neo4j.Driver;
using System.Collections.Generic;
using System.Threading.Tasks;

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

    void Start()
    {
        neo4jHelper = new Neo4jHelper("bolt://localhost:7687", "neo4j", "password");
        qrReader = GetComponent<QRCodeReader>();
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0) && !isCarryingBox)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, layerMask))
            {
                GameObject parcel = hit.collider.transform.root.gameObject;
                StartCoroutine(PickParcel(parcel));
            }
        }
    }

    private IEnumerator PickParcel(GameObject parcel)
    {
        Vector3 parcelPosition = parcel.transform.position;
        Vector3 qrCodeDirection = parcelPosition.y < 0.5f ? Vector3.left : Vector3.forward;
        Vector3 approachPosition = parcelPosition + qrCodeDirection * approachDistance;
        approachPosition.y = transform.position.y;

        yield return MoveToPosition(approachPosition);
        yield return SmoothRotateToDirection(-qrCodeDirection);

        string qrCode = null;
        if (parcelPosition.y < 0.5f) // Se il pacco è a terra, leggi il QR code
        {
            qrCode = qrReader.ReadQRCode();
        }

        yield return LiftMastToHeight(parcelPosition.y); // Solleva il mast per prendere il pacco
        yield return MoveToPosition(approachPosition - qrCodeDirection * takeBoxDistance); // Vai avanti
        yield return LiftMastToHeight(parcelPosition.y + 0.1f); // Alza un po' le mast
        AttachParcel(parcel);

        if (parcelPosition.y < 0.5f) // Se il pacco è a terra, portalo a uno scaffale
        {
            string timestamp = qrCode.Split('|')[0];
            string category = qrCode.Split('|')[1];
            qrCodeDirection = Vector3.forward;
            yield return LiftMastToHeight(parcelPosition.y + 1); // Alza un po' le mast
            var result = Task.Run(() => GetAvailableSlot(category)).Result;
            Vector3 slotPosition = GetSlotPosition(result[0]);
            float shelfHeight = slotPosition.y;
            slotPosition.y = transform.position.y;
            approachPosition = slotPosition + qrCodeDirection * approachDistance;
            yield return MoveToPosition(approachPosition);
            yield return SmoothRotateToDirection(-qrCodeDirection);
            yield return LiftMastToHeight(shelfHeight);
            yield return MoveToPosition(approachPosition - qrCodeDirection * takeBoxDistance);
            yield return LiftMastToHeight(shelfHeight - 0.1f);
            DetachParcel(parcel);
            Task.Run(() => UpdateParcelLocation(timestamp, result[0][3].As<long>()));
            yield return MoveBackwards(-qrCodeDirection, takeBoxDistance);
            yield return LiftMastToHeight(0);
        }
        else
        {
            yield return MoveBackwards(-qrCodeDirection, takeBoxDistance);
            yield return LiftMastToHeight(0);
        }
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

    private IEnumerator MoveBackwards(Vector3 direction, float distance)
    {
        float speed = agent.speed;
        Vector3 startPosition = transform.position;
        Vector3 targetPosition = startPosition - direction * distance;

        while (Vector3.Distance(transform.position, targetPosition) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);
            yield return new WaitForFixedUpdate();
        }
    }

    private void AttachParcel(GameObject parcel)
    {
        parcel.transform.SetParent(forkliftController.grabPoint);
        isCarryingBox = true;
    }

    private void DetachParcel(GameObject parcel)
    {
        parcel.transform.SetParent(null);
        isCarryingBox = false;
    }

    private async Task<IList<IRecord>> GetAvailableSlot(string category)
    {
        string query = @"
        MATCH (s:Shelf {category: $category})-[:HAS_LAYER]->(l:Layer)-[:HAS_SLOT]->(slot:Slot)
        WHERE NOT (slot)-[:CONTAINS]->(:Parcel)
        RETURN s.x + slot.z AS x, l.y AS y, s.z AS z, ID(slot) AS slotId
        LIMIT 1";
        return await neo4jHelper.ExecuteReadListAsync(query, new Dictionary<string, object> { { "category", category } });
    }

    private async Task UpdateParcelLocation(string parcelTimestamp, long slotId)
    {
        string query = @"
        MATCH (p:Parcel {timestamp: $parcelTimestamp})-[r:LOCATED_IN]->(d:Area {type: 'Delivery'})
        DELETE r
        WITH p
        MATCH (s:Slot), (p:Parcel {timestamp: $parcelTimestamp})
        WHERE ID(s) = $slotId
        CREATE (s)-[:CONTAINS]->(p)";
        var parameters = new Dictionary<string, object> { { "parcelTimestamp", parcelTimestamp }, { "slotId", slotId } };

        await neo4jHelper.ExecuteWriteAsync(query, parameters);
    }

    private Vector3 GetSlotPosition(IRecord record)
    {
        float x = record[0].As<float>();
        float y = record[1].As<float>();
        float z = record[2].As<float>();
        return new Vector3(x, y, z);
    }
}