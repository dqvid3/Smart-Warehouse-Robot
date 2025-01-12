using System.Collections.Generic;
using UnityEngine;

public class PathRequestManager : MonoBehaviour
{
    private Queue<PathRequest> pathRequestQueue = new Queue<PathRequest>();
    private PathRequest currentPathRequest;

    private Pathfinding pathfinding;
    private bool isProcessingPath;

    private static PathRequestManager instance;

    void Awake()
    {
        instance = this;
        pathfinding = GetComponent<Pathfinding>();
    }

    public static void RequestPath(Vector3 pathStart, Vector3 pathEnd, System.Action<Vector3[], bool> callback)
    {
        PathRequest newRequest = new PathRequest(pathStart, pathEnd, callback);
        instance.pathRequestQueue.Enqueue(newRequest);
        instance.TryProcessNext();
    }

    private void TryProcessNext()
    {
        if (!isProcessingPath && pathRequestQueue.Count > 0)
        {
            currentPathRequest = pathRequestQueue.Dequeue();
            isProcessingPath = true;
            pathfinding.FindPath(currentPathRequest.pathStart, currentPathRequest.pathEnd, (nodes, success) =>
            {
                if (success)
                {
                    // Convert List<Node> to Vector3[]
                    Vector3[] path = nodes.ConvertAll(node => node.worldPosition).ToArray();
                    FinishProcessingPath(path, true);
                }
                else
                {
                    FinishProcessingPath(null, false);
                }
            });
        }
    }


    private void FinishProcessingPath(Vector3[] path, bool success)
    {
        currentPathRequest.callback(path, success);
        isProcessingPath = false;
        TryProcessNext();
    }

    private struct PathRequest
    {
        public Vector3 pathStart;
        public Vector3 pathEnd;
        public System.Action<Vector3[], bool> callback;

        public PathRequest(Vector3 start, Vector3 end, System.Action<Vector3[], bool> callback)
        {
            this.pathStart = start;
            this.pathEnd = end;
            this.callback = callback;
        }
    }
}