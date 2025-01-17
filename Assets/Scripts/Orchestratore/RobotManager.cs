using System.Collections.Generic;
using UnityEngine;
using Neo4j.Driver;
using System.Linq;
using System.Threading.Tasks;
using static Robot;
using System.Collections;

public class RobotManager : MonoBehaviour
{
    public List<Robot> robots = new(); // List of all robots
    private bool isPaused = false; // Pause state
    public DatabaseManager databaseManager; // Database manager for querying data
    private Dictionary<int, Vector3> robotAssignments = new(); // Tracks robot assignments (ID -> Position)
    private Queue<(Vector3 Position, string Type, string Category, string Timestamp)> pendingTasks = new(); // Queue for pending tasks (position + type + category)
    private List<Vector3> shippingConveyorPositions; // List of conveyor positions
    private float checkInterval = 2f; // Interval for checking tasks
    private float lastCheckTime = 0f; // Last time tasks were checked
    private int currentConveyorIndex = 0; // Index for cycling through conveyor positions

    // Distanza minima tra i robot per valutare potenziali collisioni
    private float proximityThreshold = 8f;
    // Distanza dal punto di collisione per iniziare a fermarsi
    private float collisionImminenceThreshold = 5f;

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
            CheckForExpiredParcelsInBackup();
            CheckPendingTasks();
        }

        // Check robot proximity at each Update
        CheckRobotProximity();

        // Toggle pause with the P key
        if (Input.GetKeyDown(KeyCode.P))
        {
            TogglePause();
        }
    }

    private void CheckRobotProximity()
    {
        for (int i = 0; i < robots.Count; i++)
        {
            for (int j = i + 1; j < robots.Count; j++)
            {
                Robot robotA = robots[i];
                Robot robotB = robots[j];

                // Skip if either robot is not active or is idle
                if (!robotA.isActive || !robotB.isActive ||
                    robotA.currentState == RobotState.Idle || robotB.currentState == RobotState.Idle)
                    continue;

                Vector3 positionA = robotA.GetEstimatedPosition();
                Vector3 positionB = robotB.GetEstimatedPosition();
                float currentDistance = Vector3.Distance(positionA, positionB);

                // Check if robots are going to their default position
                if (!robotAssignments.ContainsKey(robotA.id) || !robotAssignments.ContainsKey(robotB.id))
                {
                    // Get the default positions of the robots
                    Vector3 defaultPositionA = robotA.GetComponent<ForkliftNavController>().defaultPosition;
                    Vector3 defaultPositionB = robotB.GetComponent<ForkliftNavController>().defaultPosition;

                    // Calculate the distance between the robots and their default positions
                    float distanceFromDefaultA = Vector3.Distance(positionA, defaultPositionA);
                    float distanceFromDefaultB = Vector3.Distance(positionB, defaultPositionB);

                    // Skip if either robot is too close from its default position
                    if (distanceFromDefaultA < 5 || distanceFromDefaultB < 5)
                        continue;
                }

                // Controllo preliminare sulla distanza
                if (currentDistance > proximityThreshold)
                {
                    continue;
                }

                // Check for potential collisions based on paths and distance to collision
                if (WillRobotsCollide(robotA, robotB, out float distanceToCollisionA, out float distanceToCollisionB))
                {
                    // Se la collisione è imminente per uno dei due robot, ferma quello più lontano dal suo obiettivo
                    if (distanceToCollisionA < collisionImminenceThreshold ||
                        distanceToCollisionB < collisionImminenceThreshold)
                    {
                        float distanceA = Vector3.Distance(positionA, robotA.destination);
                        float distanceB = Vector3.Distance(positionB, robotB.destination);

                        MovementWithAStar movementA = robotA.GetComponent<MovementWithAStar>();
                        MovementWithAStar movementB = robotB.GetComponent<MovementWithAStar>();

                        if (distanceA > distanceB)
                        {
                            // Controlla se il robot è già fermo
                            if (movementA.moveSpeed > 0)
                            {
                                Debug.Log($"Robot {robotA.id} fermato perché in rotta di collisione con Robot {robotB.id} (distanza collisione: {distanceToCollisionA}).");
                                StartCoroutine(StopRobot(robotA));
                            }
                        }
                        else
                        {
                            // Controlla se il robot è già fermo
                            if (movementB.moveSpeed > 0)
                            {
                                Debug.Log($"Robot {robotB.id} fermato perché in rotta di collisione con Robot {robotA.id} (distanza collisione: {distanceToCollisionB}).");
                                StartCoroutine(StopRobot(robotB));
                            }
                        }
                    }
                }
            }
        }
    }

    private bool WillRobotsCollide(Robot robotA, Robot robotB,
        out float distanceToCollisionA, out float distanceToCollisionB)
    {
        distanceToCollisionA = float.MaxValue;
        distanceToCollisionB = float.MaxValue;

        // Get the MovementWithAStar components
        MovementWithAStar movementA = robotA.GetComponent<MovementWithAStar>();
        MovementWithAStar movementB = robotB.GetComponent<MovementWithAStar>();

        // Get the paths of the robots
        List<Vector3> pathA = movementA.GetPath();
        List<Vector3> pathB = movementB.GetPath();

        // Check if paths are valid
        if (pathA == null || pathB == null) return false;

        // Define a threshold for considering nodes as overlapping
        float nodeOverlapThreshold = movementA.grid.nodeRadius * 2;

        // Iterate through the nodes of each path to check for overlap
        foreach (Vector3 nodeA in pathA)
        {
            foreach (Vector3 nodeB in pathB)
            {
                if (Vector3.Distance(nodeA, nodeB) < nodeOverlapThreshold)
                {
                    // Calcola la distanza dei robot dal punto di collisione
                    distanceToCollisionA = CalculateDistanceAlongPath(robotA.GetEstimatedPosition(), nodeA, pathA);
                    distanceToCollisionB = CalculateDistanceAlongPath(robotB.GetEstimatedPosition(), nodeB, pathB);
                    return true;
                }
            }
        }

        return false;
    }

    // Calcola la distanza di un punto lungo un percorso
    private float CalculateDistanceAlongPath(Vector3 start, Vector3 end, List<Vector3> path)
    {
        float distance = 0f;
        int startIndex = -1;

        // Trova l'indice del nodo più vicino al punto di partenza
        for (int i = 0; i < path.Count; i++)
        {
            if (Vector3.Distance(start, path[i]) < 0.5f)
            {
                startIndex = i;
                break;
            }
        }
        if (startIndex == -1)
        {
            startIndex = 0;
        }

        // Somma le distanze tra i nodi dal punto di partenza al punto di collisione
        for (int i = startIndex; i < path.Count - 1; i++)
        {
            distance += Vector3.Distance(path[i], path[i + 1]);
            if (path[i + 1] == end)
            {
                break;
            }
        }

        return distance;
    }

    private IEnumerator StopRobot(Robot robot)
    {
        float stopDuration = 2;

        // Nuove righe di explainability (se presente il componente)
        var explainability = robot.GetComponent<RobotExplainability>();
        if (explainability != null)
        {
            explainability.ShowExplanation(
                $"Collisione imminente! Ferma Robot {robot.id} per {stopDuration} secondi."
            );
        }

        RaycastManager raycastManager = robot.GetComponent<RaycastManager>();
        raycastManager.sensorsEnabled = false;
        Debug.Log($"Robot {robot.id} fermo per {stopDuration} secondi.");

        MovementWithAStar robMov = robot.GetComponent<MovementWithAStar>();
        robMov.moveSpeed = 0; // Ferma il robot

        // Altra explainability sul “wait”
        if (explainability != null)
        {
            explainability.ShowExplanation(
                "In attesa che la situazione si risolva, i sensori sono disabilitati."
            );
        }

        // Aspetta lo stopDuration
        yield return new WaitForSeconds(stopDuration);

        // Riprendi il movimento
        robMov.moveSpeed = robot.speed;
        raycastManager.sensorsEnabled = true;
        Debug.Log($"Robot {robot.id} riprende il movimento.");

        if (explainability != null)
        {
            explainability.ShowExplanation(
                $"Robot {robot.id} torna in movimento. Sensori riattivati."
            );
        }
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
                pendingTasks.Enqueue((slot, "Shipping", null, null)); // Category is not needed for Shipping
            }
        }
    }

    private async void CheckForDeliveries()
    {
        // Fetch parcels in the delivery area
        IList<IRecord> result = await databaseManager.GetParcelsInDeliveryArea();
        foreach (var record in result)
        {
            Vector3 parcelPosition = new(record["x"].As<float>(),
                                         record["y"].As<float>(),
                                         record["z"].As<float>());
            if (robotAssignments.ContainsValue(parcelPosition)) continue;
            pendingTasks.Enqueue((parcelPosition, "Delivery", null, null));
        }
    }

    private async void CheckForExpiredParcelsInBackup()
    {
        // Fetch parcels in the backup shelf
        var slotPositions = await databaseManager.GetExpiredParcelsInBackupShelf();
        foreach (var (slot, category, timestamp) in slotPositions)
        {
            if (robotAssignments.ContainsValue(slot)) continue;
            pendingTasks.Enqueue((slot, "Disposal", category, timestamp));
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
                AssignTask(nextTask.Position, nextTask.Type, nextTask.Category);
                robotsAvailable = robots.Any(r => r.isActive && r.currentState == RobotState.Idle);
            }
        }
    }

    private void AssignTask(Vector3 taskPosition, string taskType, string category = null, string timestamp = null)
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
            // Re-add to queue with the correct category if no robot is available
            if (!pendingTasks.Any(t => t.Position == taskPosition && t.Type == taskType))
            {
                pendingTasks.Enqueue((taskPosition, taskType, category, timestamp));
                Debug.LogWarning($"Nessun robot disponibile. Compito riaggiunto in coda. ({taskPosition})");
            }
            return;
        }

        Debug.Log($"Assegnato task di {taskType} per la posizione {taskPosition} al Robot {availableRobot.id}.");
        availableRobot.destination = taskPosition;

        switch (taskType)
        {
            case "Delivery":
                availableRobot.currentState = RobotState.DeliveryState;
                break;
            case "Shipping":
                availableRobot.currentState = RobotState.ShippingState;
                break;
            case "Disposal":
                availableRobot.currentState = RobotState.DisposalState;
                availableRobot.category = category;
                availableRobot.timestamp = timestamp;
                break;
            default:
                Debug.LogWarning($"Tipo di task sconosciuto: {taskType}");
                break;
        }
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

        if (result == null)
            return null; // Return null to indicate no slot was found

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
            robotAssignments.Remove(robotId);
        }

        // Assign the robot to the new slot position
        Debug.Log($"Robot {robotId}: Nuova assegnazione per lo slot {slotPosition}.");
        robotAssignments[robotId] = slotPosition;
        return record;
    }

    public void FreeSlotPosition(int robotId)
    {
        // Remove the robot's current assignment (if any)
        if (robotAssignments.ContainsKey(robotId))
        {
            Vector3 currentPosition = robotAssignments[robotId];
            Debug.Log($"Robot {robotId}: Rimozione assegnazione corrente per la posizione {currentPosition}.");
            robotAssignments.Remove(robotId);
        }
    }

    public void AssignConveyorPosition(int robotId, Vector3 conveyorPosition)
    {
        Debug.Log($"Robot {robotId}: Nuova assegnazione per il conveyor {conveyorPosition}.");
        robotAssignments[robotId] = conveyorPosition;
    }

    public Vector3 AskConveyorPosition()
    {
        Vector3 selectedPosition = shippingConveyorPositions[currentConveyorIndex];
        currentConveyorIndex = (currentConveyorIndex + 1) % shippingConveyorPositions.Count;
        return selectedPosition;
    }

    public bool AreThereTask()
    {
        return pendingTasks.Count > 0;
    }

    private void TogglePause()
    {
        isPaused = !isPaused;
        Time.timeScale = isPaused ? 0 : 1;

        // Show/hide explanations for all robots
        foreach (var robot in robots)
        {
            var explainability = robot.GetComponent<RobotExplainability>();
            if (explainability != null)
            {
                explainability.ToggleExplanation(isPaused);
            }
        }
    }
}
