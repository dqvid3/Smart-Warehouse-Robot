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
                        forkliftNavController.agent.ResetPath(); // Ferma il percorso corrente senza disabilitare l'agente
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
                    Debug.Log("La box NON è nella Delivery Area, trasporto alla Shipping Area non consentito.");
                }
            }
        }
    }



    IEnumerator PickUpFromDeliveryAndStore(int shelfLevelIndex)
    {
        Debug.Log("Avviata coroutine PickUpFromDeliveryAndStore");

        if (!CheckForkliftController())
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

        // 3. Vai al livello specifico dello shelf
        Transform targetShelfLevel = shelfLevels[shelfLevelIndex];
        Vector3 approachPoint = CalculateApproachPoint(targetShelfLevel.position, 0.5f);
        agent.ResetPath();
        agent.SetDestination(approachPoint);
        yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance);

        Debug.Log("Arrivato davanti allo scaffale");

        // 4. Solleva l'elevatore fino al livello dello shelf
        yield return StartCoroutine(LiftToShelfLevel(targetShelfLevel.position.y));

        // 5. Rilascia la box
        ReleaseBox();

        // **Riattiva il ForkliftNavController subito dopo aver rilasciato la box**
        forkliftNavController.enabled = true;
        if (!forkliftNavController.agent.enabled)
        {
            forkliftNavController.agent.enabled = true;
        }
        Debug.Log("ForkliftNavController riattivato.");

        // 6. Abbassa tutti i masti
        yield return StartCoroutine(LowerAllMasts());

        // 7. Torna alla posizione originale
        agent.ResetPath();
        agent.SetDestination(originalPosition);
        yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance);

        Debug.Log("Ritornato alla posizione originale");
    }

    bool SelectBoxInDelivery()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, boxLayerMask) && hit.collider.CompareTag("Grabbable"))
        {
            targetBox = hit.collider.gameObject;
            return true;
        }
        return false;
    }

    // Rimuovi qualsiasi sollevamento nel metodo `ApproachAndGrabBox`
    IEnumerator ApproachAndGrabBox()
    {
        if (targetBox == null) yield break;

        Vector3 approachPoint = CalculateApproachPoint(targetBox.transform.position, 0.4f);
        agent.SetDestination(approachPoint);
        yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance);

        AttachBox();

        // Arretra leggermente per evitare collisioni
        Vector3 retreatPoint = transform.position - transform.forward * 1.0f;
        agent.SetDestination(retreatPoint);
        yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance);
    }


    Vector3 CalculateApproachPoint(Vector3 targetPosition, float distance)
    {
        Vector3 targetHorizPos = targetPosition;
        targetHorizPos.y = transform.position.y;
        Vector3 directionToTarget = (targetHorizPos - transform.position).normalized;
        return targetHorizPos - directionToTarget * distance;
    }

    IEnumerator LiftToShelfLevel(float targetHeight)
    {
        if (!CheckForkliftController()) yield break;

        Transform grabPoint = forkliftController.grabPoint;
        if (grabPoint == null)
        {
            Debug.LogError("Grab point non assegnato!");
            yield break;
        }

        // Altezza corrente del grab point rispetto alla base del forklift
        float currentHeight = grabPoint.position.y;

        // Solleva i masti in ordine finché non si raggiunge l'altezza desiderata
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

            // Se l'altezza desiderata è stata raggiunta, interrompi il sollevamento
            if (currentHeight >= targetHeight)
            {
                Debug.Log($"Elevatore sollevato con successo fino all'altezza desiderata con il mast {i}!");
                break;
            }
        }

        // Assicurati che il liftTransform non vada sotto il limite minimo
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


    void AttachBox()
    {
        if (targetBox == null) return;

        // Disabilita temporaneamente il rigidbody per evitare rimbalzi
        Rigidbody rb = targetBox.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;    // Annulla qualsiasi velocit� residua
            rb.angularVelocity = Vector3.zero; // Annulla la rotazione residua
        }

        // Attacca la box al grab point
        targetBox.transform.SetParent(forkliftController.grabPoint);
        targetBox.transform.localPosition = Vector3.zero;
        targetBox.transform.localRotation = Quaternion.identity;

        isCarryingBox = true;

        Debug.Log("Box attaccata con successo.");
    }


    void ReleaseBox()
    {
        if (targetBox == null)
        {
            Debug.LogWarning("Tentativo di rilasciare una box nulla!");
            return;
        }

        var rb = targetBox.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.WakeUp(); // Forza l'aggiornamento immediato del Rigidbody
        }

        // Stacca la box e applica un leggero offset verso il basso
        targetBox.transform.SetParent(null);
        targetBox.transform.position += Vector3.down * 0.1f;

        targetBox = null;
        isCarryingBox = false;

        Debug.Log("Box rilasciata con successo.");
    }

    IEnumerator LowerAllMasts()
    {
        foreach (var mast in forkliftController.masts)
        {
            while (mast.liftTransform.localPosition.y > 0)
            {
                mast.liftTransform.localPosition -= Vector3.up * mast.liftSpeed * Time.deltaTime;
                yield return null;
            }
        }
    }

    bool CheckForkliftController()
    {
        if (forkliftController == null)
        {
            Debug.LogError("ForkliftController non assegnato!");
            return false;
        }
        return true;
    }
}
