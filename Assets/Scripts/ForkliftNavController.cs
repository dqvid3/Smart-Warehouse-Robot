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
    private ForkliftController forkliftController;
    private float approachDistance = 3.2f;
    private float takeBoxDistance = 1.6f;
    private float speed = 3.5f;
    private Neo4jHelper neo4jHelper;
    private QRCodeReader qrReader;
    private Vector3 defaultPosition;
    private RobotMovementWithNavMeshAndCollisionPrevention robotMovement;


    // Evento per notificare il completamento del compito
    public event Action OnTaskCompleted;

    void Start()
    {
        neo4jHelper = new Neo4jHelper("bolt://localhost:7687", "neo4j", "password");
        qrReader = GetComponent<QRCodeReader>();
        forkliftController = GetComponent<ForkliftController>();
        defaultPosition = transform.position;
        robotMovement = GetComponent<RobotMovementWithNavMeshAndCollisionPrevention>();
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

        // Leggi il QR code con tentativi ripetuti
        string qrCode = null;
        int maxAttempts = 5; // Numero massimo di tentativi
        int attempts = 0;
        while (string.IsNullOrEmpty(qrCode) && attempts < maxAttempts)
        {
            qrCode = qrReader.ReadQRCode();
            if (string.IsNullOrEmpty(qrCode))
            {
                attempts++;
                Debug.LogWarning($"Tentativo {attempts}: QR code non trovato. Ritento...");
                yield return new WaitForSeconds(1); // Attendi 5 secondi prima di ritentare
            }
        }

        if (string.IsNullOrEmpty(qrCode))
        {
            Debug.LogError("QR code non trovato dopo diversi tentativi.");
            yield break;
        }

        string[] qrParts = qrCode.Split('|');
        if (qrParts.Length < 2)
        {
            Debug.LogWarning("Formato QR code non valido.");
            yield break;
        }

        string idParcel = qrParts[0];
        string category = qrParts[1];

        // Find the parcel GameObject based on its position
        GameObject parcel = GetParcel(parcelPosition.y + 1);
        if (parcel == null)
        {
            Debug.LogWarning("Parcel non trovato.");
            yield return StartCoroutine(MoveToOriginPosition());
            yield break;
        }

        yield return StartCoroutine(LiftMastToHeight(parcelPosition.y));
        yield return StartCoroutine(MoveToPosition(approachPosition - qrCodeDirection * takeBoxDistance));
        yield return StartCoroutine(SmoothRotateToDirection(-qrCodeDirection));
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

        yield return MoveBackwards(transform.forward, takeBoxDistance);
        // Move to the approach position
        yield return StartCoroutine(MoveToPosition(approachPosition));
        // Rotate to face the slot
        yield return StartCoroutine(SmoothRotateToDirection(-qrCodeDirection));

        float shelfHeight = slotPosition.y;

        // Lift the mast to the height of the shelf layer
        yield return StartCoroutine(LiftMastToHeight(shelfHeight + 0.05f));
        // Move forward to place the parcel
        yield return StartCoroutine(MoveToPosition(approachPosition - qrCodeDirection * takeBoxDistance));
        yield return StartCoroutine(SmoothRotateToDirection(-qrCodeDirection));
        // Lower the mast slightly to release the parcel
        yield return StartCoroutine(LiftMastToHeight(shelfHeight - 0.05f));
        // Visually detach the parcel
        parcel.transform.SetParent(null);

        // Update the parcel's location in the database
        _ = UpdateParcelLocation(idParcel, slotId);

        // Move backward away from the shelf
        yield return StartCoroutine(MoveBackwards(-qrCodeDirection, takeBoxDistance));
        // Lower the mast to the default position
        yield return MoveToOriginPosition();
        yield return StartCoroutine(LiftMastToHeight(0));
        // Notifica il completamento del compito
        OnTaskCompleted?.Invoke();
    }

    public IEnumerator PickParcelFromShelf(Vector3 parcelPosition, Vector3 conveyorDestination)
    {
        string positionId = "shipping_" + conveyorDestination.z.ToString();
        _ = neo4jHelper.UpdateParcelPositionStatusAsync(positionId, true);
        Vector3 qrCodeDirection = Vector3.forward;
        Vector3 approachPosition = parcelPosition + qrCodeDirection * approachDistance;
        approachPosition.y = transform.position.y;

        // Move to the approach position
        yield return MoveToPosition(approachPosition);
        // Rotate to face the parcel
        yield return SmoothRotateToDirection(-qrCodeDirection);

        // Find the parcel GameObject based on its position
        GameObject parcel = GetParcel(parcelPosition.y + 1);
        if (parcel == null)
        {
            Debug.LogWarning("Parcel non trovato. Ritorno alla posizione originale.");
            yield return StartCoroutine(MoveToOriginPosition());
            yield break;
        }
        yield return LiftMastToHeight(parcelPosition.y);
        yield return MoveToPosition(approachPosition - qrCodeDirection * takeBoxDistance);
        yield return StartCoroutine(SmoothRotateToDirection(-qrCodeDirection));
        yield return LiftMastToHeight(parcelPosition.y + 0.05f);
        parcel.transform.SetParent(grabPoint);
        yield return MoveBackwards(-qrCodeDirection, takeBoxDistance);
        yield return LiftMastToHeight(2);
        
        qrCodeDirection = Vector3.right;
        approachPosition = conveyorDestination + qrCodeDirection * approachDistance;
        yield return MoveToPosition(approachPosition);
        yield return SmoothRotateToDirection(-qrCodeDirection);
        approachPosition.x -= takeBoxDistance;
        yield return StartCoroutine(MoveToPosition(approachPosition));
        yield return StartCoroutine(LiftMastToHeight(conveyorDestination.y)); 
        parcel.transform.SetParent(null);
        yield return MoveBackwards(-qrCodeDirection, takeBoxDistance);
        yield return MoveToOriginPosition();
        yield return StartCoroutine(LiftMastToHeight(0));
    }
    public IEnumerator MoveToOriginPosition()
    {
        robotMovement.MoveWithCollisionPrevention(defaultPosition);
        yield return new WaitUntil(() => robotMovement.HasArrivedAtDestination());
        Vector3 targetForward = Vector3.back; // Ruota di 180° rispetto all'asse Y
        yield return StartCoroutine(SmoothRotateToDirection(targetForward, 1f)); // Rotazione graduale
    }

    
    public IEnumerator MoveToPosition(Vector3 position)
    {
        robotMovement.MoveWithCollisionPrevention(position);
        yield return new WaitUntil(() => robotMovement.HasArrivedAtDestination());
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
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error updating parcel location: {ex.Message}");
        }
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

    public Vector3 GetPosition() {
        return robotMovement.GetPosition();
    }
}