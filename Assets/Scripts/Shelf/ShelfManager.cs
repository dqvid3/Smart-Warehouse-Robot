using UnityEngine;
using System.Collections.Generic;

public class ShelfManager : MonoBehaviour
{
    private List<Shelf> shelves = new List<Shelf>();

    void Start()
    {
        shelves.AddRange(FindObjectsByType<Shelf>(FindObjectsSortMode.None));
    }

    // Restituisce il numero totale di slot vuoti per una categoria
    public int GetEmptySlotsByCategory(string category)
    {
        int totalEmpty = 0;
        foreach (Shelf shelf in shelves)
        {
            if (shelf.category == category)
            {
                totalEmpty += shelf.GetEmptySlotCount();
            }
        }
        return totalEmpty;
    }

    // Debug: Stampa informazioni sugli scaffali
    public void PrintShelfInfo()
    {
        foreach (Shelf shelf in shelves)
        {
            Debug.Log($"Scaffale {shelf.name} - Categoria: {shelf.category}");
            Debug.Log($"Slot vuoti: {shelf.GetEmptySlotCount()}");
        }
    }
}