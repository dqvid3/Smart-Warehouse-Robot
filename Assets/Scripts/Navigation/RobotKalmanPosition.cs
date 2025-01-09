using UnityEngine;

public class RobotKalmanPosition : MonoBehaviour
{
    [Header("Kalman Filter Parameters")]
    public float processNoise = 0.1f;
    public float measurementNoise = 0.5f;

    [Header("Sensore")]
    public float sensorRange = 15f; // Raggio massimo di rilevamento dei sensori
    public LayerMask landmarkLayer; // Layer dei landmark nella scena

    private KalmanFilter positionKalmanFilterX;
    private KalmanFilter positionKalmanFilterZ;
    private Vector3 estimatedPosition;

    public void Start()
    {
        InitializeKalmanFilters();
    }

    public void Update()
    {
        Vector3 noisyPosition = CalculateNoisyPositionFromLandmarks();
        Debug.Log(noisyPosition);
        Debug.Log(estimatedPosition);
        UpdateEstimatedPosition(noisyPosition);
    }

    private void InitializeKalmanFilters()
    {
        positionKalmanFilterX = new KalmanFilter(0f, 1f, processNoise, measurementNoise);
        positionKalmanFilterZ = new KalmanFilter(0f, 1f, processNoise, measurementNoise);
    }

    private Vector3 CalculateNoisyPositionFromLandmarks()
    {
        Collider[] landmarks = Physics.OverlapSphere(transform.position, sensorRange, landmarkLayer);
        if (landmarks.Length == 0)
        {
            Debug.LogWarning("No landmarks detected!");
            return transform.position; // Usa la posizione corrente se non ci sono landmark
        }

        Vector3 averagePosition = Vector3.zero;
        foreach (Collider landmark in landmarks)
        {
            averagePosition += landmark.transform.position;
        }

        averagePosition /= landmarks.Length;

        // Aggiungi rumore alla posizione media rilevata
        float noiseX = Random.Range(-0.2f, 0.2f);
        float noiseZ = Random.Range(-0.2f, 0.2f);
        return new Vector3(averagePosition.x + noiseX, transform.position.y, averagePosition.z + noiseZ);
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
