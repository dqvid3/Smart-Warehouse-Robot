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
    private float takeBoxDistance = 1.35f;
    private Neo4jHelper neo4jHelper;
    private QRCodeReader qrReader;
    public Vector3 defaultPosition;
    private RobotExplainability explainability;

    private void Awake()
    {
        defaultPosition = transform.position;
    }

    void Start()
    {
        neo4jHelper = new Neo4jHelper("bolt://localhost:7687", "neo4j", "password");
        qrReader = GetComponent<QRCodeReader>();
        agent = GetComponent<NavMeshAgent>();
        forkliftController = GetComponent<ForkliftController>();
        explainability = GetComponent<RobotExplainability>();
    }

    public IEnumerator PickParcelFromDelivery(Vector3 parcelPosition, Action<string> callback)
    {
        Vector3 qrCodeDirection = Vector3.left;
        Vector3 approachPosition = parcelPosition + qrCodeDirection * approachDistance;
        approachPosition.y = transform.position.y;

        // Spostamento verso la box
        explainability.ShowExplanation("Mi sto dirigendo verso la box per iniziare il prelievo.");
        yield return StartCoroutine(MoveToPosition(approachPosition));

        // Rotazione per leggere il QR code
        explainability.ShowExplanation("Sto ruotando per leggere il QR code della box.");
        yield return StartCoroutine(SmoothRotateToDirection(-qrCodeDirection));

        // Lettura del QR code
        string qrCode = qrReader.ReadQRCode();
        string[] qrParts = qrCode.Split('|');
        string category = qrParts[1];
        explainability.ShowExplanation($"QR code letto. Categoria della box: {category}.");
        callback(qrCode);
    }

    public IEnumerator DeliverToShelf(Vector3 parcelPosition, IRecord record, string timestamp)
    {
        Vector3 qrCodeDirection = Vector3.left;
        Vector3 approachPosition = parcelPosition + qrCodeDirection * approachDistance;
        approachPosition.y = transform.position.y;
        // Sollevamento della box
        GameObject parcel = GetParcel(parcelPosition.y + 1);
        yield return StartCoroutine(LiftMastToHeight(parcelPosition.y));
        explainability.ShowExplanation("Sto sollevando la box.");

        // Avvicinamento alla box e prelievo
        yield return StartCoroutine(MoveToPosition(approachPosition - qrCodeDirection * takeBoxDistance));
        yield return StartCoroutine(LiftMastToHeight(parcelPosition.y + 1));
        parcel.transform.SetParent(grabPoint);
        explainability.ShowExplanation("Box prelevata. Mi sposto verso lo scaffale corretto.");
        // Cambio direzione per lo scaffale
        qrCodeDirection = Vector3.forward;
        Vector3 slotPosition = GetSlotPosition(record);
        float shelfHeight = slotPosition.y;
        slotPosition.y = transform.position.y;
        approachPosition = slotPosition + qrCodeDirection * approachDistance;

        // Spostamento verso lo scaffale
        yield return StartCoroutine(MoveToPosition(approachPosition));
        explainability.ShowExplanation("Sto raggiungendo lo scaffale per posare la box.");

        // Rotazione verso lo scaffale e posizionamento
        yield return StartCoroutine(SmoothRotateToDirection(-qrCodeDirection));
        yield return StartCoroutine(LiftMastToHeight(shelfHeight + 0.05f));
        yield return StartCoroutine(MoveToPosition(approachPosition - qrCodeDirection * takeBoxDistance));
        yield return StartCoroutine(LiftMastToHeight(shelfHeight - 0.05f));
        parcel.transform.SetParent(null);
        explainability.ShowExplanation("Box posata sullo scaffale. Sto tornando in posizione di standby.");

        // Ritorno alla posizione originale
        _ = UpdateParcelLocation(timestamp, record["slotId"].As<long>());
        yield return StartCoroutine(MoveBackwards(-qrCodeDirection, takeBoxDistance));
        yield return StartCoroutine(LiftMastToHeight(0));
        yield return StartCoroutine(MoveToOriginPosition());
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
        GameObject parcel = GetParcel(slotPosition.y);
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
        yield return StartCoroutine(MoveToOriginPosition());
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


    public IEnumerator SmoothRotateToDirection(Vector3 targetForward, float rotationSpeed = 1f)
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
        WHERE abs(s.x + slot.x - $x) < 0.01 AND abs(l.y - $y) < 0.01 AND abs(s.z - $z) < 0.01
        MATCH (slot)-[r:CONTAINS]->(p:Parcel)
        DELETE r
        SET slot.occupied = false";

        var parameters = new Dictionary<string, object>
        {
            { "x", slotPosition.x },
            { "y", slotPosition.y },
            { "z", slotPosition.z }
        };
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