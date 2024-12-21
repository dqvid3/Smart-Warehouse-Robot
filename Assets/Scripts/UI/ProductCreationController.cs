using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Threading.Tasks;

public class ProductCreationController : MonoBehaviour
{
    private TextField productNameField;
    private DropdownField categoryDropdown;
    private Button generateQRButton, submitButton;
    private Label notificationLabel;
    private Neo4jHelper neo4jHelper;
    private UIDocument uiDocument;
    private bool isUIVisible = false;

    private async void Start()
    {
        neo4jHelper = new Neo4jHelper("bolt://localhost:7687", "neo4j", "password");

        var root = (uiDocument = GetComponent<UIDocument>()).rootVisualElement;
        productNameField = root.Q<TextField>("productNameField");
        categoryDropdown = root.Q<DropdownField>("categoryDropdown");
        submitButton = root.Q<Button>("submitButton");
        notificationLabel = root.Q<Label>("notificationLabel");
        notificationLabel.style.display = DisplayStyle.None;

        submitButton.clicked += async () => await SubmitParcel();

        await PopulateCategoryDropdown();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1) && !isUIVisible)
        isUIVisible = true;
        if (Input.GetKeyDown(KeyCode.Escape))
            isUIVisible = false;
        uiDocument.rootVisualElement.style.display = isUIVisible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private async Task PopulateCategoryDropdown()
    {
        var categories = await neo4jHelper.GetCategoriesAsync();
        categoryDropdown.choices = categories;
        if (categories.Count > 0) categoryDropdown.value = categories[0];
    }

    private async Task SubmitParcel()
    {
        string productName = productNameField.value;
        string category = categoryDropdown.value;

        if (string.IsNullOrEmpty(productName) || string.IsNullOrEmpty(category))
        {
            ShowNotification("Please fill in all fields and select a category.");
            return;
        }

        try
        {
            var parameters = new Dictionary<string, object> { { "productName", productName } };
            var exists = (await neo4jHelper.ExecuteReadListAsync("MATCH (p:Product {product_name: $productName}) RETURN p", parameters)).Count > 0;

            if (exists)
            {
                ShowNotification($"A parcel with Product Name {productName} already exists!");
                return;
            }

            parameters.Add("category", category);
            await neo4jHelper.ExecuteWriteAsync("CREATE (p:Product {product_name: $productName, category: $category})", parameters);

            ShowNotification("Parcel successfully created!");
            productNameField.value = "";
        }
        catch (Neo4jException ex)
        {
            ShowNotification(ex.Message);
        }
    }

    private void ShowNotification(string message)
    {
        notificationLabel.text = message;
        notificationLabel.style.display = DisplayStyle.Flex;
        Invoke(nameof(HideNotification), 3f);
    }

    private void HideNotification() => notificationLabel.style.display = DisplayStyle.None;

    private void OnDestroy() => neo4jHelper?.CloseConnection();
}