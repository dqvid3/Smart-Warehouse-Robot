using System.Collections;
using UnityEngine;
using System.Threading.Tasks;
using Neo4j.Driver;
using UnityEngine.AI;
using UnityEngine.Apple;

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
    public NavMeshAgent navMeshAgent;
    public RobotManager robotManager;
    public Vector3 position;
    private Vector3 originPosition = Vector3.zero;

    public enum RobotState
    {
        Idle, // Robot inattivo, in attesa di compiti
        Wait, // Robot fermo, in attesa
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
                Debug.Log($"Robot {id} è in attesa di task.");
                break;

            case RobotState.Wait:
                WaitingRoutine();
                break;

            case RobotState.RechargeState:
                StartCoroutine(ChargingRoutine());
                break;

            case RobotState.StoreState:
                currentTask = $"Robot {id}: Picking up the parcel at position: {position}";
                Debug.Log(currentTask);
                StartCoroutine(HandleStoreTask());
                break;

            case RobotState.ShippingState:
                currentTask = $"Robot {id}: Inizia la consegna del pacco.";
                Debug.Log(currentTask);
                StartCoroutine(HandleShippingTask());
                break;
        }
    }
    private IEnumerator HandleStoreTask()
    {
        _ = UpdateStateInDatabase();

        bool taskSuccess = false;

        // Prova a eseguire il compito
        yield return StartCoroutine(forkliftNavController.PickParcelFromDelivery(position, (parcel, category, idParcel) =>
        {
            if (parcel != null && !string.IsNullOrEmpty(category) && !string.IsNullOrEmpty(idParcel))
            {
                StartCoroutine(FindSlotAndStore(parcel, category, position, idParcel));
                taskSuccess = true;
            }
        }));

        // Controlla il risultato
        if (!taskSuccess)
        {
            Debug.LogWarning($"Robot {id}: Fallimento nel prelevare il pacco. Torno alla posizione originale.");
            yield return StartCoroutine(forkliftNavController.MoveToOriginPosition());
            currentState = RobotState.Idle;
            _ = UpdateStateInDatabase();
        }
    }


    private IEnumerator HandleShippingTask()
    {
        _ = UpdateStateInDatabase();
        Vector3 destination = robotManager.askConveyorPosition(); 
        yield return StartCoroutine(forkliftNavController.PickParcelFromShelf(position, destination));
        currentState = RobotState.Idle;
        _ =  robotManager.removeParcelFromShelf(position);
        robotManager.NotifyTaskCompletion(id);
    }


    private IEnumerator FindSlotAndStore(GameObject parcel, string category, Vector3 parcelPosition, string idParcel)
    {
        var (slotPosition, slotId) = Task.Run(() => robotManager.GetAvailableSlot(id, category)).Result;

        if (slotId == -1)
        {
            yield break;
        }

        currentTask = "Stoccaggio pacco nello scaffale";
        _ = UpdateStateInDatabase();
        yield return StartCoroutine(forkliftNavController.StoreParcel(slotPosition, parcel, slotId, idParcel));

        currentState = RobotState.Idle;
        robotManager.NotifyTaskCompletion(id);
    }

    private void WaitingRoutine()
    {
        navMeshAgent.speed = 0;
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
