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

    // Controlla se la box è nella delivery area
    bool IsBoxInDeliveryArea(GameObject box)
    {
        float distanceToDelivery = Vector3.Distance(box.transform.position, deliveryPoint.position);
        Debug.Log($"Posizione della box: {box.transform.position}");
        Debug.Log($"Posizione del deliveryPoint: {deliveryPoint.position}");
        Debug.Log($"Distanza dalla Delivery Area: {distanceToDelivery}");
        return distanceToDelivery <= 30.0f; // Raggio della delivery area
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
        // Controlla il click del mouse sinistro
        if (Input.GetMouseButtonDown(0) && !isCarryingBox)
        {
            // Lancia un Raycast dalla posizione del mouse
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, boxLayerMask))
            {
                // Risali all'oggetto radice
                targetBox = hit.collider.transform.root.gameObject;

                Debug.Log($"Oggetto selezionato: {targetBox.name}");

                // Controlla che l'oggetto radice abbia il tag corretto
                if (targetBox.CompareTag("Grabbable"))
                {
                    // Verifica che la box si trovi nella delivery area
                    if (IsBoxInDeliveryArea(targetBox))
                    {
                        Debug.Log($"Oggetto {targetBox.name} valido e nella Delivery Area. Inizio il trasporto allo scaffale.");

                        // Disattiva il controllo della navetta durante l'operazione
                        if (forkliftNavController != null)
                        {
                            forkliftNavController.StopAllCoroutines();
                            forkliftNavController.agent.ResetPath();
                            forkliftNavController.enabled = false;
                            Debug.Log("ForkliftNavController disattivato.");
                        }
                        else
                        {
                            Debug.LogError("ForkliftNavController non assegnato!");
                        }

                        // Avvia la coroutine per il trasporto
                        StartCoroutine(PickUpFromDeliveryAndStore(3));
                    }
                    else
                    {
                        Debug.Log("La box NON è nella Delivery Area.");
                    }
                }
                else
                {
                    Debug.LogWarning("L'oggetto selezionato non è valido!");
                }
            }
            else
            {
                Debug.LogWarning("Nessun oggetto intercettato dal Raycast!");
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

        // Vai direttamente verso la box
        Vector3 approachPoint = targetBox.transform.position;
        agent.ResetPath();
        agent.SetDestination(approachPoint);
        Debug.DrawLine(transform.position, approachPoint, Color.green, 5f); // Debug visivo
        yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance);

        Debug.Log("Arrivato direttamente alla box");

        // Prendi la box
        yield return StartCoroutine(ApproachAndGrabBox());

        if (targetBox == null)
        {
            Debug.LogWarning("Nessuna box selezionata nella zona di delivery!");
            yield break;
        }

        // Vai al livello specifico dello shelf ma fermati a 1.5 m di distanza
        Transform targetShelfLevel = shelfLevels[shelfLevelIndex];
        Vector3 approachToShelf = ForkliftCommonFunctions.CalculateApproachPoint(transform, targetShelfLevel.position, 1.5f, 0.0f);
        agent.ResetPath();
        agent.SetDestination(approachToShelf);
        yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance);

        Debug.Log("Arrivato a 1.5m dallo scaffale");

        // Rotazione graduale per essere parallelo allo scaffale
        Vector3 targetForwardDir = Quaternion.Euler(0, 270, 0) * targetShelfLevel.forward;
        yield return StartCoroutine(SmoothRotateToDirection(targetForwardDir, 1f));
        Debug.Log("Rotazione completata, ora parallelo allo scaffale.");

        // Solleva l'elevatore fino al livello dello shelf
        yield return StartCoroutine(LiftToShelfLevel(targetShelfLevel.position.y));

        // Rilascia la box
        ForkliftCommonFunctions.ReleaseBox(ref targetBox, ref isCarryingBox, Vector3.down * 0.1f, true);

        // Riattiva il ForkliftNavController
        forkliftNavController.enabled = true;
        if (!forkliftNavController.agent.enabled)
        {
            forkliftNavController.agent.enabled = true;
        }
        Debug.Log("ForkliftNavController riattivato.");

        // Abbassa tutti i masti
        yield return StartCoroutine(ForkliftCommonFunctions.LowerAllMasts(forkliftController));

        // Torna alla posizione originale
        agent.ResetPath();
        agent.SetDestination(originalPosition);
        yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance);

        Debug.Log("Ritornato alla posizione originale");
    }

    IEnumerator ApproachAndGrabBox()
    {
        if (targetBox == null) yield break;

        Vector3 approachPoint = targetBox.transform.position;
        agent.SetDestination(approachPoint);
        yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance);

        // Attacca la box
        ForkliftCommonFunctions.AttachBox(ref targetBox, forkliftController.grabPoint, ref isCarryingBox, forkliftController);

        Debug.Log($"Box {targetBox.name} presa con successo.");
    }

    IEnumerator SmoothRotateToDirection(Vector3 targetForward, float rotationSpeed = 1f)
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
    }
}
