using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;
using Neo4j.Driver;
using System;
using static Robot;
using System.Linq;

public class RobotManager : MonoBehaviour
{
    public List<Robot> robots = new List<Robot>();
    public DatabaseManager databaseManager;
    private Dictionary<Vector3, int> assignedParcels = new Dictionary<Vector3, int>();
    private Dictionary<Vector3, int> assignedPositions = new Dictionary<Vector3, int>();
    private Queue<Vector3> pendingStoreTasks = new Queue<Vector3>();
    private Queue<Vector3> pendingShippingTasks = new Queue<Vector3>();
    private List<Vector3> conveyorShipping;
    private float checkInterval = 2f;
    private float lastCheckTime = 0f;

    private async void Start()
    {
        string robotList = string.Join(", ", robots.Select(r => "ID: " + r.id + ", Stato: " + r.currentState).ToArray());
        Debug.Log("RobotManager avviato.\nRobot collegati: [" + robotList + "]");
        conveyorShipping = await databaseManager.GetConveyorPositionsInShipping();
    }

    private async void Update()
    {
        if (Time.time - lastCheckTime > checkInterval)
        {
            lastCheckTime = Time.time;
            CheckForShippingOrders();
            await CheckForParcelsInDeliveryArea();
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
        HandleNearbyRobots();
        CheckRobotDestinations();
    }

    private async Task CheckForParcelsInDeliveryArea()
    {
        IList<IRecord> result = await databaseManager.GetParcelsInDeliveryArea();
        if (result == null) return;
        foreach (var record in result)
        {
            Vector3 parcelPosition = new Vector3(record["x"].As<float>(), record["y"].As<float>(), record["z"].As<float>());
            if (assignedParcels.ContainsKey(parcelPosition)) continue;
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
                    string orderId = order.Properties["orderId"].As<string>();
                    await databaseManager.DeleteOrderAsync(orderId);
                }
                else
                {
                    string orderId = order.Properties["orderId"].As<string>();
                    var parcelPositions = await databaseManager.GetParcelPositionsForOrderAsync(orderId);
                    foreach (var parcelPosition in parcelPositions)
                    {
                        if (assignedParcels.ContainsKey(parcelPosition)) continue;
                        AssignShippingTask(parcelPosition);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Error checking for shipping orders: " + ex.Message);
        }
    }

    private void AssignStoreTask(Vector3 parcelPosition)
    {
        Robot availableRobot = FindAvailableRobot(parcelPosition);
        if (availableRobot == null)
        {
            if (!pendingStoreTasks.Any(task => task == parcelPosition))
            {
                Debug.LogWarning("Nessun robot disponibile per Store. Compito aggiunto in coda.");
                pendingStoreTasks.Enqueue(parcelPosition);
            }
            return;
        }
        Debug.Log("Assegnato store task al Robot " + availableRobot.id);
        availableRobot.destination = parcelPosition;
        availableRobot.currentState = RobotState.StoreState;
        assignedParcels[parcelPosition] = availableRobot.id;
    }

    private void AssignShippingTask(Vector3 parcelPosition)
    {
        Robot availableRobot = FindAvailableRobot(parcelPosition);
        if (availableRobot == null)
        {
            if (!pendingShippingTasks.Any(task => task == parcelPosition))
            {
                Debug.LogWarning("Nessun robot disponibile per Shipping. Compito aggiunto in coda.");
                pendingShippingTasks.Enqueue(parcelPosition);
            }
            return;
        }
        Debug.Log("Assegnato shipping task al Robot " + availableRobot.id);
        availableRobot.destination = parcelPosition;
        availableRobot.currentState = RobotState.ShippingState;
        assignedParcels[parcelPosition] = availableRobot.id;
    }

    private int currentConveyorIndex = 0;
    public Vector3 askConveyorPosition()
    {
        if (conveyorShipping == null || conveyorShipping.Count == 0)
        {
            Debug.LogWarning("Conveyor positions list is empty!");
            return Vector3.zero;
        }
        Vector3 selectedPosition = conveyorShipping[currentConveyorIndex];
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
                assignedPositions[slot.slotPosition] = robotId;
                return slot;
            }
        }
        Debug.LogWarning("Tutti gli slot disponibili sono già assegnati.");
        return (Vector3.zero, -1);
    }

