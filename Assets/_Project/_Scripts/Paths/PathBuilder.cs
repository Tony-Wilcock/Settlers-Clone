using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PathBuilder
{
    private readonly PathManager pathManager;
    private readonly List<int> currentPath = new List<int>();
    public List<int> CurrentPath => currentPath;

    public PathBuilder(PathManager pathManager)
    {
        this.pathManager = pathManager;
    }

    public void CreatePath(int startNode)
    {
        if (startNode == -1) return;

        pathManager.IsInPathCreationMode = true;
        currentPath.Clear();
        currentPath.Add(startNode);
    }

    public void ExtendPath(int endNode)
    {
        if (!CanExtendPath(endNode)) return;

        int currentStartNode = currentPath.Last();
        pathManager.Manager.NodeManager.startPathVertexIndex = currentStartNode;
        pathManager.Manager.NodeManager.endPathVertexIndex = endNode;

        List<int> pathSegment = pathManager.FindPath(currentStartNode, endNode);
        if (pathSegment == null)
        {
            CancelPath();
            return;
        }

        List<int> tempPath = new List<int>(currentPath);
        tempPath.RemoveAt(tempPath.Count - 1);
        tempPath.AddRange(pathSegment);

        if (IsValidTemporaryPath(tempPath))
        {
            currentPath.Clear();
            currentPath.AddRange(tempPath);
            VisualizeTempPath();
        }
        else
        {
            HashSet<int> excludedNodes = GetExcludedNodes();
            List<int> alternatePath = pathManager.PathFinder.FindPathThroughPaths(currentStartNode, endNode) ?? FindPathExcludingNodes(currentStartNode, endNode, excludedNodes);
            if (alternatePath != null)
            {
                tempPath = new List<int>(currentPath);
                tempPath.RemoveAt(tempPath.Count - 1);
                tempPath.AddRange(alternatePath);
                if (IsValidTemporaryPath(tempPath))
                {
                    currentPath.Clear();
                    currentPath.AddRange(tempPath);
                    VisualizeTempPath();
                }
                else
                {
                    CancelPath();
                }
            }
            else
            {
                CancelPath();
            }
        }
    }

    private HashSet<int> GetExcludedNodes()
    {
        var excluded = new HashSet<int>(currentPath);
        foreach (var path in pathManager.AllPaths.Values)
        {
            excluded.UnionWith(path.Nodes);
        }
        return excluded;
    }

    private List<int> FindPathExcludingNodes(int startNode, int endNode, HashSet<int> excludedNodes)
    {
        var toVisit = new Queue<int>();
        var visited = new HashSet<int>();
        var cameFrom = new Dictionary<int, int>();
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
        return null;
    }

    private bool CanExtendPath(int endNode)
    {
        return pathManager.Manager.NodeManager.IsNodeValidForPath(endNode);
    }

    public void FinalisePath(int endNode)
    {
        int currentStartCell = currentPath.Last();
        List<int> pathSegment = pathManager.FindPath(currentStartCell, endNode);
        if (pathSegment == null)
        {
            CancelPath();
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
            pathManager.AssignCarriersToPaths();
        }
        CancelPath();
    }

    private void SaveAndVisualiseFinalPath()
    {
        Path newPath = new Path(pathManager.NextPathId, currentPath, pathManager.Manager);
        if (!newPath.IsValid(pathManager.Manager)) return;

        int pathId = pathManager.NextPathId++;
        pathManager.AllPaths[pathId] = newPath;
        foreach (int node in newPath.Nodes)
        {
            pathManager.Manager.NodeManager.GetNodeData(node).HasPath = true;
        }
        pathManager.Manager.PathVisualsGenerator.DrawPath(newPath.Nodes);
        pathManager.Manager.UIManager.UpdateUIText("Paths", $"Paths: {pathManager.AllPaths.Count}");
        pathManager.Manager.UIManager.HideAllPanels();
    }

    private bool IsValidTemporaryPath(List<int> path)
    {
        if (path == null || path.Count < 2) return false;

        var seenNodes = new HashSet<int>();
        for (int i = 0; i < path.Count; i++)
        {
            int node = path[i];
            if (seenNodes.Contains(node) && i != 0 && i != path.Count - 1) continue;

            foreach (var existingPath in pathManager.AllPaths.Values)
            {
                int index = existingPath.Nodes.IndexOf(node);
                if (index != -1 && index != 0 && index != existingPath.Nodes.Count - 1 && i != 0 && i != path.Count - 1)
                {
                    return false;
                }
            }
            seenNodes.Add(node);
        }
        return true;
    }

    private void VisualizeTempPath()
    {
        ClearTempPathVisuals();
        foreach (int node in currentPath)
        {
            GameObject tempNode = Object.Instantiate(pathManager.TempPathPrefab, pathManager.Manager.globalVertices[node], Quaternion.identity);
            tempNode.transform.SetParent(pathManager.Manager.tempPathTransform);
        }
    }

    private void ClearTempPathVisuals()
    {
        foreach (Transform child in pathManager.Manager.tempPathTransform)
        {
            Object.Destroy(child.gameObject);
        }
    }

    public void CancelPath()
    {
        currentPath.Clear();
        ClearTempPathVisuals();
        pathManager.Manager.UIManager.HideAllPanels();
        pathManager.Manager.NodeManager.heldVertexIndex = -1;
        pathManager.Manager.NodeManager.startPathVertexIndex = -1;
        pathManager.Manager.NodeManager.endPathVertexIndex = -1;
        pathManager.IsInPathCreationMode = false;
    }
}