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
    private int finalMastIndex = 2;

    private GameObject targetBox;
    private Vector3 originalPosition;
    private int currentMastIndex = 0;

    private float[] mastHeights = new float[] { 1.372069f, 1.525879e-07f, -7.629394e-08f };

    void Start()
    {
        originalPosition = transform.position;
    }

    void Update()
    {
        // Se vuoi mantenere anche la selezione della box via click
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit) && hit.collider.CompareTag("Grabbable"))
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

        // 1. Avvicinati fino a 0.5m dalla box (solo orizzontalmente)
        Vector3 approachPoint = CalculateApproachPoint(targetBox.transform.position, 0.5f);
        agent.SetDestination(approachPoint);
        yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance);

        // Ora siamo a 0.5m dalla box, alziamo il braccio e cerchiamo la box durante la salita
        yield return StartCoroutine(AdjustLiftHeightAndDetectBox());

        if (targetBox == null)
        {
            // Se non abbiamo trovato nessuna box col raycast durante la salita, fermiamo qui
            Debug.LogWarning("Nessuna box rilevata durante la salita!");
            yield break;
        }

        // 3. Avvicinati ulteriormente alla box e prendila
        yield return StartCoroutine(ApproachAndGrabBox());

        // 4. Abbassa tutti i masti
        yield return StartCoroutine(LowerAllMasts());

        // 5. Vai alla shipping area
        agent.SetDestination(shippingPoint.position);
        yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance);

        // Rilascia la box
        ReleaseBox();

        // Torna alla posizione originale
        agent.SetDestination(originalPosition);
        yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance);

        currentMastIndex = 0;
    }

    Vector3 CalculateApproachPoint(Vector3 boxPosition, float distance)
    {
        // Prende la direzione orizzontale dal forklift alla box
        Vector3 boxHorizPos = boxPosition;
        boxHorizPos.y = transform.position.y;
        Vector3 directionToBox = (boxHorizPos - transform.position).normalized;
        return boxHorizPos - directionToBox * distance;
    }

    IEnumerator AdjustLiftHeightAndDetectBox()
    {
        if (!CheckForkliftController()) yield break;

        bool boxFound = false;

        while (currentMastIndex < forkliftController.masts.Length && !boxFound)
        {
            var mast = forkliftController.masts[currentMastIndex];

            while (mast.liftTransform.localPosition.y < mast.maxLiftHeight && !boxFound)
            {
                float oldY = mast.liftTransform.localPosition.y;
                mast.liftTransform.localPosition += Vector3.up * mast.liftSpeed * Time.deltaTime;
                float newY = mast.liftTransform.localPosition.y;

                // Se saliti di almeno 0.001 in Y, controlliamo il raycast frontale
                if (Mathf.Abs(newY - oldY) > 0.001f)
                {
                    if (DetectBoxInFront(1.5f))
                    {
                        boxFound = true;
                    }
                }

                yield return null;
            }

            if (!boxFound) currentMastIndex++;
        }

        if (!boxFound)
        {
            Debug.LogWarning("Nessuna box trovata frontalmente durante la salita!");
        }
    }

    bool DetectBoxInFront(float maxDistance)
    {
        Ray forwardRay = new Ray(transform.position + Vector3.up * 1.0f, transform.forward);
        if (Physics.Raycast(forwardRay, out RaycastHit hit, maxDistance))
        {
            if (hit.collider.CompareTag("Grabbable"))
            {
                targetBox = hit.collider.gameObject;
                Debug.Log("Box trovata frontalmente a distanza ≤ " + maxDistance + "m!");
                return true;
            }
        }
        return false;
    }

    IEnumerator ApproachAndGrabBox()
    {
        if (targetBox == null) yield break;

        Vector3 approachPoint = CalculateApproachPoint(targetBox.transform.position, 0.5f);
        agent.SetDestination(approachPoint);
        yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance);

        // Aggancia la box
        AttachBox(currentMastIndex);
        ReattachBoxToMast(finalMastIndex);

        // Arretra di 0.5 metri per allontanarsi
        agent.SetDestination(transform.position - transform.forward * 0.5f);
        yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance);
    }

    IEnumerator LowerAllMasts()
    {
        for (int i = 0; i < forkliftController.masts.Length; i++)
        {
            var mast = forkliftController.masts[i];
            if (mast.liftTransform == null) continue;

            float targetH = mastHeights[i];
            while (mast.liftTransform.localPosition.y > targetH)
            {
                mast.liftTransform.localPosition -= Vector3.up * mast.liftSpeed * Time.deltaTime;
                yield return null;
            }

            mast.liftTransform.localPosition = new Vector3(
                mast.liftTransform.localPosition.x,
                targetH,
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

    void ReattachBoxToMast(int mastIndex)
    {
        if (!CheckMastIndex(mastIndex)) return;
        var mast = forkliftController.masts[mastIndex];

        targetBox.transform.SetParent(null);
        targetBox.transform.SetParent(mast.liftTransform);
        targetBox.transform.localPosition = Vector3.zero;
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
        if (forkliftController.masts == null || forkliftController.masts.Length == 0)
        {
            Debug.LogError("Masts non assegnati!");
            return false;
        }
        return true;
    }

    bool CheckMastIndex(int index)
    {
        if (!CheckForkliftController()) return false;
        if (index < 0 || index >= forkliftController.masts.Length)
        {
            Debug.LogError("Mast index fuori limiti!");
            return false;
        }
        if (forkliftController.masts[index].liftTransform == null)
        {
            Debug.LogError($"Mast {index} senza liftTransform!");
            return false;
        }
        return true;
    }
}
