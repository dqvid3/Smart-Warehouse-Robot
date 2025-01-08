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
    private Queue<Vector3> pendingDeliveryTasks = new Queue<Vector3>();
    private Queue<Vector3> pendingShippingTasks = new Queue<Vector3>();
    private List<Vector3> conveyorPositions;
    private float checkInterval = 2f;
    private float lastCheckTime = 0f;

    private async void Start()
    {
        string robotList = string.Join(", ", robots.Select(r => "ID: " + r.id + ", Stato: " + r.currentState).ToArray());
        Debug.Log("RobotManager avviato.\nRobot collegati: [" + robotList + "]");
        conveyorPositions = await databaseManager.GetConveyorPositions();
    }

    private async void Update()
    {
        if (Time.time - lastCheckTime > checkInterval)
        {
            lastCheckTime = Time.time;
            CheckForShippingOrders();
            await CheckForDeliveries();
            if (pendingShippingTasks.Count > 0)
            {
                var nextTask = pendingShippingTasks.Dequeue();
                AssignShippingTask(nextTask);
            }
            else if (pendingDeliveryTasks.Count > 0)
            {
                var nextTask = pendingDeliveryTasks.Dequeue();
                AssignDeliveryTask(nextTask);
            }
        }
        //HandleNearbyRobots();
    }

    private async void CheckForShippingOrders()
    {
        try
        {
            var result = await databaseManager.GetOldestOrderWithParcelCountAsync();
            foreach (var record in result)
            {
                var order = record["order"].As<INode>();
                string orderId = order.Properties["orderId"].As<string>();
                var parcelPositions = await databaseManager.GetParcelPositionsForOrderAsync(orderId);
                foreach (var parcelPosition in parcelPositions)
                {
                    if (assignedParcels.ContainsKey(parcelPosition)) continue;
                    AssignShippingTask(parcelPosition);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Error checking for shipping orders: " + ex.Message);
        }
    }

    private void AssignShippingTask(Vector3 parcelPosition)
    {
        if (assignedParcels.ContainsKey(parcelPosition))
            return;

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

        Debug.Log($"Assegnato shipping task al Robot {availableRobot.id}.");
        availableRobot.destination = parcelPosition;
        availableRobot.currentState = RobotState.ShippingState;
        assignedParcels[parcelPosition] = availableRobot.id; // Registra l'assegnazione
    }

    private async Task CheckForDeliveries()
    {
        IList<IRecord> result = await databaseManager.GetParcelsInDeliveryArea();
        if (result == null) return;
        foreach (var record in result)
        {
            Vector3 parcelPosition = new Vector3(record["x"].As<float>(), record["y"].As<float>(), record["z"].As<float>());
            //Debug.Log(parcelPosition);
            if (assignedParcels.ContainsKey(parcelPosition)) continue;
            AssignDeliveryTask(parcelPosition);
        }
    }

    private void AssignDeliveryTask(Vector3 parcelPosition)
    {
        if (assignedParcels.ContainsKey(parcelPosition))
            return;

        Robot availableRobot = FindAvailableRobot(parcelPosition);
        if (availableRobot == null)
        {
            if (!pendingDeliveryTasks.Any(task => task == parcelPosition))
            {
                Debug.LogWarning("Nessun robot disponibile per Store. Compito aggiunto in coda.");
                pendingDeliveryTasks.Enqueue(parcelPosition);
            }
            return;
        }

        Debug.Log($"Assegnato delivery task al Robot {availableRobot.id}.");
        availableRobot.destination = parcelPosition;
        availableRobot.currentState = RobotState.DeliveryState;
        assignedParcels[parcelPosition] = availableRobot.id; // Registra l'assegnazione
    }

    private int currentConveyorIndex = 0;
    public Vector3 askConveyorPosition()
    {
        Vector3 selectedPosition = conveyorPositions[currentConveyorIndex];
        currentConveyorIndex = (currentConveyorIndex + 1) % conveyorPositions.Count;
        return selectedPosition;
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
/*    public void HandleNearbyRobots(float threshold = 8f)
    {
        for (int i = 0; i < robots.Count; i++)
        {
            var robotA = robots[i];
            if (!robotA.isActive || robotA.currentRobotPosition == Vector3.zero || robotA.isPaused)
                continue;

            for (int j = i + 1; j < robots.Count; j++)
            {
                var robotB = robots[j];
                if (!robotB.isActive || robotB.currentRobotPosition == Vector3.zero || robotB.isPaused)
                    continue;

                float distanceBetweenRobots = Vector3.Distance(robotA.currentRobotPosition, robotB.currentRobotPosition);

                // Verifica se sono troppo vicini
                if (distanceBetweenRobots <= threshold)
                {
                    if (robotA.currentState == Robot.RobotState.Idle && robotB.currentState != Robot.RobotState.Idle)
                    {
                        robotA.Pause();
                    }
                    else if (robotB.currentState == Robot.RobotState.Idle && robotA.currentState != Robot.RobotState.Idle)
                    {
                        robotB.Pause();
                    }
                    else
                    {
                        float distAFromDest = Vector3.Distance(robotA.currentRobotPosition, robotA.destination);
                        float distBFromDest = Vector3.Distance(robotB.currentRobotPosition, robotB.destination);

                        if (distAFromDest > distBFromDest)
                        {
                            robotA.Pause();
                        }
                        else
                        {
                            robotB.Pause();
                        }
                    }
                }
            }
        }
    }
*/
    public void NotifyTaskCompletion(int robotId)
    {
        var parcelsToRemove = assignedParcels.Where(pair => pair.Value == robotId).ToList();
        foreach (var pair in parcelsToRemove) assignedParcels.Remove(pair.Key);
    }
}
