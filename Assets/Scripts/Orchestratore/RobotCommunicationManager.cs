using System.Collections.Generic;
using UnityEngine;

public class RobotCommunicationManager : MonoBehaviour
{
    private Dictionary<string, Robot> robots = new Dictionary<string, Robot>();
    private HashSet<string> occupiedShelves = new HashSet<string>(); // Per tenere traccia degli scaffali occupati

    // Registra un robot nel sistema
    public void RegisterRobot(string robotId, Robot robot)
    {
        if (!robots.ContainsKey(robotId))
        {
            robots.Add(robotId, robot);
        }
    }

    // Invia un compito a un robot
    public void AssignTaskToRobot(string robotId, string task, string shelfId = null)
    {
        if (robots.ContainsKey(robotId))
        {
            // Verifica se lo scaffale è occupato
            if (shelfId != null && occupiedShelves.Contains(shelfId))
            {
                Debug.LogError($"Scaffale {shelfId} è già occupato.");
                return;
            }

            // Se c'è uno scaffale associato, occuparlo
            if (shelfId != null)
            {
                occupiedShelves.Add(shelfId);
            }

            //robots[robotId].ReceiveTask(task, shelfId);
        }
        else
        {
            Debug.LogError($"Robot {robotId} non registrato.");
        }
    }

    // Notifica il completamento di un compito
    public void NotifyTaskCompletion(string robotId, string task, string shelfId = null)
    {
        Debug.Log($"Robot {robotId} ha completato il task: {task}");

        // Libera lo scaffale
        if (shelfId != null && occupiedShelves.Contains(shelfId))
        {
            occupiedShelves.Remove(shelfId);
        }
    }
}