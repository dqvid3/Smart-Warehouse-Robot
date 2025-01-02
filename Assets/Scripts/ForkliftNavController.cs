using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using Neo4j.Driver;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

public class ForkliftNavController : MonoBehaviour
{
    [SerializeField] private LayerMask layerMask; // Layer mask for detecting parcels
    [SerializeField] private Transform grabPoint;
    private NavMeshAgent agent;
    private ForkliftController forkliftController;
    private float approachDistance = 3.2f;
    private float takeBoxDistance = 1.2f;
    private Neo4jHelper neo4jHelper;
    private QRCodeReader qrReader;
    private Vector3 defaultPosition;

    // Evento per notificare il completamento del compito
    public event Action OnTaskCompleted;

    void Start()
    {
        neo4jHelper = new Neo4jHelper("bolt://localhost:7687", "neo4j", "password");
        qrReader = GetComponent<QRCodeReader>();
        agent = GetComponent<NavMeshAgent>();
        forkliftController = GetComponent<ForkliftController>();
        defaultPosition = transform.position;
    }

    void Update()
    {
    }

    public IEnumerator PickParcelFromDelivery(Vector3 parcelPosition, Action<GameObject, string, string> onCategoryRetrieved)
    {
        Vector3 qrCodeDirection = Vector3.left;
        Vector3 approachPosition = parcelPosition + qrCodeDirection * approachDistance;
        approachPosition.y = transform.position.y;

        // Move to the approach position
        yield return StartCoroutine(MoveToPosition(approachPosition));
        // Rotate to face the parcel
        yield return StartCoroutine(SmoothRotateToDirection(-qrCodeDirection));

        if (qrReader == null)
        {
            Debug.LogError("QRCodeReader non assegnato nel ForkliftNavController!");
            yield break;
        }

        // Read the QR code
        string qrCode = qrReader.ReadQRCode();
        string[] qrParts = qrCode.Split('|');
        if (qrParts.Length < 2)
        {
            Debug.LogWarning("Invalid QR code format.");
            yield break;
        }

        string idParcel = qrParts[0];
        string category = qrParts[1];

        // Find the parcel GameObject based on its position
        GameObject parcel = GetParcel(parcelPosition.y + 1);
        if (parcel == null)
        {
            Debug.LogWarning("Parcel not found.");
            yield break;
        }

        yield return StartCoroutine(LiftMastToHeight(parcelPosition.y));
        yield return StartCoroutine(MoveToPosition(approachPosition - qrCodeDirection * takeBoxDistance));
        yield return StartCoroutine(LiftMastToHeight(parcelPosition.y + 0.1f));
        parcel.transform.SetParent(grabPoint);


        yield return StartCoroutine(LiftMastToHeight(parcelPosition.y + 1));

        // Pass the category to the robot
        onCategoryRetrieved?.Invoke(parcel, category, idParcel);
    }

    public IEnumerator StoreParcel(Vector3 slotPosition, GameObject parcel, long slotId, string idParcel)
    {
        Vector3 qrCodeDirection = Vector3.forward;
        Vector3 approachPosition = slotPosition + qrCodeDirection * approachDistance;
        approachPosition.y = transform.position.y;

        // Move to the approach position
        yield return StartCoroutine(MoveToPosition(approachPosition));
        // Rotate to face the slot
        yield return StartCoroutine(SmoothRotateToDirection(-qrCodeDirection));

        float shelfHeight = slotPosition.y;

        // Lift the mast to the height of the shelf layer
        yield return StartCoroutine(LiftMastToHeight(shelfHeight));
        // Move forward to place the parcel
        yield return StartCoroutine(MoveToPosition(approachPosition - qrCodeDirection * takeBoxDistance));
        // Lower the mast slightly to release the parcel
        yield return StartCoroutine(LiftMastToHeight(shelfHeight - 0.1f));
        // Visually detach the parcel
        parcel.transform.SetParent(null);

        // Update the parcel's location in the database
        _ = UpdateParcelLocation(idParcel, slotId);


        // Move backward away from the shelf
        yield return StartCoroutine(MoveBackwards(-qrCodeDirection, takeBoxDistance));
        // Lower the mast to the default position
        yield return StartCoroutine(LiftMastToHeight(0));

        yield return StartCoroutine(MoveToOriginPosition());

        // Notifica il completamento del compito
        OnTaskCompleted?.Invoke();
    }

