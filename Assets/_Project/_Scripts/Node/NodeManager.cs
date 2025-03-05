using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static NodeTypes;

public enum Direction
{
    Northeast,
    East,
    Southeast,
    Southwest,
    West,
    Northwest,
}

public class NodeManager : MonoBehaviour
{
    [SerializeField] private GameObject flagPrefab;
    [SerializeField] private GameObject obstaclePrefab;
    private int poolSize = 100; // Initial pool size (adjust as needed)
    private Queue<GameObject> flagPool; // Object pool for flags
    private Queue<GameObject> obstaclePool; // Object pool for obstacles
    private Dictionary<int, GameObject> allFlags = new Dictionary<int, GameObject>(); // To store all flags
    private Dictionary<int, GameObject> allObstacles = new Dictionary<int, GameObject>(); // To store all obstacles

    private HexGridManager manager;
    private HexGridSettings settings;

    public ConcurrentDictionary<int, NodeData> nodeDataDictionary = new ConcurrentDictionary<int, NodeData>();
    private ConcurrentDictionary<int, bool> nodeValidityCache = new ConcurrentDictionary<int, bool>(); // Update this too if using Suggestion 1 from PathManager

    private float MaxHeightDifference => settings.maxHeightDifference;
    private float SmoothingFactor => settings.smoothingFactor;
    private float MovementAmount => settings.movementAmount;
    public Dictionary<int, Vector3> GlobalVertices => manager.globalVertices;
    private Dictionary<int, List<int>> AdjacencyList => manager.AdjacencyList;
    private List<Chunk> Chunks => manager.chunks;
    private List<int> nodeNeighbors = new List<int>();

    public int liveVertexIndex = -1;
    public int heldVertexIndex = -1; // To store the current vertex index
    public int startPathVertexIndex = -1; // To store start vertex index for pathfinding
    public int endPathVertexIndex = -1;   // To store end vertex index for pathfinding

    public void Initialise(HexGridManager manager, HexGridSettings settings) // Constructor (optional: if needed)
    {
        this.manager = manager;
        this.settings = settings;
        InitialisePools(); // Initialize the object pool at startup
    }

    private void InitialisePools()
    {
        flagPool = new Queue<GameObject>(poolSize);
        obstaclePool = new Queue<GameObject>(poolSize);

        for (int i = 0; i < poolSize; i++)
        {
            // Flag pool
            GameObject flag = Instantiate(flagPrefab, Vector3.zero, Quaternion.identity);
            flag.transform.SetParent(manager.flagsTransform);
            flag.SetActive(false);
            flagPool.Enqueue(flag);

            // Obstacle pool
            if (obstaclePrefab != null)
            {
                GameObject obstacle = Instantiate(obstaclePrefab, Vector3.zero, Quaternion.identity);
                obstacle.transform.SetParent(manager.flagsTransform); // Reuse transform or create a new one
                obstacle.SetActive(false);
                obstaclePool.Enqueue(obstacle);
            }
        }
    }

    private GameObject GetFlagFromPool()
    {
        if (flagPool.Count > 0)
        {
            GameObject pooledFlag = flagPool.Dequeue();
            pooledFlag.SetActive(true); // Activate when taken from pool
            return pooledFlag;
        }
        else
        {
            // If pool is empty, instantiate a new one (optional: you could resize pool instead)
            Debug.LogWarning("NodeManager: Pool is empty, instantiating new flag (consider increasing pool size).");
            GameObject newFlag = Instantiate(flagPrefab, Vector3.zero, Quaternion.identity);
            newFlag.transform.SetParent(manager.flagsTransform);
            return newFlag;
        }
    }

    private void ReturnFlagToPool(GameObject flag)
    {
        flag.SetActive(false); // Deactivate before returning to pool
        flagPool.Enqueue(flag); // Return to the pool
    }

    private GameObject GetObstacleFromPool()
    {
        if (obstaclePool.Count > 0)
        {
            GameObject pooledObstacle = obstaclePool.Dequeue();
            pooledObstacle.SetActive(true);
            return pooledObstacle;
        }
        else
        {
            Debug.LogWarning("NodeManager: Obstacle pool is empty, instantiating new obstacle.");
            GameObject newObstacle = Instantiate(obstaclePrefab, Vector3.zero, Quaternion.identity);
            newObstacle.transform.SetParent(manager.flagsTransform);
            return newObstacle;
        }
    }

