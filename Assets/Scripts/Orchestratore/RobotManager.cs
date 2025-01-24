using System.Collections.Generic;
using UnityEngine;
using Neo4j.Driver;
using System.Linq;
using System.Threading.Tasks;
using static Robot;

public class RobotManager : MonoBehaviour
{
    public List<Robot> robots = new();
    private bool isPaused = false;
    public DatabaseManager databaseManager;
    private Dictionary<int, Vector3> robotAssignments = new();
    private Queue<(Vector3 Position, string Type, string Category, string Timestamp)> pendingTasks = new();
    private List<Vector3> shippingConveyorPositions;
    private float checkInterval = 0.5f;
    private float lastCheckTime = 0f;
    private int currentConveyorIndex = 0;
    public float collisionCheckRadius = 9f;
    public float collisionAvoidancePauseTime = 2f;
    public float pathConflictDistanceThreshold = 2f;
    public int nextNodeCheckCount = 8;
    public float nearGoalThreshold = 4f;
    private Dictionary<int, float> robotPauseTimers = new();

    private async void Start()
    {
        Debug.Log($"RobotManager avviato.\nRobot collegati: [{string.Join(", ", robots.Select(r => $"ID: {r.id}, Stato: {r.currentState}"))}]");
        shippingConveyorPositions = await databaseManager.GetConveyorPositions();
    }

    private async void Update()
    {
        CheckForCollisions();
        if (Time.time - lastCheckTime > checkInterval)
        {
            lastCheckTime = Time.time;
            await CheckForTasks();
            CheckPendingTasks();
        }

        if (Input.GetKeyDown(KeyCode.P)) TogglePause();

        UpdateRobotPauseTimers();
    }

    private void UpdateRobotPauseTimers()
    {
        List<int> robotsToRemove = new();
        List<int> keysToRemove = robotPauseTimers.Keys.ToList(); // Create a copy of the keys
        foreach (var robotId in keysToRemove) // Iterate over the copy of keys
        {
            if (robotPauseTimers.ContainsKey(robotId)) // Check if the key still exists before accessing
            {
                robotPauseTimers[robotId] -= Time.deltaTime;
                if (robotPauseTimers[robotId] <= 0)
                {
                    robotsToRemove.Add(robotId);
                }
            }
        }

        foreach (var robotId in robotsToRemove)
        {
            robotPauseTimers.Remove(robotId);
            Robot robot = robots.Find(r => r.id == robotId);
            if (robot != null)
            {
                robot.isPaused = false;
                robot.ShowCollisionWarning(false);
                Debug.Log($"Robot {robotId}: Collision pause ended, resuming movement.");
            }
        }
    }

    public bool drawConflictGizmos = true;
    public float gizmoDisplayTime = 3f;
    private Dictionary<Vector3, float> conflictGizmos = new();
    private enum CollisionType { Proximity, PathConflict }

    private void CheckForCollisions()
    {
        for (int i = 0; i < robots.Count; i++)
        {
            if (robots[i].currentState == RobotState.Idle || robots[i].isPaused) continue;

            for (int j = i + 1; j < robots.Count; j++)
            {
                if (robots[j].currentState == RobotState.Idle || robots[j].isPaused) continue;

                Vector3 posA = robots[i].GetEstimatedPosition();
                Vector3 posB = robots[j].GetEstimatedPosition();
                float distance = Vector3.Distance(posA, posB);

                // Controllo collisione diretta
                if (distance < collisionCheckRadius - 4)
                {
                    Debug.Log($"Collisione imminente per prossimità: Robot {robots[i].id} e {robots[j].id} " +
                            $"a {distance.ToString("F2")} unità di distanza");
                    HandlePotentialCollision(robots[i], robots[j], CollisionType.Proximity);
                }

                // Controllo conflitto di percorso
                var (hasConflict, conflictingNodes) = CheckForPathConflicts(robots[i], robots[j]);
                if (distance < collisionCheckRadius && hasConflict)
                {
                    string nodeList = string.Join("\n- ", conflictingNodes.Select(n =>
                        $"Nodo A[{n.nodeA.gridX},{n.nodeA.gridY}] ({n.positionA}) ↔ " +
                        $"Nodo B[{n.nodeB.gridX},{n.nodeB.gridY}] ({n.positionB})"));

                    Debug.Log($"Conflitto di percorso tra Robot {robots[i].id} e {robots[j].id}:\n" +
                                $"Distanza: {distance.ToString("F2")}\n" +
                                $"Nodi in conflitto:\n- {nodeList}");

                    HandlePotentialCollision(robots[i], robots[j], CollisionType.PathConflict);
                }
            }
        }
    }

