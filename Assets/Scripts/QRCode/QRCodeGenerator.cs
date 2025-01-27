using UnityEngine;
using ZXing;
using ZXing.QrCode;
using ZXing.Common;

public class QRCodeGenerator : MonoBehaviour
{
    public string qrCodeString = "example.com";

    void Start()
    {
        QRCodeWriter writer = new();
        BitMatrix matrix = writer.encode(qrCodeString, BarcodeFormat.QR_CODE, 256, 256);

        Texture2D qrCodeTexture = new(matrix.Width, matrix.Height);

        for (int x = 0; x < matrix.Width; x++)
        {
            for (int y = 0; y < matrix.Height; y++)
            {
                qrCodeTexture.SetPixel(x, y, matrix[x, y] ? Color.black : Color.white);
            }
        }

        qrCodeTexture.Apply();
        GetComponent<MeshRenderer>().material.mainTexture = qrCodeTexture;
    }
}