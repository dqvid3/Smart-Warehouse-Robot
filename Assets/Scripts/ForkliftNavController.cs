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
        yield return new WaitUntil(() => agent.remainingDistance <= agent.stoppingDistance);

        // Seleziona il mast corretto in base all'altezza della box
        MastSettings selectedMast = SelectAppropriateMast(targetBox.transform.position.y);

        if (selectedMast == null || selectedMast.liftTransform == null)
        {
            Debug.LogError("Mast appropriato non trovato o LiftTransform non assegnato!");
            yield break;
        }

        // Solleva l'elevatore per raggiungere la box
        float targetLiftHeight = targetBox.transform.position.y + liftHeightOffset;
        while (selectedMast.liftTransform.localPosition.y < targetLiftHeight)
        {
            selectedMast.liftTransform.localPosition += Vector3.up * selectedMast.liftSpeed * Time.deltaTime;
            yield return null;
        }

        // Aggancia la box all'elevatore
        targetBox.transform.SetParent(selectedMast.liftTransform);
        targetBox.transform.localPosition = new Vector3(0, 0.5f, 0);
        Rigidbody boxRigidbody = targetBox.GetComponent<Rigidbody>();
        if (boxRigidbody != null)
        {
            boxRigidbody.isKinematic = true;
            boxRigidbody.useGravity = false;
        }

        Debug.Log("Box agganciata al liftTransform: " + selectedMast.liftTransform.name);

        // Muoviti leggermente indietro per liberare spazio
        Debug.Log("Sposto il Reachlift leggermente indietro");
        agent.SetDestination(transform.position - transform.forward * 2f);
        yield return new WaitUntil(() => agent.remainingDistance <= agent.stoppingDistance);

        // Muoviti verso la shipping area
        Debug.Log("Muovo il Reachlift verso la Shipping Area: " + shippingPoint.position);
        agent.SetDestination(shippingPoint.position);
        yield return new WaitUntil(() => agent.remainingDistance <= agent.stoppingDistance);
        Debug.Log("Arrivato alla Shipping Area");

        // Rilascia la box
        if (boxRigidbody != null)
        {
            boxRigidbody.isKinematic = false;
            boxRigidbody.useGravity = true;
        }
        targetBox.transform.SetParent(null);
        targetBox = null;

        // Abbassa l'elevatore
        Debug.Log("Abbasso l'elevatore");
        while (selectedMast.liftTransform.localPosition.y > 0)
        {
            selectedMast.liftTransform.localPosition -= Vector3.up * selectedMast.liftSpeed * Time.deltaTime;
            yield return null;
        }

        // Torna alla posizione originale
        Debug.Log("Torno alla posizione originale: " + originalPosition);
        agent.SetDestination(originalPosition);
    }

    // Metodo per selezionare il mast appropriato in base all'altezza della box
    MastSettings SelectAppropriateMast(float boxHeight)
    {
        foreach (MastSettings mast in forkliftController.masts)
        {
            if (mast.maxLiftHeight >= boxHeight)
            {
                Debug.Log("Selezionato Mast con altezza massima: " + mast.maxLiftHeight);
                return mast;
            }
        }
        return null;
    }
}
