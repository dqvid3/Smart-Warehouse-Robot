using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using Neo4j.Driver;
using System.Collections.Generic;
using System.Threading.Tasks;

public class ForkliftNavController : MonoBehaviour
{
    [SerializeField] private LayerMask layerMask; // Layer mask for detecting parcels
    [SerializeField] private Transform grabPoint;
    private NavMeshAgent agent;
    private ForkliftController forkliftController;
    private float approachDistance = 3.2f;
    private float takeBoxDistance = 1.35f;
    private Neo4jHelper neo4jHelper;
    private QRCodeReader qrReader;
    public Vector3 defaultPosition = Vector3.zero;

    void Start()
    {
        neo4jHelper = new Neo4jHelper("bolt://localhost:7687", "neo4j", "password");
        qrReader = GetComponent<QRCodeReader>();
        agent = GetComponent<NavMeshAgent>();
        forkliftController = GetComponent<ForkliftController>();
        defaultPosition = transform.position;
    }

    public IEnumerator DeliverParcel(Vector3 parcelPosition)
    {
        Vector3 qrCodeDirection = Vector3.left;
        Vector3 approachPosition = parcelPosition + qrCodeDirection * approachDistance;
        approachPosition.y = transform.position.y;

        // Move in front of the parcel
        yield return StartCoroutine(MoveToPosition(approachPosition));
        // Rotate to face the parcel
        yield return StartCoroutine(SmoothRotateToDirection(-qrCodeDirection));

        string qrCode = qrReader.ReadQRCode();
        string[] qrParts = qrCode.Split('|');
        string timestamp = qrParts[0];
        string category = qrParts[1];

        GameObject parcel = GetParcel(parcelPosition.y + 1);
        yield return StartCoroutine(LiftMastToHeight(parcelPosition.y));
        // Go forward to pick the parcel
        yield return StartCoroutine(MoveToPosition(approachPosition - qrCodeDirection * takeBoxDistance));
        yield return StartCoroutine(LiftMastToHeight(parcelPosition.y + 0.05f));
        parcel.transform.SetParent(grabPoint);
        // Change the QR code direction for the shelf approach
        qrCodeDirection = Vector3.forward;
        // Lift the mast further for safe transport
        yield return StartCoroutine(LiftMastToHeight(parcelPosition.y + 1));
        // Get an available slot from the database based on the parcel's category
        IList<IRecord> result = Task.Run(() => GetAvailableSlot(category)).Result;
        Vector3 slotPosition = GetSlotPosition(result[0]);
        float shelfHeight = slotPosition.y;
        slotPosition.y = transform.position.y;
        approachPosition = slotPosition + qrCodeDirection * approachDistance;
        // Move in front of the shelf
        yield return StartCoroutine(MoveToPosition(approachPosition));
        // Rotate to face the shelf
        yield return StartCoroutine(SmoothRotateToDirection(-qrCodeDirection));
        yield return StartCoroutine(LiftMastToHeight(shelfHeight + 0.05f));
        // Move forward to place the parcel
        yield return StartCoroutine(MoveToPosition(approachPosition - qrCodeDirection * takeBoxDistance));
        yield return StartCoroutine(LiftMastToHeight(shelfHeight - 0.05f));
        parcel.transform.SetParent(null);
        _ = UpdateParcelLocation(timestamp, result[0]["slotId"].As<long>());
        // Move backward away from the shelf
        yield return StartCoroutine(MoveBackwards(-qrCodeDirection, takeBoxDistance));
        yield return StartCoroutine(LiftMastToHeight(0));
    }

    public IEnumerator ShipParcel(Vector3 slotPosition, Vector3 conveyorDestination)
    {
        string positionId = "shipping_" + conveyorDestination.z.ToString();
        _ = neo4jHelper.UpdateParcelPositionStatusAsync(positionId, true);
        Vector3 qrCodeDirection = Vector3.forward;
        Vector3 approachPosition = slotPosition + qrCodeDirection * approachDistance;
        approachPosition.y = transform.position.y;

        // Move to the approach position
        yield return MoveToPosition(approachPosition);
        // Rotate to face the parcel
        yield return SmoothRotateToDirection(-qrCodeDirection);

        // Find the parcel GameObject based on its position
        GameObject parcel = GetParcel(slotPosition.y + 1);
        yield return LiftMastToHeight(slotPosition.y);
        yield return MoveToPosition(approachPosition - qrCodeDirection * takeBoxDistance);
        yield return StartCoroutine(SmoothRotateToDirection(-qrCodeDirection));
        yield return LiftMastToHeight(slotPosition.y + 0.05f);
        parcel.transform.SetParent(grabPoint);
        yield return MoveBackwards(-qrCodeDirection, takeBoxDistance);

        qrCodeDirection = Vector3.right;
        approachPosition = conveyorDestination + qrCodeDirection * approachDistance;
        yield return MoveToPosition(approachPosition);
        _ = FreeSlot(slotPosition);
        yield return SmoothRotateToDirection(-qrCodeDirection);
        approachPosition.x -= takeBoxDistance;
        yield return StartCoroutine(MoveToPosition(approachPosition));
        yield return StartCoroutine(LiftMastToHeight(conveyorDestination.y));
        parcel.transform.SetParent(null);
        yield return MoveBackwards(-qrCodeDirection, takeBoxDistance);
        yield return StartCoroutine(LiftMastToHeight(0));
    }

