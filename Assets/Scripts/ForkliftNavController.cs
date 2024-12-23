using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class ForkliftNavController : MonoBehaviour
{
    [Header("NavMesh Settings")]
    public NavMeshAgent agent;
    public Transform shippingPoint;
    public ForkliftController forkliftController;
    public float liftHeightOffset = 0.5f;
    public LayerMask boxLayerMask;

    public GameObject targetBox;
    private Vector3 originalPosition;

    private bool isCarryingBox = false; // Stato per verificare se il reachlift ha una box

    void Start()
    {
        originalPosition = transform.position;
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
                    Debug.Log($"Oggetto valido selezionato: {targetBox.name}");

                    // Avvia la coroutine per prelevare e consegnare
                    StartCoroutine(PickUpAndDeliver());
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


    public IEnumerator PickUpAndDeliver()
    {
        if (!ForkliftCommonFunctions.CheckForkliftController(forkliftController)) yield break;
        if (targetBox == null)
        {
            Debug.LogWarning("Nessuna box selezionata!");
            yield break;
        }

        // 1. Avvicinati alla box a 1.5 metri di distanza
        Vector3 distantApproachPoint = ForkliftCommonFunctions.CalculateApproachPoint(transform, targetBox.transform.position, 2f, 0.0f);
        agent.SetDestination(distantApproachPoint);
        yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance);
        Debug.Log("Arrivato a 1.5m dalla box");

        // 2. Rotazione graduale per essere parallelo allo scaffale
        Vector3 shelfForwardDir = new Vector3(0, 0, 1); // Supponendo che lo scaffale sia orientato lungo l'asse X
        yield return StartCoroutine(SmoothRotateToDirection(shelfForwardDir, 1f));
        Debug.Log("Rotazione completata, ora parallelo allo scaffale.");


        // 3. Solleva i mast in base all'altezza della box
        yield return StartCoroutine(AdjustLiftHeightAndDetectBox());
        if (targetBox == null)
        {
            Debug.LogWarning("Nessuna box rilevata durante la salita!");
            yield break;
        }
        Debug.Log("Mast sollevati alla giusta altezza");

        // 4. Avvicinati ulteriormente alla box per prenderla
        Vector3 closeApproachPoint = ForkliftCommonFunctions.CalculateApproachPoint(transform, targetBox.transform.position, 1.1f, 0.0f);
        agent.ResetPath();
        agent.SetDestination(closeApproachPoint);
        yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance);
        Debug.Log("Arrivato a 1.1m dalla box");

        // 5. Prendi la box
        ForkliftCommonFunctions.AttachBox(ref targetBox, forkliftController.grabPoint, ref isCarryingBox, forkliftController);
        Debug.Log("Box presa");

        // 6. Indietreggia di 1 metro
        Vector3 retreatPoint = transform.position - transform.forward * 1.0f;
        agent.ResetPath();
        agent.SetDestination(retreatPoint);
        yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance);
        Debug.Log("Indietreggiato di 1m");

        // 7. Abbassa i mast
        yield return StartCoroutine(ForkliftCommonFunctions.LowerAllMasts(forkliftController));
        Debug.Log("Mast abbassati");

        // 8. Porta la box alla shipping area
        agent.ResetPath();
        agent.SetDestination(shippingPoint.position);
        yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance);
        Debug.Log("Arrivato alla shipping area");

        // 9. Rilascia la box
        ForkliftCommonFunctions.ReleaseBox(ref targetBox, ref isCarryingBox, Vector3.up * 0.2f, false);
        Debug.Log("Box rilasciata");

        // 10. Torna alla posizione originale
        agent.ResetPath();
        agent.SetDestination(originalPosition);
        yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance);
        Debug.Log("Ritornato alla posizione originale");
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




    IEnumerator AdjustLiftHeightAndDetectBox()
    {
        if (!ForkliftCommonFunctions.CheckForkliftController(forkliftController)) yield break;

        Transform grabPoint = forkliftController.grabPoint;
        if (grabPoint == null)
        {
            Debug.LogError("Grab point non assegnato!");
            yield break;
        }

        bool boxFound = false;

        float boxBaseHeight = targetBox.transform.position.y;
        float forkliftBaseHeight = forkliftController.transform.position.y;

        float targetLiftHeight = boxBaseHeight - forkliftBaseHeight + liftHeightOffset;

        for (int i = 0; i < forkliftController.masts.Length; i++)
        {
            var mast = forkliftController.masts[i];
            float currentHeight = mast.liftTransform.localPosition.y;

            while (currentHeight < mast.maxLiftHeight && grabPoint.position.y < boxBaseHeight)
            {
                float step = mast.liftSpeed * Time.deltaTime;
                mast.liftTransform.localPosition += Vector3.up * step;
                currentHeight += step;

                if (DetectBoxInFront(grabPoint, 1.5f))
                {
                    boxFound = true;
                    Debug.Log($"Box rilevata durante la salita con il mast {i}!");
                    break;
                }

                yield return null;
            }

            if (boxFound) break;
        }

        if (!boxFound)
        {
            Debug.LogWarning("Nessuna box trovata frontalmente durante la salita!");
        }
    }

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

    IEnumerator ApproachAndGrabBox()
    {
        if (targetBox == null) yield break;

        Vector3 approachPoint = ForkliftCommonFunctions.CalculateApproachPoint(transform, targetBox.transform.position, 0.4f, 0.2f);
        agent.SetDestination(approachPoint);
        yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance);

        // Attach box
        ForkliftCommonFunctions.AttachBox(ref targetBox, forkliftController.grabPoint, ref isCarryingBox, forkliftController);

        Vector3 retreatPoint = transform.position - transform.forward * 1.0f;
        agent.SetDestination(retreatPoint);
        yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance);
    }
}