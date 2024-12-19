using UnityEngine;
using System.Collections.Generic;

public class Shelf : MonoBehaviour
{
    public string category; // Categoria dello scaffale
    private List<Slot> slots = new List<Slot>(); // Lista di tutti gli slot nello scaffale

    void Start()
    {
        // Trova tutti gli slot figli di questo scaffale
        slots.AddRange(GetComponentsInChildren<Slot>());
    }

    // Restituisce il numero di slot vuoti
    public int GetEmptySlotCount()
    {
        int emptyCount = 0;
        foreach (Slot slot in slots)
        {
            if (slot.IsEmpty)
                emptyCount++;
        }
        return emptyCount;
    }

    // Restituisce il primo slot vuoto disponibile
    public Slot GetFirstEmptySlot()
    {
        foreach (Slot slot in slots)
        {
            if (slot.IsEmpty)
                return slot;
        }
        return null; // Nessuno slot vuoto trovato
    }

    // Restituisce il pacco con un QR code specifico
    public Slot GetSlotWithParcel(string qrCode)
    {
        foreach (Slot slot in slots)
        {
            if (!slot.IsEmpty)
            {
                Parcel parcel = slot.currentParcel.GetComponent<Parcel>();
                if (parcel != null && parcel.qrCode == qrCode)
                    return slot;
            }
        }
        return null; // Nessun slot con il pacco specificato trovato
    }

    // Debug: Stampa lo stato di tutti gli slot
    public void PrintShelfStatus()
    {
        foreach (Slot slot in slots)
        {
            Debug.Log(slot.GetParcelInfo());
        }
    }
}