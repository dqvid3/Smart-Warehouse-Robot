using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Threading.Tasks;
using Neo4j.Driver;
using System;

public class ParcelSupplyController : MonoBehaviour
{
    private DeliveryAreaManager deliveryAreaManager;
    private DropdownField categoryDropdown, quantityDropdown, productDropdown;
    private Button insertButton, completeButton;
    private Label notificationLabel;
    private ScrollView summaryList;
    private Neo4jHelper neo4jHelper;
    private UIDocument uiDocument;
    private bool isUIVisible = false;

    private Dictionary<string, int> supplySummary = new();

    private async void Start()
    {
        deliveryAreaManager = FindFirstObjectByType<DeliveryAreaManager>();
        neo4jHelper = new Neo4jHelper("bolt://localhost:7687", "neo4j", "password");

        var root = (uiDocument = GetComponent<UIDocument>()).rootVisualElement;
        categoryDropdown = root.Q<DropdownField>("categoryDropdown");
        quantityDropdown = root.Q<DropdownField>("quantityDropdown");
        productDropdown = root.Q<DropdownField>("productDropdown");
        insertButton = root.Q<Button>("insertButton");
        completeButton = root.Q<Button>("completeButton");
        notificationLabel = root.Q<Label>("notificationLabel");
        summaryList = root.Q<ScrollView>("summaryList");
        notificationLabel.style.display = DisplayStyle.None;

        categoryDropdown.RegisterValueChangedCallback(OnCategoryChanged);
        insertButton.clicked += AddToSummary;
        completeButton.clicked += async () =>
        {
            List<Dictionary<string, string>> newParcels = await CompleteSupply();
            if (newParcels.Count > 0)
            {
                deliveryAreaManager.SpawnNewParcels(newParcels);
            }
        };

        await PopulateCategoryDropdown();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha2) && !isUIVisible)
            isUIVisible = true;
        if (Input.GetKeyDown(KeyCode.Escape))
            isUIVisible = false;
        uiDocument.rootVisualElement.style.display = isUIVisible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private async Task PopulateCategoryDropdown()
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
        await UpdateQuantityDropdown(categoryDropdown.value);
        if (productDropdown.choices.Count > 0)
        {
            insertButton.SetEnabled(true);
            productDropdown.SetEnabled(true);
            productDropdown.value = productDropdown.choices[0];
        }
        else
        {
            insertButton.SetEnabled(false);
            productDropdown.SetEnabled(false);
            productDropdown.value = "Prodotti non esistenti";
        }
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
            WHERE NOT (slot)-[:CONTAINS]->() AND NOT (slot)-[:INTENDED_FOR]->()
            RETURN count(slot)";
        var result = await neo4jHelper.ExecuteReadListAsync(query, new Dictionary<string, object> { { "category", category } });
        int availableSlots = result[0][0].As<int>();
        UpdateQuantity(availableSlots);
    }

    private void UpdateQuantity(int qty)
    {
        List<string> quantities = new();
        for (int i = 1; i <= qty; i++)
        {
            quantities.Add(i.ToString());
        }
        quantityDropdown.choices = quantities;
        quantityDropdown.value = quantities.Count > 0 ? quantities[0] : "";
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
            insertButton.SetEnabled(false);

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
        summaryList.Clear();
        foreach (var item in supplySummary)
        {
            summaryList.Add(new Label($"{item.Key}: {item.Value}"));
        }
    }

    private async Task<List<Dictionary<string, string>>> CompleteSupply()
    {
        List<Dictionary<string, string>> newParcels = new List<Dictionary<string, string>>();
        if (supplySummary.Count == 0)
        {
            ShowNotification("No items in the supply list.");
            return newParcels;
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
                    // Generate unique timestamp
                    string timestamp = DateTime.Now.ToString("yyyyMMddHHmmssfff");

                    var parameters = new Dictionary<string, object>
                    {
                        { "productName", productName },
                        { "category", category },
                        { "timestamp", timestamp}
                    };

                    // Find an empty slot for the category
                    var result = await neo4jHelper.ExecuteReadListAsync(
                        @"MATCH (shelf:Shelf {category: $category})-[:HAS_LAYER]->(layer:Layer)-[:HAS_SLOT]->(slot:Slot)
                        WHERE NOT (slot)-[:CONTAINS]->() AND NOT (slot)-[:INTENDED_FOR]->()
                        RETURN shelf, layer, slot
                        LIMIT 1", parameters);

                    if (result.Count > 0)
                    {
                        var record = result[0];
                        var shelfNode = record["shelf"].As<INode>();
                        var layerNode = record["layer"].As<INode>();
                        var slotNode = record["slot"].As<INode>();

                        string shelfId = shelfNode.ElementId;
                        string layerId = layerNode.ElementId;
                        string slotId = slotNode.ElementId;

                        // Create the parcel, link it to the slot and to the Delivery Area
                        await neo4jHelper.ExecuteWriteAsync(
                            @"MATCH (p:Product {product_name: $productName})
                            MATCH (shelf:Shelf), (layer:Layer), (slot:Slot)
                            WHERE elementId(shelf) = $shelfId AND elementId(layer) = $layerId AND elementId(slot) = $slotId
                            MATCH (da:Area {type: 'Delivery'})
                            CREATE (parcel:Parcel {product_name: p.product_name, category: p.category, timestamp: $timestamp})
                            CREATE (parcel)-[:LOCATED_IN]->(da)
                            CREATE (slot)-[:INTENDED_FOR]->(parcel)",
                        new Dictionary<string, object>
                        {
                            { "productName", productName },
                            { "shelfId", shelfId },
                            { "layerId", layerId },
                            { "slotId", slotId },
                            { "timestamp", timestamp}
                        });
                        newParcels.Add(new Dictionary<string, string>
                        {
                            { "timestamp", timestamp },
                            { "category", category },
                            { "productName", productName }
                        });
                    }
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
        return newParcels;
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