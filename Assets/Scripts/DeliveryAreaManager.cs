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

    private async void Start()
    {
        neo4jHelper = new Neo4jHelper("bolt://localhost:7687", "neo4j", "password");
        await GetDeliveryAreaDimensions();
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

    private async Task CheckAndSpawnParcels()
    {
        try
        {
            var parcelsInDelivery = await neo4jHelper.ExecuteReadListAsync(@"
                MATCH (p:Parcel)-[:LOCATED_IN]->(a:Area {type: 'Delivery'})
                RETURN p.timestamp AS timestamp, p.category AS category, p.product_name AS productName
            ");

            foreach (var record in parcelsInDelivery)
            {
                string timestamp = record["timestamp"].As<string>();
                string category = record["category"].As<string>();
                string productName = record["productName"].As<string>();

                // Generate QR Code string
                string qrCodeString = $"{timestamp} {category} {productName}";

                // Calculate a random position within the delivery area
                float randomX = deliveryAreaCenterX + UnityEngine.Random.Range(-deliveryAreaLength / 2, deliveryAreaLength / 2);
                float randomZ = deliveryAreaCenterZ + UnityEngine.Random.Range(-deliveryAreaWidth / 2, deliveryAreaWidth / 2);
                Vector3 spawnPosition = new Vector3(randomX, 1f, randomZ);

                // Instantiate the parcel prefab
                GameObject parcelObject = Instantiate(parcelPrefab, spawnPosition, Quaternion.identity);

                // Set the QR Code on the instantiated parcel
                QRCodeGenerator qRCodeGenerator = parcelObject.GetComponentInChildren<QRCodeGenerator>();
                qRCodeGenerator.qrCodeString = qrCodeString;
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

            // Generate QR Code string
            string qrCodeString = $"{timestamp} {category} {productName}";

            // Calculate a random position within the delivery area
            float randomX = deliveryAreaCenterX + UnityEngine.Random.Range(-deliveryAreaLength / 2, deliveryAreaLength / 2);
            float randomZ = deliveryAreaCenterZ + UnityEngine.Random.Range(-deliveryAreaWidth / 2, deliveryAreaWidth / 2);
            Vector3 spawnPosition = new Vector3(randomX, 1f, randomZ);

            // Instantiate the parcel prefab
            GameObject parcelObject = Instantiate(parcelPrefab, spawnPosition, Quaternion.identity);

            // Set the QR Code on the instantiated parcel
            QRCodeGenerator qRCodeGenerator = parcelObject.GetComponentInChildren<QRCodeGenerator>();
            qRCodeGenerator.qrCodeString = qrCodeString;
        }
    }

    private void OnDestroy()
    {
        neo4jHelper?.CloseConnection();
    }
}