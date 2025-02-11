using Neo4j.Driver;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class DatabaseManager : MonoBehaviour
{
    private Neo4jHelper neo4jHelper;

    private void Awake()
    {
        neo4jHelper = new Neo4jHelper("bolt://localhost:7687", "neo4j", "password");
    }

    private void OnDestroy()
    {
        neo4jHelper.CloseConnection();
    }

    public async Task<IList<IRecord>> ExecuteReadListAsync(string query, Dictionary<string, object> parameters = null)
    {
        return await neo4jHelper.ExecuteReadListAsync(query, parameters);
    }


    public async Task<List<(int id, Vector3 position)>> GetRobotPositionsAsync()
    {
        string query = @"
        MATCH (r:Robot)
        RETURN r.id AS id, r.x AS x, r.z AS z";

        var result = await neo4jHelper.ExecuteReadListAsync(query);
        var robotPositions = new List<(int id, Vector3 position)>();

        foreach (var record in result)
        {
            int id = record["id"].As<int>();
            float x = record["x"].As<float>();
            float z = record["z"].As<float>();
            Vector3 position = new Vector3(x, 0, z);

            robotPositions.Add((id, position));
        }

        return robotPositions;
    }

    public async Task<Vector3> GetLandmarkPositionFromDatabase(int id)
    {
        string query = @"
        MATCH (l:Landmark {id: $id})
        RETURN l.x AS x, l.z AS z
        LIMIT 1";

        var parameters = new Dictionary<string, object>
    {
        { "id", id }
    };

        var result = await neo4jHelper.ExecuteReadListAsync(query, parameters);

        foreach (var record in result)
        {
            float x = record["x"].As<float>();
            float z = record["z"].As<float>();
            return new Vector3(x, 0, z);
        }

        Debug.LogWarning($"No landmark found with ID: {id}");
        return Vector3.zero;
    }

    public async Task UpdateRobotStateAsync(
        int robotId, float xPosition, float zPosition, bool isActive, string currentTask, string newState, float batteryLevel)
    {
        var query = @"
            MATCH (r:Robot {id: $robotId}) 
            SET r.x = $xPosition, 
                r.z = $zPosition, 
                r.active = $isActive, 
                r.task = $currentTask, 
                r.state = $newState, 
                r.battery = $batteryLevel";

        var parameters = new Dictionary<string, object>
        {
            { "robotId", robotId },
            { "xPosition", xPosition },
            { "zPosition", zPosition },
            { "isActive", isActive },
            { "currentTask", currentTask },
            { "newState", newState },
            { "batteryLevel", batteryLevel }
        };

        await neo4jHelper.ExecuteWriteAsync(query, parameters);
    }

    public async Task UpdateRobotPositionAsync(int robotId, float xPosition, float zPosition)
    {
        var query = @"
        MATCH (r:Robot {id: $robotId}) 
        SET r.x = $xPosition, 
            r.z = $zPosition";

        var parameters = new Dictionary<string, object>
    {
        { "robotId", robotId },
        { "xPosition", xPosition },
        { "zPosition", zPosition }
    };

        await neo4jHelper.ExecuteWriteAsync(query, parameters);
    }

    public async Task<List<Vector3>> GetConveyorPositions()
    {
        string query = @"
        MATCH (shipping:Area {type: 'Shipping'})-[:HAS_POSITION]->(pos:Position)
        WHERE pos.hasParcel = false
        RETURN pos.x AS x, pos.y AS y, pos.z AS z 
        ";

        var conveyorPositions = new List<Vector3>();

        try
        {
            IList<IRecord> result = await neo4jHelper.ExecuteReadListAsync(query);

            foreach (var record in result)
            {
                float x = record["x"].As<float>();
                float y = record["y"].As<float>();
                float z = record["z"].As<float>();

                conveyorPositions.Add(new Vector3(x, y, z));
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error fetching conveyor positions: {ex.Message}");
        }

        return conveyorPositions;
    }

    public async Task<IList<IRecord>> GetParcelsInDeliveryArea()
    {
        try
        {
            string query = @"
            MATCH (delivery:Area {type: 'Delivery'})-[:HAS_POSITION]->(pos:Position {hasParcel: true})
            RETURN pos.x AS x, pos.y AS y, pos.z AS z";

            IList<IRecord> result = await neo4jHelper.ExecuteReadListAsync(query);
            return result;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error checking for parcels: {ex.Message}");
            return null;
        }
    }

    public async Task<IList<IRecord>> GetOldestOrderWithParcelCountAsync()
    {
        string query = @"
        MATCH (oldestOrder:Order)
        WHERE EXISTS {
            MATCH (oldestOrder)<-[:PART_OF]-(p:Parcel)<-[:CONTAINS]-(:Slot)
        }
        WITH oldestOrder
        ORDER BY oldestOrder.timestamp ASC
        LIMIT 1
        MATCH (p:Parcel)-[:PART_OF]->(oldestOrder)
        MATCH (p)<-[:CONTAINS]-(:Slot)
        RETURN oldestOrder AS order, COUNT(p) AS parcelCount";
        return await neo4jHelper.ExecuteReadListAsync(query);
    }


    public async Task<List<Vector3>> GetParcelPositionsForOrderAsync(string orderId)
    {
        string query = @"
        MATCH (p:Parcel)-[:PART_OF]->(oldestOrder:Order)
        MATCH (s:Shelf)-[:HAS_LAYER]->(l:Layer)-[:HAS_SLOT]->(slot:Slot)-[:CONTAINS]->(p)
        WHERE oldestOrder.orderId = $orderId
        RETURN s.x + slot.x AS x, l.y AS y, s.z AS z";

        var parameters = new Dictionary<string, object>
    {
        { "orderId", orderId }
    };

        var result = await neo4jHelper.ExecuteReadListAsync(query, parameters);
        var parcelPositions = new List<Vector3>();

        foreach (var record in result)
        {
            Vector3 position = new(
                record["x"].As<float>(),
                record["y"].As<float>(),
                record["z"].As<float>()
            );
            parcelPositions.Add(position);
        }

        return parcelPositions;
    }
    
    public async Task<List<(Vector3, string, string)>> GetExpiredParcelsInBackupShelf()
    {
        string query = @"
        MATCH (s:Shelf {category: 'Backup'})-[:HAS_LAYER]->(l:Layer)-[:HAS_SLOT]->(slot:Slot)-[:CONTAINS]->(p:Parcel)
        WHERE p.expirationTime < timestamp()
        RETURN p, s.x + slot.x AS x, l.y AS y, s.z AS z, p.category AS category, p.timestamp as timestamp"; 

        var result = await neo4jHelper.ExecuteReadListAsync(query);
        var parcels = new List<(Vector3, string, string)>();

        foreach (var record in result)
        {
            Vector3 position = new(
                record["x"].As<float>(),
                record["y"].As<float>(),
                record["z"].As<float>()
            );
            string category = record["category"].As<string>();
            string timestamp = record["category"].As<string>();
            parcels.Add((position, category, timestamp));
        }

        return parcels;
    }

    public async Task<IList<IRecord>> GetAvailableSlot(string category)
    {
        string query = @"
        MATCH (s:Shelf {category: $category})-[:HAS_LAYER]->(l:Layer)-[:HAS_SLOT]->(slot:Slot)
        WHERE NOT (slot)-[:CONTAINS]->(:Parcel) AND slot.occupied = false
        RETURN s.x + slot.x AS x, l.y AS y, s.z AS z, ID(slot) AS slotId
        LIMIT 1";
        var result = await neo4jHelper.ExecuteReadListAsync(query, new Dictionary<string, object> { { "category", category } });

         if (result == null || result.Count == 0)
            return null; // Nessuno slot disponibile
            
        long slotId = result[0]["slotId"].As<long>();
        query = @"
        MATCH (s:Slot)
        WHERE ID(s) = $slotId
        SET s.occupied = true";
        _ = neo4jHelper.ExecuteWriteAsync(query, new Dictionary<string, object> { { "slotId", slotId } });
        return result;
    }
}