    private (bool hasConflict, List<(Node nodeA, Vector3 positionA, Node nodeB, Vector3 positionB)>) CheckForPathConflicts(Robot robotA, Robot robotB)
    {
        var conflictingNodes = new List<(Node, Vector3, Node, Vector3)>();

        var pathA = robotA.movementWithAStar.GetNextNodes(nextNodeCheckCount);
        var pathB = robotB.movementWithAStar.GetNextNodes(nextNodeCheckCount);

        foreach (var nodeA in pathA)
        {
            foreach (var nodeB in pathB)
            {
                float nodeDistance = Vector3.Distance(nodeA.worldPosition, nodeB.worldPosition);
                if (nodeA == nodeB || nodeDistance < pathConflictDistanceThreshold)
                {
                    conflictingNodes.Add((
                        nodeA,
                        nodeA.worldPosition,
                        nodeB,
                        nodeB.worldPosition
                    ));
                }
            }
        }
        return (conflictingNodes.Count > 0, conflictingNodes);
    }

    private void HandlePotentialCollision(Robot robotA, Robot robotB, CollisionType collisionType)
    {
        if (!robotA.isPaused && !robotB.isPaused)
        {
            Vector3 destinationA = Vector3.zero;
            Vector3 destinationB = Vector3.zero;

            if (robotAssignments.ContainsKey(robotA.id))
                destinationA = robotAssignments[robotA.id];
            if (robotAssignments.ContainsKey(robotB.id))
                destinationB = robotAssignments[robotB.id];

            // Calculate distances to each other's destinations
            float distanceAtoBDest = Vector3.Distance(robotA.GetEstimatedPosition(), destinationB);
            float distanceBtoADest = Vector3.Distance(robotB.GetEstimatedPosition(), destinationA);

            bool aNearBDest = distanceAtoBDest < nearGoalThreshold;
            bool bNearADest = distanceBtoADest < nearGoalThreshold;

            Robot robotToPause;

            // Determine which robot to pause based on proximity to other's destination
            if (aNearBDest)
            {
                robotToPause = robotB; // Pause B since A is near B's destination
            }
            else if (bNearADest)
            {
                robotToPause = robotA; // Pause A since B is near A's destination
            }
            else
            {
                // Fallback to original distance comparison
                float distanceToDestinationA = Vector3.Distance(robotA.GetEstimatedPosition(), destinationA);
                float distanceToDestinationB = Vector3.Distance(robotB.GetEstimatedPosition(), destinationB);
                robotToPause = (distanceToDestinationA > distanceToDestinationB) ? robotA : robotB;
            }

            // Apply pause and update timers
            robotToPause.isPaused = true;
            robotPauseTimers[robotToPause.id] = collisionAvoidancePauseTime;
            Debug.LogWarning($"Collision potential detected between:\n" +
                $"Robot {robotA.id} (Position: {robotA.GetEstimatedPosition()}, Destination: {destinationA})\n" +
                $"Robot {robotB.id} (Position: {robotB.GetEstimatedPosition()}, Destination: {destinationB})\n" +
                $"Pausing Robot {robotToPause.id} based on proximity rules.");
            robotToPause.ShowCollisionWarning(true);
            string collisionReason = collisionType switch
            {
                CollisionType.Proximity => $"Troppo vicini (distanza: {Vector3.Distance(robotA.GetEstimatedPosition(), robotB.GetEstimatedPosition()).ToString("F2")})",
                CollisionType.PathConflict => "Conflitto di percorso",
                _ => "Sconosciuto"
            };
            Debug.LogWarning($"Motivo pausa: {collisionReason}\n" +
                            $"Robot {robotToPause.id} in pausa per {collisionAvoidancePauseTime} secondi");
        }
    }

    private async Task CheckForTasks()
    {
        await CheckForShippingOrders();
        await CheckForDeliveries();
        await CheckForExpiredParcelsInBackup();
    }

    private async Task CheckForShippingOrders()
    {
        var result = await databaseManager.GetOldestOrderWithParcelCountAsync();
        foreach (var record in result)
        {
            var orderId = record["order"].As<INode>().Properties["orderId"].As<string>();
            var slotPositions = await databaseManager.GetParcelPositionsForOrderAsync(orderId);
            EnqueueTasks(slotPositions, "Shipping");
        }
    }

    private async Task CheckForDeliveries()
    {
        var result = await databaseManager.GetParcelsInDeliveryArea();
        foreach (var record in result)
        {
            var parcelPosition = new Vector3(record["x"].As<float>(), record["y"].As<float>(), record["z"].As<float>());
            EnqueueTask(parcelPosition, "Delivery");
        }
    }

