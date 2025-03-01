using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;

public class PathFindingManager : MonoBehaviour
{
    [SerializeField] private GameObject tempPathPrefab;
    private HexGridManager manager; // Reference to the HexGridGenerator

    public bool isInPathCreationMode = false;

    public bool IsInPathCreationMode
    {
        get => isInPathCreationMode;
        set => isInPathCreationMode = value;
    }

    [SerializeField] private List<int> currentPath; // To store the calculated path
    private Dictionary<int, List<int>> allPaths = new Dictionary<int, List<int>>();
    public IReadOnlyDictionary<int, List<int>> AllPaths => new ReadOnlyDictionary<int, List<int>>(allPaths);
    private const int MaxPaths = 1000;
    private int nextPathId = 0;

    public void Initialise(HexGridManager manager) // Constructor to get HexGridGenerator instance
    {
        this.manager = manager;
        isInPathCreationMode = false;
        currentPath = new List<int>();
        currentPath.Clear();
    }

    public int GetPathId(int vertexIndex)
    {
        foreach (var kvp in allPaths)
        {
            if (kvp.Value.Contains(vertexIndex))
            {
                return kvp.Key;
            }
        }
        return -1;
    }

    // Called from UI Create Path button
    public void StartPathPlacement()
    {
        int startNode = manager.NodeManager.heldVertexIndex;
        Debug.Log($"Path placement started at {startNode}");
        NodeData startNodeData = manager.NodeManager.GetNodeData(startNode);
        if (startNode == -1)
        {
            Debug.Log("Path creation cancelled: Invalid start node.");
            return;
        }

        if (startNodeData.HasFlag || startNodeData.HasBuilding)
        {
            if (startNodeData.HasFlag)
            {
                Debug.Log("Start node has a flag");
            }
            else if (startNodeData.HasBuilding)
            {
                Debug.Log("Start node has a building");
            }

            CreatePath(startNode);
        }
        else
        {
            Debug.Log("Path creation cancelled: Start node must have a flag or building.");
            // Optionally, show a message to the user via UIManager.
        }
    }

    private void CreatePath(int startNode)
    {
        if (startNode == -1) return;
        manager.PathFindingManager.IsInPathCreationMode = true;

        manager.NodeManager.startPathVertexIndex = startNode;

        manager.NodeManager.endPathVertexIndex = -1; // Reset end vertex
        currentPath?.Clear();      // Clear previous path
        currentPath.Add(manager.NodeManager.heldVertexIndex);
        Debug.Log($"Path Start Vertex Selected: {manager.NodeManager.startPathVertexIndex}");
    }

    public void TryAddPathToEndNode(int endNode)
    {
        if (!isInPathCreationMode || endNode == -1) return;

        NodeData endNodeData = manager.NodeManager.GetNodeData(endNode);

        if (endNodeData != null)
        {
            if (endNodeData.HasFlag) // **Check if end node has a flag**
            {
                Debug.Log("End node has a flag. Finalising path");
                FinalisePath(endNode);
            }
            else
            {
                // Extend Path
                Debug.Log("Extending path");
                ExtendPath(endNode);
            }
        }        
        else
        {
            Debug.Log("Path creation cancelled: End node must have a flag.");
            // Optionally, show a message to the user via UIManager.
        }
    }

    private void FinalisePath(int endNode)
    {
        int currentStartCell = currentPath.Last();
        Debug.Log($"Finalising path from {currentStartCell} to {endNode}");
        List<int> pathSegment = GeneratePathSegment(currentStartCell, endNode);
        if (pathSegment == null)
        {

            foreach (int node in currentPath)
            {
                manager.NodeManager.SetNodePath(node, false);
            }
            CancelPathCreation();
            return;
        }
        currentPath.Remove(currentStartCell);
        currentPath.AddRange(pathSegment);
        if (!currentPath.Contains(endNode))
        {
            currentPath.Add(endNode);
        }

        if (IsValidPath(currentPath))
        {
            SaveAndVisualiseFinalPath();
        }

        CancelPathCreation();
    }

    private List<int> GeneratePathSegment(int startNode, int endNode)
    {
        List<int> nodeIndices = FindPath(startNode, endNode);
        if (nodeIndices == null)
        {
            Debug.Log($"No path found between {startNode} and {endNode}");
            return null;
        }

        List<int> segment = new();
        foreach (int index in nodeIndices)
        {
            segment.Add(index);
        }
        return segment;
    }

