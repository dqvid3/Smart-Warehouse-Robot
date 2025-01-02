using UnityEngine;

public class RaycastSensor : MonoBehaviour
{
    public float maxDistance = 10f; // Distanza massima del sensore
    public float noiseVariance = 0.1f; // Varianza del rumore simulato
    public LayerMask detectionLayer; // Layer per filtrare gli oggetti rilevati

    public float MeasuredDistance { get; private set; } // Distanza misurata (con rumore)
    public float TrueDistance { get; private set; } // Distanza reale
    public float MeasurementVariance => noiseVariance; // Varianza del rumore

    private void Update()
    {
        SimulateSensor();
    }

    private void SimulateSensor()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position, transform.forward, out hit, maxDistance, detectionLayer))
        {
            TrueDistance = hit.distance; // Distanza reale
            MeasuredDistance = AddGaussianNoise(TrueDistance); // Distanza con rumore
        }
        else
        {
            TrueDistance = -1f;
            MeasuredDistance = -1f;
        }
    }

    private float AddGaussianNoise(float value)
    {
        float standardDeviation = Mathf.Sqrt(noiseVariance);
        float noise = GaussianRandom(0f, standardDeviation);
        return value + noise;
    }

    private float GaussianRandom(float mean, float stdDev)
    {
        float u1 = 1.0f - Random.value;
        float u2 = 1.0f - Random.value;
        float randStdNormal = Mathf.Sqrt(-2.0f * Mathf.Log(u1)) * Mathf.Sin(2.0f * Mathf.PI * u2);
        return mean + stdDev * randStdNormal;
    }
}
