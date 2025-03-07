using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PathFinder
{
    private PathManager pathManager;

    public PathFinder(PathManager pathManager)
    {
        this.pathManager = pathManager;
    }

    public List<int> FindPath(int startVertexIndex, int endVertexIndex)
    {
        if (!pathManager.Manager.AdjacencyList.ContainsKey(startVertexIndex) || !pathManager.Manager.AdjacencyList.ContainsKey(endVertexIndex))
        {
            Debug.LogWarning("Start or end vertex index is invalid or not in adjacency list.");
            return null; // Or return an empty list if you prefer
        }

        if (startVertexIndex == endVertexIndex)
        {
            return new List<int> { startVertexIndex }; // Start and end are the same
        }

        Dictionary<int, int> cameFrom = new Dictionary<int, int>();
        Dictionary<int, float> costSoFar = new Dictionary<int, float>();
        PriorityQueue<int> frontier = new PriorityQueue<int>(); // Using Optimized Priority Queue

        frontier.Enqueue(startVertexIndex, 0); // **Enqueue() #1 - Start node**
        cameFrom[startVertexIndex] = -1; // No parent for start node
        costSoFar[startVertexIndex] = 0;

        while (!frontier.IsEmpty())
        {
            int currentVertexIndex = frontier.Dequeue();

            if (currentVertexIndex == endVertexIndex)
            {
                return ReconstructPath(cameFrom, endVertexIndex);
            }

            if (!pathManager.Manager.AdjacencyList.ContainsKey(currentVertexIndex)) continue;

            foreach (int neighborIndex in pathManager.Manager.AdjacencyList[currentVertexIndex])
            {
                NodeData nodeData = pathManager.Manager.NodeManager.nodeDataDictionary[neighborIndex];
                if (nodeData.HasObstacle) continue;

                bool isInCurrentPath = pathManager.IsInPathCreationMode && pathManager.PathBuilder.CurrentPath.Contains(neighborIndex);
                if (nodeData.HasPath && neighborIndex != endVertexIndex && !isInCurrentPath) continue;
                if (nodeData.HasFlag && neighborIndex != endVertexIndex) continue;

                float newCost = costSoFar[currentVertexIndex] + GetCost(currentVertexIndex, neighborIndex);
                if (!costSoFar.ContainsKey(neighborIndex) || newCost < costSoFar[neighborIndex])
                {
                    costSoFar[neighborIndex] = newCost;
                    float priority = newCost + Heuristic(neighborIndex, endVertexIndex);
                    frontier.Enqueue(neighborIndex, priority);
                    cameFrom[neighborIndex] = currentVertexIndex;
                }
            }
        }

        return null; // No path found
    }

    public List<int> FindPathThroughPaths(int fromNode, int toNode)
    {
        Queue<int> toVisit = new Queue<int>();
        HashSet<int> visited = new HashSet<int>();
        Dictionary<int, int> cameFrom = new Dictionary<int, int>();
        toVisit.Enqueue(fromNode);
        visited.Add(fromNode);
        cameFrom[fromNode] = -1;

        // Fallback if fromNode isn't in allPaths
        if (!pathManager.allPaths.Any(p => p.Value.ContainsNode(fromNode)))
        {
            Debug.LogWarning($"[PathManager] Starting node {fromNode} not in any path, trying grid neighbors");
            foreach (int neighbor in pathManager.Manager.NodeManager.GetNodeNieghbors(fromNode))
            {
                if (!visited.Contains(neighbor) && pathManager.Manager.NodeManager.GetNodeData(neighbor).HasPath)
                {
                    visited.Add(neighbor);
                    toVisit.Enqueue(neighbor);
                    cameFrom[neighbor] = fromNode;
                }
            }
        }

        while (toVisit.Count > 0)
        {
            int currentNode = toVisit.Dequeue();
            if (currentNode == toNode)
            {
                List<int> path = ReconstructPath(cameFrom, toNode);
                return path;
            }

            foreach (var path in pathManager.allPaths.Values)
            {
                int index = path.Nodes.IndexOf(currentNode);
                if (index != -1)
                {
                    if (index > 0)
                    {
                        int prevNode = path.Nodes[index - 1];
                        if (!visited.Contains(prevNode))
                        {
                            visited.Add(prevNode);
                            toVisit.Enqueue(prevNode);
                            cameFrom[prevNode] = currentNode;
                        }
                    }
                    if (index < path.Nodes.Count - 1)
                    {
                        int nextNode = path.Nodes[index + 1];
                        if (!visited.Contains(nextNode))
                        {
                            visited.Add(nextNode);
                            toVisit.Enqueue(nextNode);
                            cameFrom[nextNode] = currentNode;
                        }
                    }
                }
            }
        }
        Debug.LogWarning($"[PathManager] No path found through existing paths from {fromNode} to {toNode}, trying direct grid path");

        // Fallback to direct grid path if no path found in existing paths
        List<int> gridPath = FindDirectGridPath(fromNode, toNode);
        if (gridPath != null)
        {
            Debug.Log($"[PathManager] Found direct grid path from {fromNode} to {toNode}: {string.Join(", ", gridPath)}");
            return gridPath;
        }
        return null;
    }

    private List<int> FindDirectGridPath(int fromNode, int toNode)
    {
        Queue<int> toVisit = new Queue<int>();
        HashSet<int> visited = new HashSet<int>();
        Dictionary<int, int> cameFrom = new Dictionary<int, int>();
        toVisit.Enqueue(fromNode);
        visited.Add(fromNode);
        cameFrom[fromNode] = -1;

        while (toVisit.Count > 0)
        {
            int currentNode = toVisit.Dequeue();
            if (currentNode == toNode)
            {
                return ReconstructPath(cameFrom, toNode);
            }

            foreach (int neighbor in pathManager.Manager.NodeManager.GetNodeNieghbors(currentNode))
            {
                NodeData neighborData = pathManager.Manager.NodeManager.GetNodeData(neighbor);
                if (neighborData != null && !visited.Contains(neighbor) && !neighborData.HasObstacle && (neighborData.HasPath || neighbor == toNode))
                {
                    visited.Add(neighbor);
                    toVisit.Enqueue(neighbor);
                    cameFrom[neighbor] = currentNode;
                }
            }
        }
        Debug.LogWarning($"[PathManager] No direct grid path found from {fromNode} to {toNode}");
        return null;
    }

    public List<int> ReconstructPath(Dictionary<int, int> cameFrom, int endVertexIndex)
    {
        List<int> path = new List<int>();
        int current = endVertexIndex;
        while (current != -1)
        {
            path.Add(current);
            if (!cameFrom.ContainsKey(current))
            {
                Debug.LogWarning($"Key {current} not found in cameFrom during path reconstruction");
                break;
            }
            current = cameFrom[current];
        }
        path.Reverse();
        return path;
    }

    private float GetCost(int startVertexIndex, int endVertexIndex)
    {
        float baseCost = 1f; // Base cost for moving to an adjacent cell

        Vector3 startPos = pathManager.Manager.globalVertices[startVertexIndex];
        Vector3 endPos = pathManager.Manager.globalVertices[endVertexIndex];

        float heightDifference = endPos.y - startPos.y; // Calculate height difference (slope)

        float slopeCost = 0f; // Initialize slope cost

        if (heightDifference > 0) // Uphill
        {
            slopeCost = heightDifference * 2f; // Example: Higher cost for uphill (adjust multiplier as needed)
        }
        else if (heightDifference < 0) // Downhill
        {
            slopeCost = heightDifference * -0.5f; // Example: Lower (negative) cost/benefit for downhill (adjust multiplier)
        }
        // If heightDifference == 0, slopeCost remains 0 (no slope penalty/benefit)

        return baseCost + slopeCost; // Total cost is base cost + slope cost
    }

    private float Heuristic(int current, int target)
    {
        return Vector3.Distance(pathManager.Manager.globalVertices[current], pathManager.Manager.globalVertices[target]); // Euclidean distance heuristic
    }
}
