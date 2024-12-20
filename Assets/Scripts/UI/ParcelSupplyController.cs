using UnityEngine;
using UnityEngine.UIElements;
using Neo4j.Driver;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Linq;

public class ParcelSupplyController : MonoBehaviour
{
    private IDriver driver;
    private ListView parcelListView;
    private ListView summaryListView;
    private Button supplyButton;
    private Button addButton;
    private Label notificationLabel;
    private DropdownField availableSlotsDropdown;
    private List<ParcelData> availableParcels = new();
    private List<ParcelData> supplySummary = new();
    private UIDocument uiDocument;
    private bool isUIVisible = false;

    private class ParcelData
    {
        public string ProductName { get; set; }
        public string Category { get; set; }
        public int Quantity { get; set; }
    }

    private void Start()
    {
        // **Connessione al database Neo4j**
        driver = GraphDatabase.Driver("bolt://localhost:7687", AuthTokens.Basic("neo4j", "password"));

        // **Riferimenti agli elementi UI**
        uiDocument = GetComponent<UIDocument>();
        var root = uiDocument.rootVisualElement;
        parcelListView = root.Q<ListView>("parcelListView");
        summaryListView = root.Q<ListView>("summaryListView");
        supplyButton = root.Q<Button>("supplyButton");
        addButton = root.Q<Button>("addButton");
        notificationLabel = root.Q<Label>("notificationLabel");
        availableSlotsDropdown = root.Q<DropdownField>("availableSlotsDropdown");

        // **Inizializzazione del dropdown per la quantità**
        availableSlotsDropdown.choices = Enumerable.Range(1, 10).Select(i => i.ToString()).ToList();
        availableSlotsDropdown.value = "1";

        // **Nascondi la notifica all'avvio**
        notificationLabel.style.display = DisplayStyle.None;

        // **Configurazione della ListView dei pacchi disponibili**
        parcelListView.makeItem = () =>
        {
            var newItem = new VisualElement();
            newItem.style.flexDirection = FlexDirection.Row;
            var productNameLabel = new Label { name = "productNameLabel" };
            productNameLabel.style.width = Length.Percent(50); // Larghezza 50%
            var categoryLabel = new Label { name = "categoryLabel" };
            categoryLabel.style.width = Length.Percent(50); // Larghezza 50%
            newItem.Add(productNameLabel);
            newItem.Add(categoryLabel);
            return newItem;
        };

        parcelListView.bindItem = (item, index) =>
        {
            item.Q<Label>("productNameLabel").text = availableParcels[index].ProductName;
            item.Q<Label>("categoryLabel").text = availableParcels[index].Category;
        };

        // **Configurazione della ListView del riepilogo rifornimento**
        summaryListView.makeItem = () =>
        {
            var newItem = new VisualElement();
            newItem.style.flexDirection = FlexDirection.Row;
            var productNameLabel = new Label { name = "productNameLabel" };
            productNameLabel.style.width = Length.Percent(40); // Larghezza 40%
            var categoryLabel = new Label { name = "categoryLabel" };
            categoryLabel.style.width = Length.Percent(40); // Larghezza 40%
            var quantityLabel = new Label { name = "quantityLabel" };
            quantityLabel.style.width = Length.Percent(20); // Larghezza 20%
            newItem.Add(productNameLabel);
            newItem.Add(categoryLabel);
            newItem.Add(quantityLabel);
            return newItem;
        };

        summaryListView.bindItem = (item, index) =>
        {
            item.Q<Label>("productNameLabel").text = supplySummary[index].ProductName;
            item.Q<Label>("categoryLabel").text = supplySummary[index].Category;
            item.Q<Label>("quantityLabel").text = $"Qty: {supplySummary[index].Quantity}";
        };

        // **Eventi click dei bottoni**
        addButton.clicked += AddToSupplySummary;
        supplyButton.clicked += async () => await SupplyParcel();

        // **Nascondi la UI all'avvio**
        uiDocument.rootVisualElement.style.display = DisplayStyle.None;

        // **Caricamento iniziale dei pacchi e aggiornamento slot disponibili**
        parcelListView.schedule.Execute(async () =>
        {
            await LoadAvailableParcels();
            if (availableParcels.Count > 0)
            {
                parcelListView.selectedIndex = 0;
                UpdateAvailableSlotsLabel(availableParcels[0].Category);
            }
        });

        // **Aggiornamento slot disponibili al cambio di selezione nella ListView**
        parcelListView.selectionChanged += (items) =>
        {
            if (parcelListView.selectedItem is ParcelData selectedParcel)
            {
                UpdateAvailableSlotsLabel(selectedParcel.Category);
            }
        };
    }

    private void Update()
    {
        // **Mostra/Nascondi la UI con il tasto "U"**
        if (Input.GetKeyDown(KeyCode.U))
        {
            isUIVisible = !isUIVisible;
            uiDocument.rootVisualElement.style.display = isUIVisible ? DisplayStyle.Flex : DisplayStyle.None;
            if (isUIVisible)
            {
                _ = LoadAvailableParcels(); // Ricarica i pacchi quando la UI diventa visibile
            }
        }
    }

