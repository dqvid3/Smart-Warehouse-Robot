using System.Collections.Generic;
using UnityEngine;
using Neo4j.Driver;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine.AI;
using static Robot;
using System.Collections;

public class RobotManager : MonoBehaviour
{
    public List<Robot> robots = new(); // List of all robots
    private bool isPaused = false; // Pause state
    public DatabaseManager databaseManager; // Database manager for querying data
    private Dictionary<int, Vector3> robotAssignments = new(); // Tracks robot assignments (ID -> Position)
    private Queue<(Vector3 Position, string Type)> pendingTasks = new(); // Queue for pending tasks (position + type)
    private List<Vector3> shippingConveyorPositions; // List of conveyor positions
    private float checkInterval = 2f; // Interval for checking tasks
    private float lastCheckTime = 0f; // Last time tasks were checked
    private int currentConveyorIndex = 0; // Index for cycling through conveyor positions
    private float proximityCheckInterval = 1f; // Interval for checking robot proximity
    private float lastProximityCheckTime = 0f; // Last time proximity was checked
    private float proximityThreshold = 8f; // Distance threshold for stopping robots

    private async void Start()
    {
        // Log all connected robots
        string robotList = string.Join(", ", robots.Select(r => "ID: " + r.id + ", Stato: " + r.currentState).ToArray());
        Debug.Log("RobotManager avviato.\nRobot collegati: [" + robotList + "]");

        // Fetch conveyor positions from the database
        shippingConveyorPositions = await databaseManager.GetConveyorPositions();
    }

    private void Update()
    {
        // Check for tasks at regular intervals
        if (Time.time - lastCheckTime > checkInterval)
        {
            lastCheckTime = Time.time;
            CheckForShippingOrders();
            CheckForDeliveries();
            CheckPendingTasks();
        }

        // Check robot proximity at regular intervals
        if (Time.time - lastProximityCheckTime > proximityCheckInterval)
        {
            lastProximityCheckTime = Time.time;
            CheckRobotProximity();
        }

        // Toggle pause with the P key
        if (Input.GetKeyDown(KeyCode.P))
        {
            TogglePause();
        }
    }

    private void CheckRobotProximity()
    {
        // Iterate through all pairs of robots
        for (int i = 0; i < robots.Count; i++)
        {
            for (int j = i + 1; j < robots.Count; j++)
            {
                Robot robotA = robots[i];
                Robot robotB = robots[j];

                // Skip if either robot is not active or is idle
                if (!robotA.isActive || !robotB.isActive || robotA.currentState == RobotState.Idle || robotB.currentState == RobotState.Idle)
                    continue;

                // Get the estimated positions of the robots
                Vector3 positionA = robotA.GetEstimatedPosition();
                Vector3 positionB = robotB.GetEstimatedPosition();

                // Calculate the distance between the two robots
                float distance = Vector3.Distance(positionA, positionB);

                // If the robots are too close, stop the one further from its destination
                if (distance < proximityThreshold)
                {
                    float distanceA = Vector3.Distance(positionA, robotA.destination);
                    float distanceB = Vector3.Distance(positionB, robotB.destination);

                    if (distanceA > distanceB)
                    {
                        Debug.Log($"Robot {robotA.id} fermato perché troppo vicino a Robot {robotB.id}.");
                        StartCoroutine(StopRobot(robotA));
                    }
                    else
                    {
                        Debug.Log($"Robot {robotB.id} fermato perché troppo vicino a Robot {robotA.id}.");
                        StartCoroutine(StopRobot(robotB));
                    }
                }
            }
        }
    }

    private IEnumerator StopRobot(Robot robot)
    {
        float stopDuration = 4;
        Debug.Log($"Robot {robot.id} fermo per {stopDuration} secondi.");

        NavMeshAgent agent = robot.GetComponent<NavMeshAgent>();
        agent.speed = 0; // Ferma il robot
        yield return new WaitForSeconds(stopDuration); // Aspetta
        agent.speed = robot.speed; // Riprendi il movimento

        Debug.Log($"Robot {robot.id} riprende il movimento.");
    }

    private async void CheckForShippingOrders()
    {
        // Fetch the oldest order with parcel count
        var result = await databaseManager.GetOldestOrderWithParcelCountAsync();
        foreach (var record in result)
        {
            var order = record["order"].As<INode>();
            string orderId = order.Properties["orderId"].As<string>();

            // Get parcel positions for the order
            var slotPositions = await databaseManager.GetParcelPositionsForOrderAsync(orderId);
            foreach (var slot in slotPositions)
            {
                if (robotAssignments.ContainsValue(slot)) continue; // Skip if slot is already assigned
                pendingTasks.Enqueue((slot, "Shipping")); // Add slot to pending tasks with task type
            }
        }
    }

    private async void CheckForDeliveries()
    {
        // Fetch parcels in the delivery area
        IList<IRecord> result = await databaseManager.GetParcelsInDeliveryArea();
        foreach (var record in result)
        {
            Vector3 parcelPosition = new(record["x"].As<float>(), record["y"].As<float>(), record["z"].As<float>());
            if (robotAssignments.ContainsValue(parcelPosition)) continue; // Skip if conveyor is already assigned
            pendingTasks.Enqueue((parcelPosition, "Delivery")); // Add conveyor to pending tasks with task type
        }
    }

