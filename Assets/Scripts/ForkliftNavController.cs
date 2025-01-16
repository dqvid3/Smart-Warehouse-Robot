using UnityEngine;
using System.Collections;
using Neo4j.Driver;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

public class ForkliftNavController : MonoBehaviour
{
    Vector3 slotPosition;
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
    private Vector3 backupPosition;

    private void Awake()
    {
        defaultPosition = transform.position;
    }

    async void Start()
    {
        neo4jHelper = new Neo4jHelper("bolt://localhost:7687", "neo4j", "password");
        qrReader = GetComponent<QRCodeReader>();
        forkliftController = GetComponent<ForkliftController>();
        explainability = GetComponent<RobotExplainability>();
        backupPosition = await neo4jHelper.GetBackupPosition();
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
        yield return StartCoroutine(MoveBackwards(-qrCodeDirection, takeBoxDistance));

        IRecord placedRecord = null;
        bool usedBackupShelf = false; // Inizializzata a false

        yield return StartCoroutine(PlaceParcelOnShelf(category, robotId, timestamp, (record, isBackup) => {
            placedRecord = record;
            usedBackupShelf = isBackup; 
        }));

        // Aggiornamento della posizione del pacco nel database
        if (placedRecord != null)
        {
            float? expirationDuration = usedBackupShelf == true ? 5f : null;
            _ = UpdateParcelLocation(timestamp, placedRecord["slotId"].As<long>(), expirationDuration);
        }

        if (!robotManager.AreThereTask())
        {
            yield return StartCoroutine(LiftMastToHeight(0));
            yield return StartCoroutine(MoveToOriginPosition());
        }
    }

    public IEnumerator PlaceParcelOnShelf(string category, int robotId, string timestamp, Action<IRecord, bool> onComplete = null)
    {
        // Trova uno slot disponibile e senza ostacoli
        IRecord record = null;
        bool hasObstacle = true;
        bool useBackupShelf = false;
        Vector3 qrCodeDirection = Vector3.forward;
        Vector3 approachPosition = Vector3.zero;
        while (hasObstacle)
        {
            // Cerca uno slot disponibile
            if (!useBackupShelf)
            {
                record = robotManager.AskSlot(category, robotId);
                if (record == null)
                {
                    if (onComplete == null){ // se si sta facendo disposal allora non voglio riusare il backupshelf
                        yield return GetParcelBack(robotId, timestamp);
                        yield break;
                    }
                    else
                    {
                        explainability.ShowExplanation("Nessun slot disponibile nello scaffale principale. Uso lo scaffale di backup.");
                        useBackupShelf = true;
                    }
                    continue;
                }
            }
            else
            {
                record = robotManager.AskSlot("Backup", robotId);
                if (record == null)
                {
                    yield return GetParcelBack(robotId, timestamp);
                    yield break;
                }
            }

            // Ottieni la posizione dello slot
            slotPosition = GetSlotPosition(record);
            approachPosition = slotPosition + qrCodeDirection * approachDistance;
            approachPosition.y = transform.position.y;

            // Spostamento verso lo scaffale
            yield return StartCoroutine(MoveToPosition(approachPosition));
            explainability.ShowExplanation("Sto raggiungendo lo scaffale per posare la box.");

            // Rotazione verso lo scaffale e controllo ostacoli
            yield return StartCoroutine(SmoothRotateToDirection(-qrCodeDirection));
            yield return StartCoroutine(LiftMastToHeight(slotPosition.y - .95f));

            // Controllo ostacoli
            yield return new WaitForSeconds(1f);
            hasObstacle = CheckForObstacle(slotPosition);
            if (hasObstacle)
            {
                explainability.ShowExplanation("Trovato un ostacolo nello slot. Cerco un nuovo slot.");
            }
        }
        // Posizionamento del pacco
        yield return StartCoroutine(LiftMastToHeight(slotPosition.y + 0.05f));
        yield return StartCoroutine(MoveTakeBoxDistance(approachPosition, -qrCodeDirection));
        yield return StartCoroutine(LiftMastToHeight(slotPosition.y - 0.05f));

        // Stacco del pacco
        GameObject parcel = grabPoint.GetChild(1).gameObject;
        parcel.transform.SetParent(null);
        parcelRigidbody = parcel.GetComponent<Rigidbody>();
        parcelRigidbody.isKinematic = false;
        parcelRigidbody.constraints = RigidbodyConstraints.None;

        explainability.ShowExplanation("Box posata sullo scaffale.");

        // Allontanamento dallo scaffale
        yield return StartCoroutine(MoveBackwards(-qrCodeDirection, takeBoxDistance));
        robotManager.NotifyTaskCompletion(robotId);
        onComplete?.Invoke(record, useBackupShelf);
    }

