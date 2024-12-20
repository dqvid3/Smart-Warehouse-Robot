using UnityEngine;
using UnityEngine.UIElements;
using Neo4j.Driver;
using System;
using System.Threading.Tasks;

public class ProductCreationController : MonoBehaviour
{
    private TextField productNameField;
    private DropdownField categoryDropdown;
    private Button generateQRButton, submitButton;
    private Label notificationLabel;
    private IDriver driver;
    private UIDocument uiDocument;
    private bool isUIVisible = false;

    private async void Start()
    {
        driver = GraphDatabase.Driver("bolt://localhost:7687", AuthTokens.Basic("neo4j", "password"));

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
        if (Input.GetKeyDown(KeyCode.T))
            isUIVisible = !isUIVisible;
        uiDocument.rootVisualElement.style.display = isUIVisible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private async Task PopulateCategoryDropdown()
    {
        try
        {
            await using var session = driver.AsyncSession();
            var categories = await session.ExecuteReadAsync(tx =>
                tx.RunAsync("MATCH (s:Shelf) RETURN DISTINCT s.category AS category")
                  .Result.ToListAsync(record => record["category"].As<string>()));
            categoryDropdown.choices = categories;
            if (categories.Count > 0) categoryDropdown.value = categories[0];
        }
        catch (Exception ex) { Debug.LogError($"Error populating dropdown: {ex.Message}"); }
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
            await using var session = driver.AsyncSession();
            var exists = (await session.ExecuteReadAsync(tx =>
                tx.RunAsync("MATCH (p:Product {productName: $productName}) RETURN p", new { productName })
                  .Result.ToListAsync())).Count > 0;

            if (exists)
            {
                ShowNotification($"A parcel with Product Name {productName} already exists!");
                return;
            }

            await session.ExecuteWriteAsync(tx =>
                tx.RunAsync("CREATE (p:Product {product_name: $productName, category: $category})",
                new { productName, category}));

            ShowNotification("Parcel successfully created!");
            productNameField.value = "";
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error creating parcel: {ex.Message}");
            ShowNotification("An error occurred while creating the parcel.");
        }
    }

    private void ShowNotification(string message)
    {
        notificationLabel.text = message;
        notificationLabel.style.display = DisplayStyle.Flex;
        Invoke(nameof(HideNotification), 3f);
    }

    private void HideNotification() => notificationLabel.style.display = DisplayStyle.None;

    private void OnDestroy() => driver?.Dispose();
}