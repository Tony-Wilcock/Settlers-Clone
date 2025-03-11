using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;
using static NodeTypes;

public class PathManager : MonoBehaviour
{
    [field: SerializeField] public GameObject TempPathPrefab { get; private set; }

    private HexGridManager manager;
    private PathBuilder pathBuilder;
    private PathFinder pathFinder;
    private WorkerManager workerManager;
    private BuildingManager buildingManager;

    public HexGridManager Manager => manager;
    public PathBuilder PathBuilder => pathBuilder;
    public PathFinder PathFinder => pathFinder;
    public bool IsInPathCreationMode { get; set; }

    public Dictionary<int, Carrier> PathCarriers { get; set; } = new Dictionary<int, Carrier>();
    public Dictionary<int, Path> AllPaths { get; } = new Dictionary<int, Path>();
    private Dictionary<int, Queue<(StockResourceType resource, int amount)>> flagResourceQueues = new Dictionary<int, Queue<(StockResourceType, int)>>();
    public IReadOnlyDictionary<int, Path> GetAllPaths => new ReadOnlyDictionary<int, Path>(AllPaths);
    public int NextPathId { get; set; }

    private int cachedHQNode = -1;
    private int cachedHQEntranceNode = -1;
    private HashSet<int> cachedConnectedToStorehouse = new HashSet<int>();

    public void Initialise(HexGridManager manager)
    {
        this.manager = manager;
        workerManager = manager.WorkerManager;
        buildingManager = manager.BuildingManager;
        IsInPathCreationMode = false;
        pathBuilder = new PathBuilder(this);
        pathFinder = new PathFinder(this);

        // Cache HQ nodes on initialization
        cachedHQNode = GetHQNodeInternal();
        cachedHQEntranceNode = GetHQEntranceNodeInternal();
        RefreshStorehouseConnectivity();
    }

    public int GetPathId(int vertexIndex)
    {
        foreach (var path in AllPaths.Values)
        {
            if (path.Nodes.Contains(vertexIndex)) return path.Id;
        }
        return -1;
    }

    public Path GetPathById(int pathId) => AllPaths.TryGetValue(pathId, out Path path) ? path : null;

    public void StartPathPlacement()// Called from the UI
    {
        int startNode = manager.NodeManager.heldVertexIndex;
        if (startNode == -1) return;

        NodeData startNodeData = manager.NodeManager.GetNodeData(startNode);
        if (startNodeData.HasFlag || startNodeData.HasBuilding)
        {
            pathBuilder.CreatePath(startNode);
        }
    }

    public void TryAddPathToEndNode(int endNode)
    {
        if (!IsInPathCreationMode || endNode == -1) return;

        NodeData endNodeData = manager.NodeManager.GetNodeData(endNode);
        if (endNodeData == null) return;

        if (endNodeData.HasFlag)
        {
            pathBuilder.FinalisePath(endNode);
        }
        else
        {
            pathBuilder.ExtendPath(endNode);
        }
    }

    public void AssignCarriersToPaths()
    {
        foreach (var pathEntry in AllPaths)
        {
            if (!PathCarriers.ContainsKey(pathEntry.Key))
            {
                AssignCarrierToPath(pathEntry.Key);
            }
        }
    }

    private void AssignCarrierToPath(int pathId)
    {
        Path path = AllPaths[pathId];
        if (!IsConnectedToStorehouse(path.StartFlag) && !IsConnectedToStorehouse(path.EndFlag)) return;

        int hqNode = GetHQNode();
        if (hqNode == -1) return;
        int hqEntranceNode = GetHQEntranceNode();
        if (hqEntranceNode == -1) return;

        Carrier carrier = (Carrier)workerManager.GetWorker(CharacterType.Carrier, hqNode);
        if (carrier == null) return;

        List<int> pathToMidpoint = pathFinder.FindPathThroughPaths(hqEntranceNode, path.Midpoint);
        if (pathToMidpoint != null)
        {
            carrier.MoveToPathMidpoint(pathId, hqEntranceNode, path.StartFlag, path.EndFlag, pathToMidpoint, path.Midpoint);
            PathCarriers[pathId] = carrier;
        }
        else
        {
            workerManager.ReturnWorker(carrier);
        }
    }

    public int GetHQNode() => cachedHQNode != -1 ? cachedHQNode : (cachedHQNode = GetHQNodeInternal());
    public int GetHQEntranceNode() => cachedHQEntranceNode != -1 ? cachedHQEntranceNode : (cachedHQEntranceNode = GetHQEntranceNodeInternal());

