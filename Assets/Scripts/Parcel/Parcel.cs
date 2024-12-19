using UnityEngine;
using ZXing;
using ZXing.QrCode;

public class Parcel : MonoBehaviour
{
    public string category; // Categoria del pacco
    public string qrCode;   // QR Code univoco del pacco

    public GameObject qrCodeQuad; // Riferimento al Quad che mostrerà il QR Code

    // Metodo per inizializzare il pacco
    public void Initialize(string category, string qrCode)
    {
        this.category = category;
        this.qrCode = qrCode;
        GenerateQRCode(qrCode, qrCodeQuad);
    }

    public Texture2D GenerateQRCode(string text, GameObject quad)
    {
        // Opzioni per la generazione del QR code
        var options = new QrCodeEncodingOptions
        {
            DisableECI = true,
            CharacterSet = "UTF-8",
            Width = 256,
            Height = 256
        };

        // Crea un writer per il QR code
        var writer = new BarcodeWriter<Color32[]>()
        {
            Format = BarcodeFormat.QR_CODE,
            Options = options
        };

        // Genera la matrice di bit del QR code
        var color32 = writer.Write(text);

        // Crea una texture 2D
        var texture = new Texture2D(options.Width, options.Height);

        // Imposta i pixel della texture in base alla matrice di bit
        texture.SetPixels32(color32);
        texture.Apply();

        // Applica la texture al materiale del Quad
        Renderer quadRenderer = quad.GetComponent<Renderer>();
        quadRenderer.material.mainTexture = texture;

        return texture;
    }
}