    private void SaveAndVisualiseFinalPath()
    {
        if (allPaths.Count >= MaxPaths)
        {
            Debug.LogWarning($"Path limit ({MaxPaths}) reached. Removing oldest path.");
            int oldestPathId = allPaths.Keys.Min();
            RemovePathById(oldestPathId);
        }

        int pathId = nextPathId++;
        allPaths[pathId] = new List<int>(currentPath);

        foreach (int node in currentPath)
        {
            NodeData nodeData = manager.NodeManager.GetNodeData(node);
            nodeData.HasPath = true;
        }

        manager.PathVisualsGenerator.DrawPath(currentPath);
        manager.UIManager.UpdateUIText("Paths", $"Paths: {allPaths.Count}"); ;
    }

    private void ExtendPath(int endNode)
    {
        if (!CanExtendPath(endNode)) return;

        manager.NodeManager.endPathVertexIndex = endNode;
        int currentStartNode = currentPath.Last();
        manager.NodeManager.startPathVertexIndex = currentStartNode;
        List<int> pathSegment = FindPath(currentStartNode, endNode);
        Debug.Log($"Path segment found, length: {pathSegment.Count}");

        if (pathSegment == null || !CanCreatePath(pathSegment.ToArray()))
        {
            CancelPathCreation();
            return;
        }

        currentPath.Remove(currentStartNode);
        currentPath.AddRange(pathSegment);
        if (IsValidPath(currentPath))
        {
            VisualizeTempPath();
            foreach (int node in currentPath)
            {
                manager.NodeManager.SetNodePath(node, true);
            }
        }
        else
        {
            currentPath.RemoveRange(currentPath.Count - pathSegment.Count, pathSegment.Count);
        }
    }

    private bool CanExtendPath(int endNode)
    {
        NodeData endNodeData = manager.NodeManager.GetNodeData(endNode);
        if (endNodeData.HasPath)
        {
            Debug.Log("Cannot extend path: Cell already in an existing path.");
            return false;
        }
        Debug.Log($"Can extend path to node {endNode}");
        return true;
    }

    private bool IsValidPath(List<int> path)
    {
        if (path == null || path.Count < 2) return false;
        foreach (int node in path)
        {
            if (!manager.NodeManager.IsNodeValidForPath(node))
            {
                Debug.Log($"Path invalid at node {node}");
                return false;
            }
        }
        return true;
    }

    private void VisualizeTempPath()
    {
        foreach (int node in currentPath)
        {
            GameObject tempNode = Instantiate(tempPathPrefab, manager.globalVertices[node], Quaternion.identity);
            tempNode.transform.SetParent(manager.tempPathTransform);
        }
    }

    private void ClearTempPathVisuals()
    {
        foreach (Transform child in manager.tempPathTransform)
        {
            Destroy(child.gameObject);
        }
    }

    private bool CanCreatePath(int[] points)
    {
        if (points == null || points.Length < 2)
        {
            return false;
        }
        return true;
    }

