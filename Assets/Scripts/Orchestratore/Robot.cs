using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;

public class Robot : MonoBehaviour
{
    [Header("Robot Settings")]
    public int id;
    public float batteryLevel = 100f;
    public string currentTask = "None";
    public bool isActive = true;
    public DatabaseManager databaseManager;
    public ForkliftNavController forkliftNavController;

    public enum RobotState
    {
        Idle,         // Robot inattivo, in attesa di compiti
        PickUpState,  // Robot sta andando a prendere un pacco
        RechargeState,// Robot è in fase di ricarica
        StoreState,   // Robot sta portando un pacco nello scaffale
        ShippingState // Robot sta portando un pacco nell'area di spedizione
    }

    public RobotState currentState = RobotState.Idle;  // Stato iniziale del robot

    private void Start()
    {
        currentState = RobotState.Idle;
    }

    public string GetCurrentState() => currentState.ToString();

    public string GetCurrentTask() => currentTask != null ? currentTask : "No Task";

    public async void ActivateRobot()
    {
        if (batteryLevel > 10)
        {
            isActive = true;
            currentState = RobotState.Idle;
            await UpdateStateInDatabase();
            Debug.Log($"Robot {id} attivato!");
        }
        else
        {
            currentState = RobotState.RechargeState;
            isActive = false;
            await UpdateStateInDatabase();
            Debug.Log($"Robot {id}: batteria quasi scarica.");
            StartCoroutine(ChargingRoutine());
        }
    }

    public async void DeactivateRobot()
    {
        isActive = false;
        currentState = RobotState.Idle;
        currentTask = "None";
        await UpdateStateInDatabase();
        Debug.Log($"Robot {id} disattivato!");
    }

    private IEnumerator ChargingRoutine()
    {
        float chargingTime = 10f;
        float elapsedTime = 0f;

        while (elapsedTime < chargingTime)
        {
            elapsedTime += Time.deltaTime;
            batteryLevel = Mathf.Lerp(0, 100, elapsedTime / chargingTime);
            yield return null;
        }

        batteryLevel = 100f;
        isActive = true;
        currentState = RobotState.Idle;
        Debug.Log($"Robot {id} ha completato la ricarica.");
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
