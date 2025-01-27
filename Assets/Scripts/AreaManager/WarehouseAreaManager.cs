using UnityEngine;
using Neo4j.Driver;
using System;
using System.Threading.Tasks;

public class WarehouseAreaManager : MonoBehaviour
{
    public GameObject parcelPrefab;
    private Neo4jHelper neo4jHelper;

    private async void Start()
    {
        neo4jHelper = new Neo4jHelper("bolt://localhost:7687", "neo4j", "password");
        await SpawnParcelsInShelves();
    }

    private async Task SpawnParcelsInShelves()
    {
        try
        {
            var parcelsInShelves = await neo4jHelper.ExecuteReadListAsync(@"
            MATCH (slot:Slot)-[:CONTAINS]->(parcel:Parcel)
            MATCH (shelf:Shelf)-[:HAS_LAYER]->(layer:Layer)-[:HAS_SLOT]->(slot)
            RETURN parcel.timestamp AS timestamp, parcel.category AS category, parcel.product_name AS productName, 
            shelf.x + slot.x AS x, layer.y AS y, shelf.z AS z");
            foreach (var record in parcelsInShelves)
            {
                string timestamp = record["timestamp"].As<string>();
                string category = record["category"].As<string>();
                string productName = record["productName"].As<string>();
                float x = record["x"].As<float>();
                float y = record["y"].As<float>();
                float z = record["z"].As<float>();
                string qrCodeString = $"{timestamp}|{category}|{productName}";
                Vector3 position = new(x, y, z);
                SpawnParcel(qrCodeString, position);
            }

        }
        catch (Exception ex)
        {
            Debug.LogError($"Error getting parcels in shelves: {ex.Message}");
        }
    }

    private void SpawnParcel(string qrCodeString, Vector3 position)
    {
        GameObject parcelObject = Instantiate(parcelPrefab, position, Quaternion.Euler(0, 90, 0));

        QRCodeGenerator qRCodeGenerator = parcelObject.GetComponentInChildren<QRCodeGenerator>();
        qRCodeGenerator.qrCodeString = qrCodeString;
    }

    private void OnDestroy()
    {
        neo4jHelper?.CloseConnection();
    }
}