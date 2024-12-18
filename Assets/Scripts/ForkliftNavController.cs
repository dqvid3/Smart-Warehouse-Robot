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
    private float[] mastHeights = new float[] { 1.372069f, 1.525879e-07f, -7.629394e-08f };

    private GameObject targetBox;
    private Vector3 originalPosition;
    private int currentMastIndex = 0;

    void Start()
    {
        originalPosition = transform.position;
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, boxLayerMask) && hit.collider.CompareTag("Grabbable"))
            {
                targetBox = hit.collider.gameObject;
                StartCoroutine(PickUpAndDeliver());
            }
        }
    }

    IEnumerator PickUpAndDeliver()
    {
        if (!CheckForkliftController()) yield break;
        if (targetBox == null)
        {
            Debug.LogWarning("Nessuna box selezionata!");
            yield break;
        }

        // 1. Avvicinati alla box
        Vector3 approachPoint = CalculateApproachPoint(targetBox.transform.position, 0.5f);
        agent.SetDestination(approachPoint);
        yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance);

        // 2. Solleva l'elevatore in base all'altezza della box
        yield return StartCoroutine(AdjustLiftHeightAndDetectBox());

        if (targetBox == null)
        {
            Debug.LogWarning("Nessuna box rilevata durante la salita!");
            yield break;
        }

        // 3. Avvicinati ulteriormente e prendi la box
        yield return StartCoroutine(ApproachAndGrabBox());

        // 4. Abbassa tutti i masti
        yield return StartCoroutine(LowerAllMasts());

        // 5. Vai alla shipping area
        agent.SetDestination(shippingPoint.position);
        yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance);

        // 6. Rilascia la box
        ReleaseBox();

        // 7. Torna alla posizione originale
        agent.SetDestination(originalPosition);
        yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance);

        currentMastIndex = 0;
    }

    Vector3 CalculateApproachPoint(Vector3 boxPosition, float distance)
    {
        Vector3 boxHorizPos = boxPosition;
        boxHorizPos.y = transform.position.y;
        Vector3 directionToBox = (boxHorizPos - transform.position).normalized;
        return boxHorizPos - directionToBox * distance;
    }

    IEnumerator AdjustLiftHeightAndDetectBox()
    {
        if (!CheckForkliftController()) yield break;

        Transform grabPoint = forkliftController.grabPoint;
        if (grabPoint == null)
        {
            Debug.LogError("Grab point non assegnato!");
            yield break;
        }

        bool boxFound = false;

        // Calcola l'altezza della box
        float boxHeight = targetBox.transform.position.y;

        // Usa il mast 4 (indice 2) per calcolare l'altezza necessaria
        float liftHeight = CalculateLiftHeight(boxHeight, forkliftController.masts[2]);

        // Inverti l'ordine dei masti: dall'ultimo al primo
        for (int i = forkliftController.masts.Length - 1; i >= 0; i--)
        {
            var mast = forkliftController.masts[i];

            // Solleva il mast fino all'altezza necessaria
            while (mast.liftTransform.localPosition.y < liftHeight)
            {
                mast.liftTransform.localPosition += Vector3.up * mast.liftSpeed * Time.deltaTime;

                // Controlla il raycast per rilevare la box durante la salita
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


    float CalculateLiftHeight(float boxHeight, MastSettings mast)
    {
        // Altezza minima del mast
        float minLiftHeight = mast.minLiftHeight;
        float maxLiftHeight = mast.maxLiftHeight;

        // Calcola l'altezza del centro della box rispetto al forklift
        float boxCenterHeight = boxHeight - forkliftController.transform.position.y;

        // Calcola l'altezza necessaria considerando l'offset
        float targetLiftHeight = minLiftHeight + boxCenterHeight + liftHeightOffset;

        // Assicurati che l'altezza sia entro i limiti del mast
        return Mathf.Clamp(targetLiftHeight, minLiftHeight, maxLiftHeight);
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

        Vector3 approachPoint = CalculateApproachPoint(targetBox.transform.position, 0.2f);
        agent.SetDestination(approachPoint);
        yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance);

        AttachBox(0);

        // Dopo aver preso la box, arretra di 0.5 metri
        Vector3 retreatPoint = transform.position - transform.forward * 0.5f;
        agent.SetDestination(retreatPoint);
        yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance);
    }

    IEnumerator LowerAllMasts()
    {
        // Controlla che il numero di altezze corrisponda al numero di masti
        if (mastHeights.Length != forkliftController.masts.Length)
        {
            Debug.LogError("Il numero di altezze specificate non corrisponde al numero di masti!");
            yield break;
        }

        for (int i = 0; i < forkliftController.masts.Length; i++)
        {
            var mast = forkliftController.masts[i];
            if (mast.liftTransform == null) continue;

            float targetHeight = mastHeights[i];

            while (mast.liftTransform.localPosition.y > targetHeight)
            {
                mast.liftTransform.localPosition -= Vector3.up * mast.liftSpeed * Time.deltaTime;
                yield return null;
            }

            // Imposta l'altezza finale esatta per evitare piccoli errori di arrotondamento
            mast.liftTransform.localPosition = new Vector3(
                mast.liftTransform.localPosition.x,
                targetHeight,
                mast.liftTransform.localPosition.z
            );
        }
    }


    void AttachBox(int mastIndex)
    {
        if (!CheckMastIndex(mastIndex)) return;

        var mast = forkliftController.masts[mastIndex];
        targetBox.transform.SetParent(mast.liftTransform);
        targetBox.transform.localPosition = new Vector3(0, 0, 0.5f);

        var rb = targetBox.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    void ReleaseBox()
    {
        if (targetBox == null) return;

        var rb = targetBox.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        targetBox.transform.SetParent(null);
        targetBox.transform.position += Vector3.up * 0.2f;
        targetBox = null;
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

    bool CheckMastIndex(int index)
    {
        if (index < 0 || index >= forkliftController.masts.Length)
        {
            Debug.LogError("Mast index fuori limiti!");
            return false;
        }
        return true;
    }
}
