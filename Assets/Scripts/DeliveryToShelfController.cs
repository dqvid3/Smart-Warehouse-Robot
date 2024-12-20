using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class DeliveryToShelfController : MonoBehaviour
{
    [Header("NavMesh Settings")]
    public NavMeshAgent agent;
    public Transform deliveryPoint;
    public Transform[] shelfLevels;
    public ForkliftController forkliftController;
    public LayerMask boxLayerMask;
    public float liftHeightOffset = 0.5f;

    public GameObject targetBox;
    private bool isCarryingBox = false;
    private Vector3 originalPosition;
    public ForkliftNavController forkliftNavController;

    bool IsBoxInDeliveryArea(GameObject box)
    {
        float distanceToDelivery = Vector3.Distance(box.transform.position, deliveryPoint.position);
        Debug.Log($"Posizione della box: {box.transform.position}");
        Debug.Log($"Posizione del deliveryPoint: {deliveryPoint.position}");
        Debug.Log($"Distanza dalla Delivery Area: {distanceToDelivery}");
        return distanceToDelivery <= 5.0f;
    }

    void Start()
    {
        originalPosition = transform.position;
        if (forkliftNavController == null)
        {
            Debug.LogError("ForkliftNavController non assegnato! Controlla nel Inspector.");
        }
        else
        {
            Debug.Log($"ForkliftNavController assegnato: {forkliftNavController}");
        }
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0) && !isCarryingBox)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, boxLayerMask) && hit.collider.CompareTag("Grabbable"))
            {
                targetBox = hit.collider.gameObject;

                if (IsBoxInDeliveryArea(targetBox))
                {
                    Debug.Log("La box è nella Delivery Area, avvio trasporto allo scaffale.");

                    if (forkliftNavController != null)
                    {
                        forkliftNavController.StopAllCoroutines();
                        forkliftNavController.agent.ResetPath();
                        forkliftNavController.enabled = false;
                        Debug.Log("ForkliftNavController disattivato senza disabilitare il NavMeshAgent.");
                    }
                    else
                    {
                        Debug.LogError("forkliftNavController è null!");
                    }

                    StartCoroutine(PickUpFromDeliveryAndStore(3));
                }
                else
                {
                    Debug.Log("La box NON è nella Delivery Area.");
                }
            }
        }
    }

    IEnumerator PickUpFromDeliveryAndStore(int shelfLevelIndex)
    {
        Debug.Log("Avviata coroutine PickUpFromDeliveryAndStore");

        if (!ForkliftCommonFunctions.CheckForkliftController(forkliftController))
        {
            Debug.LogWarning("ForkliftController non assegnato!");
            yield break;
        }

        if (!agent.isOnNavMesh)
        {
            Debug.LogError("NavMeshAgent non è sulla NavMesh!");
            yield break;
        }

        if (!agent.enabled)
        {
            agent.enabled = true;
            Debug.Log("NavMeshAgent riabilitato.");
        }

        // 1. Vai alla zona di delivery
        agent.ResetPath();
        agent.SetDestination(deliveryPoint.position);
        yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance);

        Debug.Log("Arrivato alla Delivery Area");

        // 2. Prendi la box
        yield return StartCoroutine(ApproachAndGrabBox());

        if (targetBox == null)
        {
            Debug.LogWarning("Nessuna box selezionata nella zona di delivery!");
            yield break;
        }


        // 3. Vai al livello specifico dello shelf ma fermati a 1.5 m di distanza
        Transform targetShelfLevel = shelfLevels[shelfLevelIndex];
        Vector3 approachPoint = ForkliftCommonFunctions.CalculateApproachPoint(transform, targetShelfLevel.position, 1.5f, 0.0f);
        Vector3 positionPoint = ForkliftCommonFunctions.CalculateFromPoint(approachPoint, transform, 2.5f);
        agent.ResetPath();
        agent.SetDestination(positionPoint);
        yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance);

        Debug.Log("Arrivato a 1.5m dallo scaffale");

        // Rotazione graduale per essere parallelo allo scaffale
        Vector3 targetForwardDir = Quaternion.Euler(0, 270, 0) * targetShelfLevel.forward;
        yield return StartCoroutine(SmoothRotateToDirection(targetForwardDir, 1f));
        Debug.Log("Rotazione completata, ora parallelo allo scaffale.");


        // 4. Solleva l'elevatore fino al livello dello shelf
        yield return StartCoroutine(LiftToShelfLevel(targetShelfLevel.position.y));

        positionPoint = ForkliftCommonFunctions.CalculateFromPoint(approachPoint, transform, 1.1f);
        agent.ResetPath();
        agent.SetDestination(positionPoint);
        yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance);

        // 5. Rilascia la box
        ForkliftCommonFunctions.ReleaseBox(ref targetBox, ref isCarryingBox, Vector3.down * 0.1f, true);

        positionPoint = ForkliftCommonFunctions.CalculateFromPoint(approachPoint, transform, 2.5f);
        agent.ResetPath();
        agent.SetDestination(positionPoint);
        yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance);


        // Riattiva il ForkliftNavController
        forkliftNavController.enabled = true;
        if (!forkliftNavController.agent.enabled)
        {
            forkliftNavController.agent.enabled = true;
        }
        Debug.Log("ForkliftNavController riattivato.");

        // 6. Abbassa tutti i masti
        yield return StartCoroutine(ForkliftCommonFunctions.LowerAllMasts(forkliftController));

        // 7. Torna alla posizione originale
        agent.ResetPath();
        agent.SetDestination(originalPosition);
        yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance);

        Debug.Log("Ritornato alla posizione originale");
    }

    IEnumerator ApproachAndGrabBox()
    {
        if (targetBox == null) yield break;

        Vector3 approachPoint = ForkliftCommonFunctions.CalculateApproachPoint(transform, targetBox.transform.position, 0.4f, 0.0f);
        agent.SetDestination(approachPoint);
        yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance);

        // Attacca la box (senza mastIndex, con reset velocità, messaggio di successo)
        ForkliftCommonFunctions.AttachBox(ref targetBox, forkliftController.grabPoint, ref isCarryingBox, forkliftController);

        Vector3 retreatPoint = transform.position - transform.forward * 1.0f;
        agent.SetDestination(retreatPoint);
        yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance);
    }

    IEnumerator LiftToShelfLevel(float targetHeight)
    {
        if (!ForkliftCommonFunctions.CheckForkliftController(forkliftController)) yield break;

        Transform grabPoint = forkliftController.grabPoint;
        if (grabPoint == null)
        {
            Debug.LogError("Grab point non assegnato!");
            yield break;
        }

        float currentHeight = grabPoint.position.y;

        for (int i = 0; i < forkliftController.masts.Length; i++)
        {
            var mast = forkliftController.masts[i];

            while (currentHeight < targetHeight && mast.liftTransform.localPosition.y < mast.maxLiftHeight)
            {
                float step = mast.liftSpeed * Time.deltaTime;
                mast.liftTransform.localPosition += Vector3.up * step;
                currentHeight = grabPoint.position.y;

                yield return null;
            }

            if (currentHeight >= targetHeight)
            {
                Debug.Log($"Elevatore sollevato con successo fino all'altezza desiderata con il mast {i}!");
                break;
            }
        }

        foreach (var mast in forkliftController.masts)
        {
            float minLiftHeight = mast.minLiftHeight;
            if (mast.liftTransform.localPosition.y < minLiftHeight)
            {
                mast.liftTransform.localPosition = new Vector3(
                    mast.liftTransform.localPosition.x,
                    minLiftHeight,
                    mast.liftTransform.localPosition.z
                );
                Debug.LogWarning($"Elevatore corretto alla minima altezza consentita per il mast.");
            }
        }
    }

    IEnumerator SmoothRotateToDirection(Vector3 targetForward, float rotationSpeed = 1f)
    {
        Quaternion startRotation = transform.rotation;
        Quaternion finalRotation = Quaternion.LookRotation(targetForward, Vector3.up);
        float angle = Quaternion.Angle(startRotation, finalRotation);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * rotationSpeed;
            transform.rotation = Quaternion.Slerp(startRotation, finalRotation, t);
            yield return null;
        }
        transform.rotation = finalRotation;
    }

}