    private async Task CheckForExpiredParcelsInBackup()
    {
        var slotPositions = await databaseManager.GetExpiredParcelsInBackupShelf();
        foreach (var (slot, category, timestamp) in slotPositions)
        {
            EnqueueTask(slot, "Disposal", category, timestamp);
        }
    }

    private void EnqueueTasks(IEnumerable<Vector3> positions, string type, string category = null, string timestamp = null)
    {
        foreach (var position in positions)
        {
            EnqueueTask(position, type, category, timestamp);
        }
    }

    private void EnqueueTask(Vector3 position, string type, string category = null, string timestamp = null)
    {
        if (!IsPositionAssigned(position))
        {
            pendingTasks.Enqueue((position, type, category, timestamp));
        }
    }

    private void CheckPendingTasks()
    {
        while (pendingTasks.Count > 0 && robots.Any(r => r.currentState == RobotState.Idle && !r.isPaused))
        {
            var nextTask = pendingTasks.Dequeue();
            AssignTask(nextTask.Position, nextTask.Type, nextTask.Category, nextTask.Timestamp);
        }
    }

    private void AssignTask(Vector3 taskPosition, string taskType, string category, string timestamp)
    {
        var availableRobot = FindAvailableRobot(taskPosition);
        if (availableRobot == null)
        {
            pendingTasks.Enqueue((taskPosition, taskType, category, timestamp));
            Debug.LogWarning($"Nessun robot disponibile (o non in pausa per collisione). Compito riaggiunto in coda. ({taskPosition})");
            return;
        }

        Debug.Log($"Assegnato task di {taskType} per la posizione {taskPosition} al Robot {availableRobot.id}.");
        availableRobot.destination = taskPosition;
        availableRobot.currentState = GetRobotState(taskType);
        availableRobot.category = category;
        availableRobot.timestamp = timestamp;
        robotAssignments[availableRobot.id] = taskPosition;
    }

    private RobotState GetRobotState(string taskType) => taskType switch
    {
        "Delivery" => RobotState.DeliveryState,
        "Shipping" => RobotState.ShippingState,
        "Disposal" => RobotState.DisposalState,
        _ => throw new System.ArgumentException($"Tipo di task sconosciuto: {taskType}")
    };

    private Robot FindAvailableRobot(Vector3 taskPosition) =>
        robots
            .Where(r => r.currentState == RobotState.Idle && !r.isPaused)
            .OrderBy(r => Vector3.Distance(r.GetEstimatedPosition(), taskPosition))
            .FirstOrDefault();

    private bool IsPositionAssigned(Vector3 position) =>
        robotAssignments.ContainsValue(position) || pendingTasks.Any(task => task.Position.Equals(position));

    public void NotifyTaskCompletion(int robotId)
    {
        if (robotAssignments.Remove(robotId))
        {
            Debug.Log($"Robot {robotId}: Rimozione assegnazione completata.");
        }
    }

    public IRecord AskSlot(string category, int robotId)
    {
        var result = Task.Run(() => databaseManager.GetAvailableSlot(category)).Result;
        if (result == null) return null;

        var record = result[0];
        var slotPosition = new Vector3(record[0].As<float>(), record[1].As<float>(), record[2].As<float>());

        if (robotAssignments.ContainsKey(robotId))
        {
            Debug.Log($"Robot {robotId}: Rimozione assegnazione corrente per la posizione {robotAssignments[robotId]}.");
            robotAssignments.Remove(robotId);
        }

        Debug.Log($"Robot {robotId}: Nuova assegnazione per lo slot {slotPosition}.");
        robotAssignments[robotId] = slotPosition;
        return record;
    }

    public void FreeSlotPosition(int robotId)
    {
        if (robotAssignments.Remove(robotId, out var currentPosition))
        {
            Debug.Log($"Robot {robotId}: Rimozione assegnazione corrente per la posizione {currentPosition}.");
        }
    }

    public void AssignConveyorPosition(int robotId, Vector3 conveyorPosition)
    {
        Debug.Log($"Robot {robotId}: Nuova assegnazione per il conveyor {conveyorPosition}.");
        robotAssignments[robotId] = conveyorPosition;
    }

    public Vector3 AskConveyorPosition()
    {
        var selectedPosition = shippingConveyorPositions[currentConveyorIndex];
        currentConveyorIndex = (currentConveyorIndex + 1) % shippingConveyorPositions.Count;
        return selectedPosition;
    }

    public bool AreThereTask() => pendingTasks.Count > 0;

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
                explainability.ToggleExplanation(isPaused); // Show/hide explanations
                explainability.ToggleExplanation(isPaused);
            }
        }
    }
}