using System.Collections;
using UnityEngine;
using System.Threading.Tasks;
using Neo4j.Driver;

public class Robot : MonoBehaviour
{
    [Header("Robot Settings")]
    public int id;
    public float batteryLevel = 100f;
    public string currentTask = "None";
    private string previousTask = "None"; // Memorizza il task precedente
    private Vector3 previousPosition; // Posizione relativa al task precedente
    private RobotState previousState = RobotState.Idle; // Stato precedente
    public bool isActive = true;
    public DatabaseManager databaseManager;
    public ForkliftNavController forkliftNavController;
    public RobotManager robotManager;
    public Vector3 position;
    private Vector3 originPosition = Vector3.zero;

    public enum RobotState
    {
        Idle,         // Robot inattivo, in attesa di compiti
        RechargeState,// Robot è in fase di ricarica
        StoreState,   // Robot sta portando un pacco nello scaffale
        ShippingState // Robot sta portando un pacco nell'area di spedizione
    }

    public RobotState currentState = RobotState.Idle;  // Stato iniziale del robot

    private void Start()
    {
        currentState = RobotState.Idle;
        originPosition = transform.position;
    }

    private float batteryDrainInterval = 5f; // Intervallo in secondi per il drenaggio della batteria
    private float batteryDrainTimer = 0f;    // Timer per il drenaggio della batteria

    private void Update()
    {
        // Riduzione del livello di batteria ogni 2 secondi, solo se non si trova nella originPosition
        if (Vector3.Distance(transform.position, originPosition) > 0.1f) // Verifica che il robot non sia nella originPosition
        {
            batteryDrainTimer += Time.deltaTime;
            if (batteryDrainTimer >= batteryDrainInterval)
            {
                batteryDrainTimer = 0f; // Resetta il timer
                if (batteryLevel > 0) // Assicura che la batteria non scenda sotto 0
                {
                    batteryLevel -= 1f;
                    Debug.Log($"Robot {id}: Livello batteria attuale: {batteryLevel}%");
                }
            }
        }

        // Controllo del livello di batteria e cambio stato a RechargeState se necessario
        if (batteryLevel < 5 && currentState != RobotState.RechargeState)
        {
                // Salva il task corrente e lo stato prima di passare alla ricarica
                previousTask = currentTask;
                previousState = currentState;
                previousPosition = position;
                currentState = RobotState.RechargeState;  
        }

        // Controlla se lo stato è cambiato
        if (currentState != previousState)
        {
            OnStateChanged();
            previousState = currentState;
        }
    }



    private void OnStateChanged()
    {
        switch (currentState)
        {
            case RobotState.Idle:
                currentTask = "None";
                _ = UpdateStateInDatabase();
                Debug.Log($"Robot {id} è ora in attesa.");
                break;

            case RobotState.StoreState:
                currentTask = $"Robot {id}: Picking up the parcel at position: {position}";
                Debug.Log(currentTask);
                StartCoroutine(HandleStoreTask());
                break;

            case RobotState.RechargeState:
                StartCoroutine(ChargingRoutine());
                break;

            case RobotState.ShippingState:
                Debug.Log($"Robot {id}: Inizia la consegna del pacco.");
                break;
        }
    }

    private IEnumerator HandleStoreTask()
    {
        _ = UpdateStateInDatabase();
        yield return StartCoroutine(forkliftNavController.PickParcelFromDelivery(position, (parcel, category, idParcel) =>
        {
            StartCoroutine(FindSlotAndStore(parcel, category, position, idParcel));
        }));
    }

    private IEnumerator FindSlotAndStore(GameObject parcel, string category, Vector3 parcelPosition, string idParcel)
    {
        var (slotPosition, slotId) = Task.Run(() => robotManager.GetAvailableSlot(category)).Result;

        if (slotId == -1)
        {
            Debug.LogWarning($"Nessuno slot disponibile per la categoria {category}");
            yield break;
        }

        currentTask = "Stoccaggio pacco nello scaffale";
        _ = UpdateStateInDatabase();
        Debug.Log($"Robot {id}: Stocking the parcel");
        yield return StartCoroutine(forkliftNavController.StoreParcel(slotPosition, parcel, slotId, idParcel));

        currentState = RobotState.Idle;
        robotManager.NotifyTaskCompletion(id);
    }

    private IEnumerator ChargingRoutine()
    {
        float chargingTime = 10f;
        float elapsedTime = 0f;

        // Simula il movimento verso la posizione di origine
        yield return StartCoroutine(forkliftNavController.MoveToOriginPosition());
        Debug.Log($"Robot {id} inizia la ricarica.");
        while (elapsedTime < chargingTime)
        {
            elapsedTime += Time.deltaTime;
            batteryLevel = Mathf.Lerp(0, 100, elapsedTime / chargingTime);
            yield return null;
        }

        batteryLevel = 100f;
        isActive = true;

        // Riprendi il task precedente
        currentState = previousState;
        position = previousPosition;
        currentTask = previousTask;

        Debug.Log($"Robot {id} ha completato la ricarica e riprende il task: {currentTask}");
        OnStateChanged();
    }


    public IEnumerator DeliverParcel(Vector3 parcelPosition, Vector3 shippingPosition)
    {
        if (isActive)
        {
            currentState = RobotState.ShippingState;
            currentTask = "Trasporto pacco verso la spedizione";
            _ = UpdateStateInDatabase();
            Debug.Log($"Robot {id} sta trasportando il pacco verso la zona di spedizione.");

            // Richiama il controller per la consegna
            yield return forkliftNavController.PickParcelFromShelf(parcelPosition);
            yield return forkliftNavController.MoveToPosition(shippingPosition);

            currentTask = "None";
            currentState = RobotState.Idle;
            _ = UpdateStateInDatabase();
            Debug.Log($"Robot {id} ha completato la consegna del pacco.");
        }
        else
        {
            Debug.Log($"Robot {id} inattivo, impossibile effettuare la consegna.");
        }
    }

    private async Task UpdateStateInDatabase()
    {
        if (databaseManager != null)
        {
            string robotState = currentState.ToString();
            string task = currentTask != null ? currentTask : "No Task";
            await databaseManager.UpdateRobotStateAsync(id.ToString(), robotState, task, batteryLevel);
        }
    }
}
