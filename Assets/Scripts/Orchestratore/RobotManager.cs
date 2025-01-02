using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;
using Neo4j.Driver;
using System;
using static Robot;
using System.Linq;

public class RobotManager : MonoBehaviour
{   
    public List<Robot> robots = new List<Robot>(); // Lista di robot registrati
    public DatabaseManager databaseManager;
    
    private Dictionary<Vector3, int> assignedParcels = new Dictionary<Vector3, int>(); // Per tracciare i pacchi assegnati
    private Dictionary<Vector3, int> assignedPositions = new Dictionary<Vector3, int>(); // Per tracciare le posizioni assegnate


    private Queue<Vector3> pendingStoreTasks = new Queue<Vector3>(); // Coda dei compiti di store
    private Queue<Vector3> pendingShippingTasks = new Queue<Vector3>(); // Coda dei compiti di shipping
    private List<Vector3> conveyorShipping;
    private Neo4jHelper neo4jHelper; // Helper per il database

    private float checkInterval = 2f; // Intervallo tra le query
    private float lastCheckTime = 0f;

    private async void Start()
    {
        string robotList = string.Join(", ", robots.Select(r => $"ID: {r.id}, Stato: {r.currentState}").ToArray());
        Debug.Log($"RobotManager avviato.\nRobot collegati: [{robotList}]");
        neo4jHelper = new Neo4jHelper("bolt://localhost:7687", "neo4j", "password");
        conveyorShipping = await GetConveyorPositionsInShipping();
    }


    private void Update()
    {
        if (Time.time - lastCheckTime > checkInterval)
        {
            lastCheckTime = Time.time;
            CheckForShippingOrders();
            CheckForParcelsInDeliveryArea();
            if (pendingShippingTasks.Count > 0)
            {
                var nextTask = pendingShippingTasks.Dequeue();
                AssignShippingTask(nextTask);
            }else if(pendingStoreTasks.Count > 0)
            {
                var nextTask = pendingStoreTasks.Dequeue();
                AssignStoreTask(nextTask);
            }

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
                if (assignedParcels.ContainsKey(parcelPosition)) continue;

                // Assegna il compito
                AssignStoreTask(parcelPosition);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error checking for parcels: {ex.Message}");
        }
    }

