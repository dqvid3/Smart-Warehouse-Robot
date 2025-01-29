using UnityEngine;
using Neo4j.Driver;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

public class DeliveryAreaManager : MonoBehaviour
{
    public GameObject parcelPrefab;
    private Neo4jHelper neo4jHelper;
    private List<Vector3> predefinedPositions = new();
    private HashSet<string> spawnedParcels = new(); 
    private HashSet<Vector3> occupiedPositions = new(); 
    private float checkInterval = 5f; 
    private float lastCheckTime = 0f;

    private async void Start()
    {
        neo4jHelper = new Neo4jHelper("bolt://localhost:7687", "neo4j", "password");
        await GetPredefinedPositions();
    }

    private async void Update()
    {
        if (Time.time - lastCheckTime > checkInterval)
        {
            lastCheckTime = Time.time;
            await UpdateParcels();
        }
    }

    private async Task GetPredefinedPositions()
    {
        try
        {
            predefinedPositions.Clear();
            var positions = await neo4jHelper.ExecuteReadListAsync(@"
            MATCH (a:Area {type: 'Delivery'})-[:HAS_POSITION]->(p:Position)
            WHERE p.hasParcel = false
            RETURN p.x AS x, p.y AS y, p.z AS z");

            foreach (var pos in positions)
            {
                float x = pos["x"].As<float>() + 24; 
                float y = pos["y"].As<float>();
                float z = pos["z"].As<float>();
                Vector3 position = new(x, y, z);
                predefinedPositions.Add(position);
            }

 
            occupiedPositions.RemoveWhere(pos => predefinedPositions.Contains(pos));
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error getting predefined positions: {ex.Message}");
        }
    }

    private async Task UpdateParcels()
    {
        try
        {
            await GetPredefinedPositions();

            var parcelsInDelivery = await neo4jHelper.ExecuteReadListAsync(@"
            MATCH (p:Parcel)-[:LOCATED_IN]->(a:Area {type: 'Delivery'})
            RETURN p.timestamp AS timestamp, p.category AS category, p.product_name AS productName");
            HashSet<string> currentParcels = new HashSet<string>(parcelsInDelivery.Select(record => record["timestamp"].As<string>()));

            spawnedParcels.IntersectWith(currentParcels);

            var parcelsToSpawn = parcelsInDelivery.Where(record => !spawnedParcels.Contains(record["timestamp"].As<string>())).ToList();

            int spawnedCount = 0;
            for (int i = 0; i < predefinedPositions.Count && spawnedCount < parcelsToSpawn.Count; i++)
            {
                var position = predefinedPositions[i];
                if (occupiedPositions.Contains(position)) continue; 
                var record = parcelsToSpawn[spawnedCount];
                string timestamp = record["timestamp"].As<string>();
                string category = record["category"].As<string>();
                string productName = record["productName"].As<string>();
                string qrCodeString = $"{timestamp}|{category}|{productName}";
                SpawnParcel(qrCodeString, position);
                spawnedParcels.Add(timestamp);
                occupiedPositions.Add(position);
                spawnedCount++;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error spawning parcels: {ex.Message}");
        }
    }

    private void SpawnParcel(string qrCodeString, Vector3 position)
    {
        GameObject parcelObject = Instantiate(parcelPrefab, position, Quaternion.identity);

        QRCodeGenerator qRCodeGenerator = parcelObject.GetComponentInChildren<QRCodeGenerator>();
        qRCodeGenerator.qrCodeString = qrCodeString;
    }

    private void OnDestroy()
    {
        neo4jHelper?.CloseConnection();
    }
}
