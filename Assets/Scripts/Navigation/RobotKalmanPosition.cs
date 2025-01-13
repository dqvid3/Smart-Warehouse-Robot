using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using Unity.VisualScripting;

public class RobotKalmanPosition : MonoBehaviour
{
    [Header("Kalman Filter Parameters")]
    public float processNoise = 0.01f;
    public float measurementNoise = 0.4f;
    public float odometryNoise = 0.01f;

    [Header("Sensor")]
    public float sensorRange = 17.5f; // Raggio massimo di rilevamento dei sensori
    public LayerMask landmarkLayer; // Layer dei landmark nella scena

    [Header("Database Manager")]
    public DatabaseManager dbManager;

    [Header("Movement")]
    public MovementWithAStar movement;

    private KalmanFilter positionKalmanFilterX;
    private KalmanFilter positionKalmanFilterZ;
    private Vector3 odometryStart;
    private Vector3 estimatedPosition;

    private List<int> detectedLandmarkIDs = new List<int>();
    private int numLandmarksDetected;

    public void Start()
    {
        InitializeKalmanFilters();
        numLandmarksDetected = 0;
        odometryStart = movement.start;
        estimatedPosition = odometryStart;
    }

    public async void Update()
    {
        // Predizione della posizione basata sull'odometria
        Vector3 predictedPosition = PredictPositionFromOdometry();

        // Correzione della posizione con i landmark
        numLandmarksDetected = PerformRaycastDetection();
        if (numLandmarksDetected >= 3)
        {
            Vector3 correctedPosition = await CorrectPositionWithLandmarks(predictedPosition);
            UpdateEstimatedPosition(correctedPosition);
        }
        else
        {
            UpdateEstimatedPosition(predictedPosition);
        }
    }

    private void InitializeKalmanFilters()
    {
        positionKalmanFilterX = new KalmanFilter(0f, 1f, processNoise, measurementNoise);
        positionKalmanFilterZ = new KalmanFilter(0f, 1f, processNoise, measurementNoise);
    }

    private Vector3 PredictPositionFromOdometry()
    {
        Vector3 currentRobotPosition = movement.start;
        Vector3 deltaPosition = currentRobotPosition - odometryStart;

        odometryStart = movement.start;

        //Quaternion currentRotation = movement.robotToMove.transform.rotation;
        //Vector3 rotatedDelta = currentRotation * deltaPosition;

        // Rumore per incertezze
        float noiseX = UnityEngine.Random.Range(-odometryNoise, odometryNoise);
        float noiseZ = UnityEngine.Random.Range(-odometryNoise, odometryNoise);
        Vector3 noise = new Vector3(noiseX, 0, noiseZ);

        Vector3 predictedPosition = estimatedPosition  + noise;
        return predictedPosition;
    }

    private int PerformRaycastDetection()
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
        return detectedLandmarkIDs.Count;
    }

    public void ReceiveLandmarkID(int id)
    {
        if (!detectedLandmarkIDs.Contains(id))
        {
            detectedLandmarkIDs.Add(id);
        }
    }

    private async Task<Vector3> CorrectPositionWithLandmarks(Vector3 predictedPosition)
    {
        if (detectedLandmarkIDs.Count == 0)
        {
            Debug.LogWarning("No landmarks detected!");
            return predictedPosition;
        }

        List<int> landmarkIDsCopy = new List<int>(detectedLandmarkIDs);
        Vector3 weightedSum = Vector3.zero;
        float totalWeight = 0;

        foreach (int id in landmarkIDsCopy)
        {
            try
            {
                Vector3 landmarkPosition = await dbManager.GetLandmarkPositionFromDatabase(id);
                float distance = Vector3.Distance(predictedPosition, landmarkPosition);
                float weight = 1f / Mathf.Pow(distance + 0.1f, 2);
                weightedSum += landmarkPosition * weight;
                totalWeight += weight;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Errore nel recupero del landmark {id}: {ex.Message}");
            }
        }

        if (totalWeight > 0)
        {
            Vector3 correctedPosition = weightedSum / totalWeight;
            float actualDistance = Vector3.Distance(transform.position, correctedPosition);
            Debug.Log($"Estimated Position: {correctedPosition}, Actual Position: {transform.position}, Distance: {actualDistance}");
            Debug.DrawLine(transform.position, predictedPosition, Color.red, 0.1f);
            Debug.DrawLine(predictedPosition, correctedPosition, Color.green, 0.1f);
            return Vector3.Lerp(predictedPosition, correctedPosition, Mathf.Clamp(totalWeight / 10f, 0f, 1f)); // Peso variabile per casi in cui non vi sono Landmark rilevati
        }
        else
        {
            return predictedPosition;
        }
    }


    private void UpdateEstimatedPosition(Vector3 correctedPosition)
    {
        float estimatedX = positionKalmanFilterX.Update(correctedPosition.x);
        float estimatedZ = positionKalmanFilterZ.Update(correctedPosition.z);

        estimatedPosition = new Vector3(estimatedX, transform.position.y, estimatedZ);
    }

    public Vector3 GetEstimatedPosition()
    {
        return estimatedPosition;
    }
}
