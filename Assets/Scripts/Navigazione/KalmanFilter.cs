public class KalmanFilter
{
    private float estimate; // Stima della posizione (o distanza)
    private float variance; // Varianza della stima
    private float processNoise; // Rumore di processo
    private float measurementNoise; // Rumore di misura

    public KalmanFilter(float initialEstimate, float initialVariance, float processNoise, float measurementNoise)
    {
        estimate = initialEstimate;
        variance = initialVariance;
        this.processNoise = processNoise;
        this.measurementNoise = measurementNoise;
    }

    public float Update(float measurement)
    {
        // Predizione
        variance += processNoise;

        // Calcolo del guadagno di Kalman
        float kalmanGain = variance / (variance + measurementNoise);

        // Correzione
        estimate += kalmanGain * (measurement - estimate);
        variance *= (1 - kalmanGain);

        return estimate;
    }
}