    private int GetHQNodeInternal()
    {
        return buildingManager.AllBuildings.TryGetValue(BuildingType.HQ, out var hqList) && hqList.Count > 0 ? hqList[0].CentralNode : -1;
    }

    private int GetHQEntranceNodeInternal()
    {
        return buildingManager.AllBuildings.TryGetValue(BuildingType.HQ, out var hqList) && hqList.Count > 0 ? hqList[0].EntranceNode : -1;
    }

    public bool IsConnectedToStorehouse(int flagNode)
    {
        if (cachedConnectedToStorehouse.Contains(flagNode)) return true;

        Queue<int> toVisit = new Queue<int>();
        HashSet<int> visited = new HashSet<int>();
        toVisit.Enqueue(flagNode);

        while (toVisit.Count > 0)
        {
            int node = toVisit.Dequeue();
            if (visited.Contains(node)) continue;
            visited.Add(node);

            int northwestNeighbour = manager.NodeManager.GetNeighborInDirection(node, Direction.Northwest);
            NodeData nodeData = manager.NodeManager.GetNodeData(northwestNeighbour);
            if (nodeData.HasBuilding && (nodeData.BuildingType == BuildingType.HQ || nodeData.BuildingType == BuildingType.Storehouse))
            {
                Building building = buildingManager.GetBuildingFromNode(nodeData.BuildingID);
                if (building != null && building.IsConstructed)
                {
                    cachedConnectedToStorehouse.Add(flagNode);
                    return true;
                }
            }

            foreach (var path in AllPaths.Values)
            {
                if (path.Nodes.Contains(node))
                {
                    foreach (int nextNode in path.Nodes)
                    {
                        if (!visited.Contains(nextNode)) toVisit.Enqueue(nextNode);
                    }
                }
            }
        }
        return false;
    }

    private void RefreshStorehouseConnectivity()
    {
        cachedConnectedToStorehouse.Clear();
        foreach (var path in AllPaths.Values)
        {
            if (IsConnectedToStorehouse(path.StartFlag)) cachedConnectedToStorehouse.Add(path.StartFlag);
            if (IsConnectedToStorehouse(path.EndFlag)) cachedConnectedToStorehouse.Add(path.EndFlag);
        }
    }

    public void AddResourceToQueue(int flagId, StockResourceType resource, int amount)
    {
        if (!flagResourceQueues.TryGetValue(flagId, out var queue))
        {
            queue = new Queue<(StockResourceType, int)>();
            flagResourceQueues[flagId] = queue;
        }
        queue.Enqueue((resource, amount));

        foreach (var entry in PathCarriers)
        {
            Path path = AllPaths[entry.Key];
            if (path.StartFlag == flagId || path.EndFlag == flagId)
            {
                entry.Value.NotifyResourceAdded(flagId, resource, amount);
            }
        }
    }

    public bool TryGetResource(int flagId, out StockResourceType resource, out int amount)
    {
        if (flagResourceQueues.TryGetValue(flagId, out var queue) && queue.Count > 0)
        {
            var res = queue.Dequeue();
            resource = res.resource;
            amount = res.amount;
            return true;
        }
        resource = StockResourceType.None;
        amount = 0;
        return false;
    }

    public List<int> FindPath(int startVertexIndex, int endVertexIndex) => pathFinder.FindPath(startVertexIndex, endVertexIndex);

    public void CancelPathCreation() => pathBuilder.CancelPath();

    public void RemovePath()
    {
        int node = manager.NodeManager.heldVertexIndex;
        var pathsToRemove = AllPaths.Where(kvp => kvp.Value.Nodes.Contains(node)).Select(kvp => kvp.Key).ToList();
        foreach (int pathId in pathsToRemove)
        {
            RemovePathById(pathId);
        }
        RefreshPathVisuals();
        UpdateUI();
    }

    public void RemovePathById(int pathId)
    {
        if (!AllPaths.TryGetValue(pathId, out Path path)) return;

        foreach (int node in path.Nodes)
        {
            if (manager.NodeManager.NumberOfPathsAttachedToNode(node) <= 1)
            {
                manager.NodeManager.GetNodeData(node).HasPath = false;
            }
        }
        if (PathCarriers.TryGetValue(pathId, out Carrier carrier))
        {
            ReturnCarrierToHQ(carrier);
            PathCarriers.Remove(pathId);
        }
        AllPaths.Remove(pathId);
        RefreshStorehouseConnectivity();
    }

