using UnityEngine;
using ZXing;
using ZXing.QrCode;
using ZXing.Common;

public class QRCodeGenerator : MonoBehaviour
{
    public string qrCodeString = "example.com";

    void Start()
    {
        // Crea il QR code
        QRCodeWriter writer = new();
        BitMatrix matrix = writer.encode(qrCodeString, BarcodeFormat.QR_CODE, 256, 256);

        // Crea la texture del QR code
        Texture2D qrCodeTexture = new(matrix.Width, matrix.Height);

        // Disegna il QR code sulla texture
        for (int x = 0; x < matrix.Width; x++)
        {
            for (int y = 0; y < matrix.Height; y++)
            {
                qrCodeTexture.SetPixel(x, y, matrix[x, y] ? Color.black : Color.white);
            }
        }

        // Applica il materiale al Quad
        qrCodeTexture.Apply();
        GetComponent<MeshRenderer>().material.mainTexture = qrCodeTexture;
    }
}