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
    public Transform shippingPoint; // Not used currently, but could be used for future features
    public ForkliftController forkliftController;
    [SerializeField] private LayerMask layerMask; // Layer mask for detecting parcels (currently not used)
    private bool isCarryingBox = false;
    private float approachDistance = 3.2f;
    private float takeBoxDistance = 1.2f;
    private Neo4jHelper neo4jHelper;
    private QRCodeReader qrReader;
    private float checkInterval = 5f; // Time in seconds between checks for new parcels
    private float lastCheckTime = 0f;

    void Start()
    {
        neo4jHelper = new Neo4jHelper("bolt://localhost:7687", "neo4j", "password");
        qrReader = GetComponent<QRCodeReader>();
    }

    void Update()
    {
        // Automated behavior to check for parcels
        if (Time.time - lastCheckTime > checkInterval)
        {
            lastCheckTime = Time.time;
            CheckForParcels();
        }
    }

    private async void CheckForParcels()
    {
        Vector3? parcelPosition = await GetParcelPosition();
        if (parcelPosition != null)
        {
            Debug.Log("Parcel found at: " + parcelPosition);
            StartCoroutine(PickParcelFromDelivery(parcelPosition.Value));
        }
    }

    private IEnumerator PickParcelFromDelivery(Vector3 parcelPosition)
    {
        isCarryingBox = true; // Prevent picking up multiple parcels

        // Define the direction of the QR code relative to the parcel
        Vector3 qrCodeDirection = Vector3.left; 
        // Calculate the approach position
        Vector3 approachPosition = parcelPosition + qrCodeDirection * approachDistance;
        approachPosition.y = transform.position.y;

        // Move to the approach position
        yield return MoveToPosition(approachPosition);
        // Rotate to face the parcel
        yield return SmoothRotateToDirection(-qrCodeDirection);

        // Read the QR code
        string qrCode = qrReader.ReadQRCode();
        if (string.IsNullOrEmpty(qrCode))
        {
            Debug.LogError("Failed to read QR code.");
            isCarryingBox = false;
            yield break;
        }
        // Extract timestamp and category from the QR code
        string timestamp = qrCode.Split('|')[0];
        string category = qrCode.Split('|')[1];

        // Lift the mast to the height of the parcel
        yield return LiftMastToHeight(parcelPosition.y);
        // Move forward to pick up the parcel
        yield return MoveToPosition(approachPosition - qrCodeDirection * takeBoxDistance);
        // Lift the mast slightly to secure the parcel
        yield return LiftMastToHeight(parcelPosition.y + 0.1f);
        // Visually attach the parcel (you'll need to implement this based on your game objects)
        // AttachParcel(parcel);

        // Change the QR code direction for the shelf approach
        qrCodeDirection = Vector3.forward;
        // Lift the mast further for safe transport
        yield return LiftMastToHeight(parcelPosition.y + 1);
        // Get an available slot from the database based on the parcel's category
        IList<IRecord> result = Task.Run(() => GetAvailableSlot(category)).Result;
        // Get the position of the slot
        Vector3 slotPosition = GetSlotPosition(result[0]);
        float shelfHeight = slotPosition.y;
        slotPosition.y = transform.position.y;
        // Calculate the approach position for the shelf
        approachPosition = slotPosition + qrCodeDirection * approachDistance;
        // Move to the approach position for the shelf
        yield return MoveToPosition(approachPosition);
        // Rotate to face the shelf
        yield return SmoothRotateToDirection(-qrCodeDirection);
        // Lift the mast to the height of the shelf layer
        yield return LiftMastToHeight(shelfHeight);
        // Move forward to place the parcel
        yield return MoveToPosition(approachPosition - qrCodeDirection * takeBoxDistance);
        // Lower the mast slightly to release the parcel
        yield return LiftMastToHeight(shelfHeight - 0.1f);
        // Visually detach the parcel
        // DetachParcel(parcel);
        // Update the parcel's location in the database
        Task.Run(() => UpdateParcelLocation(timestamp, result[0][3].As<long>()));
        // Move backward away from the shelf
        yield return MoveBackwards(-qrCodeDirection, takeBoxDistance);
        // Lower the mast to the default position
        yield return LiftMastToHeight(0);

        // Update parcel position status after successful placement
        Task.Run(() => UpdateParcelPositionStatus(parcelPosition, false));

        isCarryingBox = false; // Ready to pick up another parcel
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
        // Placeholder for attaching the parcel visually
        // Example: parcel.transform.SetParent(forkliftController.grabPoint);
        // You'll need to implement this based on your game objects
    }

    private void DetachParcel(GameObject parcel)
    {
        // Placeholder for detaching the parcel visually
        // Example: parcel.transform.SetParent(null);
        // You'll need to implement this based on your game objects
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

    private async Task<Vector3?> GetParcelPosition()
    {
        string query = @"
        MATCH (d:Area {type: 'Delivery'})-[:HAS_POSITION]->(p:Position {hasParcel: true})
        RETURN p.x AS x, p.y AS y, p.z AS z
        LIMIT 1";
        IList<IRecord> result = await neo4jHelper.ExecuteReadListAsync(query);
        if (result.Count > 0)
        {
            IRecord record = result[0];
            return new Vector3(record["x"].As<float>(), record["y"].As<float>(), record["z"].As<float>());
        }
        return null;
    }

    private async Task UpdateParcelPositionStatus(Vector3 position, bool hasParcel)
    {
        string query = @"
        MATCH (p:Position {x: $x, y: $y, z: $z})
        SET p.hasParcel = $hasParcel";
        var parameters = new Dictionary<string, object>
        {
            { "x", position.x },
            { "y", position.y },
            { "z", position.z },
            { "hasParcel", hasParcel }
        };
        await neo4jHelper.ExecuteWriteAsync(query, parameters);
    }
}