    public void SplitPathAt(int splitNode)
    {
        var pathsToSplit = AllPaths.Where(kvp => kvp.Value.ContainsNode(splitNode)).ToList();
        foreach (var kvp in pathsToSplit)
        {
            SplitPath(kvp.Key, splitNode);
        }
        RefreshPathVisuals();
        UpdateUI();
    }

    private void SplitPath(int pathId, int splitNode)
    {
        Path path = AllPaths[pathId];
        int splitIndex = path.Nodes.IndexOf(splitNode);
        if (splitIndex < 0) return;

        Carrier originalCarrier = PathCarriers.ContainsKey(pathId) ? PathCarriers[pathId] : null;
        PathCarriers.Remove(pathId);

        List<int> firstPart = path.Nodes.GetRange(0, splitIndex + 1);
        List<int> secondPart = path.Nodes.GetRange(splitIndex, path.Nodes.Count - splitIndex);

        if (firstPart.Count > 1)
        {
            CreateSplitPath(firstPart, originalCarrier);
        }
        if (secondPart.Count > 1)
        {
            CreateSplitPath(secondPart, null);
        }
        RemovePathById(pathId);
    }

    private void CreateSplitPath(List<int> nodes, Carrier carrier)
    {
        int newPathId = NextPathId++;
        Path newPath = new Path(newPathId, nodes, Manager);
        if (newPath.IsValid(manager))
        {
            AllPaths[newPathId] = newPath;
            UpdateNodePathStatus(newPath.Nodes, true);
            if (carrier != null)
            {
                AssignCarrierToNewPath(newPathId, carrier, carrier.CurrentNode);
            }
            else
            {
                AssignCarrierToPath(newPathId);
            }
        }
    }

    public void JoinPathAt(int joinNode)
    {
        var pathsToJoin = AllPaths.Where(kvp => kvp.Value.ContainsNode(joinNode)).ToList();
        if (pathsToJoin.Count == 2)
        {
            JoinPath(pathsToJoin, joinNode);
            RefreshPathVisuals();
            UpdateUI();
        }
    }

    private void JoinPath(List<KeyValuePair<int, Path>> pathsToJoin, int joinNode)
    {
        Path path1 = pathsToJoin[0].Value;
        Path path2 = pathsToJoin[1].Value;
        List<Carrier> carriers = new List<Carrier>();
        if (PathCarriers.ContainsKey(pathsToJoin[0].Key)) carriers.Add(PathCarriers[pathsToJoin[0].Key]);
        if (PathCarriers.ContainsKey(pathsToJoin[1].Key)) carriers.Add(PathCarriers[pathsToJoin[1].Key]);

        List<int> joinedNodes = CombinePaths(path1, path2, joinNode);
        if (joinedNodes == null) return;

        int newPathId = NextPathId++;
        Path joinedPath = new Path(newPathId, joinedNodes, Manager);
        if (joinedPath.IsValid(manager))
        {
            RemovePathById(pathsToJoin[0].Key);
            RemovePathById(pathsToJoin[1].Key);
            AllPaths[newPathId] = joinedPath;
            UpdateNodePathStatus(joinedNodes, true);

            Carrier selectedCarrier = carriers.FirstOrDefault();
            if (selectedCarrier != null)
            {
                AssignCarrierToNewPath(newPathId, selectedCarrier, selectedCarrier.CurrentNode);
            }
            foreach (var extraCarrier in carriers.Skip(1))
            {
                ReturnCarrierToHQ(extraCarrier);
            }
        }
    }

    // Add this method inside the TerrainPainter class
    private void AssignCarrierToNewPath(int pathId, Carrier carrier, int startNode)
    {
        Path path = AllPaths[pathId];
        List<int> pathToMidpoint = pathFinder.FindPathThroughPaths(startNode, path.Midpoint);
        if (pathToMidpoint != null)
        {
            carrier.MoveToPathMidpoint(pathId, startNode, path.StartFlag, path.EndFlag, pathToMidpoint, path.Midpoint);
            PathCarriers[pathId] = carrier;
        }
        else
        {
            ReturnCarrierToHQ(carrier);
        }
    }

