using System.Collections.Generic;
using UnityEngine;
using Neo4j.Driver;

public class DatabaseManager : MonoBehaviour
{
    private IDriver _driver;

    // Inizializzazione del driver Neo4j
    public void Init(string uri, string user, string password)
    {
        _driver = GraphDatabase.Driver(uri, AuthTokens.Basic(user, password));
    }

    private void OnDestroy()
    {
        _driver?.Dispose();
    }

    // Esegui una query generica
    public async System.Threading.Tasks.Task<IResultCursor> RunQueryAsync(string query, Dictionary<string, object> parameters = null)
    {
        using (var session = _driver.AsyncSession())
        {
            return await session.RunAsync(query, parameters ?? new Dictionary<string, object>());
        }
    }

    // Recupera lo stato di tutti i robot dal database
    public async System.Threading.Tasks.Task<List<Dictionary<string, object>>> GetRobotsAsync()
    {
        var query = @"
            MATCH (r:Robot)
            RETURN r.id AS id, r.state AS state, r.battery AS battery, r.currentTask AS currentTask";

        var result = await RunQueryAsync(query);

        var robots = new List<Dictionary<string, object>>();

        while (await result.FetchAsync())
        {
            var robot = new Dictionary<string, object>
            {
                { "id", result.Current["id"].As<string>() },
                { "state", result.Current["state"].As<string>() },
                { "battery", result.Current["battery"].As<float>() },
                { "currentTask", result.Current["currentTask"]?.As<string>() }
            };
            robots.Add(robot);
        }

        return robots;
    }

    // Recupera pacchi nell'area di consegna (già implementato)
    public async System.Threading.Tasks.Task<List<string>> GetPendingParcelsAsync()
    {
        var query = @"
            MATCH (p:Position {hasParcel: true}) 
            RETURN p.x AS x, p.z AS z";

        var result = await RunQueryAsync(query);

        var parcels = new List<string>();

        while (await result.FetchAsync())
        {
            parcels.Add($"{result.Current["x"]},{result.Current["z"]}");
        }

        return parcels;
    }

    // Recupera ordini disponibili (già implementato)
    public async System.Threading.Tasks.Task<List<string>> GetPendingOrdersAsync()
    {
        var query = @"
            MATCH (o:Product) 
            WHERE NOT EXISTS(o.assignedRobot) 
            RETURN o.product_name AS name, o.category AS category";

        var result = await RunQueryAsync(query);

        var orders = new List<string>();

        while (await result.FetchAsync())
        {
            orders.Add($"{result.Current["name"]}:{result.Current["category"]}");
        }

        return orders;
    }

    // Aggiorna lo stato di un robot (già implementato)
    public async System.Threading.Tasks.Task UpdateRobotStateAsync(string robotId, string newState, string currentTask, float batteryLevel)
    {
        var query = "MATCH (r:Robot {id: $robotId}) SET r.state = $state RETURN r";
        var parameters = new Dictionary<string, object>
        {
            { "robotId", robotId },
            { "state", newState }
        };

        await RunQueryAsync(query, parameters);
    }

    // Assegna un task a un robot (già implementato)
    public async System.Threading.Tasks.Task AssignTaskToRobotAsync(string robotId, string task)
    {
        var query = "MATCH (r:Robot {id: $robotId}) SET r.currentTask = $task, r.state = 'busy' RETURN r";
        var parameters = new Dictionary<string, object>
        {
            { "robotId", robotId },
            { "task", task }
        };

        await RunQueryAsync(query, parameters);
    }
}