    public IEnumerator GetParcelBack(int robotId, string timestamp){
        explainability.ShowExplanation("Nessun slot disponibile nello scaffale principale. Mando il pacco indietro.");
        yield return StartCoroutine(PlaceParcelOnConveyor(robotId, backupPosition));
        _ = neo4jHelper.DeleteParcel(timestamp);
        if (!robotManager.AreThereTask())
        {
            yield return StartCoroutine(LiftMastToHeight(0));
            yield return StartCoroutine(MoveToOriginPosition());
        }
    }

    public IEnumerator ShipParcel(Vector3 slotPosition, int robotId)
    {

        Vector3 conveyorDestination = robotManager.AskConveyorPosition();

        yield return TakeParcelFromShelf(slotPosition, robotId);

        yield return PlaceParcelOnConveyor(robotId, conveyorDestination);

        if (!robotManager.AreThereTask())
        {
            yield return StartCoroutine(LiftMastToHeight(0));
            yield return StartCoroutine(MoveToOriginPosition());
        }
    }

    public IEnumerator TakeParcelFromShelf(Vector3 slotPosition, int robotId)
    {
        Vector3 qrCodeDirection = Vector3.forward;
        Vector3 approachPosition = slotPosition + qrCodeDirection * approachDistance;
        approachPosition.y = transform.position.y;

        // Spostamento verso la posizione di approccio
        yield return MoveToPosition(approachPosition);
        explainability.ShowExplanation("Raggiungo lo scaffale per prelevare la box.");

        // Rotazione per affrontare lo scaffale
        yield return SmoothRotateToDirection(-qrCodeDirection);
        explainability.ShowExplanation("Mi oriento verso lo scaffale.");

        // Trova il GameObject del pacco basato sulla sua posizione
        GameObject parcel = GetParcel(slotPosition.y);

        // Allineamento e sollevamento del pacco
        yield return LiftMastToHeight(slotPosition.y);
        yield return StartCoroutine(MoveTakeBoxDistance(approachPosition, -qrCodeDirection));
        yield return LiftMastToHeight(slotPosition.y + 0.05f);
        explainability.ShowExplanation("Sto sollevando la box.");

        // Aggancio del pacco al punto di presa
        parcel.transform.SetParent(grabPoint);
        parcelRigidbody = parcel.GetComponent<Rigidbody>();
        parcelRigidbody.isKinematic = true;
        parcelRigidbody.constraints = RigidbodyConstraints.FreezePosition | RigidbodyConstraints.FreezeRotation;

        // Allontanamento dallo scaffale
        yield return MoveBackwards(-qrCodeDirection, takeBoxDistance);
        explainability.ShowExplanation("Mi allontano dallo scaffale con la box.");
        robotManager.FreeSlotPosition(robotId);
        _ = FreeSlot(slotPosition);
    }