    // **Carica i pacchi disponibili dal database**
    public async Task LoadAvailableParcels()
    {
        try
        {
            await using var session = driver.AsyncSession();
            availableParcels = await session.ExecuteReadAsync(tx =>
                tx.RunAsync("MATCH (p:Parcel) RETURN p.product_name, p.category")
                  .Result.ToListAsync(record => new ParcelData
                  {
                      ProductName = record[0].As<string>(),
                      Category = record[1].As<string>()
                  }));
            parcelListView.itemsSource = availableParcels;
            parcelListView.Rebuild();
            parcelListView.selectedIndex = availableParcels.Any() ? 0 : -1;
            if (availableParcels.Any())
            {
                UpdateAvailableSlotsLabel(availableParcels[0].Category);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error loading parcels: {ex.Message}");
            ShowNotification("Errore nel caricamento dei pacchi.");
        }
    }

    // **Aggiorna l'etichetta degli slot disponibili**
    private async void UpdateAvailableSlotsLabel(string category)
    {
        int availableSlots = await GetAvailableSlots(category);
        availableSlotsDropdown.label = $"Slot Disponibili ({category}): {availableSlots}";
    }

    // **Aggiunge un pacco al riepilogo del rifornimento**
    private void AddToSupplySummary()
    {
        if (parcelListView.selectedItem is not ParcelData selectedParcel)
        {
            ShowNotification("Seleziona un pacco da aggiungere al riepilogo.");
            return;
        }

        int requestedQuantity = int.Parse(availableSlotsDropdown.value);

        // **Controlla se l'elemento esiste già nel riepilogo e aggiorna la quantità**
        var existingItem = supplySummary.FirstOrDefault(item => item.ProductName == selectedParcel.ProductName && item.Category == selectedParcel.Category);
        if (existingItem != null)
        {
            existingItem.Quantity += requestedQuantity;
        }
        else
        {
            supplySummary.Add(new ParcelData
            {
                ProductName = selectedParcel.ProductName,
                Category = selectedParcel.Category,
                Quantity = requestedQuantity
            });
        }

        summaryListView.itemsSource = supplySummary;
        summaryListView.Rebuild();
    }

    // **Invia la richiesta di rifornimento al database**
    private async Task SupplyParcel()
    {
        HideNotification();
        if (supplySummary.Count == 0)
        {
            ShowNotification("Aggiungi prima degli elementi al riepilogo del rifornimento.");
            return;
        }

        foreach (var item in supplySummary)
        {
            // Verifica che il pacco esista ancora in availableParcels
            var parcelData = availableParcels.FirstOrDefault(p => p.ProductName == item.ProductName && p.Category == item.Category);
            if (parcelData == null)
            {
                Debug.LogError($"Impossibile trovare il pacco '{item.ProductName}' - '{item.Category}' nel database. Potrebbe essere stato rimosso.");
                ShowNotification($"Impossibile trovare il pacco '{item.ProductName}' - '{item.Category}'.");
                continue;
            }
            
            await CreateSupplyRelationships(parcelData, item.Quantity);
            ShowNotification($"Richiesta di rifornimento per {item.Quantity} {item.ProductName} ({item.Category}) completata.");
            Debug.Log($"Rifornimento richiesto per {item.Quantity} di: {item.ProductName} - {item.Category}");
        }

        // **Aggiorna la lista dei pacchi e pulisci il riepilogo**
        await LoadAvailableParcels();
        supplySummary.Clear();
        summaryListView.itemsSource = null;
        summaryListView.Rebuild();
    }

    // **Ottiene il numero di slot disponibili per una determinata categoria**
    private async Task<int> GetAvailableSlots(string category)
    {
        try
        {
            await using var session = driver.AsyncSession();
            return await session.ExecuteReadAsync(tx =>
                tx.RunAsync("MATCH (s:Shelf {category: $category})-[:HAS_LAYER]->(l)-[:HAS_SLOT]->(sl) WHERE NOT (sl)-[:CONTAINS]->() RETURN COUNT(sl)", new { category })
                  .Result.SingleAsync().ContinueWith(task => task.Result[0].As<int>()));
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error checking available slots: {ex.Message}");
            ShowNotification("Errore nel controllo degli slot disponibili.");
            return 0;
        }
    }

    // **Crea le relazioni di rifornimento nel database**
    private async Task CreateSupplyRelationships(ParcelData parcel, int quantity)
    {
        try
        {
            await using var session = driver.AsyncSession();
            await session.ExecuteWriteAsync(async tx =>
            {
                await tx.RunAsync(@"
                MATCH (p:Parcel {product_name: $productName, category: $category})
                MATCH (d:Area {type: 'Delivery'})
                MERGE (p)-[r:LOCATED_IN]->(d)
                ON CREATE SET r.quantity = $quantity
                ON MATCH SET r.quantity = r.quantity + $quantity",
                    new { productName = parcel.ProductName, quantity, category = parcel.Category });
            });
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error creating supply relationships: {ex.Message}");
            ShowNotification("Errore nella creazione delle relazioni di rifornimento.");
        }
    }

    // **Mostra una notifica per 3 secondi**
    private void ShowNotification(string message)
    {
        notificationLabel.text = message;
        notificationLabel.style.display = DisplayStyle.Flex;
        Invoke(nameof(HideNotification), 3f);
    }

    // **Nasconde la notifica**
    private void HideNotification() => notificationLabel.style.display = DisplayStyle.None;

    // **Chiude la connessione al database alla distruzione dell'oggetto**
    private void OnDestroy() => driver?.Dispose();
}