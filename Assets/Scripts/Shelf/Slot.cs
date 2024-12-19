using UnityEngine;

public class Slot : MonoBehaviour
{
    public GameObject currentParcel; // Il pacco presente nello slot (null se vuoto)

    // Verifica se lo slot è vuoto
    public bool IsEmpty => currentParcel == null;

    // Metodo per aggiungere un pacco nello slot
    public void AddParcel(GameObject parcel)
    {
        if (IsEmpty)
        {
            currentParcel = parcel;
            parcel.transform.SetParent(transform); // Assegna il pacco come figlio dello slot
            parcel.transform.localPosition = Vector3.zero; // Posiziona il pacco correttamente nello slot
            Debug.Log($"Pacco aggiunto nello slot {name}.");
        }
        else
        {
            Debug.LogWarning($"Slot {name} è già occupato!");
        }
    }

    // Metodo per rimuovere e restituire il pacco dallo slot
    public GameObject RemoveParcel()
    {
        if (!IsEmpty)
        {
            GameObject parcel = currentParcel;
            currentParcel.transform.SetParent(null); // Rimuovi il pacco dallo slot
            currentParcel = null;
            Debug.Log($"Pacco rimosso dallo slot {name}.");
            return parcel;
        }
        else
        {
            Debug.LogWarning($"Slot {name} è vuoto, nessun pacco da rimuovere.");
            return null;
        }
    }

    // Metodo per ottenere informazioni sul pacco nello slot
    public string GetParcelInfo()
    {
        if (!IsEmpty)
        {
            Parcel parcel = currentParcel.GetComponent<Parcel>();
            if (parcel != null)
            {
                return $"Slot {name}: QR Code = {parcel.qrCode}, Categoria = {parcel.category}";
            }
        }
        return $"Slot {name}: Vuoto";
    }
}