    private void ReturnObstacleToPool(GameObject obstacle)
    {
        obstacle.SetActive(false);
        obstaclePool.Enqueue(obstacle);
    }

    public void InitializeNodeData(int vertexIndex) // Method to create NodeData
    {
        nodeDataDictionary[vertexIndex] = new NodeData();
    }

    public NodeData GetNodeData(int vertexIndex)
    {
        return nodeDataDictionary.TryGetValue(vertexIndex, out NodeData data) ? data : null;
    }

    public void SetNodeData(int vertexIndex, NodeData data)
    {
        if (vertexIndex == -1 || data == null)
        {
            Debug.LogWarning("GridManager: Attempted to set null cell or neighborData.");
            return;
        }
        nodeDataDictionary[vertexIndex] = data; // ConcurrentDictionary handles thread safety
    }

    public void SetNodeTerrainType(int vertexIndex, TerrainType type)
    {
        NodeData data = EnsureNodeData(vertexIndex);
        data.TerrainType = type;
        UpdateNodeValidity(vertexIndex);
    }

    public void SetNodeBuildingType(int vertexIndex, BuildingType type)
    {
        NodeData data = EnsureNodeData(vertexIndex);
        data.BuildingType = type;
        
        UpdateNodeValidity(vertexIndex);
    }

    public void SetNodeObstacle(int vertexIndex, bool hasObstacle)
    {
        NodeData data = EnsureNodeData(vertexIndex);
        data.HasObstacle = hasObstacle;
        if (hasObstacle)
        {
            if (!allObstacles.ContainsKey(vertexIndex))
            {
                GameObject obstacle = GetObstacleFromPool();
                obstacle.transform.position = GlobalVertices[vertexIndex];
                allObstacles[vertexIndex] = obstacle;
            }
        }
        else
        {
            if (allObstacles.ContainsKey(vertexIndex))
            {
                ReturnObstacleToPool(allObstacles[vertexIndex]);
                allObstacles.Remove(vertexIndex);
            }
        }
        UpdateNodeValidity(vertexIndex);
    }

    public void SetNodeFlag(int vertexIndex, bool hasFlag)
    {
        NodeData data = EnsureNodeData(vertexIndex);
        data.HasFlag = hasFlag;
        UpdateNodeValidity(vertexIndex);
    }

    public void SetNodePath(int vertexIndex, bool hasPath)
    {
        NodeData data = EnsureNodeData(vertexIndex);
        data.HasPath = hasPath;
        UpdateNodeValidity(vertexIndex);
    }

    public void SetMultipleNodesPath(List<int> path, bool hasPath)
    {
        foreach (int vertexIndex in path)
        {
            SetNodePath(vertexIndex, hasPath);
        }
    }

    public List<int> GetPathNodeIndexes(List<int> path)
    {
        List<int> pathNodeIndexes = new List<int>();
        foreach (int vertexIndex in path)
        {
            if (GetNodeData(vertexIndex).HasPath)
            {
                pathNodeIndexes.Add(vertexIndex);
            }
        }
        return pathNodeIndexes;
    }

    public void SetNodeResourceType(int vertexIndex, WorldResourceType type, int amount = 0)
    {
        NodeData data = EnsureNodeData(vertexIndex);
        data.ResourceType = type;
        data.ResourceAmount = amount;
        UpdateNodeValidity(vertexIndex);
    }

    private void UpdateNodeValidity(int vertexIndex) // Update node validity based on terrain, obstacles, buildings, etc.
    {
        NodeData data = GetNodeData(vertexIndex);
        if (data == null)
        {
            nodeValidityCache[vertexIndex] = true; // Default valid if no data
            return;
        }

        bool isValid = !(data.TerrainType is TerrainType.Water or TerrainType.MountainTop or TerrainType.Marsh) && !data.HasObstacle && !data.HasBuilding;

        nodeValidityCache[vertexIndex] = isValid;
    }

    private NodeData EnsureNodeData(int vertexIndex)
    {
        return nodeDataDictionary.GetOrAdd(vertexIndex, _ => new NodeData());
    }

    public void ClearNodeData()
    {
        nodeDataDictionary.Clear();
        nodeValidityCache.Clear(); // If using cache
    }