    private List<int> CombinePaths(Path path1, Path path2, int joinNode)
    {
        int path1JoinIndex = path1.Nodes.IndexOf(joinNode);
        int path2JoinIndex = path2.Nodes.IndexOf(joinNode);
        bool path1StartsAtJoin = path1JoinIndex == 0;
        bool path1EndsAtJoin = path1JoinIndex == path1.Nodes.Count - 1;
        bool path2StartsAtJoin = path2JoinIndex == 0;
        bool path2EndsAtJoin = path2JoinIndex == path2.Nodes.Count - 1;

        List<int> joinedNodes = new List<int>();
        if (path1EndsAtJoin && path2StartsAtJoin)
        {
            joinedNodes.AddRange(path1.Nodes);
            joinedNodes.AddRange(path2.Nodes.Skip(1));
        }
        else if (path2EndsAtJoin && path1StartsAtJoin)
        {
            joinedNodes.AddRange(path2.Nodes);
            joinedNodes.AddRange(path1.Nodes.Skip(1));
        }
        else if (path1StartsAtJoin && path2StartsAtJoin)
        {
            joinedNodes.Add(joinNode);
            joinedNodes.AddRange(path1.Nodes.Skip(1));
            joinedNodes.Add(joinNode);
            joinedNodes.AddRange(path2.Nodes.Skip(1));
        }
        else if (path1EndsAtJoin && path2EndsAtJoin && path1.Nodes[0] == path2.Nodes[0])
        {
            joinedNodes.AddRange(path1.Nodes);
            List<int> reversedPath2 = new List<int>(path2.Nodes);
            reversedPath2.Reverse();
            joinedNodes.AddRange(reversedPath2.Skip(1));
            if (joinedNodes.Last() != path1.Nodes[0]) joinedNodes.Add(path1.Nodes[0]);
        }
        else
        {
            return null;
        }
        return joinedNodes;
    }

    public void ReturnCarrierToHQ(Carrier carrier)
    {
        int hqEntranceNode = GetHQEntranceNode();
        if (hqEntranceNode == -1)
        {
            workerManager.ReturnWorker(carrier);
            return;
        }

        List<int> pathToEntrance = pathFinder.FindPathThroughPaths(carrier.CurrentNode, hqEntranceNode);
        if (pathToEntrance != null)
        {
            carrier.AssignPath(pathToEntrance, () => MoveToHQ(carrier, hqEntranceNode));
        }
        else
        {
            workerManager.ReturnWorker(carrier);
        }
    }

    private void MoveToHQ(Carrier carrier, int hqEntranceNode)
    {
        int hqNode = GetHQNode();
        List<int> pathToHQ = pathFinder.FindDirectGridPath(hqEntranceNode, hqNode) ?? new List<int> { hqEntranceNode, hqNode };
        carrier.AssignPath(pathToHQ, () => workerManager.ReturnWorker(carrier));
    }

    public int GetCentreNodeOfPath(int pathId)
    {
        if (!AllPaths.TryGetValue(pathId, out Path path)) return -1;
        List<int> nodes = path.Nodes;
        if (nodes.Count < 2) return nodes[0];

        if (nodes[0] == nodes[nodes.Count - 1])
        {
            int startNode = nodes[0];
            int farthestNode = startNode;
            float maxDistance = 0f;
            for (int i = 1; i < nodes.Count - 1; i++)
            {
                float distance = Vector3.Distance(manager.globalVertices[startNode], manager.globalVertices[nodes[i]]);
                if (distance > maxDistance)
                {
                    maxDistance = distance;
                    farthestNode = nodes[i];
                }
            }
            return farthestNode;
        }

        int midIndex = nodes.Count / 2;
        NodeData midNodeData = manager.NodeManager.GetNodeData(nodes[midIndex]);
        if (midNodeData.HasFlag && nodes.Count > 2)
        {
            int leftIndex = midIndex - 1;
            int rightIndex = midIndex + 1;
            if (leftIndex >= 0 && !manager.NodeManager.GetNodeData(nodes[leftIndex]).HasFlag) return nodes[leftIndex];
            if (rightIndex < nodes.Count && !manager.NodeManager.GetNodeData(nodes[rightIndex]).HasFlag) return nodes[rightIndex];
        }
        return nodes[midIndex];
    }

    public void RefreshPathVisuals()
    {
        manager.PathVisualsGenerator.ClearPathVisuals();
        foreach (var path in AllPaths.Values)
        {
            manager.PathVisualsGenerator.DrawPath(path.Nodes);
        }
    }

    private void UpdateNodePathStatus(List<int> nodes, bool hasPath)
    {
        foreach (int node in nodes)
        {
            manager.NodeManager.GetNodeData(node).HasPath = hasPath;
        }
    }

    private void UpdateUI() => manager.UIManager.UpdateUIText("Paths", $"Paths: {AllPaths.Count}");
}