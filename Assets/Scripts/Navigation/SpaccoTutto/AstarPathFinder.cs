using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class AStarPathfinder : MonoBehaviour
{
    public int width = 10;
    public int height = 10;
    public float cellSize = 1f;
    public bool[,] walkable;

    public void Initialize(bool[,] walkableMap)
    {
        walkable = walkableMap;
    }

    // Calcola il path da startPos a endPos in coordinate "mondo"
    public List<Vector3> ComputePath(Vector3 startPos, Vector3 endPos)
    {
        // Converti le posizioni mondo in coordinate griglia
        Vector2Int startCell = WorldToGrid(startPos);
        Vector2Int endCell = WorldToGrid(endPos);

        // Se start o end sono fuori, interrompi
        if (!IsInsideGrid(startCell) || !IsInsideGrid(endCell) ||
            !walkable[startCell.x, startCell.y] || !walkable[endCell.x, endCell.y])
        {
            // Non esiste un path valido
            return new List<Vector3>();
        }

        // A* standard
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        var gScore = new Dictionary<Vector2Int, float>();
        var fScore = new Dictionary<Vector2Int, float>();

        var openSet = new SimplePriorityQueue<Vector2Int>();
        openSet.Enqueue(startCell, 0);

        gScore[startCell] = 0;
        fScore[startCell] = HeuristicCost(startCell, endCell);

        while (openSet.Count > 0)
        {
            Vector2Int current = openSet.Dequeue();

            if (current == endCell)
            {
                return ReconstructPath(cameFrom, current);
            }

            foreach (var neighbor in GetNeighbors(current))
            {
                if (!walkable[neighbor.x, neighbor.y]) continue;

                float tentative_gScore = gScore[current] + 1f; // costo costante=1 per cella
                if (!gScore.ContainsKey(neighbor) || tentative_gScore < gScore[neighbor])
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentative_gScore;
                    fScore[neighbor] = gScore[neighbor] + HeuristicCost(neighbor, endCell);

                    if (!openSet.Contains(neighbor))
                    {
                        openSet.Enqueue(neighbor, fScore[neighbor]);
                    }
                    else
                    {
                        openSet.UpdatePriority(neighbor, fScore[neighbor]);
                    }
                }
            }
        }
        // Se arrivi qui, nessun path trovato
        return new List<Vector3>();
    }

    private List<Vector3> ReconstructPath(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int current)
    {
        var path = new List<Vector3>();
        path.Add(GridToWorld(current));

        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Add(GridToWorld(current));
        }
        path.Reverse();
        return path;
    }

    // Heuristica (Manhattan o Euclidea)
    private float HeuristicCost(Vector2Int a, Vector2Int b)
    {
        // Euclidea
        return Vector2.Distance(a, b);
    }

    private Vector2Int[] GetNeighbors(Vector2Int cell)
    {
        // 4-direzioni (o 8 se vuoi diagonali)
        return new Vector2Int[]
        {
            new Vector2Int(cell.x+1, cell.y),
            new Vector2Int(cell.x-1, cell.y),
            new Vector2Int(cell.x, cell.y+1),
            new Vector2Int(cell.x, cell.y-1)
        };
    }

    private Vector2Int WorldToGrid(Vector3 pos)
    {
        int x = Mathf.FloorToInt(pos.x / cellSize);
        int y = Mathf.FloorToInt(pos.z / cellSize);
        return new Vector2Int(x, y);
    }

    private Vector3 GridToWorld(Vector2Int cell)
    {
        float worldX = (cell.x + 0.5f) * cellSize;
        float worldZ = (cell.y + 0.5f) * cellSize;
        return new Vector3(worldX, 0, worldZ);
    }

    private bool IsInsideGrid(Vector2Int c)
    {
        return (c.x >= 0 && c.x < width && c.y >= 0 && c.y < height);
    }
}
