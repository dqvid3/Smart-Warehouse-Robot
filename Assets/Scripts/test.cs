using UnityEngine;
using System.Collections.Generic;

public class test : MonoBehaviour
{
    // Lista per tenere traccia degli oggetti rilevati
    private List<GameObject> detectedObjects = new List<GameObject>();

    // Funzione per ottenere la distanza dall'oggetto più vicino
    public float GetClosestDistance()
    {
        if (detectedObjects.Count == 0)
        {
            Debug.LogWarning("Nessun oggetto rilevato!");
            return -1f; // -1 indica nessun oggetto rilevato
        }

        float closestDistance = float.MaxValue;
        foreach (var obj in detectedObjects)
        {
            if (obj != null)
            {
                float distance = Vector3.Distance(transform.position, obj.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                }
            }
        }

        return closestDistance;
    }

    // Metodo chiamato quando un oggetto entra nel trigger
    private void OnTriggerEnter(Collider other)
    {
        if (!detectedObjects.Contains(other.gameObject))
        {
            detectedObjects.Add(other.gameObject);
        }
    }

    // Metodo chiamato quando un oggetto esce dal trigger
    private void OnTriggerExit(Collider other)
    {
        if (detectedObjects.Contains(other.gameObject))
        {
            detectedObjects.Remove(other.gameObject);
        }
    }

    // Debug per visualizzare le distanze in tempo reale
    private void Update()
    {
        if (detectedObjects.Count > 0)
        {
            Debug.Log("Distanza più vicina: " + GetClosestDistance());
        }
    }
}