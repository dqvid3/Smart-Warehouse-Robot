using UnityEngine;
using TMPro;

public class RobotExplainability : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI explanationText; // Campo per il testo
    [SerializeField] private Canvas explanationCanvas;       // Canvas per mostrare le spiegazioni
    public float messageDisplayTime = 8f;                   // Durata del messaggio

    private void Start()
    {
        if (explanationCanvas != null)
        {
            explanationCanvas.gameObject.SetActive(false);   // Nascondi il Canvas inizialmente
        }
    }

    public void ShowExplanation(string message)
    {
        if (explanationText != null)
        {
            explanationText.text = message;
        }

        if (explanationCanvas != null)
        {
            explanationCanvas.gameObject.SetActive(true);
            CancelInvoke(nameof(HideExplanation));
            Invoke(nameof(HideExplanation), messageDisplayTime);
        }
    }


    // Metodo per nascondere il canvas
    private void HideExplanation()
    {
        if (explanationCanvas != null)
        {
            explanationCanvas.gameObject.SetActive(false);
        }
    }
}
