using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;
using Neo4j.Driver;
using System;
using System.Collections;

public class RobotManager : MonoBehaviour
{
    public List<Robot> robots = new List<Robot>(); // Lista di robot registrati
    private HashSet<Vector3> assignedParcels = new HashSet<Vector3>(); // Per tracciare i pacchi assegnati
    private Queue<TaskAssignment> pendingTasks = new Queue<TaskAssignment>(); // Coda dei compiti
    private Neo4jHelper neo4jHelper; // Helper per il database

    private float checkInterval = 2f; // Intervallo tra le query
    private float lastCheckTime = 0f;

    private void Start()
    {
        Debug.Log("RobotManager avviato.");
        neo4jHelper = new Neo4jHelper("bolt://localhost:7687", "neo4j", "password");
    }

    private void Update()
    {
        if (Time.time - lastCheckTime > checkInterval)
        {
            lastCheckTime = Time.time;
            CheckForParcelsInDeliveryArea();
        }
    }

    // Query per trovare pacchi nella zona di consegna
    private async void CheckForParcelsInDeliveryArea()
    {
        try
        {
            string query = @"
            MATCH (delivery:Area {type: 'Delivery'})-[:HAS_POSITION]->(pos:Position {hasParcel: true})
            RETURN pos.x AS x, pos.y AS y, pos.z AS z
            ";

            IList<IRecord> result = await neo4jHelper.ExecuteReadListAsync(query);

            foreach (var record in result)
            {
                Vector3 parcelPosition = new Vector3(record["x"].As<float>(), record["y"].As<float>(), record["z"].As<float>());

                // Verifica se il pacco è già stato assegnato
                if (assignedParcels.Contains(parcelPosition)) continue;

                Debug.Log($"Parcel detected at position {parcelPosition}");

                // Assegna il compito
                AssignTask(parcelPosition);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error checking for parcels: {ex.Message}");
        }
    }

    // Assegna un compito a un robot disponibile
    private void AssignTask(Vector3 parcelPosition)
    {
        Robot availableRobot = FindAvailableRobot();
        if (availableRobot == null)
        {
            Debug.LogWarning("Nessun robot disponibile. Compito aggiunto in coda.");
            pendingTasks.Enqueue(new TaskAssignment(parcelPosition));
            return;
        }

        // Aggiorna lo stato del robot
        availableRobot.currentState = Robot.RobotState.TrasportoPacco;

        Debug.Log($"Assegnando compito al Robot {availableRobot.id}...");
        StartCoroutine(HandleRobotTask(availableRobot, parcelPosition));

        // Aggiorna il database per indicare che il pacco non è più nella delivery area
        UpdateParcelStatus(parcelPosition.z, false);

        // Rimuove il pacco assegnato dall'elenco
        assignedParcels.Add(parcelPosition);
    }

    // Coroutine per gestire il compito del robot
    private IEnumerator HandleRobotTask(Robot robot, Vector3 parcelPosition)
    {
        yield return StartCoroutine(robot.forkliftNavController.PickParcelFromDelivery(parcelPosition));

        // Una volta completato il task, aggiorna lo stato del robot
        robot.currentState = Robot.RobotState.InAttesa;
        NotifyTaskCompletion(robot.id, "Trasporto Pacco");
    }

    // Trova un robot disponibile
    private Robot FindAvailableRobot()
    {
        foreach (Robot robot in robots)
        {
            if (robot.currentState == Robot.RobotState.InAttesa)
            {
                return robot;
            }
        }
        return null;
    }

    // Aggiorna lo stato del pacco nella posizione specificata
    private async void UpdateParcelStatus(float z, bool hasParcel)
    {
        await neo4jHelper.UpdateParcelPositionStatusAsync(z, hasParcel);
    }

    // Notifica il completamento del compito
    public void NotifyTaskCompletion(int robotId, string task)
    {
        Debug.Log($"Robot {robotId} ha completato il task: {task}");

        // Assegna il prossimo compito dalla coda
        if (pendingTasks.Count > 0)
        {
            var nextTask = pendingTasks.Dequeue();
            AssignTask(nextTask.ParcelPosition);
        }
    }
}

// Classe per rappresentare un compito
public class TaskAssignment
{
    public Vector3 ParcelPosition { get; private set; }

    public TaskAssignment(Vector3 parcelPosition)
    {
        ParcelPosition = parcelPosition;
    }
}
