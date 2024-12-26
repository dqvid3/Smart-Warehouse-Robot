using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class ForkliftNavController : MonoBehaviour
{
    [Header("NavMesh Settings")]
    public NavMeshAgent agent;
    public Transform shippingPoint;
    public ForkliftController forkliftController;
    [SerializeField] private LayerMask layerMask;
    private bool isCarryingBox = false; 
    private float approachDistance = 3.2f; // Distanza da mantenere per considerare le pale
    private float takeBoxDistance = 1.7f;

    void Update()
    {
        if (Input.GetMouseButtonDown(0) && !isCarryingBox)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, layerMask))
            {
                GameObject parcel = hit.collider.transform.root.gameObject;
                StartCoroutine(PickParcel(parcel));
                Debug.Log($"Pacco selezionato: {parcel.name}");
            }
        }
    }

    private IEnumerator PickParcel(GameObject parcel)
    {
        Vector3 pos = parcel.transform.position;
        Vector3 approachPosition = new(pos.x, transform.position.y, pos.z);
        Vector3 qrCodeDirection = new(1, 0, 0);
        if (pos.y > 0) // il pacco è in uno scaffale
            qrCodeDirection = new Vector3(0, 0, -1);
        Debug.Log($"Posizione: {pos}, Approach: {approachPosition}, QRCode: {qrCodeDirection}");
        approachPosition -= qrCodeDirection * approachDistance;
        
        // 1. Avvicinati alla box da lontano
        agent.SetDestination(approachPosition);
        yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance);
        
        // 2. Ruota di fronte al box
        yield return StartCoroutine(SmoothRotateToDirection(qrCodeDirection));
        
        // 3. Solleva un po' il mast
        yield return StartCoroutine(forkliftController.LiftMast(pos.y + 0.05f)); 
        
        // 4. Vai avanti per prendere la box
        approachPosition += qrCodeDirection * takeBoxDistance;   
        agent.SetDestination(approachPosition);
        yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance);
        
        // 5. Solleva un po' la box
        yield return StartCoroutine(forkliftController.LiftMast(pos.y + 0.05f));
        parcel.transform.SetParent(forkliftController.grabPoint);
        isCarryingBox = true; 
        
        // 6. Torna indietro
        approachPosition -= qrCodeDirection * takeBoxDistance;   
        agent.SetDestination(approachPosition);
        yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance);
        
        // 7. Abbassa i mast
        if (pos.y > 0) // il pacco è in uno scaffale
            yield return StartCoroutine(forkliftController.LiftMast(0.1f));
        agent.ResetPath();
        
        // logica per dirgli in quale scaffale metterlo oppure di portarlo al punto di spedizione
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
            yield return null;
        }
        transform.rotation = finalRotation;
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