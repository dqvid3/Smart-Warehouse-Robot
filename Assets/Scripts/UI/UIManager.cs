using UnityEngine;
using UnityEngine.UIElements;

public class UIManager : MonoBehaviour
{
    public UIDocument productCreationUI; // UI gestita da ProductCreationController (tasto 1)
    public UIDocument parcelSupplyUI; // UI gestita da ParcelSupplyController (tasto 2)
    public UIDocument parcelOrderUI; // UI gestita da OrderManagementController (tasto 3)
    private int currentUI = 0; // 0 = nessuna UI, 1 = UI 1, 2 = UI 2, ecc...
    
    private void Start()
    {
        HideAllUI();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1) && currentUI != 1)
        {
            currentUI = 1;
            HideAllUI();
            productCreationUI.rootVisualElement.style.display = DisplayStyle.Flex;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2) && currentUI != 2)
        {
            currentUI = 2;
            HideAllUI();
            parcelSupplyUI.rootVisualElement.style.display = DisplayStyle.Flex;
            _ = parcelSupplyUI.GetComponent<ParcelSupplyController>().PopulateCategoryDropdown();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3) && currentUI != 3) // Usa un numero diverso per ogni UI
        {
            currentUI = 3;
            HideAllUI();
            parcelOrderUI.rootVisualElement.style.display = DisplayStyle.Flex;
            _ = parcelOrderUI.GetComponent<ParcelOrderController>().PopulateCategoryDropdown();
        }
        else if (Input.GetKeyDown(KeyCode.Escape))
        {
            currentUI = 0;
            HideAllUI();
        }
    }

    private void HideAllUI()
    {
        productCreationUI.rootVisualElement.style.display = DisplayStyle.None;
        parcelSupplyUI.rootVisualElement.style.display = DisplayStyle.None;
        parcelOrderUI.rootVisualElement.style.display = DisplayStyle.None;
    }
}