    private async void CheckForShippingOrders()
    {
        try
        {
            string query = @"
            MATCH (oldestOrder:Order)
            WITH oldestOrder
            ORDER BY oldestOrder.timestamp ASC
            LIMIT 1

            OPTIONAL MATCH (p:Parcel)-[:PART_OF]->(oldestOrder)
            RETURN oldestOrder, COUNT(p) AS parcelCount
        ";

            IList<IRecord> result = await neo4jHelper.ExecuteReadListAsync(query);

            foreach (var record in result)
            {
                var order = record["oldestOrder"].As<INode>();
                int parcelCount = record["parcelCount"].As<int>();

                if (parcelCount == 0)
                {
                    // L'ordine non ha pacchi, cancellalo
                    string deleteQuery = @"
                    MATCH (order:Order {orderId: $orderId})
                    DETACH DELETE order
                ";

                    var parameters = new Dictionary<string, object>
                {
                    { "orderId", order.Properties["orderId"].As<string>() }
                };

                    await neo4jHelper.ExecuteWriteAsync(deleteQuery, parameters);
                }
                else
                {
                    // Se ci sono pacchi, processa le posizioni dei pacchi
                    string parcelQuery = @"
                    MATCH (p:Parcel)-[:PART_OF]->(oldestOrder)
                    MATCH (s:Shelf)-[:HAS_LAYER]->(l:Layer)-[:HAS_SLOT]->(slot:Slot)-[:CONTAINS]->(p)
                    WHERE oldestOrder.orderId = $orderId
                    RETURN s.x + slot.x AS x, l.y AS y, s.z AS z
                ";

                    var parcelParameters = new Dictionary<string, object>
                {
                    { "orderId", order.Properties["orderId"].As<string>() }
                };

                    IList<IRecord> parcelResult = await neo4jHelper.ExecuteReadListAsync(parcelQuery, parcelParameters);

                    foreach (var parcelRecord in parcelResult)
                    {
                        Vector3 parcelPosition = new Vector3(
                            parcelRecord["x"].As<float>(),
                            parcelRecord["y"].As<float>(),
                            parcelRecord["z"].As<float>()
                        );

                        // Verifica se il pacco è già stato assegnato
                        if (assignedParcels.ContainsKey(parcelPosition)) continue;

                        // Assegna il compito
                        AssignShippingTask(parcelPosition);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error checking for shipping orders: {ex.Message}");
        }
    }

    private void AssignStoreTask(Vector3 parcelPosition)
    {
        Robot availableRobot = FindAvailableRobot();
        if (availableRobot == null)
        {
            // Controlla se il task è già presente nella coda
            if (!pendingStoreTasks.Any(task => task == parcelPosition))
            {
                Debug.LogWarning("Nessun robot disponibile. Compito aggiunto in coda.");
                pendingStoreTasks.Enqueue(parcelPosition);
            }
        }

        Debug.Log($"Assegnato store task al Robot {availableRobot.id}");

        availableRobot.position = parcelPosition;
        availableRobot.currentState = RobotState.StoreState;

        assignedParcels[parcelPosition] = availableRobot.id;
    }

    private void AssignShippingTask(Vector3 parcelPosition)
    {
        Robot availableRobot = FindAvailableRobot();
        if (availableRobot == null)
        {
            if (!pendingShippingTasks.Any(task => task == parcelPosition))
            {
                Debug.LogWarning("Nessun robot disponibile. Compito aggiunto in coda.");
                pendingShippingTasks.Enqueue(parcelPosition);
            }
            return;
        }

        Debug.Log($"Assegnato shipping task al Robot {availableRobot.id}");

        availableRobot.position = parcelPosition;
        availableRobot.currentState = RobotState.ShippingState;

        assignedParcels[parcelPosition] = availableRobot.id;
    }

    private int currentConveyorIndex = 0;

    public Vector3 askConveyorPosition()
    {
        // Controlla se la lista è vuota
        if (conveyorShipping == null || conveyorShipping.Count == 0)
        {
            Debug.LogWarning("Conveyor positions list is empty!");
            return Vector3.zero; // Ritorna una posizione nulla se la lista è vuota
        }

        // Ottieni la posizione corrente
        Vector3 selectedPosition = conveyorShipping[currentConveyorIndex];

        // Aggiorna l'indice per il prossimo valore (ciclico)
        currentConveyorIndex = (currentConveyorIndex + 1) % conveyorShipping.Count;

        return selectedPosition;
    }


    public async Task<(Vector3 slotPosition, long slotId)> GetAvailableSlot(int robotId, string category)
    {
        string query = @"
        MATCH (s:Shelf {category: $category})-[:HAS_LAYER]->(l:Layer)-[:HAS_SLOT]->(slot:Slot)
        WHERE NOT (slot)-[:CONTAINS]->(:Parcel)
        RETURN s.x + slot.x AS x, l.y AS y, s.z AS z, ID(slot) AS slotId";

        IList<IRecord> result = await neo4jHelper.ExecuteReadListAsync(query, new Dictionary<string, object> { { "category", category } });

        if (result.Count == 0)
        {
            return (Vector3.zero, -1); // Restituisce valori predefiniti se nessuno slot è disponibile
        }

        for (int i = 0; i < result.Count; i++)
        {
            var slotData = result[i];
            float x = slotData["x"].As<float>();
            float y = slotData["y"].As<float>();
            float z = slotData["z"].As<float>();
            long slotId = slotData["slotId"].As<long>();

            Vector3 slotPosition = new Vector3(x, y, z);

            if (!assignedPositions.ContainsKey(slotPosition))
            {
                assignedPositions[slotPosition] = robotId; // Segna lo slot come assegnato
                return (slotPosition, slotId);
            }
        }

        Debug.LogWarning("Tutti gli slot disponibili sono già assegnati.");
        return (Vector3.zero, -1);
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

    public async Task removeParcelFromShelf(Vector3 parcelPositionInShelf)
    {
        string query = @"
MATCH (s:Shelf)-[:HAS_LAYER]->(l:Layer)-[:HAS_SLOT]->(slot:Slot)-[:CONTAINS]->(p:Parcel)
WHERE abs(s.x + slot.x - $x) < 0.01 AND abs(l.y - $y) < 0.01 AND abs(s.z - $z) < 0.01
DETACH DELETE p
";

        var parameters = new Dictionary<string, object>
    {
        { "x", parcelPositionInShelf.x },
        { "y", parcelPositionInShelf.y },
        { "z", parcelPositionInShelf.z }
    };

        await neo4jHelper.ExecuteWriteAsync(query, parameters);
    }

    public void NotifyTaskCompletion(int robotId)
    {
        Debug.Log($"Robot {robotId} ha completato il task.");

        // Rimuovi le coppie chiave-valore dai dizionari che hanno il robotId come valore
        var parcelsToRemove = assignedParcels.Where(pair => pair.Value == robotId).ToList();
        foreach (var pair in parcelsToRemove)
        {
            assignedParcels.Remove(pair.Key);
        }

        var positionsToRemove = assignedPositions.Where(pair => pair.Value == robotId).ToList();
        foreach (var pair in positionsToRemove)
        {
            assignedPositions.Remove(pair.Key);
        }
    }

    private async Task<List<Vector3>> GetConveyorPositionsInShipping()
    {
        string query = @"
        MATCH (shipping:Area {type: 'Shipping'})-[:HAS_POSITION]->(pos:Position)
        WHERE pos.hasParcel = false
        RETURN pos.x AS x, pos.y AS y, pos.z AS z 
        ";

        var conveyorPositions = new List<Vector3>();

        try
        {
            IList<IRecord> result = await neo4jHelper.ExecuteReadListAsync(query);

            foreach (var record in result)
            {
                float x = record["x"].As<float>();
                float y = record["y"].As<float>();
                float z = record["z"].As<float>();

                conveyorPositions.Add(new Vector3(x, y, z));
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error fetching conveyor positions: {ex.Message}");
        }

        return conveyorPositions;
    }


}