    public IEnumerator MoveToOriginPosition()
    {
        yield return MoveToPosition(defaultPosition);
        yield return StartCoroutine(SmoothRotateToDirection(Vector3.back));
    }

    public IEnumerator MoveToPosition(Vector3 position)
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
        Vector3 startPosition = transform.position;
        Vector3 targetPosition = startPosition - direction * distance;
        float speed = 2;

        while (Vector3.Distance(transform.position, targetPosition) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);
            yield return new WaitForFixedUpdate();
        }
    }

    private GameObject GetParcel(float height)
    {
        // Definisci l'origine del raggio (ad esempio, la posizione del forklift + un offset in avanti)
        Vector3 rayOrigin = transform.position + transform.forward * 1.5f;
        // Definisci la direzione del raggio (avanti rispetto al forklift)
        Vector3 rayDirection = transform.forward;
        // Definisci la lunghezza massima del raggio
        rayOrigin.y = height;
        if (Physics.Raycast(rayOrigin, rayDirection, out RaycastHit hit, 3f, layerMask))
            return hit.collider.gameObject.transform.root.gameObject;
        return null;
    }

    private async Task UpdateParcelLocation(string parcelTimestamp, long slotId)
    {
        string query = @"
        MATCH (p:Parcel {timestamp: $parcelTimestamp})-[r:LOCATED_IN]->(d:Area {type: 'Delivery'})
        DELETE r
        WITH p
        MATCH (s:Slot), (p:Parcel {timestamp: $parcelTimestamp})
        WHERE ID(s) = $slotId
        CREATE (s)-[:CONTAINS]->(p)
        SET s.occupied = true";
        var parameters = new Dictionary<string, object> { { "parcelTimestamp", parcelTimestamp }, { "slotId", slotId } };
        await neo4jHelper.ExecuteWriteAsync(query, parameters);
    }

    private async Task FreeSlot(Vector3 slotPosition)
    {
        string query = @"
        MATCH (s:Shelf)-[:HAS_LAYER]->(l:Layer)-[:HAS_SLOT]->(slot:Slot)
        WHERE s.x + slot.x = $x AND l.y = $y AND s.z = $z
        MATCH (slot)-[r:CONTAINS]->(p:Parcel)
        DELETE r
        SET slot.occupied = false";
        var parameters = new Dictionary<string, object>
        {
            { "x", slotPosition.x },
            { "y", slotPosition.y },
            { "z", slotPosition.z }
        }; 
        Debug.Log(slotPosition);
        Debug.Log(query);
        await neo4jHelper.ExecuteWriteAsync(query, parameters);
    }

    private async Task<IList<IRecord>> GetAvailableSlot(string category)
    {
        string query = @"
        MATCH (s:Shelf {category: $category})-[:HAS_LAYER]->(l:Layer)-[:HAS_SLOT]->(slot:Slot)
        WHERE NOT (slot)-[:CONTAINS]->(:Parcel) AND slot.occupied = false
        RETURN s.x + slot.x AS x, l.y AS y, s.z AS z, ID(slot) AS slotId
        LIMIT 1";
        var result = await neo4jHelper.ExecuteReadListAsync(query, new Dictionary<string, object> { { "category", category } });
        long slotId = result[0]["slotId"].As<long>();
        query = @"
        MATCH (s:Slot)
        WHERE ID(s) = $slotId
        SET s.occupied = true";
        _ = neo4jHelper.ExecuteWriteAsync(query, new Dictionary<string, object> { { "slotId", slotId } });
        return result;
    }

    private Vector3 GetSlotPosition(IRecord record)
    {
        float x = record[0].As<float>();
        float y = record[1].As<float>();
        float z = record[2].As<float>();
        return new Vector3(x, y, z);
    }
}