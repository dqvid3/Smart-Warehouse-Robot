using System.Collections;
using UnityEngine;
using System.Threading.Tasks;
using Neo4j.Driver;

public class Robot : MonoBehaviour
{
    [Header ("Caratteristiche Robot")]
    public int id;
    public RobotManager robotManager;
    public Vector3 destination;
    public float batteryLevel = 100f;
    public RobotState currentState = RobotState.Idle;
    public string currentTask = "None";
    public bool isActive = true;
    [Header("Script posizione stimata Robot")]
    public RobotKalmanPosition kalmanPosition;
    private RobotState previousState = RobotState.Idle;
    private string previousTask = "None";
    private Vector3 previousDestination;
    private DatabaseManager databaseManager;
    private ForkliftNavController forkliftNavController;
    private RobotExplainability explainability; // Per la spiegazione contestuale
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
        explainability = GetComponent<RobotExplainability>(); // Inizializza la classe per le spiegazioni contestuali
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
        RobotExplainability explainability = GetComponent<RobotExplainability>();

        switch (currentState)
        {
            case RobotState.Idle:
                explainability.ShowExplanation("Sono in standby, in attesa di un compito.");
                break;

            case RobotState.RechargeState:
                explainability.ShowExplanation("Mi sto ricaricando perch� la batteria � bassa.");
                StartCoroutine(ChargingRoutine());
                break;

            case RobotState.DeliveryState:
                explainability.ShowExplanation($"Sto prelevando un pacco all'area {destination} per stoccarlo nello scaffale corretto.");
                StartCoroutine(HandleDeliveryTask());
                break;

            case RobotState.ShippingState:
                explainability.ShowExplanation($"Sto consegnando un pacco al nastro trasportatore.");
                StartCoroutine(HandleShippingTask());
                break;
        }
    }

    private IEnumerator ChargingRoutine()
    {
        float chargingTime = 10f;
        float elapsedTime = 0f;

        explainability.ShowExplanation("Mi sto spostando verso l'area di ricarica.");
        yield return StartCoroutine(forkliftNavController.MoveToOriginPosition());

        explainability.ShowExplanation("Inizio la ricarica della batteria.");
        while (elapsedTime < chargingTime)
        {
            elapsedTime += Time.deltaTime;
            batteryLevel = Mathf.Lerp(0, 100, elapsedTime / chargingTime);
            yield return null;
        }

        explainability.ShowExplanation("Ricarica completata. Torno operativo.");
        batteryLevel = 100f;
        isActive = true;
        currentState = previousState;
        destination = previousDestination;
        currentTask = previousTask;
    }

    private IEnumerator HandleDeliveryTask()
    {
        _ = UpdateStateInDatabase();
        string qrCodeResult = "";
        yield return StartCoroutine(forkliftNavController.PickParcelFromDelivery(destination, (qrCode) => { qrCodeResult = qrCode; }));
        string[] qrParts = qrCodeResult.Split('|');
        IRecord record = robotManager.AskSlot(qrParts[1], id);
        yield return StartCoroutine(forkliftNavController.DeliverToShelf(destination, record, qrParts[0]));
        currentState = RobotState.Idle;
        robotManager.NotifyTaskCompletion(id);
    }

    private IEnumerator HandleShippingTask()
    {
        _ = UpdateStateInDatabase();
        Vector3 conveyorPosition = robotManager.AskConveyorPosition();
        yield return StartCoroutine(forkliftNavController.ShipParcel(destination, conveyorPosition));
        currentState = RobotState.Idle;
        robotManager.NotifyTaskCompletion(id);
    }

    public Vector3 GetEstimatedPosition()
    {
        return kalmanPosition.GetEstimatedPosition();
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