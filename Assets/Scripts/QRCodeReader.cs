using UnityEngine;
using ZXing;

public class QRCodeReader : MonoBehaviour
{
    public Camera qrCamera; // La Camera che cattura il QR code
    public RenderTexture renderTexture; // La RenderTexture associata alla Camera

    void Update()
    {
        // Crea una texture 2D temporanea dalla RenderTexture
        RenderTexture.active = renderTexture;
        Texture2D cameraTexture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);
        cameraTexture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        cameraTexture.Apply();
        RenderTexture.active = null;

        // Decodifica il QR code usando ZXing
        IBarcodeReader barcodeReader = new BarcodeReader();
        var result = barcodeReader.Decode(cameraTexture.GetPixels32(), cameraTexture.width, cameraTexture.height);

        if (result != null)
        {
            Debug.Log("QR Code Detected: " + result.Text);
            // Qui puoi aggiungere logica per classificare la categoria
        }

        // Rilascia la texture per evitare memory leak
        Destroy(cameraTexture);
    }
}