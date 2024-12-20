using UnityEngine;
using UnityEngine.UIElements;
using Neo4j.Driver;
using System;
using System.Threading.Tasks;

public class ParcelCreationController : MonoBehaviour
{
    private TextField productNameField, qrcodeField;
    private DropdownField categoryDropdown;
    private Button generateQRButton, submitButton;
    private Label notificationLabel;
    private IDriver driver;
    private UIDocument uiDocument;
    private bool isUIVisible = true;

    private async void Start()
    {
        driver = GraphDatabase.Driver("bolt://localhost:7687", AuthTokens.Basic("neo4j", "password"));

        var root = (uiDocument = GetComponent<UIDocument>()).rootVisualElement;
        productNameField = root.Q<TextField>("productNameField");
        categoryDropdown = root.Q<DropdownField>("categoryDropdown");
        qrcodeField = root.Q<TextField>("qrcodeField");
        generateQRButton = root.Q<Button>("generateQRButton");
        submitButton = root.Q<Button>("submitButton");
        notificationLabel = root.Q<Label>("notificationLabel");
        notificationLabel.style.display = DisplayStyle.None;

        generateQRButton.clicked += () => qrcodeField.value = Guid.NewGuid().ToString();
        submitButton.clicked += async () => await SubmitParcel();

        await PopulateCategoryDropdown();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
            uiDocument.rootVisualElement.style.display = (isUIVisible = !isUIVisible) ? DisplayStyle.Flex : DisplayStyle.None;
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
        string productName = productNameField.value, category = categoryDropdown.value, qrcode = qrcodeField.value;

        if (string.IsNullOrEmpty(productName) || string.IsNullOrEmpty(category) || string.IsNullOrEmpty(qrcode))
        {
            ShowNotification("Please fill in all fields and select a category.");
            return;
        }

        try
        {
            await using var session = driver.AsyncSession();
            var exists = (await session.ExecuteReadAsync(tx =>
                tx.RunAsync("MATCH (p:Parcel {qrcode: $qrcode}) RETURN p", new { qrcode })
                  .Result.ToListAsync())).Count > 0;

            if (exists)
            {
                ShowNotification($"A parcel with QR Code {qrcode} already exists!");
                return;
            }

            await session.ExecuteWriteAsync(tx =>
                tx.RunAsync("CREATE (p:Parcel {product_name: $productName, qrcode: $qrcode, category: $category})",
                new { productName, category, qrcode }));

            ShowNotification("Parcel successfully created!");
            productNameField.value = qrcodeField.value = "";
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

    private void HideNotification()
    {
        notificationLabel.style.display = DisplayStyle.None;
    }

    private void OnDestroy() => driver?.Dispose();
}