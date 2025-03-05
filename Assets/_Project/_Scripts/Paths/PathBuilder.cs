using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PathBuilder
{
    private PathManager pathManager;

    private List<int> currentPath = new();
    public List<int> CurrentPath => currentPath;

    public PathBuilder(PathManager pathManager)
    {
        this.pathManager = pathManager;
    }

    public void CreatePath(int startNode)
    {
        if (startNode == -1) return;

        pathManager.IsInPathCreationMode = true;
        currentPath?.Clear();
        currentPath.Add(startNode);
    }

    public void ExtendPath(int endNode)
    {
        if (!CanExtendPath(endNode)) return;

        pathManager.Manager.NodeManager.endPathVertexIndex = endNode;
        int currentStartNode = currentPath.Last();
        pathManager.Manager.NodeManager.startPathVertexIndex = currentStartNode;
        List<int> pathSegment = pathManager.FindPath(currentStartNode, endNode);

        if (pathSegment == null || !CanCreatePath(pathSegment.ToArray()))
        {
            Debug.LogWarning($"[PathManager] No initial path from {currentStartNode} to {endNode}");
            CancelPath();
            return;
        }

        List<int> tempPath = new List<int>(currentPath);
        tempPath.RemoveAt(tempPath.Count - 1); // Remove currentStartNode to avoid double-counting
        tempPath.AddRange(pathSegment);

        if (IsValidTemporaryPath(tempPath))
        {
            currentPath = tempPath;
            VisualizeTempPath();
            Debug.Log($"[PathManager] Extended path to {endNode}: {string.Join(", ", currentPath)}");
        }
        else
        {
            // Overlap detected, find alternate route
            HashSet<int> excludedNodes = GetAllExcludedNodes(currentPath); // Exclude all currentPath nodes
            excludedNodes.Remove(currentStartNode); // Allow currentStartNode for connection
            Debug.Log($"[PathManager] Overlap detected in path {string.Join(", ", tempPath)}, excluded nodes: {string.Join(", ", excludedNodes)}");

            List<int> alternatePath = FindPathExcludingNodes(currentStartNode, endNode, excludedNodes);
            if (alternatePath != null)
            {
                tempPath = new List<int>(currentPath);
                tempPath.RemoveAt(tempPath.Count - 1);
                tempPath.AddRange(alternatePath);
                if (IsValidTemporaryPath(tempPath))
                {
                    currentPath = tempPath;
                    VisualizeTempPath();
                    Debug.Log($"[PathManager] Found alternate path to {endNode}: {string.Join(", ", currentPath)}");
                }
                else
                {
                    Debug.LogWarning($"[PathManager] Alternate path {string.Join(", ", tempPath)} still invalid");
                    CancelPath();
                }
            }
            else
            {
                Debug.LogWarning($"[PathManager] No alternate path found from {currentStartNode} to {endNode} avoiding {string.Join(", ", excludedNodes)}");
                CancelPath();
            }
        }
    }

    private HashSet<int> GetAllExcludedNodes(List<int> currentPath)
    {
        HashSet<int> excludedNodes = new HashSet<int>(currentPath);
        foreach (var path in pathManager.allPaths.Values)
        {
            foreach (int node in path.Nodes)
            {
                excludedNodes.Add(node); // Exclude all nodes, flag or not
            }
        }
        return excludedNodes;
    }

    private List<int> FindPathExcludingNodes(int startNode, int endNode, HashSet<int> excludedNodes)
    {
        Queue<int> toVisit = new Queue<int>();
        HashSet<int> visited = new HashSet<int>();
        Dictionary<int, int> cameFrom = new Dictionary<int, int>();
        toVisit.Enqueue(startNode);
        visited.Add(startNode);
        cameFrom[startNode] = -1;

        while (toVisit.Count > 0)
        {
            int currentNode = toVisit.Dequeue();
            if (currentNode == endNode)
            {
                return pathManager.PathFinder.ReconstructPath(cameFrom, endNode);
            }

            foreach (var path in pathManager.allPaths.Values)
            {
                int index = path.Nodes.IndexOf(currentNode);
                if (index != -1)
                {
                    if (index > 0)
                    {
                        int prevNode = path.Nodes[index - 1];
                        if (!visited.Contains(prevNode) && !excludedNodes.Contains(prevNode))
                        {
                            visited.Add(prevNode);
                            toVisit.Enqueue(prevNode);
                            cameFrom[prevNode] = currentNode;
                        }
                    }
                    if (index < path.Nodes.Count - 1)
                    {
                        int nextNode = path.Nodes[index + 1];
                        if (!visited.Contains(nextNode) && !excludedNodes.Contains(nextNode))
                        {
                            visited.Add(nextNode);
                            toVisit.Enqueue(nextNode);
                            cameFrom[nextNode] = currentNode;
                        }
                    }
                }
            }

            // Fallback to grid neighbors if needed
            foreach (int neighbor in pathManager.Manager.NodeManager.GetNodeNieghbors(currentNode))
            {
                NodeData neighborData = pathManager.Manager.NodeManager.GetNodeData(neighbor);
                if (!visited.Contains(neighbor) && !excludedNodes.Contains(neighbor) && neighborData != null && !neighborData.HasObstacle)
                {
                    visited.Add(neighbor);
                    toVisit.Enqueue(neighbor);
                    cameFrom[neighbor] = currentNode;
                }
            }
        }
        Debug.LogWarning($"[PathManager] No path found from {startNode} to {endNode} excluding {string.Join(", ", excludedNodes)}");
        return null; // No path found
    }

    private bool CanExtendPath(int endNode)
    {
        NodeData endNodeData = pathManager.Manager.NodeManager.GetNodeData(endNode);
        if (!pathManager.Manager.NodeManager.IsNodeValidForPath(endNode))
        {
            Debug.Log("Cannot extend path: Cell already in an existing path.");
            return false;
        }
        Debug.Log($"Can extend path to node {endNode}");
        return true;
    }

    private bool CanCreatePath(int[] points)
    {
        if (points == null || points.Length < 2)
        {
            return false;
        }
        return true;
    }

    public void FinalisePath(int endNode)
    {
        int currentStartCell = currentPath.Last();
        List<int> pathSegment = GeneratePathSegment(currentStartCell, endNode);
        if (pathSegment == null)
        {
            foreach (int node in currentPath)
            {
                pathManager.Manager.NodeManager.SetNodePath(node, false);
            }
            pathManager.CancelPathCreation();
            return;
        }

        currentPath.Remove(currentStartCell);
        currentPath.AddRange(pathSegment);
        if (!currentPath.Contains(endNode))
        {
            currentPath.Add(endNode);
        }

        if (IsValidTemporaryPath(currentPath))
        {
            SaveAndVisualiseFinalPath();
            pathManager.AssignCarriersToPaths(); // Reassign carriers to all connected paths
        }
        else
        {
            Debug.Log("Path creation failed: Invalid path");
        }
        CancelPath();
    }

    private List<int> GeneratePathSegment(int startNode, int endNode)
    {
        List<int> nodeIndices = pathManager.FindPath(startNode, endNode);
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
        Path newPath = new Path(pathManager.nextPathId, currentPath, pathManager.Manager);
        if (!newPath.IsValid(pathManager.Manager)) // Final check with strict rules
        {
            Debug.LogWarning($"Path ID {pathManager.nextPathId} rejected: Invalid final path");
            return;
        }

        int pathId = pathManager.nextPathId++;
        pathManager.allPaths[pathId] = newPath;
        foreach (int node in newPath.Nodes)
        {
            NodeData nodeData = pathManager.Manager.NodeManager.GetNodeData(node);
            nodeData.HasPath = true;
        }

        pathManager.Manager.PathVisualsGenerator.DrawPath(newPath.Nodes);
        pathManager.Manager.UIManager.UpdateUIText("Paths", $"Paths: {pathManager.allPaths.Count}");
    }

    private bool IsValidTemporaryPath(List<int> path)
    {
        if (path == null || path.Count < 2)
        {
            Debug.LogWarning("[PathManager] Temporary path invalid: Too few nodes");
            return false;
        }

        HashSet<int> seenNodes = new HashSet<int>();
        for (int i = 0; i < path.Count; i++)
        {
            int node = path[i];
            NodeData nodeData = pathManager.Manager.NodeManager.GetNodeData(node);

            // Check for self-overlap within tempPath
            if (seenNodes.Contains(node) && i != 0 && i != path.Count - 1)
            {
                Debug.LogWarning($"[PathManager] Temporary path invalid: Node {node} overlaps within path at index {i}");
                return false;
            }

            // Check for overlap with other paths in allPaths
            foreach (var existingPath in pathManager.allPaths.Values)
            {
                if (existingPath.Nodes.Contains(node)) // Non-flag node
                {
                    int existingIndex = existingPath.Nodes.IndexOf(node);
                    if (existingIndex != 0 && existingIndex != existingPath.Nodes.Count - 1) // Intermediate node
                    {
                        // Allow if this is the start or end node of the tempPath being extended
                        if (i != 0 && i != path.Count - 1)
                        {
                            Debug.LogWarning($"[PathManager] Temporary path invalid: Node {node} overlaps with existing path ID {existingPath.Id} at index {existingIndex}");
                            return false;
                        }
                    }
                }
            }

            seenNodes.Add(node);
        }
        return true;
    }

    private void VisualizeTempPath()
    {
        foreach (int node in currentPath)
        {
            GameObject tempNode = GameObject.Instantiate(pathManager.TempPathPrefab, pathManager.Manager.globalVertices[node], Quaternion.identity);
            tempNode.transform.SetParent(pathManager.Manager.tempPathTransform);
        }
    }

    private void ClearTempPathVisuals()
    {
        foreach (Transform child in pathManager.Manager.tempPathTransform)
        {
            GameObject.Destroy(child.gameObject);
        }
    }

    public void CancelPath()
    {
        currentPath ??= new List<int>();
        pathManager.Manager.UIManager.HideAllPanels();
        ClearTempPathVisuals();
        if (pathManager.Manager.NodeManager.heldVertexIndex != -1) pathManager.Manager.NodeManager.heldVertexIndex = -1;
        if (pathManager.Manager.NodeManager.startPathVertexIndex != -1) pathManager.Manager.NodeManager.startPathVertexIndex = -1;
        if (pathManager.Manager.NodeManager.endPathVertexIndex != -1) pathManager.Manager.NodeManager.endPathVertexIndex = -1;
        pathManager.IsInPathCreationMode = false;
    }
}
