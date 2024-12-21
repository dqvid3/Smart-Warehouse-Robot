using UnityEngine;
using System.Collections;

public class MessagingSystem : MonoBehaviour
{
    public delegate void RobotMessageHandler(string message, MessagingSystem sender);
    public event RobotMessageHandler OnMessageReceived;

    public string systemName;

    // Simulated database access
    public void GetTaskDetailsFromDatabase(string taskId, System.Action<string> callback)
    {
        StartCoroutine(SimulateDatabaseQuery(taskId, callback));
    }

    private IEnumerator SimulateDatabaseQuery(string taskId, System.Action<string> callback)
    {
        // Simulating network latency
        yield return new WaitForSeconds(0.1f);
        string result = $"Details for Task ID {taskId}: Pickup package from Zone A and deliver to Zone B.";
        callback?.Invoke(result);
    }

    public void ComposeAndSendMessageTo(MessagingSystem targetSystem, string taskId)
    {
        GetTaskDetailsFromDatabase(taskId, taskDetails =>
        {
            string message = $"Task Assigned: {taskDetails}";
            SendMessageTo(targetSystem, message);
        });
    }

    public void ComposeAndBroadcastMessage(MessagingSystem[] systems, string taskId)
    {
        GetTaskDetailsFromDatabase(taskId, taskDetails =>
        {
            string message = $"Broadcast Task: {taskDetails}";
            BroadcastMessage(systems, message);
        });
    }

    public void SendMessageTo(MessagingSystem targetSystem, string message)
    {
        Debug.Log($"{systemName} sending message to {targetSystem.systemName}: {message}");
        targetSystem.OnMessageReceived?.Invoke(message, this);
    }

    public void BroadcastMessage(MessagingSystem[] systems, string message)
    {
        foreach (var system in systems)
        {
            if (system != this)
            {
                Debug.Log($"{systemName} broadcasting message to {system.systemName}: {message}");
                system.OnMessageReceived?.Invoke(message, this);
            }
        }
    }
}




/* ESEMPIO DI UTILIZZO


void Start()
{
    MessagingSystem robot1 = new MessagingSystem { systemName = "Robot1" };
    MessagingSystem robot2 = new MessagingSystem { systemName = "Robot2" };

    robot1.OnMessageReceived += (message, sender) =>
    {
        Debug.Log($"Robot1 received message: {message}");
    };

    robot2.OnMessageReceived += (message, sender) =>
    {
        Debug.Log($"Robot2 received message: {message}");
    };

    // Test sending a message
    robot1.ComposeAndSendMessageTo(robot2, "1234");

    // Test broadcasting a message
    MessagingSystem[] robots = { robot1, robot2 };
    robot1.ComposeAndBroadcastMessage(robots, "5678");
}




*/