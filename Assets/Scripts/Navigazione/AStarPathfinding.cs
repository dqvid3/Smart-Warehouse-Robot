using System.Collections.Generic;
using UnityEngine;

public class AStarPathfinding : MonoBehaviour
{
    public int gridWidth = 10;
    public int gridHeight = 10;
    public float nodeSize = 1f;
    public Vector2 startPosition;
    public Vector2 targetPosition;

    private Node[,] grid;

    public List<Vector2> FindPath(Vector2 start, Vector2 target)
    {
        grid = GenerateGrid();

        Node startNode = grid[(int)start.x, (int)start.y];
        Node targetNode = grid[(int)target.x, (int)target.y];

        List<Node> openList = new List<Node>();
        HashSet<Node> closedList = new HashSet<Node>();
        openList.Add(startNode);

        while (openList.Count > 0)
        {
            Node currentNode = openList[0];
            foreach (var node in openList)
            {
                if (node.fCost < currentNode.fCost || node.fCost == currentNode.fCost && node.hCost < currentNode.hCost)
                {
                    currentNode = node;
                }
            }

            openList.Remove(currentNode);
            closedList.Add(currentNode);

            if (currentNode == targetNode)
            {
                return RetracePath(startNode, targetNode);
            }

            foreach (Node neighbor in GetNeighbors(currentNode))
            {
                if (closedList.Contains(neighbor)) continue;
                if (neighbor.isObstacle) continue;

                float newMovementCostToNeighbor = currentNode.gCost + GetDistance(currentNode, neighbor);
                if (newMovementCostToNeighbor < neighbor.gCost || !openList.Contains(neighbor))
                {
                    neighbor.gCost = newMovementCostToNeighbor;
                    neighbor.hCost = GetDistance(neighbor, targetNode);
                    neighbor.parent = currentNode;

                    if (!openList.Contains(neighbor))
                    {
                        openList.Add(neighbor);
                    }
                }
            }
        }

        return new List<Vector2>(); // No path found
    }

    private List<Node> GetNeighbors(Node node)
    {
        List<Node> neighbors = new List<Node>();

        int[,] directions = new int[,] { { 0, 1 }, { 1, 0 }, { 0, -1 }, { -1, 0 } };

        for (int i = 0; i < directions.GetLength(0); i++)
        {
            int neighborX = node.gridX + directions[i, 0];
            int neighborY = node.gridY + directions[i, 1];

            if (neighborX >= 0 && neighborX < gridWidth && neighborY >= 0 && neighborY < gridHeight)
            {
                neighbors.Add(grid[neighborX, neighborY]);
            }
        }

        return neighbors;
    }

    private List<Vector2> RetracePath(Node startNode, Node endNode)
    {
        List<Vector2> path = new List<Vector2>();
        Node currentNode = endNode;

        while (currentNode != startNode)
        {
            path.Add(new Vector2(currentNode.gridX, currentNode.gridY));
            currentNode = currentNode.parent;
        }
        path.Reverse();
        return path;
    }

    private float GetDistance(Node nodeA, Node nodeB)
    {
        int dstX = Mathf.Abs(nodeA.gridX - nodeB.gridX);
        int dstY = Mathf.Abs(nodeA.gridY - nodeB.gridY);
        return dstX + dstY;
    }

    private Node[,] GenerateGrid()
    {
        Node[,] grid = new Node[gridWidth, gridHeight];
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                grid[x, y] = new Node(x, y);
            }
        }
        return grid;
    }

    public class Node
    {
        public bool isObstacle;
        public float gCost;
        public float hCost;
        public Node parent;
        public int gridX;
        public int gridY;

        public float fCost { get { return gCost + hCost; } }

        public Node(int x, int y)
        {
            gridX = x;
            gridY = y;
            isObstacle = false; // You can mark obstacles later
        }
    }
}
