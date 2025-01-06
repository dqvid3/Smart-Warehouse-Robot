using System.Collections;
using UnityEngine;
using System.Threading.Tasks;
using Neo4j.Driver;
using UnityEngine.AI;
using System;

public class Robot : MonoBehaviour
{
    public int id;
    public float batteryLevel = 100f;
    public string currentTask = "None";
    private RobotState previousState = RobotState.Idle;
    private string previousTask = "None";
    private Vector3 previousDestination;
    public bool isActive = true;
    public DatabaseManager databaseManager;
    public ForkliftNavController forkliftNavController;
    public NavMeshAgent navMeshAgent;
    public RobotManager robotManager;
    public Vector3 destination;
    private Vector3 originPosition = Vector3.zero;
    public Vector3 currentRobotPosition;
    private RobotState lastKnownState = RobotState.Idle;
    public bool isPaused = false;
    private float pauseTimer = 0f;
    private float pauseDuration = 0f;

    public enum RobotState
    {
        Idle,
        RechargeState,
        StoreState,
        ShippingState
    }

    public RobotState currentState = RobotState.Idle;
    private float batteryDrainInterval = 5f;
    private float batteryDrainTimer = 0f;
    private float positionUpdateInterval = 0.3f;
    private float positionUpdateTimer = 0f;

    private void Start()
    {
        currentState = RobotState.Idle;
        originPosition = forkliftNavController.GetPosition();
        currentRobotPosition = originPosition;
    }

    private async void Update()
    {
        positionUpdateTimer += Time.deltaTime;
        if (positionUpdateTimer >= positionUpdateInterval)
        {
            positionUpdateTimer = 0f;
            currentRobotPosition = forkliftNavController.GetPosition();
            await UpdateRobotPositionInDatabase();
        }
        if (Vector3.Distance(transform.position, originPosition) > 0.1f)
        {
            batteryDrainTimer += Time.deltaTime;
            if (batteryDrainTimer >= batteryDrainInterval)
            {
                batteryDrainTimer = 0f;
                if (batteryLevel > 0) batteryLevel -= 1f;
            }
        }
        if (batteryLevel < 5 && currentState != RobotState.RechargeState && !isPaused)
        {
            previousTask = currentTask;
            previousState = currentState;
            previousDestination = destination;
            currentState = RobotState.RechargeState;
        }
        if (currentState != lastKnownState)
        {
            OnStateChanged();
            lastKnownState = currentState;
        }
        if (isPaused)
        {
            pauseTimer += Time.deltaTime;
            if (pauseTimer >= pauseDuration)
            {
                isPaused = false;
                pauseTimer = 0f;
                if (navMeshAgent != null) navMeshAgent.speed = 3.5f;
            }
        }
    }

    private void OnStateChanged()
    {
        switch (currentState)
        {
            case RobotState.Idle:
                currentTask = "None";
                _ = UpdateStateInDatabase();
                break;
            case RobotState.RechargeState:
                StartCoroutine(ChargingRoutine());
                break;
            case RobotState.StoreState:
                currentTask = "Robot " + id + ": Picking up the parcel at position: " + destination;
                StartCoroutine(HandleStoreTask());
                break;
            case RobotState.ShippingState:
                currentTask = "Robot " + id + ": Inizia la consegna del pacco.";
                StartCoroutine(HandleShippingTask());
                break;
        }
    }

    private IEnumerator HandleStoreTask()
    {
        _ = UpdateStateInDatabase();
        bool taskSuccess = false;
        yield return StartCoroutine(forkliftNavController.PickParcelFromDelivery(destination, (parcel, category, idParcel) =>
        {
            if (parcel != null && !string.IsNullOrEmpty(category) && !string.IsNullOrEmpty(idParcel))
            {
                StartCoroutine(FindSlotAndStore(parcel, category, destination, idParcel));
                taskSuccess = true;
            }
        }));
        if (!taskSuccess)
        {
            yield return StartCoroutine(forkliftNavController.MoveToOriginPosition());
            currentState = RobotState.Idle;
            _ = UpdateStateInDatabase();
        }
    }

    private IEnumerator HandleShippingTask()
    {
        _ = UpdateStateInDatabase();
        Vector3 conveyor = robotManager.askConveyorPosition();
        yield return StartCoroutine(forkliftNavController.PickParcelFromShelf(destination, conveyor));
        currentState = RobotState.Idle;
        _ = robotManager.RemoveParcelFromShelf(destination);
        robotManager.NotifyTaskCompletion(id);
    }

    private IEnumerator FindSlotAndStore(GameObject parcel, string category, Vector3 parcelPosition, string idParcel)
    {
        var (slotPosition, slotId) = Task.Run(() => robotManager.GetAvailableSlot(id, category)).Result;
        if (slotId == -1) yield break;
        currentTask = "Stoccaggio pacco nello scaffale";
        _ = UpdateStateInDatabase();
        yield return StartCoroutine(forkliftNavController.StoreParcel(slotPosition, parcel, slotId, idParcel));
        currentState = RobotState.Idle;
        robotManager.NotifyTaskCompletion(id);
    }

    public void Pause(float seconds=3f)
    {
        if (isPaused || currentState == RobotState.RechargeState) return;
        isPaused = true;
        pauseTimer = 0f;
        pauseDuration = seconds;
        if (navMeshAgent != null) navMeshAgent.speed = 0f;
    }

    private IEnumerator ChargingRoutine()
    {
        float chargingTime = 10f;
        float elapsedTime = 0f;
        yield return StartCoroutine(forkliftNavController.MoveToOriginPosition());
        while (elapsedTime < chargingTime)
        {
            elapsedTime += Time.deltaTime;
            batteryLevel = Mathf.Lerp(0, 100, elapsedTime / chargingTime);
            yield return null;
        }
        batteryLevel = 100f;
        isActive = true;
        currentState = previousState;
        destination = previousDestination;
        currentTask = previousTask;
    }

    private async Task UpdateStateInDatabase()
    {
        if (databaseManager != null)
        {
            string robotState = currentState.ToString();
            string task = currentTask != null ? currentTask : "No Task";
            await databaseManager.UpdateRobotStateAsync(
                id,
                currentRobotPosition.x,
                currentRobotPosition.z,
                isActive,
                task,
                robotState,
                batteryLevel
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
                    id,
                    currentRobotPosition.x,
                    currentRobotPosition.z
                );
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Errore durante l'aggiornamento della posizione del robot " + id + ": " + ex.Message);
        }
    }
}
