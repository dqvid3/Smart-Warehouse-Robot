using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Threading.Tasks;
using Neo4j.Driver;
using System;
using System.Linq;

public class ParcelOrderController : MonoBehaviour
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

        uiDocument = GetComponent<UIDocument>();
        var root = uiDocument.rootVisualElement;

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
        productDropdown.RegisterValueChangedCallback(OnProductChanged);
        insertButton.clicked += AddToSummary;
        completeButton.clicked += CompleteOrder;

        await PopulateCategoryDropdown();
    }

    public async Task PopulateCategoryDropdown()
    {
        var categories = await neo4jHelper.GetCategoriesAsync();
        categoryDropdown.choices = categories;
        if (categories.Any())
        {
            categoryDropdown.value = categories.First();
            await PopulateProductDropdown(categoryDropdown.value);
        }
    }

    private async void OnCategoryChanged(ChangeEvent<string> evt)
    {
        await PopulateProductDropdown(evt.newValue);
        productDropdown.SetEnabled(productDropdown.choices.Any());
        productDropdown.value = productDropdown.choices.FirstOrDefault() ?? "No Products";
    }

    private async Task PopulateProductDropdown(string category)
    {
        var products = await neo4jHelper.ExecuteReadListAsync(@"
        MATCH (s:Shelf {category: $category})-[:HAS_LAYER]->(l)-[:HAS_SLOT]->(slot)
        MATCH (slot)-[:CONTAINS]->(parcel:Parcel)
        WHERE NOT (parcel)-[:PART_OF]->(:Order) 
        RETURN DISTINCT parcel.product_name as product_name",
            new Dictionary<string, object> { { "category", category } }
        );

        productDropdown.choices = products.Select(record => record["product_name"].As<string>()).ToList();
        productDropdown.SetEnabled(productDropdown.choices.Any());
        insertButton.SetEnabled(productDropdown.choices.Any());
        
        if (productDropdown.choices.Any())
        {
            productDropdown.value = productDropdown.choices.First();
            await UpdateQuantityDropdown(productDropdown.value);
            quantityDropdown.SetEnabled(true);
        }
        else
        {
            productDropdown.value = "No Products";
            quantityDropdown.choices.Clear();
            quantityDropdown.value = string.Empty;
            quantityDropdown.SetEnabled(false);
        }
    }

    private async Task UpdateQuantityDropdown(string productName)
    {
        var result = await neo4jHelper.ExecuteReadListAsync(@"
    MATCH (parcel:Parcel {product_name: $productName})<-[:CONTAINS]-(slot:Slot)
    WHERE NOT (parcel)-[:PART_OF]->(:Order)
    RETURN count(parcel) AS quantity",
            new Dictionary<string, object> { { "productName", productName } }
        );

        if (result.Any())
        {
            int availableQuantity = result.First()["quantity"].As<int>();

            // Sottrarre la quantità già aggiunta al riepilogo
            if (supplySummary.TryGetValue(productName, out int alreadyAddedQuantity))
            {
                availableQuantity -= alreadyAddedQuantity;
            }

            // Aggiorna il dropdown della quantità
            quantityDropdown.choices = availableQuantity > 0
                ? Enumerable.Range(1, availableQuantity).Select(i => i.ToString()).ToList()
                : new List<string>();
            quantityDropdown.SetEnabled(availableQuantity > 0);
            quantityDropdown.value = availableQuantity > 0 ? quantityDropdown.choices.FirstOrDefault() : string.Empty;
        }
        else
        {
            quantityDropdown.choices.Clear();
            quantityDropdown.value = string.Empty;
            quantityDropdown.SetEnabled(false);
        }
    }

    private void OnProductChanged(ChangeEvent<string> evt)
    {
        _ = UpdateQuantityDropdown(evt.newValue);
    }

    private void AddToSummary()
    {
        if (string.IsNullOrEmpty(productDropdown.value))
        {
            ShowNotification("Please select a product.");
            return;
        }

        int quantity = int.Parse(quantityDropdown.value);
        int maxQty = int.Parse(quantityDropdown.choices.Last());

        supplySummary[productDropdown.value] = supplySummary.TryGetValue(productDropdown.value, out int existingQuantity)
            ? existingQuantity + quantity
            : quantity;

        UpdateQuantity(maxQty - quantity);
        insertButton.SetEnabled(maxQty - quantity > 0);
        UpdateSummaryList();
    }

    private void UpdateQuantity(int qty)
    {
        quantityDropdown.choices = Enumerable.Range(1, qty).Select(i => i.ToString()).ToList();
        quantityDropdown.SetEnabled(qty > 0);
        quantityDropdown.value = qty > 0 ? quantityDropdown.choices.FirstOrDefault() : string.Empty;
    }

    private void UpdateSummaryList()
    {
        summaryListContainer.Clear();
        foreach (var item in supplySummary)
        {
            var rowContainer = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            var itemLabel = new Label($"{item.Key}: {item.Value}") { style = { flexGrow = 1 } };
            var removeButton = new Button(() => RemoveFromSummary(item.Key)) { text = "Rimuovi" };
            rowContainer.Add(itemLabel);
            rowContainer.Add(removeButton);
            summaryListContainer.Add(rowContainer);
        }
    }

    private async void RemoveFromSummary(string productName)
    {
        // Rimuovi il prodotto dal riepilogo
        supplySummary.Remove(productName);
        UpdateSummaryList();

        // Se il prodotto rimosso è quello attualmente selezionato nel productDropdown
        if (productDropdown.value == productName)
        {
            // Aggiorna il dropdown della quantità per il prodotto selezionato
            await UpdateQuantityDropdown(productName);

            // Se il productDropdown è vuoto (nessun prodotto disponibile nella categoria attuale)
            if (!productDropdown.choices.Any())
            {
                // Disabilita il quantityDropdown e rendilo vuoto
                quantityDropdown.choices.Clear();
                quantityDropdown.value = string.Empty;
                quantityDropdown.SetEnabled(false);
            }
        }

        // Abilita il pulsante "Inserisci" solo se ci sono prodotti disponibili
        insertButton.SetEnabled(productDropdown.choices.Any());
    }

    private async void CompleteOrder()
    {
        if (!supplySummary.Any())
        {
            ShowNotification("No items in the supply list.");
            return;
        }

        try
        {
            // Creare un nodo Order
            string orderId = Guid.NewGuid().ToString(); // Genera un ID univoco per l'ordine
            string orderTimestamp = DateTime.Now.ToString("yyyyMMddHHmmssfff");

            await neo4jHelper.ExecuteWriteAsync(@"
            CREATE (o:Order {orderId: $orderId, timestamp: $orderTimestamp})",
                new Dictionary<string, object>
                {
                { "orderId", orderId },
                { "orderTimestamp", orderTimestamp }
                }
            );

            foreach (var item in supplySummary)
            {
                string productName = item.Key;
                int quantity = item.Value;

                // 2. Trovare i parcel corrispondenti e collegarli all'ordine
                await neo4jHelper.ExecuteWriteAsync(@"
                MATCH (o:Order {orderId: $orderId})
                MATCH (parcel:Parcel)<-[:CONTAINS]-(slot:Slot)
                WHERE parcel.product_name = $productName
                WITH o, parcel
                LIMIT $quantity
                WITH o, collect(parcel) as parcels
                UNWIND parcels as parcel
                MERGE (parcel)-[:PART_OF]->(o)",
                    new Dictionary<string, object>
                    {
                    { "orderId", orderId },
                    { "productName", productName },
                    { "quantity", quantity }
                    }
                );
            }

            ShowNotification("Supply completed and order created successfully!");
            supplySummary.Clear();
            UpdateSummaryList();
            await UpdateQuantityDropdown(productDropdown.value);
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

    private void HideNotification()
    {
        notificationLabel.style.display = DisplayStyle.None;
    }

    private void OnDestroy()
    {
        neo4jHelper?.CloseConnection();
    }
}