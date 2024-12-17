using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class ForkliftNavController : MonoBehaviour
{
    [Header("NavMesh Settings")]
    public NavMeshAgent agent;
    public Transform shippingPoint;
    public ForkliftController forkliftController; // Riferimento per controllare l'elevatore
    public float liftHeightOffset = 1f; // Offset per sollevare la box leggermente sopra il livello

    private GameObject targetBox;
    private Vector3 originalPosition;
    private int currentMastIndex = 0; // Indice del mast corrente

    void Start()
    {
        originalPosition = transform.position; // Salva la posizione iniziale
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                if (hit.collider.CompareTag("Grabbable"))
                {
                    targetBox = hit.collider.gameObject;
                    StartCoroutine(PickUpAndDeliver());
                }
            }
        }
    }

    IEnumerator PickUpAndDeliver()
    {
        // Controlli per evitare errori
        if (forkliftController == null)
        {
            Debug.LogError("ForkliftController non assegnato!");
            yield break;
        }

        if (forkliftController.masts == null || forkliftController.masts.Length == 0)
        {
            Debug.LogError("L'array masts è vuoto o non assegnato!");
            yield break;
        }

        if (targetBox == null)
        {
            Debug.LogError("Nessuna box selezionata!");
            yield break;
        }

        // Muoviti verso la box
        Debug.Log("Muovo il Reachlift verso la box: " + targetBox.transform.position);
        agent.SetDestination(targetBox.transform.position);
        yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance);

        // Seleziona il mast corretto e solleva l'elevatore
        float boxHeight = targetBox.transform.position.y;
        yield return StartCoroutine(AdjustLiftHeight(boxHeight));

        // Aggancia la box all'elevatore
        AttachBox();

        // Muoviti leggermente indietro per liberare spazio
        Debug.Log("Sposto il Reachlift leggermente indietro");
        agent.SetDestination(transform.position - transform.forward * 2f);
        yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance);

        // Abbassa l'elevatore
        Debug.Log("Abbasso l'elevatore");
        yield return StartCoroutine(LowerLift());

        // Muoviti verso la shipping area
        Debug.Log("Muovo il Reachlift verso la Shipping Area: " + shippingPoint.position);
        agent.SetDestination(shippingPoint.position);
        yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance);

        // Rilascia la box
        ReleaseBox();

        // Torna alla posizione originale
        Debug.Log("Torno alla posizione originale: " + originalPosition);
        agent.SetDestination(originalPosition);
        yield return new WaitUntil(() => !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance);

        // Reset del mast corrente
        currentMastIndex = 0;
    }

    IEnumerator AdjustLiftHeight(float targetHeight)
    {
        while (currentMastIndex < forkliftController.masts.Length)
        {
            MastSettings currentMast = forkliftController.masts[currentMastIndex];

            Debug.Log($"Tentativo di sollevare con Mast {currentMastIndex}: Max Lift Height = {currentMast.maxLiftHeight}");

            while (currentMast.liftTransform.localPosition.y < currentMast.maxLiftHeight &&
                   currentMast.liftTransform.localPosition.y < targetHeight + liftHeightOffset)
            {
                currentMast.liftTransform.localPosition += Vector3.up * currentMast.liftSpeed * Time.deltaTime;
                Debug.Log($"Mast {currentMastIndex}: Altezza corrente = {currentMast.liftTransform.localPosition.y}");
                yield return null;
            }

            // Se l'altezza desiderata è raggiunta, esci dal ciclo
            if (currentMast.liftTransform.localPosition.y >= targetHeight + liftHeightOffset)
            {
                Debug.Log($"Altezza raggiunta con Mast {currentMastIndex}");
                yield break;
            }

            // Passa al mast successivo
            currentMastIndex++;
            Debug.Log("Passo al mast successivo: " + currentMastIndex);
        }

        // Se esauriti tutti i masts, mostra un errore
        Debug.LogError("Nessun mast disponibile può raggiungere l'altezza della box!");
        currentMastIndex = 0; // Reset per sicurezza
    }


    void AttachBox()
    {
        if (currentMastIndex < 0 || currentMastIndex >= forkliftController.masts.Length)
        {
            Debug.LogError("currentMastIndex è fuori dai limiti dell'array masts!");
            return;
        }

        MastSettings currentMast = forkliftController.masts[currentMastIndex];

        if (currentMast.liftTransform == null)
        {
            Debug.LogError("LiftTransform del mast corrente non è assegnato!");
            return;
        }

        // Aggancia la box all'elevatore senza modificare la sua scala
        targetBox.transform.SetParent(currentMast.liftTransform, worldPositionStays: true);

        // Posiziona la box mantenendo la posizione locale coerente
        targetBox.transform.localPosition = new Vector3(0, 0.5f, 0);
        targetBox.transform.localRotation = Quaternion.identity; // Resetta la rotazione locale

        Rigidbody boxRigidbody = targetBox.GetComponent<Rigidbody>();
        if (boxRigidbody != null)
        {
            boxRigidbody.isKinematic = true;
            boxRigidbody.useGravity = false;
        }

        Debug.Log("Box agganciata al liftTransform: " + currentMast.liftTransform.name);
    }



    void ReleaseBox()
    {
        if (targetBox == null) return;

        Rigidbody boxRigidbody = targetBox.GetComponent<Rigidbody>();
        if (boxRigidbody != null)
        {
            boxRigidbody.isKinematic = false;
            boxRigidbody.useGravity = true;
        }

        // Posiziona la box leggermente sopra il punto di rilascio per evitare compenetrazione
        targetBox.transform.SetParent(null);
        targetBox.transform.position += Vector3.up * 0.2f;

        Debug.Log("Box rilasciata.");
        targetBox = null;
    }


    IEnumerator LowerLift()
    {
        MastSettings currentMast = forkliftController.masts[currentMastIndex];

        while (currentMast.liftTransform.localPosition.y > currentMast.minLiftHeight)
        {
            currentMast.liftTransform.localPosition -= Vector3.up * currentMast.liftSpeed * Time.deltaTime;
            yield return null;
        }

        currentMast.liftTransform.localPosition = new Vector3(
            currentMast.liftTransform.localPosition.x,
            currentMast.minLiftHeight,
            currentMast.liftTransform.localPosition.z
        );

        Debug.Log("Elevatore abbassato completamente.");
    }
}
