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

    [Header("Approach Point Settings")]
    public Vector3? predefinedApproachPoint = null; // Punto 3D specificato manualmente


    bool IsBoxInDeliveryArea(GameObject box)
    {
        // Controlla lo stato della box
        BoxState boxState = box.GetComponent<BoxState>();
        if (boxState != null)
        {
            Debug.Log($"La box {box.name} ha isInDeliveryArea = {boxState.isInDeliveryArea}");
            return boxState.isInDeliveryArea;
        }

        Debug.LogWarning($"La box {box.name} non ha un componente BoxState!");
        return false;
    }

    void Start()
    {
        originalPosition = transform.position;

        // Esempio di punto 3D a 1.5 metri dalla shelf (usando il primo shelf per esempio)
        if (shelfLevels.Length > 0 && shelfLevels[0] != null)
        {
            Vector3 shelfPosition = shelfLevels[0].position;
            Vector3 directionToShelf = (shelfPosition - transform.position).normalized;

            // Punto a 1.5 metri dalla shelf lungo la direzione
            predefinedApproachPoint = new Vector3(9.0f, 0.08f, -10.0f); // Esempio di coordinate X, Y, Z

            Debug.Log($"Punto 3D predefinito impostato: {predefinedApproachPoint}");
        }
        else
        {
            Debug.LogWarning("Nessuno shelf configurato, impossibile calcolare il punto di approccio!");
        }

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

        // 2. Prendi la box
        yield return StartCoroutine(ApproachAndGrabBox());

        if (targetBox == null)
        {
            Debug.LogWarning("Nessuna box selezionata nella zona di delivery!");
            yield break;
        }

        // 3. Determina il punto di approccio
        Transform targetShelfLevel = shelfLevels[shelfLevelIndex];
        Vector3 approachPoint;

        if (predefinedApproachPoint.HasValue)
        {
            approachPoint = predefinedApproachPoint.Value; // Usa il punto specificato
            Debug.Log($"Usando il punto di approccio predefinito: {approachPoint}");
        }
        else
        {
            approachPoint = ForkliftCommonFunctions.CalculateApproachPoint(transform, targetShelfLevel.position, 1.5f);
            Debug.Log($"Calcolato il punto di approccio: {approachPoint}");
        }

        Vector3 positionPoint = ForkliftCommonFunctions.CalculateFromPoint(approachPoint, transform, 2.5f);
        agent.ResetPath();
        agent.SetDestination(positionPoint);
        yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance);

        Debug.Log("Arrivato a 1.5m dal punto di approccio");

        // Rotazione graduale per essere parallelo allo scaffale
        Vector3 targetForwardDir = Quaternion.Euler(0, 270, 0) * targetShelfLevel.forward;
        yield return StartCoroutine(SmoothRotateToDirection(targetForwardDir, 1f));
        Debug.Log("Rotazione completata, ora parallelo al punto di approccio.");

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

        Vector3 approachPoint = targetBox.transform.position;
        agent.SetDestination(approachPoint);
        yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance);

        // Attacca la box
        ForkliftCommonFunctions.AttachBox(ref targetBox, forkliftController.grabPoint, ref isCarryingBox);

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
        Transform grabPoint = forkliftController.grabPoint;
        if (grabPoint == null)
        {
            Debug.LogError("Grab point non assegnato!");
            yield break;
        }

        float currentHeight = grabPoint.position.y;

        for (int i = 0; i < forkliftController.mastsLiftTransform.Length; i++)
        {
            var mast = forkliftController.mastsLiftTransform[i];

            while (currentHeight < targetHeight && mast.localPosition.y < forkliftController.liftHeight)
            {
                float step = forkliftController.liftSpeed * Time.deltaTime;
                mast.localPosition += Vector3.up * step;
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
