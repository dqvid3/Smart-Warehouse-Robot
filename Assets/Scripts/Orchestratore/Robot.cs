using System.Collections;
using UnityEngine;
using System.Threading.Tasks;
using Neo4j.Driver;
using UnityEngine.AI;
using UnityEngine.Apple;
using UnityEngine.Rendering;
using System;

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
    public Vector3 destination;
    private Vector3 originPosition = Vector3.zero;
    private Vector3 currentRobotPosition;

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
        originPosition = forkliftNavController.GetPosition();
        currentRobotPosition = originPosition;
    }

    private float batteryDrainInterval = 5f; // Intervallo in secondi per il drenaggio della batteria
    private float batteryDrainTimer = 0f;    // Timer per il drenaggio della batteria

    private float positionUpdateInterval = 2f; // Intervallo in secondi per l'aggiornamento della posizione
    private float positionUpdateTimer = 0f;    // Timer per l'aggiornamento della posizione

    private async void Update()
    {
        // Aggiorna la posizione corrente ogni 2 secondi
        positionUpdateTimer += Time.deltaTime;
        if (positionUpdateTimer >= positionUpdateInterval)
        {
            positionUpdateTimer = 0f; // Resetta il timer
            currentRobotPosition = forkliftNavController.GetPosition(); // Aggiorna la posizione
            await UpdateRobotPositionInDatabase();
        }

        // Riduzione del livello di batteria
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
            previousPosition = destination;
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
                currentTask = $"Robot {id}: Picking up the parcel at position: {destination}";
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
        yield return StartCoroutine(forkliftNavController.PickParcelFromDelivery(destination, (parcel, category, idParcel) =>
        {
            if (parcel != null && !string.IsNullOrEmpty(category) && !string.IsNullOrEmpty(idParcel))
            {
                StartCoroutine(FindSlotAndStore(parcel, category, destination, idParcel));
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
        yield return StartCoroutine(forkliftNavController.PickParcelFromShelf(destination, destination));
        currentState = RobotState.Idle;
        _ =  robotManager.RemoveParcelFromShelf(destination);
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

    private Coroutine waitingRoutineCoroutine;

    private void WaitingRoutine()
    {
        if (waitingRoutineCoroutine != null)
        {
            StopCoroutine(waitingRoutineCoroutine); // Interrompe eventuali routine precedenti
        }

        waitingRoutineCoroutine = StartCoroutine(HandleWaitingRoutine());
    }

    private IEnumerator HandleWaitingRoutine()
    {
        float initialSpeed = navMeshAgent.speed; // Salva la velocità iniziale del NavMeshAgent
        navMeshAgent.speed = 0; // Ferma il movimento del robot

        Debug.Log($"Robot {id} è in attesa. Tornerà allo stato precedente in 5 secondi.");

        yield return new WaitForSeconds(5f); // Aspetta 5 secondi

        navMeshAgent.speed = initialSpeed; // Ripristina la velocità del robot
        currentState = previousState; // Torna allo stato precedente salvato
        Debug.Log($"Robot {id} ha ripreso lo stato: {previousState}");

        waitingRoutineCoroutine = null; // Reset della variabile Coroutine
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
        destination = previousPosition;
        currentTask = previousTask;

        Debug.Log($"Robot {id} ha completato la ricarica e riprende il task: {currentTask}");
    }

    private async Task UpdateStateInDatabase()
    {
        if (databaseManager != null)
        {
            string robotState = currentState.ToString();
            string task = currentTask != null ? currentTask : "No Task";

            await databaseManager.UpdateRobotStateAsync(
                id,   // ID del robot
                currentRobotPosition.x,  // Posizione X
                currentRobotPosition.z,  // Posizione Z
                isActive,  // Stato attivo
                task,  // Task corrente
                robotState,  // Stato del robot
                batteryLevel  // Livello della batteria
            );
        }
    }

    private async Task UpdateRobotPositionInDatabase()
    {
        try
        {
            if (databaseManager != null)
            {
                await databaseManager.UpdateRobotPositionAsync(
                    id,   // ID del robot
                    currentRobotPosition.x,  // Posizione X
                    currentRobotPosition.z   // Posizione Z
                );
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Errore durante l'aggiornamento della posizione del robot {id}: {ex.Message}");
        }
    }
}
