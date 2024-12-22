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
    private VisualElement summaryListContainer;
    private Neo4jHelper neo4jHelper;
    private UIDocument uiDocument;
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
        summaryListContainer = root.Q<VisualElement>("summaryListContainer");
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
            WHERE NOT (slot)-[:CONTAINS]->()
            WITH count(slot) AS emptySlots
            OPTIONAL MATCH (p:Parcel)-[:LOCATED_IN]->(delivery:Area {type: 'Delivery'})
            WHERE p.category = $category
            WITH emptySlots, count(p) AS numParcelsInDelivery
            RETURN emptySlots - numParcelsInDelivery AS availableSlots";
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
        summaryListContainer.Clear();
        foreach (var item in supplySummary)
        {
            // Crea un contenitore per la riga
            var rowContainer = new VisualElement();
            rowContainer.style.flexDirection = FlexDirection.Row;

            // Crea la label per il nome del prodotto e la quantità
            var itemLabel = new Label($"{item.Key}: {item.Value}");
            itemLabel.style.flexGrow = 1;

            // Crea il pulsante "Rimuovi"
            var removeButton = new Button(() => RemoveFromSummary(item.Key, item.Value));
            removeButton.text = "Rimuovi";

            // Aggiunge label e pulsante al contenitore della riga
            rowContainer.Add(itemLabel);
            rowContainer.Add(removeButton);

            // Aggiunge il contenitore della riga al contenitore principale del riepilogo
            summaryListContainer.Add(rowContainer);
        }
    }

    private async void RemoveFromSummary(string productName, int quantityToRemove)
    {
        // Rimuove l'elemento dal dizionario
        supplySummary.Remove(productName);

        // Aggiorna la lista di riepilogo
        UpdateSummaryList();

        // Aggiorna la quantità disponibile
        await UpdateQuantityDropdown(categoryDropdown.value);

        insertButton.SetEnabled(true);
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

                    // Create the parcel, link it to the the Delivery Area
                    await neo4jHelper.ExecuteWriteAsync(
                        @"MATCH (p:Product {product_name: $productName})
                        MATCH (da:Area {type: 'Delivery'})
                        CREATE (parcel:Parcel {product_name: p.product_name, category: p.category, timestamp: $timestamp})
                        CREATE (parcel)-[:LOCATED_IN]->(da)",
                    new Dictionary<string, object>
                    {
                        { "productName", productName },
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