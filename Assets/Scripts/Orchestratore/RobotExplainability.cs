using UnityEngine;
using TMPro;

public class RobotExplainability : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI explanationText; 
    [SerializeField] private Canvas explanationCanvas;  

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
    }

    public void ToggleExplanation(bool show)
    {
        if (explanationCanvas != null)
        {
            explanationCanvas.gameObject.SetActive(show);
        }
    }
}
