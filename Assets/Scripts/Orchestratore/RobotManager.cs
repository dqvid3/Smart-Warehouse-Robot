using System.Collections.Generic;
using UnityEngine;
using Neo4j.Driver;
using static Robot;
using System.Linq;
using System.Threading.Tasks;
using System.Collections;
using UnityEngine.AI;

public class RobotManager : MonoBehaviour
{
    public List<Robot> robots = new();
    public DatabaseManager databaseManager;
    public RobotKalmanPosition robotKalmanPosition;
    private Dictionary<Vector3, int> assignedParcels = new();
    private Queue<Vector3> pendingDeliveryTasks = new();
    private Queue<Vector3> pendingShippingTasks = new();
    private List<Vector3> conveyorPositions;
    private float checkInterval = 2f;
    private float lastCheckTime = 0f;
    private HashSet<int> stoppedRobots = new HashSet<int>(); // Tiene traccia dei robot che sono stati fermati

    private async void Start()
    {
        string robotList = string.Join(", ", robots.Select(r => "ID: " + r.id + ", Stato: " + r.currentState).ToArray());
        Debug.Log("RobotManager avviato.\nRobot collegati: [" + robotList + "]");
        conveyorPositions = await databaseManager.GetConveyorPositions();
    }

    private void Update()
    {
        if (Time.time - lastCheckTime > checkInterval)
        {
            lastCheckTime = Time.time;
            CheckForShippingOrders();
            CheckForDeliveries();
            CheckPendingTasks();
        }
        CheckAdjacentRobotConflicts();
    }

    private async void CheckForShippingOrders()
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

    private async void CheckForDeliveries()
    {
        IList<IRecord> result = await databaseManager.GetParcelsInDeliveryArea();
        foreach (var record in result)
        {
            Vector3 parcelPosition = new(record["x"].As<float>(), record["y"].As<float>(), record["z"].As<float>());
            if (assignedParcels.ContainsKey(parcelPosition)) continue;
            AssignDeliveryTask(parcelPosition);
        }
    }

    private void CheckPendingTasks()
    {
        // Controlla se ci sono robot disponibili PRIMA di assegnare i task
        bool robotsAvailable = robots.Any(r => r.isActive && r.currentState == RobotState.Idle);

        if (robotsAvailable)
        {
            // Assegna i task di shipping
            while (pendingShippingTasks.Count > 0 && robotsAvailable)
            {
                var nextTask = pendingShippingTasks.Dequeue();
                AssignShippingTask(nextTask);
                robotsAvailable = robots.Any(r => r.isActive && r.currentState == RobotState.Idle);
            }

            // Assegna i task di delivery
            while (pendingDeliveryTasks.Count > 0 && robotsAvailable)
            {
                var nextTask = pendingDeliveryTasks.Dequeue();
                AssignDeliveryTask(nextTask);
                robotsAvailable = robots.Any(r => r.isActive && r.currentState == RobotState.Idle);
            }
        }
    }

    private void CheckAdjacentRobotConflicts()
    {
        foreach (var parcel1 in assignedParcels)
        {
            foreach (var parcel2 in assignedParcels)
            {
                // Ignora lo stesso pacco o non adiacenti
                if (parcel1.Key == parcel2.Key || !IsAdjacent(parcel1.Key, parcel2.Key))
                    continue;

                // Trova i robot assegnati
                Robot robot1 = robots.FirstOrDefault(r => r.id == parcel1.Value);
                Robot robot2 = robots.FirstOrDefault(r => r.id == parcel2.Value);

                if (robot1 == null || robot2 == null || !robot1.isActive || !robot2.isActive)
                    continue;

                float distanceBetweenRobots = Vector3.Distance(robot1.GetEstimatedPosition(), robot2.GetEstimatedPosition());

                // Fermare i robot solo se sono vicini
                if (distanceBetweenRobots <= 7f) // Soglia per considerare i robot "vicini"
                {
                    Debug.Log($"Conflitto tra Robot {robot1.id} e Robot {robot2.id}, distanza: {distanceBetweenRobots}");

                    float distance1 = Vector3.Distance(robot1.GetEstimatedPosition(), parcel1.Key);
                    float distance2 = Vector3.Distance(robot2.GetEstimatedPosition(), parcel2.Key);

                    if (distance1 > distance2)
                    {
                        if (!stoppedRobots.Contains(robot1.id))
                        {
                            StartCoroutine(DelayRobotMovement(robot1, 3f));
                            stoppedRobots.Add(robot1.id);
                        }
                    }
                    else
                    {
                        if (!stoppedRobots.Contains(robot2.id))
                        {
                            StartCoroutine(DelayRobotMovement(robot2, 3f));
                            stoppedRobots.Add(robot2.id);
                        }
                    }
                }
            }
        }
    }

