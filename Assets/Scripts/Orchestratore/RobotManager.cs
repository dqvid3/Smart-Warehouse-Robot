using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;
using Neo4j.Driver;
using System;
using System.Collections;
using static Robot;

public class RobotManager : MonoBehaviour
{   
    public List<Robot> robots = new List<Robot>(); // Lista di robot registrati
    public DatabaseManager databaseManager;
    private HashSet<Vector3> assignedParcels = new HashSet<Vector3>(); // Per tracciare i pacchi assegnati
    private Queue<TaskAssignment> pendingTasks = new Queue<TaskAssignment>(); // Coda dei compiti
    private List<Vector3> slotPositions = new List<Vector3>(); // Per tracciare le posizioni assegnate
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

        Debug.Log($"Assegnando compito al Robot {availableRobot.id}...");
        StartCoroutine(HandlePickTask(availableRobot, parcelPosition));

        // Rimuove il pacco assegnato dall'elenco
        assignedParcels.Add(parcelPosition);
    }

    // Coroutine per gestire il compito di prelevare il pacco
    private IEnumerator HandlePickTask(Robot robot, Vector3 parcelPosition)
    { 
        if (robot.isActive)
        {
            robot.currentState = Robot.RobotState.PickUpState;
            robot.currentTask = "Picking up the parcel";
            _ = UpdateStateInDatabase(robot);
            Debug.Log($"Robot {robot.id} is taking the parcel.");

            yield return StartCoroutine(robot.forkliftNavController.PickParcelFromDelivery(parcelPosition, (parcel, category, idParcel) =>
            {
                StartCoroutine(FindSlotAndStore(robot, parcel, category, parcelPosition, idParcel));
            }));
        }
        
    }


    private IEnumerator FindSlotAndStore(Robot robot, GameObject parcel, string category, Vector3 parcelPosition, string idParcel)
    {

        IList<IRecord> result = Task.Run(() => GetAvailableSlot(category)).Result;

        if (result.Count == 0)
        {
            Debug.LogWarning($"No available slot found for category {category}");
            yield break;
        }

        Vector3 slotPosition = new Vector3(
            result[0]["x"].As<float>(),
            result[0]["y"].As<float>(),
            result[0]["z"].As<float>()
        );

        long slotId = result[0]["slotId"].As<long>();

        if (slotPositions.Contains(slotPosition))
        {
            Debug.LogWarning("Slot già assegnato, riprova.");
            yield break;
        }

        slotPositions.Add(slotPosition);
        robot.currentState = RobotState.StoreState;
        robot.currentTask = "Stoccaggio pacco nello scaffale";
        _ = UpdateStateInDatabase(robot);
        Debug.Log($"Robot {robot.id} sta stoccando il pacco.");
        yield return StartCoroutine(robot.forkliftNavController.StoreParcel(slotPosition, parcel, slotId, idParcel));

        slotPositions.Remove(slotPosition);

        robot.currentTask = "None";
        robot.currentState = RobotState.Idle;
        _ = UpdateStateInDatabase(robot);
        NotifyTaskCompletion(robot.id, "Store Parcel");
    }

    private async Task<IList<IRecord>> GetAvailableSlot(string category)
    {
        string query = @"
        MATCH (s:Shelf {category: $category})-[:HAS_LAYER]->(l:Layer)-[:HAS_SLOT]->(slot:Slot)
        WHERE NOT (slot)-[:CONTAINS]->(:Parcel)
        RETURN s.x + slot.x AS x, l.y AS y, s.z AS z, ID(slot) AS slotId
        LIMIT 1";
        return await neo4jHelper.ExecuteReadListAsync(query, new Dictionary<string, object> { { "category", category } });
    }

    private Robot FindAvailableRobot()
    {
        Robot bestRobot = null;
        float shortestDistance = float.MaxValue;

        foreach (Robot robot in robots)
        {
            if (robot.currentState == Robot.RobotState.Idle)
            {
                float distance = Vector3.Distance(robot.transform.position, transform.position);
                if (distance < shortestDistance)
                {
                    shortestDistance = distance;
                    bestRobot = robot;
                }
            }
        }
        return bestRobot;
    }

    private async void UpdateParcelStatus(float z, bool hasParcel)
    {
        await neo4jHelper.UpdateParcelPositionStatusAsync(z, hasParcel);
    }

    public void NotifyTaskCompletion(int robotId, string task)
    {
        Debug.Log($"Robot {robotId} ha completato il task: {task}");
        Debug.Log($"Pending tasks remaining: {pendingTasks.Count}");

        if (pendingTasks.Count > 0)
        {
            var nextTask = pendingTasks.Dequeue();
            AssignTask(nextTask.ParcelPosition);
        }
    }

    private async Task UpdateStateInDatabase(Robot robot)
    {
        if (databaseManager != null)
        {
            string robotState = robot.currentState.ToString();
            string task = robot.currentTask != null ? robot.currentTask : "No Task";
            await databaseManager.UpdateRobotStateAsync(robot.id.ToString(), robotState, task, robot.batteryLevel);
        }
    }
}

public class TaskAssignment
{
    public Vector3 ParcelPosition { get; private set; }

    public TaskAssignment(Vector3 parcelPosition)
    {
        ParcelPosition = parcelPosition;
    }
}
