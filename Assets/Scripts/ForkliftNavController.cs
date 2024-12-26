using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using Neo4j.Driver;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

public class ForkliftNavController : MonoBehaviour
{
    [Header("NavMesh Settings")]
    public NavMeshAgent agent;
    public Transform shippingPoint;
    public ForkliftController forkliftController;
    [SerializeField] private LayerMask layerMask;
    private bool isCarryingBox = false;
    private float approachDistance = 3.2f; // Distanza da mantenere per considerare le pale
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
        Vector3 pos = parcel.transform.position;
        Vector3 approachPosition = new(pos.x, transform.position.y, pos.z);
        Vector3 qrCodeDirection = new(1, 0, 0);
        if (pos.y > 0.1f) // il pacco è in uno scaffale
            qrCodeDirection = new Vector3(0, 0, -1);
        approachPosition -= qrCodeDirection * approachDistance;

        // 1. Avvicinati alla box da lontano
        agent.SetDestination(approachPosition);
        yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance);

        // 2. Ruota di fronte al box
        yield return StartCoroutine(SmoothRotateToDirection(qrCodeDirection));
        IList<IRecord> result = null;
        if (pos.y < 0.1f)
        {
            string category = qrReader.ReadQRCode().Split('|')[1]; // prende la categoria dal QR code
            string query = @"MATCH (s:Shelf {category: $category})-[:HAS_LAYER]->(l:Layer)-[:HAS_SLOT]->(slot:Slot)
                WHERE NOT (slot)-[:CONTAINS]->(:Product)
                RETURN s.x + slot.z AS x, l.y AS y, s.z AS z, ID(slot) AS slotId
                LIMIT 1";
            // La x è s.x + slot.z perchè gli scaffali sono ruotati di 90 e slot.z è la pos relativa allo scaffale
            result = Task.Run(() => neo4jHelper.ExecuteReadListAsync(query, new Dictionary<string, object> { { "category", category } })).Result;
        }

        // 3. Solleva un po' il mast
        yield return StartCoroutine(forkliftController.LiftMastToHeight(pos.y));

        // 4. Vai avanti per prendere la box
        approachPosition += qrCodeDirection * takeBoxDistance;
        agent.SetDestination(approachPosition);
        yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance);

        // 5. Solleva un po' la box
        yield return StartCoroutine(forkliftController.LiftMastToHeight(pos.y + 0.5f));
        parcel.transform.SetParent(forkliftController.grabPoint);
        isCarryingBox = true;
        agent.ResetPath();

        if (pos.y > 0.1f)
        {
            // 6. Torna indietro linearmente se il pacco è in uno scaffale
            yield return StartCoroutine(MoveBackwards(qrCodeDirection, takeBoxDistance));
            // 7. Abbassa i mast
            yield return StartCoroutine(forkliftController.LiftMastToHeight(0));
        }
        else
        {
            float x = result[0][0].As<float>();
            float y = result[0][1].As<float>();
            float z = result[0][2].As<float>();
            string slotId = result[0][3].As<string>();
            Vector3 slotPosition = new(x, transform.position.y, z);
            slotPosition.z += approachDistance;
            agent.SetDestination(slotPosition);
            yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance);
            yield return StartCoroutine(SmoothRotateToDirection(new Vector3(0, 0, -1)));
            yield return StartCoroutine(forkliftController.LiftMastToHeight(y));
            slotPosition.z -= takeBoxDistance;
            agent.SetDestination(slotPosition);
            yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance);
            parcel.transform.SetParent(null);
            isCarryingBox = false;
            yield return StartCoroutine(forkliftController.LiftMastToHeight(y - 0.1f));
            agent.ResetPath();
            yield return StartCoroutine(MoveBackwards(new Vector3(0, 0, -1), takeBoxDistance));
            yield return StartCoroutine(forkliftController.LiftMastToHeight(0));
        }

        // logica per dirgli di portarlo in un punto di spedizione
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

    private IEnumerator MoveBackwards(Vector3 direction, float distance)
    {
        float speed = agent.speed; // Usa la velocità dell'agente
        Vector3 startPosition = transform.position;
        Vector3 targetPosition = startPosition - direction * distance;

        while (Vector3.Distance(transform.position, targetPosition) > 0.1f)
        {
            // Muovi il muletto all'indietro in modo lineare
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);
            yield return new WaitForFixedUpdate();
        }
    }
    /*
        bool DetectBoxInFront(Transform grabPoint, float maxDistance)
        {
            if (grabPoint == null)
            {
                Debug.LogError("Grab point non valido per il rilevamento della box!");
                return false;
            }

            Vector3 rayOrigin = grabPoint.position;
            Ray forwardRay = new Ray(rayOrigin, grabPoint.forward);

            Debug.DrawRay(rayOrigin, grabPoint.forward * maxDistance, Color.red, 2.0f);

            if (Physics.Raycast(forwardRay, out RaycastHit hit, maxDistance, boxLayerMask))
            {
                if (hit.collider.CompareTag("Grabbable"))
                {
                    targetBox = hit.collider.gameObject;
                    Debug.Log("Box trovata frontalmente!");
                    return true;
                }
            }

            return false;
        }
        */
}