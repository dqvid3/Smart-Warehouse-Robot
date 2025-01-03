using UnityEngine;

public class KalmanFilter
{
    private float estimationError; // P_k
    private float processNoise;    // Q
    private float measurementNoise; // R
    private float stateEstimate;   // x_k
    private float kalmanGain;      // K_k

    public KalmanFilter(float initialEstimate, float initialError, float processNoise, float measurementNoise)
    {
        stateEstimate = initialEstimate;  // Valore iniziale dello stato
        estimationError = initialError;   // Errore iniziale della stima
        this.processNoise = processNoise; // Rumore del processo (Q)
        this.measurementNoise = measurementNoise; // Rumore di misura (R)
    }

    public float Update(float measurement)
    {
        // 1. Calcolo del guadagno di Kalman
        kalmanGain = estimationError / (estimationError + measurementNoise);

        // 2. Aggiornamento dello stato stimato
        stateEstimate = stateEstimate + kalmanGain * (measurement - stateEstimate);

        // 3. Aggiornamento dell'errore di stima
        estimationError = (1 - kalmanGain) * estimationError + processNoise;

        return stateEstimate;
    }
}
