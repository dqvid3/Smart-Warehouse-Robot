using UnityEngine;
using ZXing;

public class QRCodeReader : MonoBehaviour
{
    public Camera qrCamera; // La Camera che cattura il QR code
    public RenderTexture renderTexture; // La RenderTexture associata alla Camera

    public string ReadQRCode()
    {
        // Crea una texture 2D temporanea dalla RenderTexture
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

        return result?.Text;
    }

    private void OnGUI()
    {
        GUI.DrawTexture(new Rect(10, 10, 256, 256), renderTexture, ScaleMode.ScaleToFit);
    }
}