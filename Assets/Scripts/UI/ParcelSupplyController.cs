using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Threading.Tasks;
using Neo4j.Driver;
using System;

public class ParcelSupplyController : MonoBehaviour
{
    private DropdownField categoryDropdown, quantityDropdown, productDropdown;
    private Button insertButton, completeButton;
    private Label notificationLabel;
    private ScrollView summaryList;
    private VisualElement summaryListContainer;
    private Neo4jHelper neo4jHelper;
    private UIDocument uiDocument;
    private Dictionary<string, int> supplySummary = new();

    private async void Start()
    {
        neo4jHelper = new Neo4jHelper("bolt://localhost:7687", "neo4j", "password");

        var root = (uiDocument = GetComponent<UIDocument>()).rootVisualElement;
        categoryDropdown = root.Q<DropdownField>("categoryDropdown");
        quantityDropdown = root.Q<DropdownField>("quantityDropdown");
        productDropdown = root.Q<DropdownField>("productDropdown");
        insertButton = root.Q<Button>("insertButton");
        completeButton = root.Q<Button>("completeButton");
        notificationLabel = root.Q<Label>("notificationLabel");
        summaryList = root.Q<ScrollView>("summaryList");
        summaryListContainer = root.Q<VisualElement>("summaryListContainer");
        notificationLabel.style.display = DisplayStyle.None;

        categoryDropdown.RegisterValueChangedCallback(OnCategoryChanged);
        insertButton.clicked += AddToSummary;
        completeButton.clicked += CompleteSupply;

        await PopulateCategoryDropdown();
    }

    public async Task PopulateCategoryDropdown()
    {
        var categories = await neo4jHelper.GetCategoriesAsync();
        categoryDropdown.choices = categories;
        if (categories.Count > 0)
        {
            categoryDropdown.value = categories[0];
            await PopulateProductDropdown(categoryDropdown.value);
            await UpdateQuantityDropdown(categoryDropdown.value);
        }
    }

    private async void OnCategoryChanged(ChangeEvent<string> evt)
    {
        string selectedCategory = evt.newValue;
        await PopulateProductDropdown(selectedCategory);
        await UpdateQuantityDropdown(selectedCategory);

        // Verifica se ci sono prodotti o quantità disponibili
        bool hasProducts = productDropdown.choices.Count > 0;
        bool hasAvailableQuantity = quantityDropdown.choices.Count > 0;

        // Abilita/disabilita il pulsante di inserimento in base alla disponibilità
        insertButton.SetEnabled(hasProducts && hasAvailableQuantity);
        productDropdown.SetEnabled(hasProducts);
        productDropdown.value = hasProducts ? productDropdown.choices[0] : "No Products";
    }

    private async Task PopulateProductDropdown(string category)
    {
        var products = await neo4jHelper.GetProductsByCategoryAsync(category);
        productDropdown.choices = products;
    }

    private async Task UpdateQuantityDropdown(string category)
    {
        string query = @"
            MATCH (shelf:Shelf {category: $category})-[:HAS_LAYER]->(layer:Layer)-[:HAS_SLOT]->(slot:Slot)
            WHERE NOT (slot)-[:CONTAINS]->()
            WITH count(slot) AS emptySlots
            OPTIONAL MATCH (p:Parcel)-[:LOCATED_IN]->(delivery:Area {type: 'Delivery'})
            WHERE p.category = $category
            WITH emptySlots, count(p) AS numParcelsInDelivery
            RETURN emptySlots - numParcelsInDelivery AS availableSlots";
        var result = await neo4jHelper.ExecuteReadListAsync(query, new Dictionary<string, object> { { "category", category } });
        int availableSlots = result[0][0].As<int>();

        foreach (var item in supplySummary)
        {
            string productName = item.Key;
            int quantity = item.Value;
            string productCategory = await GetProductCategory(productName);

            if (productCategory == category)
            {
                availableSlots -= quantity;
            }
        }

        UpdateQuantity(availableSlots);
    }