    public IEnumerator PlaceParcelOnConveyor(int robotId, Vector3 conveyorDestination)
    {
        robotManager.AssignConveyorPosition(robotId, conveyorDestination);
        // Direzione e posizione di approccio al nastro trasportatore
        Vector3 qrCodeDirection = Vector3.right;
        Vector3 approachPosition = conveyorDestination + qrCodeDirection * approachDistance;
        approachPosition.y = transform.position.y;

        // Spostamento verso la posizione di approccio
        yield return MoveToPosition(approachPosition);
        explainability.ShowExplanation("Raggiungo il nastro trasportatore per posare la box.");

        // Rotazione per affrontare il nastro trasportatore
        yield return SmoothRotateToDirection(-qrCodeDirection);
        explainability.ShowExplanation("Mi oriento verso il nastro trasportatore.");

        // Allineamento e posizionamento del pacco
        yield return StartCoroutine(MoveTakeBoxDistance(approachPosition, -qrCodeDirection));
        yield return StartCoroutine(LiftMastToHeight(conveyorDestination.y));

        // Stacco del pacco
        GameObject parcel = grabPoint.GetChild(1).gameObject;
        parcel.transform.SetParent(null);
        parcelRigidbody = parcel.GetComponent<Rigidbody>();
        parcelRigidbody.isKinematic = false;
        parcelRigidbody.constraints = RigidbodyConstraints.None;

        explainability.ShowExplanation("Box posata sul nastro trasportatore.");

        // Allontanamento dal nastro trasportatore
        yield return MoveBackwards(-qrCodeDirection, takeBoxDistance);

        // Notifica al gestore dei robot che il task è completato
        robotManager.NotifyTaskCompletion(robotId);
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

    private bool HasObstacleBehind()
    {
        Vector3 boxSize = new Vector3(4f, 4f, 4f); // Larghezza, altezza, profondità. Scatola di controllo ostacolo dietro il robot

        // Posizione del centro della scatola (dietro il robot)
        Vector3 boxCenter = transform.position - transform.forward * 0.8f; // Leggermente dietro al robot
        boxCenter.y += boxSize.y / 2f; // Al centro dell'altezza

        // Debug visivo: disegna la scatola nella scena
        Debug.DrawRay(boxCenter, Vector3.up * boxSize.y, Color.red, 0.1f); 
        Debug.DrawRay(boxCenter, -transform.forward * boxSize.z, Color.red, 0.1f); // Linea dietro

        // Controlla i collider all'interno della scatola
        Collider[] colliders = Physics.OverlapBox(boxCenter, boxSize / 2, transform.rotation, layerMask);

        // Restituisce true se trova ostacoli
        return colliders.Length > 0;
    }



    private IEnumerator MoveBackwards(Vector3 direction, float distance)
    {
        // 1) Aspetta mezzo secondo per vedere possibili ostacoli dietro
        explainability.ShowExplanation("Controllo ostacoli dietro...");
        yield return new WaitForSeconds(2f);

        // Controllo iniziale: se c'è qualcosa dietro, aspetta
        while (HasObstacleBehind())
        {
            explainability.ShowExplanation("Ostacolo rilevato dietro. Aspetto che si liberi...");
            yield return new WaitForSeconds(0.5f);
        }

        // 2) Calcolo del target
        Vector3 startPosition = transform.position;
        Vector3 targetPosition = startPosition - direction * distance;
        float speed = 3f;

        // 3) Movimento effettivo
        while (Vector3.Distance(transform.position, targetPosition) > 0.1f)
        {
            // Controllo durante il movimento
            if (HasObstacleBehind())
            {
                explainability.ShowExplanation("Ostacolo improvviso dietro. Mi fermo...");
                while (HasObstacleBehind())
                {
                    yield return new WaitForSeconds(0.5f);
                }
                explainability.ShowExplanation("Zona dietro libera. Riprendo la retromarcia.");
            }

            // Movimento verso la posizione target
            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                speed * Time.deltaTime
            );

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

    private async Task UpdateParcelLocation(string parcelTimestamp, long slotId, float? expirationDuration = null)
    {
        string query = @"
        MATCH (p:Parcel {timestamp: $parcelTimestamp})-[r:LOCATED_IN]->(d:Area {type: 'Delivery'})
        DELETE r
        WITH p
        MATCH (s:Slot) WHERE ID(s) = $slotId
        FOREACH (_ IN CASE WHEN $expirationDuration IS NOT NULL THEN [1] ELSE [] END |
            SET p.expirationTime = timestamp() + $expirationDuration
        )
        CREATE (s)-[:CONTAINS]->(p)";

        var parameters = new Dictionary<string, object>
        {
            { "parcelTimestamp", parcelTimestamp },
            { "slotId", slotId },
            { "expirationDuration", expirationDuration.HasValue ? (long?)(expirationDuration.Value * 1000) : null }
        };

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
        Vector3 boxSize = new(1.5f, 1.35f, 1.4f); // Regola le dimensioni in base alle necessità

        // Calcoliamo il centro del box correttamente
        Vector3 boxCenter = new(slotPosition.x, slotPosition.y + boxSize.y / 2, slotPosition.z);

        DrawBox(boxCenter, boxSize, Color.red, 1f);

        // Controlliamo se ci sono collider all'interno del box
        Collider[] colliders = Physics.OverlapBox(boxCenter, boxSize / 2, Quaternion.identity, layerMask);
        // Restituiamo true se ci sono oggetti nel box, altrimenti false
        return colliders.Length > 0;
    }

     private void DrawBox(Vector3 center, Vector3 size, Color color, float duration)
    {
        Vector3 halfSize = size / 2;

        // Calcola gli 8 vertici del box
        Vector3[] vertices = new Vector3[8]
        {
            center + new Vector3(-halfSize.x, -halfSize.y, -halfSize.z),
            center + new Vector3(halfSize.x, -halfSize.y, -halfSize.z),
            center + new Vector3(halfSize.x, halfSize.y, -halfSize.z),
            center + new Vector3(-halfSize.x, halfSize.y, -halfSize.z),
            center + new Vector3(-halfSize.x, -halfSize.y, halfSize.z),
            center + new Vector3(halfSize.x, -halfSize.y, halfSize.z),
            center + new Vector3(halfSize.x, halfSize.y, halfSize.z),
            center + new Vector3(-halfSize.x, halfSize.y, halfSize.z)
        };

        // Disegna le linee del box
        Debug.DrawLine(vertices[0], vertices[1], color, duration);
        Debug.DrawLine(vertices[1], vertices[2], color, duration);
        Debug.DrawLine(vertices[2], vertices[3], color, duration);
        Debug.DrawLine(vertices[3], vertices[0], color, duration);

        Debug.DrawLine(vertices[4], vertices[5], color, duration);
        Debug.DrawLine(vertices[5], vertices[6], color, duration);
        Debug.DrawLine(vertices[6], vertices[7], color, duration);
        Debug.DrawLine(vertices[7], vertices[4], color, duration);

        Debug.DrawLine(vertices[0], vertices[4], color, duration);
        Debug.DrawLine(vertices[1], vertices[5], color, duration);
        Debug.DrawLine(vertices[2], vertices[6], color, duration);
        Debug.DrawLine(vertices[3], vertices[7], color, duration);
    }
}