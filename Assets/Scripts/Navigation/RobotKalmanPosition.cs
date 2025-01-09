using UnityEngine;

public class RobotKalmanPosition : MonoBehaviour
{
    // --- PUBLIC PROPERTIES ---
    [Header("Kalman Filter Parameters")]
    public float processNoise = 0.1f;
    public float measurementNoise = 0.5f;

    // --- PRIVATE VARIABLES ---
    private KalmanFilter positionKalmanFilterX;
    private KalmanFilter positionKalmanFilterZ;
    private Vector3 noisyPosition;
    private Vector3 estimatedPosition;

    // --- UNITY METHODS ---
    public void Start()
    {
        InitializeKalmanFilters();
    }

    public void Update()
    {
        UpdateNoisyAndEstimatedPosition();
    }

    // --- INITIALIZATION METHODS ---
    private void InitializeKalmanFilters()
    {
        positionKalmanFilterX = new KalmanFilter(
            initialEstimate: 0f,
            initialError: 1f,
            processNoise: processNoise,
            measurementNoise: measurementNoise
        );

        positionKalmanFilterZ = new KalmanFilter(
            initialEstimate: 0f,
            initialError: 1f,
            processNoise: processNoise,
            measurementNoise: measurementNoise
        );
    }

    // --- POSITION MANAGEMENT METHODS ---

    private void UpdateNoisyAndEstimatedPosition()
    {
        // Introduce rumore alla posizione attuale del robot
        noisyPosition = transform.position + new Vector3(Random.Range(-0.2f, 0.2f), 0, Random.Range(-0.2f, 0.2f));

        // Applica il filtro di Kalman per ottenere la posizione stimata
        float estimatedX = Mathf.Round(positionKalmanFilterX.Update(noisyPosition.x) * 100f) / 100f;
        float estimatedZ = Mathf.Round(positionKalmanFilterZ.Update(noisyPosition.z) * 100f) / 100f;

        estimatedPosition = new Vector3(estimatedX, transform.position.y, estimatedZ);
    }

    // --- PUBLIC METHODS ---

    public Vector3 GetEstimatedPosition()
    {
        return estimatedPosition;
    }
}
