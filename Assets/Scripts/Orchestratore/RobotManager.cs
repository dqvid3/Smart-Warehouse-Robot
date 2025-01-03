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

    private float checkInterval = 2f; // Intervallo tra le query
    private float lastCheckTime = 0f;

    private async void Start()
    {
        string robotList = string.Join(", ", robots.Select(r => $"ID: {r.id}, Stato: {r.currentState}").ToArray());
        Debug.Log($"RobotManager avviato.\nRobot collegati: [{robotList}]");
        conveyorShipping = await databaseManager.GetConveyorPositionsInShipping();
    }


    private async void Update()
    {
        if (Time.time - lastCheckTime > checkInterval)
        {
            lastCheckTime = Time.time;

            CheckForShippingOrders();
            await CheckForParcelsInDeliveryArea();
            await HandleNearbyRobotsAsync(); // Controlla i robot vicini

            if (pendingShippingTasks.Count > 0)
            {
                var nextTask = pendingShippingTasks.Dequeue();
                AssignShippingTask(nextTask);
            }
            else if (pendingStoreTasks.Count > 0)
            {
                var nextTask = pendingStoreTasks.Dequeue();
                AssignStoreTask(nextTask);
            }
        }
    }


    // Query per trovare pacchi nella zona di consegna
    private async Task CheckForParcelsInDeliveryArea()
    {
        IList<IRecord> result = await databaseManager.GetParcelsInDeliveryArea();

        if (result == null) return;

        foreach (var record in result)
        {
            Vector3 parcelPosition = new Vector3(record["x"].As<float>(), record["y"].As<float>(), record["z"].As<float>());

            // Verifica se il pacco è già stato assegnato
            if (assignedParcels.ContainsKey(parcelPosition)) continue;

            // Assegna il compito
            AssignStoreTask(parcelPosition);
        }
    }


    private async void CheckForShippingOrders()
    {
        try
        {
            var result = await databaseManager.GetOldestOrderWithParcelCountAsync();

            foreach (var record in result)
            {
                var order = record["order"].As<INode>();
                int parcelCount = record["parcelCount"].As<int>();

                if (parcelCount == 0)
                {
                    // Cancella l'ordine senza pacchi
                    string orderId = order.Properties["orderId"].As<string>();
                    await databaseManager.DeleteOrderAsync(orderId);
                }
                else
                {
                    // Ottieni le posizioni dei pacchi per l'ordine
                    string orderId = order.Properties["orderId"].As<string>();
                    var parcelPositions = await databaseManager.GetParcelPositionsForOrderAsync(orderId);

                    foreach (var parcelPosition in parcelPositions)
                    {
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

        availableRobot.destination = parcelPosition;
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

        availableRobot.destination = parcelPosition;
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
        var availableSlots = await databaseManager.GetAvailableSlotsAsync(category);

        foreach (var slot in availableSlots)
        {
            if (!assignedPositions.ContainsKey(slot.slotPosition))
            {
                assignedPositions[slot.slotPosition] = robotId; // Segna lo slot come assegnato
                return slot;
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

    public async Task HandleNearbyRobotsAsync(float proximityThreshold = 2f)
    {
        // Ottieni la lista delle posizioni dei robot dal database
        var robotPositions = await databaseManager.GetRobotPositionsAsync();

        // Verifica i robot vicini
        foreach (var robot in robots)
        {
            if (!robot.isActive) continue;

            Vector3 robotPosition = robot.transform.position;

            foreach (var otherRobot in robots)
            {
                if (robot.id == otherRobot.id || !otherRobot.isActive) continue;

                Vector3 otherRobotPosition = otherRobot.transform.position;

                // Controlla se i robot sono vicini
                if (Vector3.Distance(robotPosition, otherRobotPosition) <= proximityThreshold)
                {
                    // Imposta uno dei due robot in attesa (priorità ai robot in Idle)
                    if (robot.currentState == Robot.RobotState.Idle)
                    {
                        SetRobotToWait(otherRobot);
                    }
                    else
                    {
                        SetRobotToWait(robot);
                    }
                }
            }
        }
    }

    private void SetRobotToWait(Robot robot)
    {
        robot.currentState = Robot.RobotState.Wait;
        Debug.Log($"Robot {robot.id} è stato messo in attesa per evitare collisioni.");
    }


    public async Task RemoveParcelFromShelf(Vector3 parcelPositionInShelf)
    {
        await databaseManager.DeleteParcelFromShelfAsync(parcelPositionInShelf);
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
}