using System.Collections.Generic;

public class SimplePriorityQueue<T>
{
    private readonly List<(T item, float priority)> elements = new List<(T, float)>();

    public int Count => elements.Count;

    public void Enqueue(T item, float priority)
    {
        elements.Add((item, priority));
        elements.Sort((x, y) => x.priority.CompareTo(y.priority)); // Ordina per priorità crescente
    }

    public T Dequeue()
    {
        if (elements.Count == 0)
        {
            throw new System.InvalidOperationException("La coda è vuota!");
        }
        var item = elements[0].item;
        elements.RemoveAt(0);
        return item;
    }

    public bool Contains(T item)
    {
        return elements.Exists(e => EqualityComparer<T>.Default.Equals(e.item, item));
    }

    public void UpdatePriority(T item, float newPriority)
    {
        for (int i = 0; i < elements.Count; i++)
        {
            if (EqualityComparer<T>.Default.Equals(elements[i].item, item))
            {
                elements[i] = (item, newPriority);
                elements.Sort((x, y) => x.priority.CompareTo(y.priority)); // Riordina
                return;
            }
        }
    }
}
