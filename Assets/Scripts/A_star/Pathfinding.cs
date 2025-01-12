using System.Collections.Generic;
using UnityEngine;

public class Pathfinding : MonoBehaviour
{
    public Grid grid;

    public void FindPath(Vector3 startPos, Vector3 targetPos, System.Action<List<Node>, bool> callback)
    {
        Node startNode = grid.NodeFromWorldPoint(startPos);
        Node targetNode = grid.NodeFromWorldPoint(targetPos);

        // Cambia la tua openSet in un Heap di nodi
        Heap<Node> openSet = new Heap<Node>(grid.MaxSize);
        HashSet<Node> closedSet = new HashSet<Node>();

        openSet.Add(startNode);

        while (openSet.Count > 0)
        {
            Node currentNode = openSet.RemoveFirst();
            closedSet.Add(currentNode);

            if (currentNode == targetNode)
            {
                callback(RetracePath(startNode, targetNode), true);
                return;
            }

            foreach (Node neighbor in grid.GetNeighbours(currentNode))
            {
                if (!neighbor.walkable || closedSet.Contains(neighbor)) continue;

                // (Usa la penalty qui!)
                int newCostToNeighbor = currentNode.gCost 
                                        + GetDistance(currentNode, neighbor) 
                                        + neighbor.movementPenalty;
                
                if (newCostToNeighbor < neighbor.gCost || !openSet.Contains(neighbor))
                {
                    neighbor.gCost = newCostToNeighbor;
                    neighbor.hCost = GetDistance(neighbor, targetNode);
                    neighbor.parent = currentNode;

                    if (!openSet.Contains(neighbor))
                    {
                        openSet.Add(neighbor);
                    }
                    else
                    {
                        // Se già esiste, aggiorna la sua posizione nell'heap
                        openSet.UpdateItem(neighbor);
                    }
                }
            }
        }

        callback(null, false); // Path not found
    }


    private List<Node> RetracePath(Node startNode, Node endNode)
    {
        List<Node> path = new List<Node>();
        Node currentNode = endNode;

        while (currentNode != startNode)
        {
            path.Add(currentNode);
            currentNode = currentNode.parent;
        }

        path.Reverse();
        return path;
    }

    private int GetDistance(Node nodeA, Node nodeB)
    {
        int dstX = Mathf.Abs(nodeA.gridX - nodeB.gridX);
        int dstY = Mathf.Abs(nodeA.gridY - nodeB.gridY);

        return dstX + dstY;
    }
}