    private void CheckPendingTasks()
    {
        // Check if there are available robots before assigning tasks
        bool robotsAvailable = robots.Any(r => r.isActive && r.currentState == RobotState.Idle);

        if (robotsAvailable)
        {
            // Assign tasks if there are available robots
            while (pendingTasks.Count > 0 && robotsAvailable)
            {
                var nextTask = pendingTasks.Dequeue();
                AssignTask(nextTask.Position, nextTask.Type); // Pass task type explicitly
                robotsAvailable = robots.Any(r => r.isActive && r.currentState == RobotState.Idle);
            }
        }
    }

    private void AssignTask(Vector3 taskPosition, string taskType)
    {
        // Skip if the task is already assigned
        if (robotAssignments.ContainsValue(taskPosition))
        {
            Debug.Log($"Task per la posizione {taskPosition} è già stato assegnato.");
            return;
        }

        // Find an available robot
        Robot availableRobot = FindAvailableRobot(taskPosition);
        if (availableRobot == null)
        {
            if (!pendingTasks.Any(t => t.Position == taskPosition))
            {
                pendingTasks.Enqueue((taskPosition, taskType)); // Re-add to queue if no robot is available
                Debug.LogWarning($"Nessun robot disponibile. Compito riaggiunto in coda. ({taskPosition})");
            }
            return;
        }

        // Assign the task to the robot
        Debug.Log($"Assegnato task di {taskType} per la posizione {taskPosition} al Robot {availableRobot.id}.");
        availableRobot.destination = taskPosition;
        availableRobot.currentState = taskType == "Delivery" ? RobotState.DeliveryState : RobotState.ShippingState;
        robotAssignments[availableRobot.id] = taskPosition; // Track the assignment
    }

    private Robot FindAvailableRobot(Vector3 taskPosition)
    {
        Robot closestRobot = null;
        float shortestDistance = float.MaxValue;

        // Find the closest idle robot
        foreach (Robot robot in robots)
        {
            if (!robot.isActive || robot.currentState != RobotState.Idle) continue;
            Vector3 robotPosition = robot.GetEstimatedPosition();
            float distanceToTask = Vector3.Distance(robotPosition, taskPosition);
            if (distanceToTask < shortestDistance)
            {
                shortestDistance = distanceToTask;
                closestRobot = robot;
            }
        }
        return closestRobot;
    }

    public void NotifyTaskCompletion(int robotId)
    {
        // Remove the robot's assignment when the task is completed
        if (robotAssignments.TryGetValue(robotId, out Vector3 taskPosition))
        {
            Debug.Log($"Robot {robotId}: Rimozione assegnazione per la posizione {taskPosition} completata.");
            robotAssignments.Remove(robotId);
        }
    }

    public IRecord AskSlot(string category, int robotId)
    {
        // Get an available slot from the database
        IList<IRecord> result = Task.Run(() => databaseManager.GetAvailableSlot(category)).Result;
        var record = result[0];
        float x = record[0].As<float>();
        float y = record[1].As<float>();
        float z = record[2].As<float>();
        Vector3 slotPosition = new(x, y, z);

        // Remove the robot's current assignment (if any)
        if (robotAssignments.ContainsKey(robotId))
        {
            Vector3 currentPosition = robotAssignments[robotId];
            Debug.Log($"Robot {robotId}: Rimozione assegnazione corrente per la posizione {currentPosition}.");
            robotAssignments.Remove(robotId); // Remove the current assignment
        }

        // Assign the robot to the new slot position
        Debug.Log($"Robot {robotId}: Nuova assegnazione per lo slot {slotPosition}.");
        robotAssignments[robotId] = slotPosition; // Assign the robot to the new slot
        return record;
    }

    public void FreeSlotPosition(int robotId, Vector3 conveyorPosition)
    {
        // Remove the robot's current assignment (if any)
        if (robotAssignments.ContainsKey(robotId))
        {
            Vector3 currentPosition = robotAssignments[robotId];
            Debug.Log($"Robot {robotId}: Rimozione assegnazione corrente per la posizione {currentPosition}.");
            robotAssignments.Remove(robotId); // Remove the current assignment
        }

        // Assign the robot to the new conveyor position
        Debug.Log($"Robot {robotId}: Nuova assegnazione per il conveyor {conveyorPosition}.");
        robotAssignments[robotId] = conveyorPosition; // Assign the robot to the new conveyor
    }

    public Vector3 AskConveyorPosition()
    {
        // Get the next conveyor position in a round-robin fashion
        Vector3 selectedPosition = shippingConveyorPositions[currentConveyorIndex];
        currentConveyorIndex = (currentConveyorIndex + 1) % shippingConveyorPositions.Count;
        return selectedPosition;
    }

    public bool AreThereTask()
    {
        // Check if there are any pending tasks
        return pendingTasks.Count > 0;
    }

    private void TogglePause()
    {
        // Toggle pause state
        isPaused = !isPaused;
        Time.timeScale = isPaused ? 0 : 1; // Pause or resume the scene

        // Show/hide explanations for all robots
        foreach (var robot in robots)
        {
            var explainability = robot.GetComponent<RobotExplainability>();
            if (explainability != null)
            {
                explainability.ToggleExplanation(isPaused); // Show/hide explanations
            }
        }
    }
}