    public IEnumerator PickParcelFromShelf(Vector3 parcelPosition)
    {
        Vector3 qrCodeDirection = Vector3.forward;
        Vector3 approachPosition = parcelPosition + qrCodeDirection * approachDistance;
        approachPosition.y = transform.position.y;

        // Move to the approach position
        yield return MoveToPosition(approachPosition);
        // Rotate to face the parcel
        yield return SmoothRotateToDirection(-qrCodeDirection);

        // Find the parcel GameObject based on its position
        GameObject parcel = GetParcel(parcelPosition.y + 1);
        yield return LiftMastToHeight(parcelPosition.y);
        yield return MoveToPosition(approachPosition - qrCodeDirection * takeBoxDistance);
        yield return LiftMastToHeight(parcelPosition.y + 0.05f);
        parcel.transform.SetParent(grabPoint);
        yield return MoveBackwards(-qrCodeDirection, takeBoxDistance);
        yield return LiftMastToHeight(1);
        Vector3 randomShippingPosition = Task.Run(() => GetRandomShippingPosition()).Result;
        yield return MoveToPosition(randomShippingPosition);
        yield return LiftMastToHeight(0);
        parcel.transform.SetParent(null);
        yield return MoveBackwards(-qrCodeDirection, takeBoxDistance);
        yield return MoveToOriginPosition();
    }

    public IEnumerator MoveToOriginPosition()
    {
        agent.SetDestination(defaultPosition);
        yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance);
        agent.ResetPath();
        Vector3 targetForward = Vector3.back; // Ruota di 180° rispetto all'asse Y
        yield return StartCoroutine(SmoothRotateToDirection(targetForward, 1f)); // Rotazione graduale
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
        float speed = agent.speed;
        Vector3 startPosition = transform.position;
        Vector3 targetPosition = startPosition - direction * distance;

        while (Vector3.Distance(transform.position, targetPosition) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);
            yield return new WaitForFixedUpdate();
        }
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

        try
        {
            await neo4jHelper.ExecuteWriteAsync(query, parameters);
            Debug.Log($"UpdateParcelLocation completed successfully for Parcel ID: {parcelTimestamp}, Slot ID: {slotId}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error updating parcel location: {ex.Message}");
        }
    }


     private async Task<Vector3> GetRandomShippingPosition()
    {
        string query = @"
        MATCH (shipping:Area {type: 'Shipping'})
        WITH shipping, shipping.center_x AS cx, shipping.center_z AS cz, shipping.length AS len, shipping.width AS wid
        RETURN cx + (rand() - 0.5) * len AS x, cz + (rand() - 0.5) * wid AS z";
        IList<IRecord> result = await neo4jHelper.ExecuteReadListAsync(query);
        if (result.Count > 0)
        {
            float x = result[0]["x"].As<float>();
            float z = result[0]["z"].As<float>();
            return new Vector3(x, 0, z); 
        }
        return Vector3.zero;    
    }

    private GameObject GetParcel(float height)
    {
        // Definisci l'origine del raggio (ad esempio, la posizione del forklift + un offset in avanti)
        Vector3 rayOrigin = transform.position + transform.forward * 1.5f;
        // Definisci la direzione del raggio (avanti rispetto al forklift)
        Vector3 rayDirection = transform.forward;
        // Definisci la lunghezza massima del raggio
        float maxDistance = 2f;
        rayOrigin.y = height;
        if (Physics.Raycast(rayOrigin, rayDirection, out RaycastHit hit, maxDistance, layerMask))
        {
            return hit.collider.gameObject.transform.root.gameObject;
        }
        return null;
    }
}