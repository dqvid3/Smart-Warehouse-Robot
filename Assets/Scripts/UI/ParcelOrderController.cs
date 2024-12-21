using UnityEngine;
using UnityEngine.UIElements;
using Neo4j.Driver;
using System;
using System.Linq;
using System.Threading.Tasks;

public class OrderManagementController : MonoBehaviour
{
    private ScrollView primaryScrollView;
    private ScrollView secondaryScrollView;
    private Button completeButton;
    private Label notificationLabel;
    private IDriver driver;
    private UIDocument uiDocument;

    private async void Start()
    {
        driver = GraphDatabase.Driver("bolt://localhost:7687", AuthTokens.Basic("neo4j", "password"));

        var root = (uiDocument = GetComponent<UIDocument>()).rootVisualElement;
        primaryScrollView = root.Q<ScrollView>("primaryScrollView");
        secondaryScrollView = root.Q<ScrollView>("secondaryScrollView");
        completeButton = root.Q<Button>("completeButton");
        notificationLabel = root.Q<Label>("notificationLabel");

        notificationLabel.style.display = DisplayStyle.None;
        completeButton.clicked += async () => await CompleteOrder();

        await PopulatePrimaryList();
    }

    private async Task PopulatePrimaryList()
    {
        try
        {
            await using var session = driver.AsyncSession();
            var items = await session.ExecuteReadAsync(tx =>
                tx.RunAsync("MATCH (s:Shelf)-[:HAS_LAYER]->(:Layer)-[:HAS_SLOT]->(slot:Slot) RETURN DISTINCT s.category AS category")
                  .Result.ToListAsync(record => record["category"].As<string>()));

            primaryScrollView.Clear();

            foreach (var item in items)
            {
                var itemContainer = new VisualElement { style = { flexDirection = FlexDirection.Row } };

                var label = new Label(item) { style = { flexGrow = 1 } };
                var button = new Button { text = "Add to Cart" };
                button.clicked += () => AddToCart(item);

                itemContainer.Add(label);
                itemContainer.Add(button);
                primaryScrollView.Add(itemContainer);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error populating primary list: {ex.Message}");
        }
    }

    private void AddToCart(string item)
    {
        var cartItemContainer = new VisualElement { style = { flexDirection = FlexDirection.Row } };

        var label = new Label(item) { style = { flexGrow = 1 } };
        var removeButton = new Button { text = "Remove" };
        removeButton.clicked += () => secondaryScrollView.Remove(cartItemContainer);

        cartItemContainer.Add(label);
        cartItemContainer.Add(removeButton);
        secondaryScrollView.Add(cartItemContainer);
    }

    private async Task CompleteOrder()
    {
        try
        {
            var items = secondaryScrollView.Children().Select(child => (child as VisualElement)?.Q<Label>()?.text).Where(text => !string.IsNullOrEmpty(text)).ToList();

            if (items.Count == 0)
            {
                ShowNotification("No items in the cart to complete the order.");
                return;
            }

            await using var session = driver.AsyncSession();
            await session.ExecuteWriteAsync(tx =>
            {
                foreach (var item in items)
                {
                    tx.RunAsync("MERGE (o:Order {item: $item, status: 'Pending'})", new { item });
                }
                return Task.CompletedTask;
            });

            ShowNotification("Order completed successfully!");
            secondaryScrollView.Clear();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error completing order: {ex.Message}");
            ShowNotification("An error occurred while completing the order.");
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