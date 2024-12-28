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
        TrasportoPacco,  // Robot sta trasportando un pacco (mettere diversi tipi di trasporto?)
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
            Debug.Log("Robot attivato!");
        }
        else
        {
            Debug.Log("Impossibile attivare il robot: batteria esaurita!");
        }
    }

    public async void DeactivateRobot()
    {
        isActive = false;
        currentState = RobotState.InAttesa;
        currentTask = "None";
        await UpdateStateInDatabase();
        Debug.Log("Robot disattivato!");
    }

    public async void StartCharging()
    {
        currentState = RobotState.InRicarica;
        isActive = false;
        await UpdateStateInDatabase();
        Debug.Log("Robot in ricarica...");
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
        //await UpdateStateInDatabase();
        Debug.Log("Ricarica completata. Robot attivato.");
    }

    /*public async void PickupParcel(Vector3 parcelPosition, Vector3 shelfPosition, Vector3 shippingPosition)
    {
        if (isActive)
        {
            currentState = RobotState.TrasportoPacco;
            currentTask = "Trasportando Pacco";
            await UpdateStateInDatabase();
            Debug.Log("Robot inizia a trasportare il pacco.");

            // Fase 1: Prendi il pacco
            yield return forkliftNavController.PickParcelFromDelivery(parcelPosition);

            // Fase 2: Posiziona il pacco nello scaffale
            //yield return forkliftNavController.PlaceParcelOnShelf(shelfPosition);

            // Fase 3: Sposta il pacco nell'area di spedizione
           // yield return forkliftNavController.DeliverParcelToShipping(shippingPosition);

            currentTask = "None";
            currentState = RobotState.Attivo;
            await UpdateStateInDatabase();
            Debug.Log("Pacco consegnato con successo.");
        }
        else
        {
            Debug.Log("Impossibile prendere il pacco: il robot è inattivo.");
        }
    }*/

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
