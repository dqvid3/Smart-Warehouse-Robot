using UnityEngine;
using Neo4j.Driver;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;

public class DeliveryAreaManager : MonoBehaviour
{
    public GameObject parcelPrefab;
    private Neo4jHelper neo4jHelper;
    private float deliveryAreaCenterX;
    private float deliveryAreaCenterZ;
    private float deliveryAreaLength;
    private float deliveryAreaWidth;
    private List<Vector3> predefinedPositions = new();
    private int nextPredefinedPositionIndex = 0;

    private async void Start()
    {
        neo4jHelper = new Neo4jHelper("bolt://localhost:7687", "neo4j", "password");
        await GetDeliveryAreaDimensions();
        await GeneratePredefinedPositions(36.8f, 1.259f, 0);
        await CheckAndSpawnParcels();
    }

    private async Task GetDeliveryAreaDimensions()
    {
        try
        {
            var result = await neo4jHelper.ExecuteReadAsync(@"
                MATCH (a:Area {type: 'Delivery'})
                RETURN a.center_x AS x, a.center_z AS z, a.length AS length, a.width AS width
            ");

            deliveryAreaCenterX = result["x"].As<float>();
            deliveryAreaCenterZ = result["z"].As<float>();
            deliveryAreaLength = result["length"].As<float>();
            deliveryAreaWidth = result["width"].As<float>();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error getting delivery area dimensions: {ex.Message}");
        }
    }

    private async Task GeneratePredefinedPositions(float x, float y, float z)
    {
        try
        {
            // Rimuovi tutte le posizioni precedenti, se esistono
            predefinedPositions.Clear();

            // Aggiungi una sola posizione basata sugli argomenti forniti
            predefinedPositions.Add(new Vector3(x, y, z));

            Debug.Log($"Posizione predefinita aggiunta: ({x}, {y}, {z})");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error generating predefined position: {ex.Message}");
        }
    }


    private async Task CheckAndSpawnParcels()
    {
        try
        {
            var parcelsInDelivery = await neo4jHelper.ExecuteReadListAsync(@"
                MATCH (p:Parcel)-[:LOCATED_IN]->(a:Area {type: 'Delivery'})
                RETURN p.timestamp AS timestamp, p.category AS category, p.product_name AS productName
            ");

            for (int i = 0; i < parcelsInDelivery.Count; i++)
            {
                var record = parcelsInDelivery[i];
                string timestamp = record["timestamp"].As<string>();
                string category = record["category"].As<string>();
                string productName = record["productName"].As<string>();

                string qrCodeString = $"{timestamp}|{category}|{productName}";
                SpawnParcel(qrCodeString);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error spawning parcels: {ex.Message}");
        }
    }

    public void SpawnNewParcels(List<Dictionary<string, string>> newParcels)
    {
        foreach (var parcelData in newParcels)
        {
            string timestamp = parcelData["timestamp"];
            string category = parcelData["category"];
            string productName = parcelData["productName"];

            string qrCodeString = $"{timestamp}|{category}|{productName}";
            SpawnParcel(qrCodeString);
        }
    }

    private void SpawnParcel(string qrCodeString)
    {
        // Instantiate the parcel prefab
        GameObject parcelObject = Instantiate(parcelPrefab, predefinedPositions[nextPredefinedPositionIndex], Quaternion.identity);
        nextPredefinedPositionIndex++;
        // Set the QR Code on the instantiated parcel
        QRCodeGenerator qRCodeGenerator = parcelObject.GetComponentInChildren<QRCodeGenerator>();
        qRCodeGenerator.qrCodeString = qrCodeString;

    }

    private void OnDestroy()
    {
        neo4jHelper?.CloseConnection();
    }
}