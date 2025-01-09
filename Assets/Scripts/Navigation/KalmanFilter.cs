using System;

public class KalmanFilter
{
    private float stateEstimate;   // x_k|k: Stima corrente dello stato
    private float estimationError; // P_k|k: Errore di stima corrente
    private float processNoise;    // Q: Rumore di processo
    private float measurementNoise; // R: Rumore di misura
    private float kalmanGain;      // K_k: Guadagno di Kalman

    public KalmanFilter(float initialEstimate, float initialError, float processNoise, float measurementNoise)
    {
        stateEstimate = initialEstimate;  // Valore iniziale dello stato
        estimationError = initialError;   // Errore iniziale della stima
        this.processNoise = processNoise; // Rumore del processo (Q)
        this.measurementNoise = measurementNoise; // Rumore di misura (R)
    }

    public float Update(float measurement)
    {
        // 1. Predizione
        float predictedEstimationError = estimationError + processNoise;

        // 2. Calcolo del guadagno di Kalman
        kalmanGain = predictedEstimationError / (predictedEstimationError + measurementNoise);

        // 3. Aggiornamento dello stato stimato
        stateEstimate = stateEstimate + kalmanGain * (measurement - stateEstimate);

        // 4. Aggiornamento dell'errore di stima
        estimationError = (1 - kalmanGain) * predictedEstimationError;

        return stateEstimate;
    }
}
