using System.Collections;
using UnityEngine;
using System.Threading.Tasks;

public class Robot : MonoBehaviour
{
    public int id;
    public RobotManager robotManager;  
    public Vector3 destination; 
    public float batteryLevel = 100f;
    public RobotState currentState = RobotState.Idle;
    public string currentTask = "None";
    public bool isActive = true;
    private RobotState previousState = RobotState.Idle;
    private string previousTask = "None";
    private Vector3 previousDestination;
    private DatabaseManager databaseManager;
    private ForkliftNavController forkliftNavController;
    private RobotState lastKnownState = RobotState.Idle;

    public enum RobotState
    {
        Idle,
        RechargeState,
        DeliveryState,
        ShippingState
    }

    private float batteryDrainInterval = 5f;
    private float batteryDrainTimer = 0f;

    void Start()
    {
        forkliftNavController = GetComponent<ForkliftNavController>();
        databaseManager = GetComponent<DatabaseManager>();
    }

    void Update()
    {
        if (Vector3.Distance(transform.position, forkliftNavController.defaultPosition) > 0.1f)
        {
            batteryDrainTimer += Time.deltaTime;
            if (batteryDrainTimer >= batteryDrainInterval)
            {
                batteryDrainTimer = 0f;
                if (batteryLevel > 0) batteryLevel -= 1f;
            }
        }
        if (batteryLevel < 5 && currentState != RobotState.RechargeState)
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
    }

    private void OnStateChanged()
    {
        switch (currentState)
        {
            case RobotState.Idle:
                currentTask = "Idle";
                _ = UpdateStateInDatabase();
                break;
            case RobotState.RechargeState:
                currentTask = "Recharge";
                StartCoroutine(ChargingRoutine());
                break;
            case RobotState.DeliveryState:
                currentTask = "Robot " + id + ": Picking up the parcel at position: " + destination;
                StartCoroutine(HandleDeliveryTask());
                break;
            case RobotState.ShippingState:
                currentTask = "Robot " + id + ": Inizia la consegna del pacco.";
                StartCoroutine(HandleShippingTask());
                break;
        }
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

    private IEnumerator HandleDeliveryTask()
    {
        _ = UpdateStateInDatabase();
        yield return StartCoroutine(forkliftNavController.DeliverParcel(destination));
        currentState = RobotState.Idle;
        robotManager.NotifyTaskCompletion(id);
    }

    private IEnumerator HandleShippingTask()
    {
        _ = UpdateStateInDatabase();
        Vector3 conveyorPosition = robotManager.askConveyorPosition();
        yield return StartCoroutine(forkliftNavController.ShipParcel(destination, conveyorPosition));
        currentState = RobotState.Idle;
        robotManager.NotifyTaskCompletion(id);
    }

    private async Task UpdateStateInDatabase()
    {/*
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
        }*/
    }
}