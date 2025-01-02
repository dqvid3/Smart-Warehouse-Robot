using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;
using Neo4j.Driver;
using System;
using System.Collections;
using static Robot;
using System.Linq;

public class RobotManager : MonoBehaviour
{   
    public List<Robot> robots = new List<Robot>(); // Lista di robot registrati
    public DatabaseManager databaseManager;
    private HashSet<Vector3> assignedParcels = new HashSet<Vector3>(); // Per tracciare i pacchi assegnati
    private Queue<TaskAssignment> pendingTasks = new Queue<TaskAssignment>(); // Coda dei compiti
    public List<Vector3> slotPositions = new List<Vector3>(); // Per tracciare le posizioni assegnate
    private Neo4jHelper neo4jHelper; // Helper per il database

    private float checkInterval = 2f; // Intervallo tra le query
    private float lastCheckTime = 0f;

    private void Start()
    {
        string robotList = string.Join(", ", robots.Select(r => $"ID: {r.id}, Stato: {r.currentState}").ToArray());
        Debug.Log($"RobotManager avviato.\nRobot collegati: [{robotList}]");
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
                AssignStoreTask(parcelPosition);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error checking for parcels: {ex.Message}");
        }
    }

    // Assegna un compito a un robot disponibile
    private void AssignStoreTask(Vector3 parcelPosition)
    {
        Robot availableRobot = FindAvailableRobot();
        if (availableRobot == null)
        {
            Debug.LogWarning("Nessun robot disponibile. Compito aggiunto in coda.");
            pendingTasks.Enqueue(new TaskAssignment(parcelPosition));
            return;
        }

        Debug.Log($"Assegnando compito al Robot {availableRobot.id}...");
        
        availableRobot.position = parcelPosition;
        availableRobot.currentState = RobotState.StoreState;
        // Rimuove il pacco assegnato dall'elenco
        assignedParcels.Add(parcelPosition);
    }


    public async Task<(Vector3 slotPosition, long slotId)> GetAvailableSlot(string category)
    {
        string query = @"
    MATCH (s:Shelf {category: $category})-[:HAS_LAYER]->(l:Layer)-[:HAS_SLOT]->(slot:Slot)
    WHERE NOT (slot)-[:CONTAINS]->(:Parcel)
    RETURN s.x + slot.x AS x, l.y AS y, s.z AS z, ID(slot) AS slotId
    LIMIT 1";

        IList<IRecord> result = await neo4jHelper.ExecuteReadListAsync(query, new Dictionary<string, object> { { "category", category } });

        if (result.Count > 0)
        {
            var slotData = result[0];
            float x = slotData["x"].As<float>();
            float y = slotData["y"].As<float>();
            float z = slotData["z"].As<float>();
            long slotId = slotData["slotId"].As<long>();

            Vector3 slotPosition = new Vector3(x, y, z);

            Debug.Log($"Slot trovato: Posizione ({slotPosition.x}, {slotPosition.y}, {slotPosition.z}), Slot ID: {slotId}");

            return (slotPosition, slotId);
        }
        else
        {
            Debug.LogWarning("Nessuno slot disponibile per la categoria specificata.");
            return (Vector3.zero, -1); // Restituisce valori predefiniti se nessuno slot è disponibile
        }
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

    public void NotifyTaskCompletion(int robotId)
    {
        Debug.Log($"Robot {robotId} ha completato il task.");
        Debug.Log($"Pending tasks remaining: {pendingTasks.Count}");

        if (pendingTasks.Count > 0)
        {
            var nextTask = pendingTasks.Dequeue();
            AssignStoreTask(nextTask.ParcelPosition);
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