    private Robot FindAvailableRobot(Vector3 parcelPosition)
    {
        Robot closestRobot = null;
        float shortestDistance = float.MaxValue;
        foreach (Robot robot in robots)
        {
            if (!robot.isActive || robot.currentState != RobotState.Idle) continue;
            float distanceToParcel = Vector3.Distance(robot.transform.position, parcelPosition);
            if (distanceToParcel < shortestDistance)
            {
                shortestDistance = distanceToParcel;
                closestRobot = robot;
            }
        }
        return closestRobot;
    }

    public void HandleNearbyRobots(float threshold = 8f)
    {
        for (int i = 0; i < robots.Count; i++)
        {
            var robotA = robots[i];
            if (!robotA.isActive || robotA.currentRobotPosition == Vector3.zero || robotA.isPaused) continue;
            for (int j = i + 1; j < robots.Count; j++)
            {
                var robotB = robots[j];
                if (!robotB.isActive || robotB.currentRobotPosition == Vector3.zero || robotB.isPaused) continue;
                float distanceBetweenRobots = Vector3.Distance(robotA.currentRobotPosition, robotB.currentRobotPosition);
                if (distanceBetweenRobots <= threshold)
                {
                    if (robotA.currentState == RobotState.Idle && robotB.currentState != RobotState.Idle)
                    {
                        robotA.Pause();
                    }
                    else if (robotB.currentState == RobotState.Idle && robotA.currentState != RobotState.Idle)
                    {
                        robotB.Pause();
                    }
                    else
                    {
                        float distAFromDest = Vector3.Distance(robotA.currentRobotPosition, robotA.destination);
                        float distBFromDest = Vector3.Distance(robotB.currentRobotPosition, robotB.destination);
                        if (distAFromDest > distBFromDest)
                            robotA.Pause();
                        else
                            robotB.Pause();
                    }
                    break;
                }
            }
        }
    }

    public void CheckRobotDestinations(float threshold = 4f)
    {
        for (int i = 0; i < robots.Count; i++)
        {
            var robotA = robots[i];

            if (!robotA.isActive
                || robotA.isPaused
                || robotA.destination == Vector3.zero)
                continue;

            for (int j = i + 1; j < robots.Count; j++)
            {
                var robotB = robots[j];

                if (!robotB.isActive
                    || robotB.isPaused
                    || robotB.destination == Vector3.zero)
                    continue;

                float distanceBetweenDestinations = Vector3.Distance(robotA.destination, robotB.destination);

                if (distanceBetweenDestinations <= threshold)
                {
                    if (robotA.currentState == RobotState.Idle && robotB.currentState != RobotState.Idle)
                    {
                        robotA.Pause();
                    }
                    else if (robotB.currentState == RobotState.Idle && robotA.currentState != RobotState.Idle)
                    {
                        robotB.Pause();
                    }
                    else
                    {
                        float distanceA = Vector3.Distance(robotA.currentRobotPosition, robotA.destination);
                        float distanceB = Vector3.Distance(robotB.currentRobotPosition, robotB.destination);

                        if (distanceA > distanceB)
                            robotA.Pause();
                        else
                            robotB.Pause();
                    }

                    break;
                }
            }
        }
    }



    public async Task RemoveParcelFromShelf(Vector3 parcelPositionInShelf)
    {
        await databaseManager.DeleteParcelFromShelfAsync(parcelPositionInShelf);
    }

    public void NotifyTaskCompletion(int robotId)
    {
        var parcelsToRemove = assignedParcels.Where(pair => pair.Value == robotId).ToList();
        foreach (var pair in parcelsToRemove) assignedParcels.Remove(pair.Key);
        var positionsToRemove = assignedPositions.Where(pair => pair.Value == robotId).ToList();
        foreach (var pair in positionsToRemove) assignedPositions.Remove(pair.Key);
    }
}
