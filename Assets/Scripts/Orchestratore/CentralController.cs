using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;

public class CentralController : MonoBehaviour
{
    [Header("Central Controller Settings")]
    public List<Robot> robots;
    public DatabaseManager databaseManager;

    private bool isRunning = true;

    private async void Start()
    {
        if (robots.Count == 0)
        {
            Debug.LogError("Nessun robot assegnato!");
            return;
        }

        foreach (var robot in robots)
        {
            robot.databaseManager = databaseManager;
        }

        Debug.Log("Inizio gestione automatica dei task dei robot...");
        await ManageTasksLoop();
    }

    private async Task ManageTasksLoop()
    {
        while (isRunning)
        {
            Debug.Log("Inizio ciclo di gestione dei task...");

            var robotsStates = await databaseManager.GetRobotsAsync();
            var pendingParcels = await databaseManager.GetPendingParcelsAsync();
            var pendingOrders = await databaseManager.GetPendingOrdersAsync();

            foreach (var robotState in robotsStates)
            {
                var robotId = robotState["id"].ToString();
                var battery = (float)robotState["battery"];
                var state = robotState["state"].ToString();

                if (battery < 20 && state != "charging")
                {
                    await SendRobotToRecharge(robotId);
                    continue;
                }

                if ((state == "inactive" || state == "waiting") && pendingParcels.Count > 0)
                {
                    var parcel = pendingParcels[0];
                    pendingParcels.RemoveAt(0);

                    var positions = ParseParcelPositions(parcel);
                    await AssignTaskToRobot(robotId, positions);
                }
            }

            await Task.Delay(5000);
        }
    }

    private Task SendRobotToRecharge(string robotId)
    {
        Robot robot = GetRobotById(robotId);
        if (robot != null)
        {
            robot.StartCharging();
            Debug.Log($"Robot {robotId} inviato alla stazione di ricarica.");
        }

        return Task.CompletedTask;
    }

    private Task AssignTaskToRobot(string robotId, (Vector3 pickup, Vector3 shelf, Vector3 shipping) positions)
    {
        Robot robot = GetRobotById(robotId);
        if (robot != null)
        {
            //robot.PickupParcel(positions.pickup, positions.shelf, positions.shipping);
            Debug.Log($"Task assegnato al robot {robotId}.");
        }

        return Task.CompletedTask;
    }

    private Robot GetRobotById(string robotId)
    {
        return robots.Find(robot => robot.id.ToString() == robotId);
    }

    private (Vector3 pickup, Vector3 shelf, Vector3 shipping) ParseParcelPositions(string parcelData)
    {
        var positions = parcelData.Split(';');
        return (
            StringToVector3(positions[0]),
            StringToVector3(positions[1]),
            StringToVector3(positions[2])
        );
    }

    private Vector3 StringToVector3(string position)
    {
        var coords = position.Split(',');
        return new Vector3(float.Parse(coords[0]), 0, float.Parse(coords[1]));
    }
}