using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using Neo4j.Driver;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

public class ForkliftNavController : MonoBehaviour
{
    public RobotManager robotManager;
    public MovementWithAStar movementWithAStar;
    [SerializeField] private LayerMask layerMask; // Layer mask for detecting parcels
    [SerializeField] private Transform grabPoint;
    private ForkliftController forkliftController;
    private float approachDistance = 3.2f;
    private float takeBoxDistance = 1.3f;
    private Neo4jHelper neo4jHelper;
    private QRCodeReader qrReader;
    public Vector3 defaultPosition;
    private RobotExplainability explainability;
    private Rigidbody parcelRigidbody;

    private void Awake()
    {
        defaultPosition = transform.position;
    }

    void Start()
    {
        neo4jHelper = new Neo4jHelper("bolt://localhost:7687", "neo4j", "password");
        qrReader = GetComponent<QRCodeReader>();
        forkliftController = GetComponent<ForkliftController>();
        explainability = GetComponent<RobotExplainability>();
    }

    public IEnumerator PickParcelFromDelivery(Vector3 parcelPosition, int robotId)
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
        string timestamp = qrParts[0];
        string category = qrParts[1];
        explainability.ShowExplanation($"QR code letto. Categoria della box: {category}.");

        // Allineo le aste alla box
        GameObject parcel = GetParcel(parcelPosition.y + 1);
        yield return StartCoroutine(LiftMastToHeight(parcelPosition.y));
        explainability.ShowExplanation("Allineo le aste alla box.");

        // Avvicinamento alla box e prelievo
        yield return StartCoroutine(MoveTakeBoxDistance(approachPosition, -qrCodeDirection));
        yield return StartCoroutine(LiftMastToHeight(parcelPosition.y + 1));

        parcel.transform.SetParent(grabPoint);
        parcelRigidbody = parcel.GetComponent<Rigidbody>();
        parcelRigidbody.isKinematic = true;
        parcelRigidbody.constraints = RigidbodyConstraints.FreezePosition | RigidbodyConstraints.FreezeRotation;

        explainability.ShowExplanation("Box prelevata. Mi sposto verso lo scaffale corretto.");

        // Trova uno slot disponibile e senza ostacoli
        IRecord record = null;
        float shelfHeight = 0;
        bool hasObstacle = true;
        bool useBackupShelf = false; // Flag per indicare se stiamo usando lo scaffale di backup
        // Cambio direzione per lo scaffale
        qrCodeDirection = Vector3.forward;
        while (hasObstacle)
        {
            // Cerca uno slot disponibile
            if (!useBackupShelf)
            {
                record = robotManager.AskSlot(category, robotId);
                if (record == null)
                {
                    explainability.ShowExplanation("Nessun slot disponibile nello scaffale principale. Uso lo scaffale di backup.");
                    useBackupShelf = true; // Passa allo scaffale di backup
                    continue;
                }
            }
            else
            {
                record = robotManager.AskSlot("Backup", robotId);
                if (record == null)
                {
                    explainability.ShowExplanation("Nessuno scaffale di backup disponibile. Impossibile completare il task.");
                    yield break; // Esci dalla coroutine se non ci sono slot disponibili
                }
            }

            // Ottieni la posizione dello slot
            Vector3 slotPosition = GetSlotPosition(record);
            shelfHeight = slotPosition.y;
            slotPosition.y = transform.position.y;
            approachPosition = slotPosition + qrCodeDirection * approachDistance;
            // Spostamento verso lo scaffale
            yield return StartCoroutine(MoveToPosition(approachPosition));
            explainability.ShowExplanation("Sto raggiungendo lo scaffale per posare la box.");

            // Rotazione verso lo scaffale e controllo ostacoli
            yield return StartCoroutine(SmoothRotateToDirection(-qrCodeDirection));
            yield return StartCoroutine(LiftMastToHeight(shelfHeight - 1.5f));

            // Controllo se c'è un ostacolo nello slot
            slotPosition.y = shelfHeight;
            hasObstacle = CheckForObstacle(slotPosition);
            if (hasObstacle){
                explainability.ShowExplanation("Trovato un ostacolo nello slot. Cerco un nuovo slot.");
            }
        }

        // Posizionamento della box
        yield return StartCoroutine(LiftMastToHeight(shelfHeight + 0.05f));
        yield return StartCoroutine(MoveTakeBoxDistance(approachPosition, -qrCodeDirection));
        yield return StartCoroutine(LiftMastToHeight(shelfHeight - 0.05f));
        parcel.transform.SetParent(null);
        parcelRigidbody = parcel.GetComponent<Rigidbody>();
        parcelRigidbody.isKinematic = false;
        parcelRigidbody.constraints = RigidbodyConstraints.None;