    public List<int> FindPath(int startVertexIndex, int endVertexIndex)
    {
        if (!manager.AdjacencyList.ContainsKey(startVertexIndex) || !manager.AdjacencyList.ContainsKey(endVertexIndex))
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

            if (!manager.AdjacencyList.ContainsKey(currentVertexIndex)) continue;

            foreach (int neighborIndex in manager.AdjacencyList[currentVertexIndex])
            {
                NodeData nodeData = manager.NodeManager.nodeDataDictionary[neighborIndex];
                if (nodeData.HasObstacle) continue;
                if (nodeData.HasPath && neighborIndex != endVertexIndex) continue;
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

    private int NumberOfFlagsAttachedToPath(List<int> path)
    {
        int count = 0;
        foreach (int vertex in path)
        {
            NodeData vertexData = manager.NodeManager.GetNodeData(vertex);
            if (vertexData.HasFlag) count++;
        }
        Debug.Log($"Number of flags attached to path: {count}");
        return count;
    }

    // Called from UI Cancel Path button
    public void CancelPathCreation()
    {
        if (currentPath == null) currentPath = new List<int>();
        manager.UIManager.HideAllPanels();
        ClearTempPathVisuals();
        if (manager.NodeManager.heldVertexIndex != -1) manager.NodeManager.heldVertexIndex = -1;
        if (manager.NodeManager.startPathVertexIndex != -1) manager.NodeManager.startPathVertexIndex = -1;
        if (manager.NodeManager.endPathVertexIndex != -1) manager.NodeManager.endPathVertexIndex = -1;
        isInPathCreationMode = false;
        // frontier.Clear(); // Not needed as frontier is local variable in FindPath, but if you reused frontier, you'd clear it here.
    }

    public void RemovePath()
    {
        int node = manager.NodeManager.heldVertexIndex;
        var pathsToRemove = allPaths.Where(kvp => kvp.Value.Contains(node)).ToList();
        foreach (var kvp in pathsToRemove)
        {
            RemovePathById(kvp.Key);
        }
        RefreshPathVisuals();
        manager.UIManager.UpdateUIText("Paths", $"Paths: {allPaths.Count}");
        manager.UIManager.HideAllPanels();
    }

    private void RemovePathById(int pathId)
    {
        if (!allPaths.ContainsKey(pathId)) return;

        List<int> path = allPaths[pathId];
        foreach (int vertex in path)
        {
            NodeData vertexData = manager.NodeManager.GetNodeData(vertex);
            if (!vertexData.HasFlag) vertexData.HasPath = false;
        }
        allPaths.Remove(pathId);
    }

    public void SplitPathAt(int splitNode)
    {
        var pathsToSplit = allPaths.Where(kvp => kvp.Value.Contains(splitNode)).ToList();
        foreach (var kvp in pathsToSplit)
        {
            int pathId = kvp.Key;
            List<int> path = kvp.Value;
            int splitIndex = path.IndexOf(splitNode);
            if (splitIndex >= 0)
            {
                Debug.Log($"Splitting path {pathId} at node {splitNode}, index {splitIndex}");
                RemovePathById(pathId); // Clear original path data
                AddSplitPaths(path, splitIndex);
            }
        }
        RefreshPathVisuals();
        manager.UIManager.UpdateUIText("Paths", $"Paths: {allPaths.Count}");
    }

    private void AddSplitPaths(List<int> path, int splitIndex)
    {
        List<int> firstPart = path.GetRange(0, splitIndex + 1);
        List<int> secondPart = path.GetRange(splitIndex, path.Count - splitIndex);

        if (firstPart.Count > 1)
        {
            int newPathId = nextPathId++;
            allPaths[newPathId] = firstPart;
            manager.NodeManager.SetMultipleNodesPath(firstPart, true);
            Debug.Log($"Added first part (ID: {newPathId}): {string.Join(", ", firstPart)}");
        }

        if (secondPart.Count > 1)
        {
            int newPathId = nextPathId++;
            allPaths[newPathId] = secondPart;
            manager.NodeManager.SetMultipleNodesPath(secondPart, true);
            Debug.Log($"Added second part (ID: {newPathId}): {string.Join(", ", secondPart)}");
        }
    }

    public void JoinPathAt(int joinNode)
    {
        var pathsToJoin = allPaths.Where(kvp => kvp.Value.Contains(joinNode)).ToList();
        if (pathsToJoin.Count == 2)
        {
            JoinPath(pathsToJoin, joinNode);
        }
        else
        {
            Debug.LogWarning($"Cannot join paths: Found {pathsToJoin.Count} paths at node {joinNode}");
        }
    }

    private void JoinPath(List<KeyValuePair<int, List<int>>> pathsToJoin, int joinNode)
    {
        List<int> path1 = pathsToJoin[0].Value;
        List<int> path2 = pathsToJoin[1].Value;

        // Identify start and end flags
        int startFlag = path1.FirstOrDefault(n => manager.NodeManager.GetNodeData(n).HasFlag && n != joinNode);
        int endFlag = path2.LastOrDefault(n => manager.NodeManager.GetNodeData(n).HasFlag && n != joinNode);

        if (startFlag == 0 || endFlag == 0)
        {
            Debug.LogError($"JoinPath failed: Could not identify start ({startFlag}) or end ({endFlag}) flags.");
            return;
        }

        Debug.Log($"Joining paths at {joinNode}. Start flag: {startFlag}, End flag: {endFlag}");

        // Clear HasPath flags from old paths (except flags)
        foreach (int node in path1.Concat(path2))
        {
            NodeData nodeData = manager.NodeManager.GetNodeData(node);
            if (!nodeData.HasFlag) nodeData.HasPath = false;
        }

        // Recompute path between start and end flags through joinNode
        List<int> firstSegment = FindPath(startFlag, joinNode);
        List<int> secondSegment = FindPath(joinNode, endFlag);

        if (firstSegment == null || secondSegment == null)
        {
            Debug.LogWarning($"Cannot join paths: No valid path from {startFlag} to {joinNode} or {joinNode} to {endFlag}");
            return;
        }

        List<int> joinedPath = new List<int>(firstSegment);
        joinedPath.AddRange(secondSegment.Skip(1)); // Skip joinNode duplicate

        if (IsValidPath(joinedPath))
        {
            RemovePathById(pathsToJoin[0].Key);
            RemovePathById(pathsToJoin[1].Key);
            int newPathId = nextPathId++;
            allPaths[newPathId] = joinedPath;
            manager.NodeManager.SetMultipleNodesPath(joinedPath, true);
            RefreshPathVisuals();
            manager.UIManager.UpdateUIText("Paths", $"Paths: {allPaths.Count}");
            Debug.Log($"Joined path (ID: {newPathId}): {string.Join(", ", joinedPath)}");
        }
        else
        {
            Debug.LogWarning("Cannot join paths: Resulting path is invalid");
        }
    }

    public void RefreshPathVisuals()
    {
        Debug.Log("Refreshing all path visuals");
        // Clear existing path visuals (assuming PathGenerator manages this)
        manager.PathVisualsGenerator.ClearPathVisuals();
        foreach (var path in allPaths.Values)
        {
            manager.PathVisualsGenerator.DrawPath(path);
        }
    }

    private float GetCost(int startVertexIndex, int endVertexIndex)
    {
        float baseCost = 1f; // Base cost for moving to an adjacent cell

        Vector3 startPos = manager.globalVertices[startVertexIndex];
        Vector3 endPos = manager.globalVertices[endVertexIndex];

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
        return Vector3.Distance(manager.globalVertices[current], manager.globalVertices[target]); // Euclidean distance heuristic
    }

    private List<int> ReconstructPath(Dictionary<int, int> cameFrom, int endVertexIndex)
    {
        List<int> path = new List<int>();
        int current = endVertexIndex;
        while (current != -1)
        {
            path.Add(current);
            current = cameFrom[current];
        }
        path.Reverse(); // Reverse to get path from start to end
        return path;
    }
}

// Optimized Priority Queue implementation with counter-based unique keys
public class PriorityQueue<T>
{
    private readonly List<(float priority, int counter, T item)> heap = new List<(float, int, T)>();
    private int counter = 0;

    public int Count => heap.Count;

    public void Enqueue(T item, float priority)
    {
        heap.Add((priority, counter++, item));
        HeapifyUp(heap.Count - 1);
    }

    public T Dequeue()
    {
        if (Count == 0) throw new InvalidOperationException("PriorityQueue is empty");

        T item = heap[0].item;
        heap[0] = heap[Count - 1];
        heap.RemoveAt(Count - 1);

        if (Count > 0) HeapifyDown(0);

        return item;
    }

    public bool IsEmpty() => Count == 0;

    public void Clear()
    {
        heap.Clear();
        counter = 0;
    }

    private void HeapifyUp(int index)
    {
        while (index > 0)
        {
            int parent = (index - 1) / 2;
            if (Compare(heap[parent], heap[index]) <= 0) break;

            Swap(parent, index);
            index = parent;
        }
    }

    private void HeapifyDown(int index)
    {
        int minIndex = index;
        int leftChild = 2 * index + 1;
        int rightChild = 2 * index + 2;

        if (leftChild < Count && Compare(heap[leftChild], heap[minIndex]) < 0)
            minIndex = leftChild;
        if (rightChild < Count && Compare(heap[rightChild], heap[minIndex]) < 0)
            minIndex = rightChild;

        if (minIndex != index)
        {
            Swap(index, minIndex);
            HeapifyDown(minIndex);
        }
    }

    private void Swap(int i, int j)
    {
        var temp = heap[i];
        heap[i] = heap[j];
        heap[j] = temp;
    }

    private int Compare((float priority, int counter, T item) a, (float priority, int counter, T item) b)
    {
        int cmp = a.priority.CompareTo(b.priority);
        return cmp != 0 ? cmp : a.counter.CompareTo(b.counter); // Use counter for tie-breaking
    }
}