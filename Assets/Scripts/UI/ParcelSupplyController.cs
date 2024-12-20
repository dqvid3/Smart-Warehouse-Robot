using UnityEngine;
using UnityEngine.UIElements;
using Neo4j.Driver;
using System;
using System.Threading.Tasks;

public class SupplyManagementController : MonoBehaviour
{
    private DropdownField categoryDropdown;
    private DropdownField quantityDropdown;
    private DropdownField productDropdown;
    private TextField infoBox;
    private Button insertButton;
    private Button completeButton;
    private Label notificationLabel;
    private IDriver driver;
    private UIDocument uiDocument;
    private bool isUIVisible = false;

    private async void Start()
    {
        driver = GraphDatabase.Driver("bolt://localhost:7687", AuthTokens.Basic("neo4j", "password"));

        var root = (uiDocument = GetComponent<UIDocument>()).rootVisualElement;
        categoryDropdown = root.Q<DropdownField>("categoryDropdown");
        quantityDropdown = root.Q<DropdownField>("quantityDropdown");
        productDropdown = root.Q<DropdownField>("productDropdown");
        infoBox = root.Q<TextField>("infoBox");
        insertButton = root.Q<Button>("insertButton");
        completeButton = root.Q<Button>("completeButton");
        notificationLabel = root.Q<Label>("notificationLabel");

        notificationLabel.style.display = DisplayStyle.None;
        insertButton.clicked += InsertSupply;
        completeButton.clicked += async () => await CompleteSupply();

        await PopulateProductDropdown();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
            isUIVisible = !isUIVisible;
        uiDocument.rootVisualElement.style.display = isUIVisible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private async Task PopulateProductDropdown()
    {
        try
        {
            await using var session = driver.AsyncSession();
            var products = await session.ExecuteReadAsync(tx =>
                tx.RunAsync("MATCH (p:Product) RETURN DISTINCT p.name AS name")
                  .Result.ToListAsync(record => record["name"].As<string>()));

            productDropdown.choices = products;
            if (products.Count > 0) productDropdown.value = products[0];
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error populating product dropdown: {ex.Message}");
        }
    }

    private void InsertSupply()
    {
        string category = categoryDropdown.value;
        string quantity = quantityDropdown.value;
        string product = productDropdown.value;

        if (string.IsNullOrEmpty(category) || string.IsNullOrEmpty(quantity) || string.IsNullOrEmpty(product))
        {
            ShowNotification("Please fill in all fields.");
            return;
        }

        string entry = $"Product: {product}, Category: {category}, Quantity: {quantity}\n";
        infoBox.value += entry;
    }

    private async Task CompleteSupply()
    {
        string[] entries = infoBox.value.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

        if (entries.Length == 0)
        {
            ShowNotification("No entries to complete.");
            return;
        }

        try
        {
            await using var session = driver.AsyncSession();
            await session.ExecuteWriteAsync(tx =>
            {
                foreach (var entry in entries)
                {
                    var parts = entry.Split(',');
                    if (parts.Length != 3) continue;

                    string product = parts[0].Split(':')[1].Trim();
                    string category = parts[1].Split(':')[1].Trim();
                    int quantity = int.Parse(parts[2].Split(':')[1].Trim());

                    tx.RunAsync("MERGE (p:Product {name: $product}) SET p.category = $category SET p.quantity = coalesce(p.quantity, 0) + $quantity",
                        new { product, category, quantity });
                }
                return Task.CompletedTask;
            });

            ShowNotification("Supply successfully completed!");
            infoBox.value = string.Empty;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error completing supply: {ex.Message}");
            ShowNotification("An error occurred while completing the supply.");
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