        explainability.ShowExplanation("Box posata sullo scaffale. Sto tornando in posizione di standby.");

        // Ritorno alla posizione originale
        _ = UpdateParcelLocation(timestamp, record["slotId"].As<long>());
        yield return StartCoroutine(MoveBackwards(-qrCodeDirection, takeBoxDistance));
        robotManager.NotifyTaskCompletion(robotId);

        if (!robotManager.AreThereTask())
        {
            yield return StartCoroutine(LiftMastToHeight(0));
            yield return StartCoroutine(MoveToOriginPosition());
        }
    }
    public IEnumerator ShipParcel(Vector3 slotPosition, int robotId)
    {
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
        yield return StartCoroutine(MoveTakeBoxDistance(approachPosition, -qrCodeDirection));
        yield return StartCoroutine(SmoothRotateToDirection(-qrCodeDirection));
        yield return LiftMastToHeight(slotPosition.y + 0.05f);

        parcel.transform.SetParent(grabPoint);
        Rigidbody parcelRigidbody = parcel.GetComponent<Rigidbody>();
        parcelRigidbody.isKinematic = true;
        parcelRigidbody.constraints = RigidbodyConstraints.FreezePosition | RigidbodyConstraints.FreezeRotation;
        yield return MoveBackwards(-qrCodeDirection, takeBoxDistance);

        Vector3 conveyorDestination = robotManager.AskConveyorPosition();
        qrCodeDirection = Vector3.right;
        float heightConveyor = conveyorDestination.y;
        conveyorDestination.y = 0;
        approachPosition = conveyorDestination + qrCodeDirection * approachDistance;
        yield return MoveToPosition(approachPosition);
        robotManager.FreeSlotPosition(robotId, conveyorDestination);
        _ = FreeSlot(slotPosition);
        yield return SmoothRotateToDirection(-qrCodeDirection);
        //approachPosition.x -= takeBoxDistance;
        yield return StartCoroutine(MoveTakeBoxDistance(approachPosition, -qrCodeDirection));
        yield return StartCoroutine(LiftMastToHeight(heightConveyor));
        parcel.transform.SetParent(null);
        parcelRigidbody = parcel.GetComponent<Rigidbody>();
        parcelRigidbody.isKinematic = false;
        parcelRigidbody.constraints = RigidbodyConstraints.None;

        yield return MoveBackwards(-qrCodeDirection, takeBoxDistance);
        robotManager.NotifyTaskCompletion(robotId);

        if (!robotManager.AreThereTask())
        {
            yield return StartCoroutine(LiftMastToHeight(0));
            yield return StartCoroutine(MoveToOriginPosition());
        }
    }

    public IEnumerator MoveToOriginPosition()
    {
        yield return MoveToPosition(defaultPosition);
        yield return StartCoroutine(SmoothRotateToDirection(Vector3.back));
    }

    public IEnumerator MoveToPosition(Vector3 position)
    {
        yield return movementWithAStar.MovementToPosition(position);
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
        float speed = 3f;
        while (Vector3.Distance(transform.position, targetPosition) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);
            yield return new WaitForFixedUpdate();
        }
    }

    public IEnumerator MoveTakeBoxDistance(Vector3 approachPosition, Vector3 qrCodeDirection, float speed = 2f)
    {
        Vector3 targetPosition = approachPosition + (qrCodeDirection * takeBoxDistance);

        while (Vector3.Distance(transform.position, targetPosition) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);
            yield return null;
        }
        transform.position = targetPosition;
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
        CREATE (s)-[:CONTAINS]->(p)";
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

    private bool CheckForObstacle(Vector3 slotPosition)
    {
        Vector3 rayOrigin = slotPosition - Vector3.forward * .75f;
        rayOrigin.y += 0.75f;
        Vector3 rayDirection = Vector3.forward;
        float rayLength = 1.5f; // Lunghezza del raggio
        Debug.Log($"Origine del raggio: {rayOrigin}");
        Debug.DrawRay(rayOrigin, rayDirection * rayLength, Color.red, 2f); // Debug per visualizzare il raggio
        if (Physics.Raycast(rayOrigin, rayDirection, out RaycastHit hit, rayLength, layerMask))
        {
            Debug.Log($"Ostacolo trovato: {hit.collider.gameObject.name}");
            return true;
        }
        return false;
    }
}