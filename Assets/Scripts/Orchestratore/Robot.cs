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
    public bool isActive = false;
    public DatabaseManager databaseManager;
    public ForkliftNavController forkliftNavController;

    public enum RobotState
    {
        InAttesa,        // Robot inattivo, in attesa di compiti
        TrasportoPacco,  // Robot sta trasportando un pacco
        InRicarica,      // Robot è in fase di ricarica
        Attivo           // Robot attivo e pronto a lavorare
    }

    public RobotState currentState = RobotState.InAttesa;  // Stato iniziale del robot

    private void Start()
    {
        currentState = RobotState.InAttesa;
    }

    public string GetCurrentState() => currentState.ToString();

    public string GetCurrentTask() => currentTask != null ? currentTask : "No Task";

    public async void ActivateRobot()
    {
        if (batteryLevel > 0)
        {
            isActive = true;
            currentState = RobotState.Attivo;
            await UpdateStateInDatabase();
            Debug.Log($"Robot {id} attivato!");
        }
        else
        {
            Debug.Log($"Robot {id}: batteria esaurita, impossibile attivarlo.");
        }
    }

    public async void DeactivateRobot()
    {
        isActive = false;
        currentState = RobotState.InAttesa;
        currentTask = "None";
        await UpdateStateInDatabase();
        Debug.Log($"Robot {id} disattivato!");
    }

    public async void StartCharging()
    {
        currentState = RobotState.InRicarica;
        isActive = false;
        await UpdateStateInDatabase();
        Debug.Log($"Robot {id} in ricarica...");
        StartCoroutine(ChargingRoutine());
    }

    private IEnumerator ChargingRoutine()
    {
        float chargingTime = 5f;
        float elapsedTime = 0f;

        while (elapsedTime < chargingTime)
        {
            elapsedTime += Time.deltaTime;
            batteryLevel = Mathf.Lerp(0, 100, elapsedTime / chargingTime);
            yield return null;
        }

        batteryLevel = 100f;
        isActive = true;
        currentState = RobotState.Attivo;
        Debug.Log($"Robot {id} ha completato la ricarica.");
    }

    public IEnumerator PickupParcel(Vector3 parcelPosition)
    {
        if (isActive)
        {
            currentState = RobotState.TrasportoPacco;
            currentTask = "Trasporto pacco dalla zona di consegna";
            _ = UpdateStateInDatabase(); // Chiamata senza "await" perché non è più asincrona
            Debug.Log($"Robot {id} sta iniziando il trasporto del pacco.");

            // Richiama il controller per gestire il trasporto
            yield return forkliftNavController.PickParcelFromDelivery(parcelPosition);

            // Aggiorna lo stato del robot al completamento
            currentTask = "None";
            currentState = RobotState.Attivo;
            _ = UpdateStateInDatabase();
            Debug.Log($"Robot {id} ha completato il trasporto del pacco.");
        }
        else
        {
            Debug.Log($"Robot {id} inattivo, impossibile iniziare il trasporto.");
        }
    }


    public IEnumerator DeliverParcel(Vector3 parcelPosition, Vector3 shippingPosition)
    {
        if (isActive)
        {
            currentState = RobotState.TrasportoPacco;
            currentTask = "Trasporto pacco verso la spedizione";
            _ = UpdateStateInDatabase();
            Debug.Log($"Robot {id} sta trasportando il pacco verso la zona di spedizione.");

            // Richiama il controller per gestire il trasporto
            yield return forkliftNavController.PickParcelFromShelf(parcelPosition);
            yield return forkliftNavController.MoveToPosition(shippingPosition);

            // Aggiorna lo stato del robot al completamento
            currentTask = "None";
            currentState = RobotState.Attivo;
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
