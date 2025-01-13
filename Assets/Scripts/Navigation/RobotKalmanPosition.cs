using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

public class RobotKalmanPosition : MonoBehaviour
{
    [Header("Kalman Filter Parameters")]
    public float processNoise = 0.01f;
    public float measurementNoise = 0.3f;

    [Header("Sensore")]
    public float sensorRange = 17.5f; // Raggio massimo di rilevamento dei sensori
    public LayerMask landmarkLayer; // Layer dei landmark nella scena

    [Header("Database Manager")]
    public DatabaseManager dbManager;

    private KalmanFilter positionKalmanFilterX;
    private KalmanFilter positionKalmanFilterZ;
    private Vector3 estimatedPosition;

    private List<int> detectedLandmarkIDs = new List<int>();

    public void Start()
    {
        InitializeKalmanFilters();
    }

    public async void Update()
    {
        PerformRaycastDetection();
        Vector3 noisyPosition = await CalculateWeightedNoisyPositionFromIDs(); // Usando media ponderata
        UpdateEstimatedPosition(noisyPosition);
    }

    private void InitializeKalmanFilters()
    {
        positionKalmanFilterX = new KalmanFilter(0f, 1f, processNoise, measurementNoise);
        positionKalmanFilterZ = new KalmanFilter(0f, 1f, processNoise, measurementNoise);
    }

    private void PerformRaycastDetection()
    {
        detectedLandmarkIDs.Clear();

        Collider[] colliders = Physics.OverlapSphere(transform.position, sensorRange, landmarkLayer);

        foreach (Collider collider in colliders)
        {
            Landmark landmark = collider.GetComponent<Landmark>();
            if (landmark != null)
            {
                Vector3 direction = (collider.transform.position - transform.position).normalized;
                if (Physics.Raycast(transform.position, direction, out RaycastHit hit, sensorRange, landmarkLayer))
                {
                    if (hit.collider.GetComponent<Landmark>() == landmark)
                    {
                        landmark.OnHitByRay(this);
                    }
                }
            }
        }
    }

    public void ReceiveLandmarkID(int id)
    {
        if (!detectedLandmarkIDs.Contains(id))
        {
            detectedLandmarkIDs.Add(id);
        }
    }

    private async Task<Vector3> CalculateWeightedNoisyPositionFromIDs()
    {
        if (detectedLandmarkIDs.Count == 0)
        {
            Debug.LogWarning("No landmarks detected!");
            return transform.position; // Usa la posizione corrente se non ci sono landmark
        }

        List<int> landmarkIDsCopy = new List<int>(detectedLandmarkIDs);
        Vector3 weightedSum = Vector3.zero;
        float totalWeight = 0;

        foreach (int id in landmarkIDsCopy)
        {
            try
            {
                Vector3 landmarkPosition = await dbManager.GetLandmarkPositionFromDatabase(id);
                float distance = Vector3.Distance(transform.position, landmarkPosition);
                float weight = 1f / Mathf.Pow(distance + 0.1f, 2);
                weightedSum += landmarkPosition * weight;
                totalWeight += weight;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error fetching landmark ID {id}: {ex.Message}");
            }
        }

        Vector3 weightedAverage = weightedSum / totalWeight;
        float actualDistance = Vector3.Distance(transform.position, weightedAverage);
        Debug.Log($"Estimated Position: {weightedAverage}, Actual Position: {transform.position}, Distance: {actualDistance}");


        // Rumore
        float noiseX = UnityEngine.Random.Range(-0.1f, 0.1f);
        float noiseZ = UnityEngine.Random.Range(-0.1f, 0.1f);
        return new Vector3(weightedAverage.x + noiseX, transform.position.y, weightedAverage.z + noiseZ);
    }

    private void UpdateEstimatedPosition(Vector3 noisyPosition)
    {
        float estimatedX = positionKalmanFilterX.Update(noisyPosition.x);
        float estimatedZ = positionKalmanFilterZ.Update(noisyPosition.z);

        estimatedPosition = new Vector3(estimatedX, transform.position.y, estimatedZ);
    }

    public Vector3 GetEstimatedPosition()
    {
        return estimatedPosition;
    }
}
