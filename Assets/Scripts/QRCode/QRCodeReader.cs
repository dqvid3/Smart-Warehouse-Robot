using UnityEngine;
using ZXing;

public class QRCodeReader : MonoBehaviour
{
    public Camera qrCamera; // La Camera che cattura il QR code
    public RenderTexture renderTexture; // La RenderTexture associata alla Camera

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

        // Cattura la scena nella RenderTexture
        RenderTexture.active = renderTexture;
        Texture2D cameraTexture = new(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);
        cameraTexture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        cameraTexture.Apply();
        RenderTexture.active = null;

        // Decodifica il QR code usando ZXing
        IBarcodeReader barcodeReader = new BarcodeReader();
        var result = barcodeReader.Decode(cameraTexture.GetPixels32(), cameraTexture.width, cameraTexture.height);

        // Rilascia la texture per evitare memory leak
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
    /*
    private void OnGUI()
    {
        if (renderTexture != null)
        {
            // Disegna la Render Texture della telecamera in posizioni diverse sullo schermo
            GUI.DrawTexture(new Rect(10, 10, 256, 256), renderTexture, ScaleMode.ScaleToFit);
        }
        else
        {
            GUI.Label(new Rect(10, 10 + (256 + 10) * gameObject.GetInstanceID(), 200, 20), "RenderTexture non assegnata!");
        }
    }*/

}
