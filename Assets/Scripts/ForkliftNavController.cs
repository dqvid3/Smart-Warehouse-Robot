﻿using UnityEngine;
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
    private bool isCarryingBox = false;
    private float approachDistance = 3.2f;
    private float takeBoxDistance = 1.2f;
    private Neo4jHelper neo4jHelper;
    private QRCodeReader qrReader;
    private float checkInterval = 2; // Time in seconds between checks for new parcels
    private float lastCheckTime = 0f;
    private Vector3 defaultPosition = Vector3.zero;

    // Evento per notificare il completamento del compito
    public event Action OnTaskCompleted;

    void Start()
    {
        neo4jHelper = new Neo4jHelper("bolt://localhost:7687", "neo4j", "password");
        qrReader = GetComponent<QRCodeReader>();
        agent = GetComponent<NavMeshAgent>();
        forkliftController = GetComponent<ForkliftController>();
    }

    void Update()
    {
    }

    public IEnumerator PickParcelFromDelivery(Vector3 parcelPosition)
    {
        isCarryingBox = true;
        Vector3 qrCodeDirection = Vector3.left;
        Vector3 approachPosition = parcelPosition + qrCodeDirection * approachDistance;
        approachPosition.y = transform.position.y;

        // Move to the approach position
        yield return StartCoroutine(MoveToPosition(approachPosition));
        // Rotate to face the parcel
        yield return StartCoroutine(SmoothRotateToDirection(-qrCodeDirection));

        // Read the QR code
        string qrCode = qrReader.ReadQRCode();
        // Extract timestamp and category from the QR code
        string[] qrParts = qrCode.Split('|');
        if (qrParts.Length < 2)
        {
            Debug.LogWarning("Invalid QR code format.");
            yield break;
        }
        string timestamp = qrParts[0];
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
        // Change the QR code direction for the shelf approach
        qrCodeDirection = Vector3.forward;
        // Lift the mast further for safe transport
        yield return StartCoroutine(LiftMastToHeight(parcelPosition.y + 1));
        // Get an available slot from the database based on the parcel's category
        IList<IRecord> result = Task.Run(() => GetAvailableSlot(category)).Result;
        if (result.Count == 0)
        {
            Debug.LogWarning($"No available slot found for category {category}");
            yield break;
        }
        // Get the position of the slot
        Vector3 slotPosition = GetSlotPosition(result[0]);
        float shelfHeight = slotPosition.y;
        slotPosition.y = transform.position.y;
        // Calculate the approach position for the shelf
        approachPosition = slotPosition + qrCodeDirection * approachDistance;
        // Move to the approach position for the shelf
        yield return StartCoroutine(MoveToPosition(approachPosition));
        // Rotate to face the shelf
        yield return StartCoroutine(SmoothRotateToDirection(-qrCodeDirection));
        // Lift the mast to the height of the shelf layer
        yield return StartCoroutine(LiftMastToHeight(shelfHeight));
        // Move forward to place the parcel
        yield return StartCoroutine(MoveToPosition(approachPosition - qrCodeDirection * takeBoxDistance));
        // Lower the mast slightly to release the parcel
        yield return StartCoroutine(LiftMastToHeight(shelfHeight - 0.1f));
        // Visually detach the parcel
        parcel.transform.SetParent(null);
        // Update the parcel's location in the database
        _ = UpdateParcelLocation(timestamp, result[0]["slotId"].As<long>());
        // Move backward away from the shelf
        yield return StartCoroutine(MoveBackwards(-qrCodeDirection, takeBoxDistance));
        // Lower the mast to the default position
        yield return StartCoroutine(LiftMastToHeight(0));
        isCarryingBox = false; // Ready to pick up another parcel
        yield return StartCoroutine(MoveToPosition(defaultPosition));

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
        yield return MoveToPosition(defaultPosition);
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

    private async Task<IList<IRecord>> GetAvailableSlot(string category)
    {
        string query = @"
        MATCH (s:Shelf {category: $category})-[:HAS_LAYER]->(l:Layer)-[:HAS_SLOT]->(slot:Slot)
        WHERE NOT (slot)-[:CONTAINS]->(:Parcel)
        RETURN s.x + slot.x AS x, l.y AS y, s.z AS z, ID(slot) AS slotId
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