    private bool IsAdjacent(Vector3 slot1, Vector3 slot2)
    {
        float distanceX = Mathf.Abs(slot1.x - slot2.x);
        return distanceX >= 1.8f && distanceX <= 3f;
    }

    private IEnumerator DelayRobotMovement(Robot robot, float delayTime)
    {
        Debug.Log($"Robot {robot.id} fermo per {delayTime} secondi.");
        NavMeshAgent agent = robot.GetComponent<NavMeshAgent>();
        agent.isStopped = true;
        yield return new WaitForSeconds(delayTime);
        agent.isStopped = false;
        Debug.Log($"Robot {robot.id} riprende il movimento.");
    }

    private void AssignShippingTask(Vector3 parcelPosition)
    {
        if (assignedParcels.ContainsKey(parcelPosition))
            return; // Considera il task già assegnato

        Robot availableRobot = FindAvailableRobot(parcelPosition);
        if (availableRobot == null)
        {
            if (!pendingShippingTasks.Contains(parcelPosition))
            {
                pendingShippingTasks.Enqueue(parcelPosition);
                Debug.LogWarning($"Nessun robot disponibile per Shipping. Compito aggiunto in coda. ({parcelPosition})");
            }
            return; // Nessun robot disponibile
        }

        Debug.Log($"Assegnato shipping task al Robot {availableRobot.id}.");
        availableRobot.destination = parcelPosition;
        availableRobot.currentState = RobotState.ShippingState;
        assignedParcels[parcelPosition] = availableRobot.id; // Registra l'assegnazione
        return; // Task assegnato con successo
    }

    private void AssignDeliveryTask(Vector3 parcelPosition)
    {
        if (assignedParcels.ContainsKey(parcelPosition))
            return; // Considera il task già assegnato

        Robot availableRobot = FindAvailableRobot(parcelPosition);
        if (availableRobot == null)
        {
            if (!pendingDeliveryTasks.Contains(parcelPosition))
            {
                pendingDeliveryTasks.Enqueue(parcelPosition);
                Debug.LogWarning($"Nessun robot disponibile per Delivery. Compito aggiunto in coda. {parcelPosition}");
            }
            return; // Nessun robot disponibile
        }

        Debug.Log($"Assegnato delivery task al Robot {availableRobot.id}.");
        availableRobot.destination = parcelPosition;
        availableRobot.currentState = RobotState.DeliveryState;
        assignedParcels[parcelPosition] = availableRobot.id; // Registra l'assegnazione
        return; // Task assegnato con successo
    }

    public bool AreThereTask()
    {
        if (pendingDeliveryTasks.Count > 0)
        {
            return true;
        }
        else if (pendingShippingTasks.Count > 0)
        {
            return true;
        }
        return false;
    }

    private int currentConveyorIndex = 0;
    public Vector3 AskConveyorPosition()
    {
        Vector3 selectedPosition = conveyorPositions[currentConveyorIndex];
        currentConveyorIndex = (currentConveyorIndex + 1) % conveyorPositions.Count;
        return selectedPosition;
    }

    public IRecord AskSlot(string category, int robotId)
    {
        IList<IRecord> result = Task.Run(() => databaseManager.GetAvailableSlot(category)).Result;
        var record = result[0];
        float x = record[0].As<float>();
        float y = record[1].As<float>();
        float z = record[2].As<float>();
        Vector3 slotPosition = new(x, y, z);
        assignedParcels[slotPosition] = robotId;
        return result[0];
    }

    private Robot FindAvailableRobot(Vector3 parcelPosition)
    {
        Robot closestRobot = null;
        float shortestDistance = float.MaxValue;
        foreach (Robot robot in robots)
        {
            if (!robot.isActive || robot.currentState != RobotState.Idle) continue;
            Vector3 robotPosition = robot.GetEstimatedPosition();
            Debug.Log(robotPosition);
            float distanceToParcel = Vector3.Distance(robotPosition, parcelPosition);
            if (distanceToParcel < shortestDistance)
            {
                shortestDistance = distanceToParcel;
                closestRobot = robot;
            }
        }
        return closestRobot;
    }

    public void NotifyTaskCompletion(int robotId)
    {
        var parcelsToRemove = assignedParcels.Where(pair => pair.Value == robotId).ToList();
        foreach (var pair in parcelsToRemove) assignedParcels.Remove(pair.Key);
        // Rimuovi il robot dalla lista dei robot fermati quando ha finito
        stoppedRobots.Remove(robotId);
    }
}