    public int GetNearestNode(Vector3 mousePosition)
    {
        Ray ray = manager.MainCamera.ScreenPointToRay(mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, settings.hexGridLayerMask))
        {
            float closestDistance = float.MaxValue;
            int newSelectedVertexIndex = -1;

            for (int c = 0; c < manager.chunks.Count; c++)
            {
                Chunk chunk = manager.chunks[c];
                if (chunk.chunkObject == hit.collider.gameObject)
                {
                    Vector3 localHitPoint = chunk.chunkObject.transform.InverseTransformPoint(hit.point);
                    for (int i = 0; i < chunk.vertices.Length; i++)
                    {
                        float distance = Vector3.Distance(localHitPoint, chunk.vertices[i]);
                        if (distance < closestDistance && distance < settings.cellSize * 0.6f)
                        {
                            closestDistance = distance;
                            newSelectedVertexIndex = chunk.localToGlobalVertexMap[i];
                        }
                    }
                    break; // Chunk found, no need to check other chunks
                }
            }
            return newSelectedVertexIndex;
        }
        return -1; // No vertex found under mouse
    }

    public int GetHQEntranceNode()
    {
        foreach (var kvp in nodeDataDictionary)
        {
            if (kvp.Value.BuildingType == BuildingType.HQ)
            {
                int hqNode = kvp.Key;
                int entranceNode = GetNeighborInDirection(hqNode, Direction.Southeast);
                if (entranceNode != -1 && GetNodeData(entranceNode).HasFlag)
                {
                    return entranceNode;
                }
            }
        }
        Debug.LogWarning("No HQ entrance node found.");
        return -1;
    }

    public List<int> GetNodeNieghbors(int vertexIndex)
    {
        nodeNeighbors.Clear();
        if (AdjacencyList != null && AdjacencyList.ContainsKey(vertexIndex))
        {
            nodeNeighbors.AddRange(AdjacencyList[vertexIndex]);
        }
        else
        {
            // Optional: Log for debugging
            if (AdjacencyList == null) Debug.LogWarning($"AdjacencyList is null for vertex {vertexIndex}. Returning empty neighbor list.");
        }
        return nodeNeighbors;
    }

    public int GetNeighborInDirection(int vertexIndex, Direction direction)
    {
        List<int> neighbors = GetNodeNieghbors(vertexIndex);
        if (neighbors.Count == 0) return -1;

        Vector3 centerPos = GlobalVertices[vertexIndex];
        int bestMatchIndex = -1;
        float smallestAngleDiff = float.MaxValue;

        // Define direction angles based on orientation
        float targetAngle = direction switch
        {
            Direction.Northeast => 60f,   // NE
            Direction.East => 0f,        // E
            Direction.Southeast => 300f, // SE (flag/entry point)
            Direction.Southwest => 240f, // SW
            Direction.West => 180f,      // W
            Direction.Northwest => 120f, // NW
            _ => throw new System.ArgumentException($"Unknown direction: {direction}")
        };

        foreach (int neighborIndex in neighbors)
        {
            Vector3 neighborPos = GlobalVertices[neighborIndex];
            Vector3 directionVec = (neighborPos - centerPos).normalized;
            Vector3 targetDirection = new Vector3(Mathf.Cos(targetAngle * Mathf.Deg2Rad), 0, Mathf.Sin(targetAngle * Mathf.Deg2Rad));
            float angleDiff = Vector3.Angle(directionVec, targetDirection);

            if (angleDiff < smallestAngleDiff && angleDiff < 30f) // Tolerance of 30° to ensure correct neighbor
            {
                smallestAngleDiff = angleDiff;
                bestMatchIndex = neighborIndex;
            }
        }

        if (bestMatchIndex == -1)
        {
            Debug.LogWarning($"No neighbor found in direction {direction} for vertex {vertexIndex}");
        }

        return bestMatchIndex;
    }

    public (int row, int col) GetCellAtMousePosition(Vector3 mousePosition)
    {
        Ray ray = manager.MainCamera.ScreenPointToRay(mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, settings.hexGridLayerMask))
        {
            Vector3 worldPoint = hit.point;
            return GetCellCoordinates(worldPoint);
        }
        return (-1, -1);
    }

    private (int row, int col) GetCellCoordinates(Vector3 worldPosition)
    {
        float outerRadius = settings.cellSize;
        float innerRadius = outerRadius * Mathf.Sqrt(3) / 2f;

        float rowFloat, colFloat;


        rowFloat = worldPosition.x / (outerRadius * 1.5f);
        colFloat = (worldPosition.z + (Mathf.RoundToInt(rowFloat) % 2 == 1 ? -innerRadius : 0f)) / (innerRadius * 2f); // Example Flat-side up - needs verification

        int row = Mathf.RoundToInt(rowFloat);
        int col = Mathf.RoundToInt(colFloat);

        if (row >= 0 && row < settings.height && col >= 0 && col < settings.width)
        {
            return (row, col);
        }
        else
        {
            return (-1, -1);
        }
    }

    private void AdjustVertexHeight(int vertexIndex, float yMovement)
    {
        if (manager.EdgeVertices.Contains(vertexIndex)) return;

        AdjustSingleVertex(vertexIndex, yMovement);
        SmoothNeighboringVertices(vertexIndex);
        manager.editableVerticesIndices = GlobalVertices.Keys.ToArray();
    }

    private void AdjustSingleVertex(int vertexIndex, float yMovement)
    {
        Vector3 pos = GlobalVertices[vertexIndex];
        pos.y += yMovement;
        GlobalVertices[vertexIndex] = pos;
        SyncVertexToChunks(vertexIndex);
    }

    private void SmoothNeighboringVertices(int startVertexIndex)
    {
        Queue<int> verticesToCheck = new Queue<int>();
        HashSet<int> checkedVertices = new HashSet<int>();
        verticesToCheck.Enqueue(startVertexIndex);

        while (verticesToCheck.Count > 0)
        {
            int currentIndex = verticesToCheck.Dequeue();
            if (checkedVertices.Contains(currentIndex)) continue;
            checkedVertices.Add(currentIndex);

            float currentHeight = GlobalVertices[currentIndex].y;

            if (AdjacencyList != null && AdjacencyList.ContainsKey(currentIndex))
            {
                foreach (int neighborIndex in AdjacencyList[currentIndex])
                {
                    if (manager.EdgeVertices.Contains(neighborIndex)) continue;

                    float heightDifference = GlobalVertices[neighborIndex].y - currentHeight;
                    if (Mathf.Abs(heightDifference) > MaxHeightDifference)
                    {
                        float adjustment = (heightDifference > 0) ? -MovementAmount : MovementAmount;
                        adjustment *= SmoothingFactor;
                        adjustment = Mathf.Clamp(
                            adjustment,
                            -Mathf.Abs(heightDifference) + MaxHeightDifference,
                            Mathf.Abs(heightDifference) - MaxHeightDifference
                        );

                        Vector3 neighborPos = GlobalVertices[neighborIndex];
                        neighborPos.y += adjustment;
                        GlobalVertices[neighborIndex] = neighborPos;
                        SyncVertexToChunks(neighborIndex);

                        verticesToCheck.Enqueue(neighborIndex);
                    }
                }
            }
        }
    }

    public int GetVertexHeight(int vertexIndex)
    {
        return Mathf.RoundToInt(GlobalVertices[vertexIndex].y);
    }

    // Called from UI Create Flag Button
    public void PlaceFlag()
    {
        if (heldVertexIndex == -1 || !CanPlaceFlag(heldVertexIndex))
        {
            manager.UIManager.HideAllPanels();
            return;
        }

        NodeData data = GetNodeData(heldVertexIndex);

        GameObject flag = GetFlagFromPool();
        flag.transform.position = GlobalVertices[heldVertexIndex];
        allFlags.Add(heldVertexIndex, flag);
        manager.UIManager.UpdateUIText("Flags", $"Flags: {allFlags.Count}");
        if (data.HasPath)
        {
            manager.PathManager.SplitPathAt(heldVertexIndex);
        }
        SetNodeFlag(heldVertexIndex, true);
        manager.UIManager.HideAllPanels();

        heldVertexIndex = -1;
    }

    public bool CanPlaceFlag(int vertexIndex)
    {
        if (vertexIndex == -1) return false; // Invalid vertex index

        NodeData indexData = GetNodeData(vertexIndex); // Get the data of the vertex

        if (!IsValidTerrainForFlag(indexData.TerrainType)) return false; // Check if the terrain is valid
        if (indexData == null || indexData.HasFlag) return false; // Check if there is already a flag
        if (indexData.HasObstacle) return false; // Check if there is an obstacle
        if (indexData.HasBuilding) return false; // Check if there is a building
        if (HasNeighborGotFlag(vertexIndex)) return false; // Check if any neighbor has a flag

        return true;
    }

    private bool HasNeighborGotFlag(int vertexIndex)
    {
        foreach (int neighbor in GetNodeNieghbors(vertexIndex))
        {
            NodeData neighborData = GetNodeData(neighbor);
            if (neighborData != null && neighborData.HasFlag) return true;
        }
        return false;
    }

    public bool HasNodeGotFlag(int vertexIndex)
    {
        return GetNodeData(vertexIndex)?.HasFlag ?? false;
    }

    public int NumberOfPathsAttachedToNode(int vertexIndex)
    {
        int count = 0;
        List<int> attachedPathIds = new List<int>();
        foreach (var path in manager.PathManager.GetAllPaths)
        {
            if (path.Value.ContainsNode(vertexIndex))
            {
                count++;
                attachedPathIds.Add(path.Key);
            }
        }
        Debug.Log($"[NodeManager] Node {vertexIndex} has {count} paths attached, path IDs: {string.Join(", ", attachedPathIds)}");
        return count;
    }

    private bool IsValidTerrainForFlag(TerrainType terrainType)
    {
        return terrainType == TerrainType.Desert || terrainType == TerrainType.Grass || terrainType == TerrainType.Mountain;
    }

    public void TryRemoveFlag()
    {
        int vertexIndex = heldVertexIndex;
        if (vertexIndex == -1) return;

        int neighborNode = GetNeighborInDirection(vertexIndex, Direction.Northwest);
        NodeData neighborData = GetNodeData(neighborNode);
        if (neighborData != null && neighborData.BuildingType != BuildingType.None)
        {
            Debug.LogWarning($"Removing flag will destroy the building {neighborData.BuildingType}.");
            return;
        }

        NodeData data = GetNodeData(vertexIndex);

        if (data.HasPath)
        {
            int pathCount = NumberOfPathsAttachedToNode(vertexIndex);
            Debug.Log($"[NodeManager] Checking removal at {vertexIndex}, paths attached: {pathCount}");
            switch (pathCount)
            {
                case 0:
                    RemoveFlag(vertexIndex);
                    break;
                case 1:
                    RemoveFlag(vertexIndex);
                    manager.PathManager.RemovePath();
                    break;
                case 2:
                    RemoveFlag(vertexIndex);
                    manager.PathManager.JoinPathAt(vertexIndex);
                    break;
                default:
                    Debug.LogWarning($"Warning: This will remove all paths connected to node {vertexIndex} - {NumberOfPathsAttachedToNode(vertexIndex)}.");
                    // Warn user that this will remove all paths connected to this node
                    // Highlight all paths connected to this node
                    // Remove all paths attached to this node if user confirms
                    break;
            }
        }

        manager.UIManager.HideAllPanels();
    }

    private void RemoveFlag(int vertexIndex)
    {
        if (allFlags.ContainsKey(vertexIndex))
        {
            ReturnFlagToPool(allFlags[vertexIndex]);
            allFlags.Remove(vertexIndex);
            manager.UIManager.UpdateUIText("Flags", $"Flags: {allFlags.Count}");
            SetNodeFlag(vertexIndex, false);
        }
    }

    public bool IsNodeValidForPath(int node)
    {
        if (!nodeValidityCache.ContainsKey(node)) // Check if cache is populated
        {
            UpdateNodeValidity(node); // Ensure cache is populated
        }
        return nodeValidityCache[node];
    }

    // Called from UI Cancel Flag Button
    public void CancelFlagPlacement()
    {
        manager.UIManager.HideAllPanels();
        heldVertexIndex = -1;
    }

    private void SyncVertexToChunks(int globalIndex)
    {
        Vector3 pos = GlobalVertices[globalIndex];
        foreach (var chunk in manager.chunks)
        {
            if (chunk.globalToLocalVertexMap.TryGetValue(globalIndex, out int localIndex))
            {
                chunk.vertices[localIndex] = pos;
            }
        }
    }

    public void RaiseHeight()
    {
        float yMovement = MovementAmount;
        AdjustVertexHeight(liveVertexIndex, yMovement);
        foreach (var chunk in Chunks) chunk.UpdateMesh();
    }

    public void LowerHeight(int newSelectedVertexIndex)
    {
        if (newSelectedVertexIndex != -1 && !manager.EdgeVertices.Contains(newSelectedVertexIndex))
        {
            float yMovement = -MovementAmount;
            AdjustVertexHeight(liveVertexIndex, yMovement);
            foreach (var chunk in Chunks) chunk.UpdateMesh();
        }
    }
}
