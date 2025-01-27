using UnityEngine;
using ZXing;

public class QRCodeReader : MonoBehaviour
{
    public Camera qrCamera;
    public RenderTexture renderTexture;

    public string ReadQRCode()
    {
        // Verifica che la Camera e la RenderTexture siano assegnate
        if (qrCamera == null)
        {
            Debug.LogError("Camera non assegnata al QRCodeReader!");
            return null;
        }

        if (renderTexture == null)
        {
            Debug.LogError("RenderTexture non assegnata al QRCodeReader!");
            return null;
        }

        // Associa la RenderTexture alla Camera
        if (qrCamera.targetTexture != renderTexture)
        {
            qrCamera.targetTexture = renderTexture;
            Debug.Log($"RenderTexture {renderTexture.name} assegnata alla Camera {qrCamera.name}");
        }

        RenderTexture.active = renderTexture;
        Texture2D cameraTexture = new(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);
        cameraTexture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        cameraTexture.Apply();
        RenderTexture.active = null;

        // Decodifica il QR code usando ZXing
        IBarcodeReader barcodeReader = new BarcodeReader();
        var result = barcodeReader.Decode(cameraTexture.GetPixels32(), cameraTexture.width, cameraTexture.height);

        Destroy(cameraTexture);

        // Restituisci il risultato della decodifica
        if (result != null && !string.IsNullOrEmpty(result.Text))
        {
            return result.Text;
        }
        else
        {
            Debug.LogWarning("Nessun QR Code rilevato!");
            return null;
        }
    }
}
