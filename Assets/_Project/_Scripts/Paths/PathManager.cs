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
    public HexGridManager Manager => manager;
    public PathBuilder PathBuilder => pathBuilder;
    public PathFinder PathFinder => pathFinder;

    private bool isInPathCreationMode = false;
    public bool IsInPathCreationMode
    {
        get => isInPathCreationMode;
        set => isInPathCreationMode = value;
    }

    public Dictionary<int, Carrier> pathCarriers = new Dictionary<int, Carrier>();
    public Dictionary<int, Path> allPaths = new Dictionary<int, Path>();
    private Dictionary<int, Queue<(StockResourceType resource, int amount)>> flagResourceQueues = new Dictionary<int, Queue<(StockResourceType, int)>>();
    public IReadOnlyDictionary<int, Path> GetAllPaths => new ReadOnlyDictionary<int, Path>(allPaths);
    public int nextPathId = 0;

    private WorkerManager workerManager;
    private BuildingManager buildingManager;

    public void Initialise(HexGridManager manager)
    {
        this.manager = manager;
        workerManager = manager.WorkerManager;
        buildingManager = manager.BuildingManager;
        isInPathCreationMode = false;

        pathBuilder = new PathBuilder(this);
        pathFinder = new PathFinder(this);
    }

    public int GetPathId(int vertexIndex)
    {
        foreach (var path in allPaths.Values)
        {
            if (path.Nodes.Contains(vertexIndex))
            {
                return path.Id;
            }
        }
        return -1; // Return -1 if no path is found for the node
    }

    public Path GetPathById(int pathId)
    {
        return allPaths.ContainsKey(pathId) ? allPaths[pathId] : null;
    }

    public void StartPathPlacement()
    {
        int startNode = manager.NodeManager.heldVertexIndex;
        NodeData startNodeData = manager.NodeManager.GetNodeData(startNode);
        if (startNode == -1)
        {
            Debug.Log("Path creation cancelled: Invalid start node.");
            return;
        }

        if (startNodeData.HasFlag || startNodeData.HasBuilding)
        {
            pathBuilder.CreatePath(startNode);
        }
        else
        {
            Debug.Log("Path creation cancelled: Start node must have a flag or building.");
        }
    }

    public void TryAddPathToEndNode(int endNode)
    {
        if (!isInPathCreationMode || endNode == -1) return;

        NodeData endNodeData = manager.NodeManager.GetNodeData(endNode);
        if (endNodeData == null)
        {
            Debug.LogWarning("Path creation cancelled: End node invalid.");
            return;
        }

        if (endNodeData.HasFlag)
        {
            pathBuilder.FinalisePath(endNode);
        }
        else
        {
            pathBuilder.ExtendPath(endNode);
        }
    }

    public void RegisterBuildingPath(List<int> path)
    {
        if (path == null || path.Count < 2)
        {
            Debug.LogWarning("Invalid building path provided");
            return;
        }

        Path newPath = new Path(nextPathId, path, Manager);
        if (allPaths.Any(kvp => kvp.Value.StartFlag == newPath.StartFlag && kvp.Value.EndFlag == newPath.EndFlag))
        {
            Debug.LogWarning($"Path from {newPath.StartFlag} to {newPath.EndFlag} already exists");
            return;
        }

        if (newPath.IsValid(manager))
        {
            int pathId = nextPathId++;
            allPaths[pathId] = newPath;
            foreach (int node in newPath.Nodes)
            {
                NodeData nodeData = manager.NodeManager.GetNodeData(node);
                nodeData.HasPath = true;
            }
            manager.PathVisualsGenerator.DrawPath(newPath.Nodes);
            manager.UIManager.UpdateUIText("Paths", $"Paths: {allPaths.Count}");
            AssignCarriersToPaths();
        }
        else
        {
            Debug.LogWarning("Building path registration failed: Invalid path");
        }
    }

    public void AssignCarriersToPaths()
    {
        foreach (var path in allPaths)
        {
            int pathId = path.Key;
            if (!pathCarriers.ContainsKey(pathId))
            {
                AssignCarrierToPath(pathId);
            }
        }
    }

    private void AssignCarrierToPath(int pathId)
    {
        Path path = allPaths[pathId];
        if (IsConnectedToStorehouse(path.StartFlag) || IsConnectedToStorehouse(path.EndFlag))
        {
            int hqEntranceNode = GetHQEntranceNode();
            if (hqEntranceNode == -1) return;

            Carrier carrier = (Carrier)workerManager.GetWorker(CharacterType.Carrier, hqEntranceNode);
            if (carrier != null)
            {
                List<int> pathToMidpoint = pathFinder.FindPathThroughPaths(hqEntranceNode, path.Midpoint);
                if (pathToMidpoint != null)
                {
                    carrier.MoveToPathMidpoint(pathId, hqEntranceNode, path.StartFlag, path.EndFlag, pathToMidpoint, path.Midpoint);
                    pathCarriers[pathId] = carrier;
                }
                else
                {
                    workerManager.ReturnWorker(carrier); // Return if no path
                    Debug.LogWarning($"No path found from HQ {hqEntranceNode} to midpoint {path.Midpoint} for path ID {pathId}");
                }
            }
        }
    }

    public int GetHQNode()
    {
        if (buildingManager.AllBuildings.ContainsKey(BuildingType.HQ) && buildingManager.AllBuildings[BuildingType.HQ].Count > 0)
        {
            return buildingManager.AllBuildings[BuildingType.HQ][0].CentralNode;
        }
        Debug.LogWarning("No HQ found in AllBuildings");
        return -1;
    }

    private int GetHQEntranceNode()
    {
        if (buildingManager.AllBuildings.ContainsKey(BuildingType.HQ) && buildingManager.AllBuildings[BuildingType.HQ].Count > 0)
        {
            return buildingManager.AllBuildings[BuildingType.HQ][0].EntranceNode;
        }
        Debug.LogWarning("No HQ entrance found in AllBuildings");
        return -1;
    }

    public bool IsConnectedToStorehouse(int flagNode)
    {
        Queue<int> toVisit = new Queue<int>(); // Breadth-first search (BFS)
        HashSet<int> visited = new HashSet<int>(); // Visited nodes
        toVisit.Enqueue(flagNode); // Start from flag node

        while (toVisit.Count > 0) // While there are nodes to visit
        {
            int node = toVisit.Dequeue(); // Get the next node
            if (visited.Contains(node)) continue; // Skip if already visited
            visited.Add(node); // Mark as visited

            int northwestNeighbour = manager.NodeManager.GetNeighborInDirection(node, Direction.Northwest);
            NodeData nodeData = manager.NodeManager.GetNodeData(northwestNeighbour);
            if (nodeData.HasBuilding && (nodeData.BuildingType == BuildingType.HQ || nodeData.BuildingType == BuildingType.Storehouse))
            {
                return true; // Found a storehouse or HQ
            }

            foreach (var path in allPaths.Values) // Check all paths
            {
                if (path.Nodes.Contains(node)) // If the path contains the current node
                {
                    foreach (int nextNode in path.Nodes) // Check all nodes in the path
                    {
                        if (!visited.Contains(nextNode)) toVisit.Enqueue(nextNode); // Add to queue if not visited
                    }
                }
            }
        }
        return false; // No storehouse or HQ found
    }

    public void AddResourceToQueue(int flagId, StockResourceType resource, int amount)
    {
        if (!flagResourceQueues.ContainsKey(flagId))
            flagResourceQueues[flagId] = new Queue<(StockResourceType, int)>();
        flagResourceQueues[flagId].Enqueue((resource, amount));
        Debug.Log($"Added {amount} {resource} to queue at flag {flagId}");

        foreach (var entry in pathCarriers)
        {
            Path path = allPaths[entry.Key];
            if (path.StartFlag == flagId || path.EndFlag == flagId)
            {
                entry.Value.NotifyResourceAdded(flagId, resource, amount);
            }
        }
    }

    public bool TryGetResource(int flagId, out StockResourceType resource, out int amount)
    {
        if (flagResourceQueues.ContainsKey(flagId) && flagResourceQueues[flagId].Count > 0)
        {
            var res = flagResourceQueues[flagId].Dequeue();
            resource = res.Item1;
            amount = res.Item2;
            Debug.Log($"Retrieved {amount} {resource} from queue at flag {flagId}");
            return true;
        }
        resource = StockResourceType.None;
        amount = 0;
        return false;
    }

    public List<int> FindPath(int startVertexIndex, int endVertexIndex) => pathFinder.FindPath(startVertexIndex, endVertexIndex);

    // Called from UI Cancel Path button
    public void CancelPathCreation()
    {
        pathBuilder.CancelPath();
    }

    public void RemovePath()
    {
        int node = manager.NodeManager.heldVertexIndex;
        var pathsToRemove = allPaths.Where(kvp => kvp.Value.Nodes.Contains(node)).ToList();
        foreach (var kvp in pathsToRemove)
        {
            RemovePathById(kvp.Key);
        }
        RefreshPathVisuals();
        manager.UIManager.UpdateUIText("Paths", $"Paths: {allPaths.Count}");
        manager.UIManager.HideAllPanels();
    }

    public void RemovePathById(int pathId)
    {
        if (allPaths.ContainsKey(pathId))
        {
            Path path = allPaths[pathId];
            foreach (int node in path.Nodes)
            {
                NodeData nodeData = manager.NodeManager.GetNodeData(node);
                if (nodeData != null)
                {
                    // Only clear HasPath if this is the last path using the node
                    if (manager.NodeManager.NumberOfPathsAttachedToNode(node) <= 1)
                    {
                        nodeData.HasPath = false;
                    }
                }
            }
            if (pathCarriers.ContainsKey(pathId))
            {
                ReturnCarrierToHQ(pathCarriers[pathId]);
                pathCarriers.Remove(pathId);
            }
            allPaths.Remove(pathId);
        }
    }

    public void SplitPathAt(int splitNode)
    {
        var pathsToSplit = allPaths.Where(kvp => kvp.Value.ContainsNode(splitNode)).ToList();

        foreach (var kvp in pathsToSplit)
        {
            int pathId = kvp.Key;
            Path path = kvp.Value;
            int splitIndex = path.Nodes.IndexOf(splitNode);
            if (splitIndex >= 0)
            {
                Carrier originalCarrier = pathCarriers.ContainsKey(pathId) ? pathCarriers[pathId] : null;
                pathCarriers.Remove(pathId);

                List<int> firstPart = path.Nodes.GetRange(0, splitIndex + 1);
                List<int> secondPart = path.Nodes.GetRange(splitIndex, path.Nodes.Count - splitIndex);

                int firstPathId = -1;
                if (firstPart.Count > 1)
                {
                    firstPathId = nextPathId++;
                    Path firstPath = new Path(firstPathId, firstPart, Manager);
                    if (firstPath.IsValid(manager, isTemporary: false, originalPathNodes: path.Nodes))
                    {
                        allPaths[firstPathId] = firstPath;
                        manager.NodeManager.SetMultipleNodesPath(firstPart, true);

                        if (originalCarrier != null)
                        {
                            List<int> pathToMidpoint = pathFinder.FindPathThroughPaths(originalCarrier.CurrentNode, firstPath.Midpoint);
                            if (pathToMidpoint != null)
                            {
                                originalCarrier.MoveToPathMidpoint(firstPathId, originalCarrier.CurrentNode, firstPath.StartFlag, firstPath.EndFlag, pathToMidpoint, firstPath.Midpoint);
                                pathCarriers[firstPathId] = originalCarrier;
                            }
                            else
                            {
                                Debug.LogWarning($"[PathManager] No path for original carrier to midpoint {firstPath.Midpoint}, returning to HQ");
                                ReturnCarrierToHQ(originalCarrier);
                            }
                        }
                    }
                }

                int secondPathId = -1;
                if (secondPart.Count > 1)
                {
                    secondPathId = nextPathId++;
                    Path secondPath = new Path(secondPathId, secondPart, Manager);
                    if (secondPath.IsValid(manager, isTemporary: false, originalPathNodes: path.Nodes))
                    {
                        allPaths[secondPathId] = secondPath;
                        manager.NodeManager.SetMultipleNodesPath(secondPart, true);

                        int hqNode = GetHQNode();
                        if (hqNode != -1)
                        {
                            Carrier newCarrier = (Carrier)workerManager.GetWorker(CharacterType.Carrier, hqNode);
                            List<int> pathToMidpoint = pathFinder.FindPathThroughPaths(hqNode, secondPath.Midpoint);
                            if (pathToMidpoint != null)
                            {
                                newCarrier.MoveToPathMidpoint(secondPathId, hqNode, secondPath.StartFlag, secondPath.EndFlag, pathToMidpoint, secondPath.Midpoint);
                                pathCarriers[secondPathId] = newCarrier;
                            }
                            else
                            {
                                workerManager.ReturnWorker(newCarrier);
                                Debug.LogWarning($"[PathManager] No path for new carrier to midpoint {secondPath.Midpoint}");
                            }
                        }
                    }
                }

                if (firstPathId != -1 || secondPathId != -1)
                {
                    RemovePathById(pathId);
                }
            }
        }
        RefreshPathVisuals();
        manager.UIManager.UpdateUIText("Paths", $"Paths: {allPaths.Count}");
    }

    public void JoinPathAt(int joinNode)
    {
        var pathsToJoin = allPaths.Where(kvp => kvp.Value.ContainsNode(joinNode)).ToList();
        if (pathsToJoin.Count == 2)
        {
            JoinPath(pathsToJoin, joinNode);
        }
        else
        {
            Debug.LogWarning($"Cannot join paths: Found {pathsToJoin.Count} paths at node {joinNode}");
        }
    }

    private void JoinPath(List<KeyValuePair<int, Path>> pathsToJoin, int joinNode)
    {
        Path path1 = pathsToJoin[0].Value;
        Path path2 = pathsToJoin[1].Value;

        // Collect original carriers
        List<Carrier> originalCarriers = new List<Carrier>();
        if (pathCarriers.ContainsKey(pathsToJoin[0].Key)) originalCarriers.Add(pathCarriers[pathsToJoin[0].Key]);
        if (pathCarriers.ContainsKey(pathsToJoin[1].Key)) originalCarriers.Add(pathCarriers[pathsToJoin[1].Key]);

        // Determine join indices
        int path1JoinIndex = path1.Nodes.IndexOf(joinNode);
        int path2JoinIndex = path2.Nodes.IndexOf(joinNode);

        bool path1StartsAtJoin = path1JoinIndex == 0;
        bool path1EndsAtJoin = path1JoinIndex == path1.Nodes.Count - 1;
        bool path2StartsAtJoin = path2JoinIndex == 0;
        bool path2EndsAtJoin = path2JoinIndex == path2.Nodes.Count - 1;

        // Log flags for debugging
        List<int> path1Flags = path1.Nodes.Where(n => manager.NodeManager.GetNodeData(n).HasFlag).ToList();
        List<int> path2Flags = path2.Nodes.Where(n => manager.NodeManager.GetNodeData(n).HasFlag).ToList();
        List<int> joinedNodes = new List<int>();
        int newPathId = nextPathId++;

        // Case 1: One path ends at joinNode, the other starts there (end-to-start)
        if ((path1EndsAtJoin && path2StartsAtJoin) || (path2EndsAtJoin && path1StartsAtJoin))
        {
            if (path1EndsAtJoin && path2StartsAtJoin)
            {
                joinedNodes.AddRange(path1.Nodes);
                joinedNodes.AddRange(path2.Nodes.Skip(1)); // Skip joinNode to avoid duplication
            }
            else
            {
                joinedNodes.AddRange(path2.Nodes);
                joinedNodes.AddRange(path1.Nodes.Skip(1)); // Skip joinNode to avoid duplication
            }
        }
        // Case 2: Both paths start at joinNode (diverging paths)
        else if (path1StartsAtJoin && path2StartsAtJoin)
        {
            // Create a branched path by combining both paths, starting with the join node
            joinedNodes.Add(joinNode); // Start at the join node
                                       // Add the rest of Path 1 (skip joinNode)
            joinedNodes.AddRange(path1.Nodes.Skip(1));
            // Add the rest of Path 2 (skip joinNode) as a separate branch, marked with a flag or delimiter
            joinedNodes.Add(joinNode); // Return to join node for branching
            joinedNodes.AddRange(path2.Nodes.Skip(1));
        }
        // Case 3: Both paths end at joinNode with a shared flagged start
        else if (path1EndsAtJoin && path2EndsAtJoin)
        {
            int sharedStart = path1.Nodes[0];
            if (sharedStart == path2.Nodes[0] && path1Flags.Contains(sharedStart) && path2Flags.Contains(sharedStart))
            {
                joinedNodes.AddRange(path1.Nodes);
                List<int> reversedPath2 = new List<int>(path2.Nodes);
                reversedPath2.Reverse();
                joinedNodes.AddRange(reversedPath2.Skip(1));
                if (joinedNodes.Last() != sharedStart)
                {
                    joinedNodes.Add(sharedStart);
                }
            }
            else
            {
                Debug.LogWarning("[PathManager] Paths don’t share a common start flag");
                return;
            }
        }
        else
        {
            Debug.LogWarning("[PathManager] Paths do not align correctly for joining");
            return;
        }

        // Create and validate the new joined path
        Path joinedPath = new Path(newPathId, joinedNodes, Manager);
        if (joinedPath.IsValid(manager, isTemporary: false))
        {
            // Remove original paths
            RemovePathById(pathsToJoin[0].Key);
            RemovePathById(pathsToJoin[1].Key);
            allPaths[newPathId] = joinedPath;
            manager.NodeManager.SetMultipleNodesPath(joinedNodes, true);

            // Calculate midpoint (for non-branching paths, use the center; for branches, adjust logic)
            int midpoint;
            if (path1StartsAtJoin && path2StartsAtJoin)
            {
                // For diverging paths, choose a midpoint or handle branches separately
                midpoint = GetCentreNodeOfPathForDivergingPaths(joinedNodes, joinNode);
            }
            else
            {
                midpoint = GetCentreNodeOfPath(newPathId);
            }

            // Assign carrier to the new path's midpoint
            Carrier selectedCarrier = originalCarriers.FirstOrDefault();
            if (selectedCarrier != null)
            {
                List<int> pathToMidpoint = PathFinder.FindPathThroughPaths(selectedCarrier.CurrentNode, midpoint);
                if (pathToMidpoint != null)
                {
                    selectedCarrier.MoveToPathMidpoint(newPathId, selectedCarrier.CurrentNode, joinedPath.StartFlag, joinedPath.EndFlag, pathToMidpoint, midpoint);
                    pathCarriers[newPathId] = selectedCarrier;
                }
            }

            // Return extra carriers to HQ
            foreach (var extraCarrier in originalCarriers.Skip(1))
            {
                ReturnCarrierToHQ(extraCarrier);
            }

            // Update visuals and UI
            RefreshPathVisuals();
            manager.UIManager.UpdateUIText("Paths", $"Paths: {allPaths.Count}");
        }
        else
        {
            Debug.LogWarning($"[PathManager] Joined path ID {newPathId} is invalid: {string.Join(", ", joinedNodes)}");
        }
    }

    // Helper method for diverging paths (optional, implement based on your needs)
    private int GetCentreNodeOfPathForDivergingPaths(List<int> nodes, int joinNode)
    {
        // For diverging paths, you might want to return the join node or calculate a midpoint between branches
        // This is a placeholder; customize based on your grid and path structure
        return joinNode; // Default to the join node for simplicity
    }

    public void ReturnCarrierToHQ(Carrier carrier)
    {
        int hqEntranceNode = GetHQEntranceNode();
        int hqNode = GetHQNode();

        if (hqEntranceNode == -1 || hqNode == -1)
        {
            Debug.LogWarning($"[ReturnCarrierToHQ] HQ entrance ({hqEntranceNode}) or node ({hqNode}) not found, returning carrier directly to pool");
            workerManager.ReturnWorker(carrier);
            return;
        }

        HashSet<int> connectedNodes = GetNodesConnectedToHQ(hqEntranceNode);

        if (!connectedNodes.Contains(carrier.CurrentNode))
        {
            int nearestConnectedNode = FindNearestConnectedNode(carrier.CurrentNode, connectedNodes);
            if (nearestConnectedNode != -1)
            {
                List<int> pathToNearest = pathFinder.FindPathThroughPaths(carrier.CurrentNode, nearestConnectedNode) ?? FindPath(carrier.CurrentNode, nearestConnectedNode);
                if (pathToNearest != null)
                {
                    carrier.AssignPath(pathToNearest, () => MoveToHQEntrance(carrier, hqEntranceNode, hqNode));
                }
                else
                {
                    Debug.LogWarning($"[ReturnCarrierToHQ] No path (even direct) to nearest connected node {nearestConnectedNode} from {carrier.CurrentNode}, returning directly");
                    workerManager.ReturnWorker(carrier);
                }
            }
            else
            {
                Debug.LogWarning($"[ReturnCarrierToHQ] No connected nodes found near {carrier.CurrentNode}, returning directly");
                workerManager.ReturnWorker(carrier);
            }
        }
        else
        {
            MoveToHQEntrance(carrier, hqEntranceNode, hqNode);
        }
    }

    private void MoveToHQEntrance(Carrier carrier, int hqEntranceNode, int hqNode)
    {
        List<int> pathToEntrance = pathFinder.FindPathThroughPaths(carrier.CurrentNode, hqEntranceNode);
        if (pathToEntrance != null)
        {
            carrier.AssignPath(pathToEntrance, () =>
            {
                List<int> pathToHQ = pathFinder.FindPathThroughPaths(hqEntranceNode, hqNode) ?? new List<int> { hqEntranceNode, hqNode };
                if (pathToHQ != null && pathToHQ.Count > 0)
                {
                    Action returnCallback = () =>
                    {
                        workerManager.ReturnWorker(carrier);
                    };
                    carrier.AssignPath(pathToHQ, returnCallback);
                }
                else
                {
                    Debug.LogWarning($"[ReturnCarrierToHQ] No path from HQ entrance {hqEntranceNode} to HQ {hqNode}, returning directly");
                    workerManager.ReturnWorker(carrier);
                }
            });
        }
        else
        {
            Debug.LogWarning($"[ReturnCarrierToHQ] No path from {carrier.CurrentNode} to HQ entrance {hqEntranceNode}, returning directly");
            workerManager.ReturnWorker(carrier);
        }
    }

    private HashSet<int> GetNodesConnectedToHQ(int hqEntranceNode)
    {
        HashSet<int> connectedNodes = new HashSet<int>();
        Queue<int> toVisit = new Queue<int>();
        toVisit.Enqueue(hqEntranceNode);
        connectedNodes.Add(hqEntranceNode);

        while (toVisit.Count > 0)
        {
            int currentNode = toVisit.Dequeue();
            foreach (var path in allPaths.Values)
            {
                int index = path.Nodes.IndexOf(currentNode);
                if (index != -1)
                {
                    if (index > 0)
                    {
                        int prevNode = path.Nodes[index - 1];
                        if (connectedNodes.Add(prevNode))
                        {
                            toVisit.Enqueue(prevNode);
                        }
                    }
                    if (index < path.Nodes.Count - 1)
                    {
                        int nextNode = path.Nodes[index + 1];
                        if (connectedNodes.Add(nextNode))
                        {
                            toVisit.Enqueue(nextNode);
                        }
                    }
                }
            }
        }
        return connectedNodes;
    }

    private int FindNearestConnectedNode(int currentNode, HashSet<int> connectedNodes)
    {
        if (connectedNodes.Count == 0) return -1;

        int nearestNode = -1;
        float minDistance = float.MaxValue;
        Vector3 currentPos = manager.globalVertices[currentNode];

        foreach (int node in connectedNodes)
        {
            Vector3 nodePos = manager.globalVertices[node];
            float distance = Vector3.Distance(currentPos, nodePos);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearestNode = node;
            }
        }
        return nearestNode;
    }

    public int GetCentreNodeOfPath(int pathId)
    {
        if (!allPaths.ContainsKey(pathId)) return -1;
        List<int> path = allPaths[pathId].Nodes;
        if (path.Count < 2) return path[0];

        // Check if it's a loop (starts and ends at same node)
        if (path[0] == path[path.Count - 1])
        {
            // Find farthest node from start/end (e.g., 49)
            int startNode = path[0];
            int farthestNode = startNode;
            float maxDistance = 0f;

            for (int i = 1; i < path.Count - 1; i++) // Skip start and end
            {
                int node = path[i];
                float distance = Vector3.Distance(
                    manager.globalVertices[startNode],
                    manager.globalVertices[node]
                );
                if (distance > maxDistance)
                {
                    maxDistance = distance;
                    farthestNode = node;
                }
            }

            return farthestNode;
        }

        // Non-loop: use simple midpoint
        int midIndex = path.Count / 2;
        NodeData midNodeData = manager.NodeManager.GetNodeData(path[midIndex]);
        if (midNodeData.HasFlag && path.Count > 2)
        {
            int leftIndex = midIndex - 1;
            int rightIndex = midIndex + 1;
            if (leftIndex >= 0 && !manager.NodeManager.GetNodeData(path[leftIndex]).HasFlag)
                return path[leftIndex];
            if (rightIndex < path.Count && !manager.NodeManager.GetNodeData(path[rightIndex]).HasFlag)
                return path[rightIndex];
        }
        return path[midIndex];
    }

    public void RefreshPathVisuals()
    {
        // Clear existing path visuals (assuming PathGenerator manages this)
        manager.PathVisualsGenerator.ClearPathVisuals();
        foreach (var path in allPaths.Values)
        {
            manager.PathVisualsGenerator.DrawPath(path.Nodes);
        }
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