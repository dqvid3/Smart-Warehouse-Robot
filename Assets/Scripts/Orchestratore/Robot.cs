using System.Collections;
using UnityEngine;

public class Robot : MonoBehaviour
{
    [Header("Caratteristiche Robot")]
    public int id;
    public Vector3 destination;
    public float batteryLevel = 100f;
    public RobotState currentState = RobotState.Idle;
    public string currentTask = "None";

    [Header("Script posizione stimata Robot")]
    public RobotKalmanPosition kalmanPosition;

    private RobotState previousState = RobotState.Idle;
    private string previousTask = "None";
    private Vector3 previousDestination;
    private ForkliftNavController forkliftNavController;
    private RobotExplainability explainability; 
    private RobotState lastKnownState = RobotState.Idle;
    public float speed;

    public enum RobotState
    {
        Idle,
        RechargeState,
        DeliveryState,
        ShippingState,
        DisposalState
    }

    private float batteryDrainInterval = 5f;
    private float batteryDrainTimer = 0f;
    public bool isPaused = false;

    public string category = null;
    public string timestamp = null;
    public GameObject collisionWarningIndicator; 
    public MovementWithAStar movementWithAStar;

    void Start()
    {
        collisionWarningIndicator.SetActive(false);
        forkliftNavController = GetComponent<ForkliftNavController>();
        explainability = GetComponent<RobotExplainability>();
        movementWithAStar = GetComponent<MovementWithAStar>();
        speed = movementWithAStar.moveSpeed;
    }

    public void ShowCollisionWarning(bool show)
    {
        collisionWarningIndicator.SetActive(show);
    }

    void Update()
    {
        if (isPaused) 
            movementWithAStar.moveSpeed = 0;
        else
            movementWithAStar.moveSpeed = speed;
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
            case RobotState.DisposalState:
                StartCoroutine(HandleDisposalTask());
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
        currentState = previousState;
        destination = previousDestination;
        currentTask = previousTask;
    }

    private IEnumerator HandleDeliveryTask()
    {
        yield return StartCoroutine(forkliftNavController.PickParcelFromDelivery(destination, id));
        currentState = RobotState.Idle;
    }

    private IEnumerator HandleShippingTask()
    {
        yield return StartCoroutine(forkliftNavController.ShipParcel(destination, id));
        currentState = RobotState.Idle;
    }

    private IEnumerator HandleDisposalTask() 
    {
        yield return StartCoroutine(forkliftNavController.TakeParcelFromShelf(destination, id));   
        yield return StartCoroutine(forkliftNavController.PlaceParcelOnShelf(category, id, timestamp));   
        currentState = RobotState.Idle;
    }
    public Vector3 GetEstimatedPosition()
    {
        return kalmanPosition.GetEstimatedPosition();
    }
}