    private async Task<string> GetProductCategory(string productName)
    {
        string query = @"MATCH (p:Product {product_name: $productName}) RETURN p.category AS category";
        var result = await neo4jHelper.ExecuteReadListAsync(query, new Dictionary<string, object> { { "productName", productName } });
        return result.Count > 0 ? result[0][0].As<string>() : null;
    }

    private void UpdateQuantity(int qty)
    {
        List<string> quantities = new();
        for (int i = 1; i <= qty; i++)
        {
            quantities.Add(i.ToString());
        }
        quantityDropdown.choices = quantities;

        if (qty == 0)
        {
            quantityDropdown.SetEnabled(false);
            quantityDropdown.value = "";
            insertButton.SetEnabled(false); // Disabilita il pulsante se non ci sono quantità disponibili
        }
        else
        {
            quantityDropdown.SetEnabled(true);
            quantityDropdown.value = quantities.Count > 0 ? quantities[0] : "";
            insertButton.SetEnabled(true); // Abilita il pulsante se ci sono quantità disponibili
        }
    }

    private void AddToSummary()
    {
        string selectedProduct = productDropdown.value;
        if (string.IsNullOrEmpty(selectedProduct))
        {
            ShowNotification("Please select a product.");
            return;
        }
        int quantity = int.Parse(quantityDropdown.value);
        int maxqty = int.Parse(quantityDropdown.choices[quantityDropdown.choices.Count - 1]);
        if (maxqty - quantity == 0)
            insertButton.SetEnabled(false); // Disabilita il pulsante se non ci sono più quantità disponibili

        if (supplySummary.ContainsKey(selectedProduct))
        {
            supplySummary[selectedProduct] += quantity;
        }
        else
        {
            supplySummary.Add(selectedProduct, quantity);
        }
        UpdateQuantity(maxqty - quantity);
        UpdateSummaryList();
    }

    private void UpdateSummaryList()
    {
        summaryListContainer.Clear();
        foreach (var item in supplySummary)
        {
            var rowContainer = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            var itemLabel = new Label($"{item.Key}: {item.Value}") { style = { flexGrow = 1 } };
            var removeButton = new Button(() => RemoveFromSummary(item.Key, item.Value)) { text = "Rimuovi" };
            rowContainer.Add(itemLabel);
            rowContainer.Add(removeButton);
            summaryListContainer.Add(rowContainer);
        }
    }

    private async void RemoveFromSummary(string productName, int quantityToRemove)
    {
        supplySummary.Remove(productName);
        UpdateSummaryList();
        await UpdateQuantityDropdown(categoryDropdown.value);

        // Verifica se ci sono prodotti o quantità disponibili
        bool hasProducts = productDropdown.choices.Count > 0;
        bool hasAvailableQuantity = quantityDropdown.choices.Count > 0;

        // Abilita/disabilita il pulsante di inserimento in base alla disponibilità
        insertButton.SetEnabled(hasProducts && hasAvailableQuantity);
    }

    private async void CompleteSupply()
    {
        if (supplySummary.Count == 0)
        {
            ShowNotification("No items in the supply list.");
            return;
        }

        try
        {
            foreach (var item in supplySummary)
            {
                string productName = item.Key;
                int quantity = item.Value;
                string category = categoryDropdown.value;

                for (int i = 0; i < quantity; i++)
                {
                    string timestamp = DateTime.Now.ToString("yyyyMMddHHmmssfff");
                    await neo4jHelper.ExecuteWriteAsync(
                        @"MATCH (p:Product {product_name: $productName})
                        MATCH (da:Area {type: 'Delivery'})
                        CREATE (parcel:Parcel {product_name: p.product_name, category: p.category, timestamp: $timestamp})
                        CREATE (parcel)-[:LOCATED_IN]->(da)",
                        new Dictionary<string, object>
                        {
                            { "productName", productName },
                            { "timestamp", timestamp }
                        });
                }
            }
            ShowNotification("Supply completed successfully!");
            supplySummary.Clear();
            UpdateSummaryList();
            await UpdateQuantityDropdown(categoryDropdown.value);
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