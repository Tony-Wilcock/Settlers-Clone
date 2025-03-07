using System.Collections.Generic;
using UnityEngine;
using static NodeTypes;
using static ResourceManager;

public class BuildingManager : MonoBehaviour
{
    [SerializeField] private GameObject[] buildingGameObjects;

    public Dictionary<BuildingType, List<Building>> AllBuildings { get; private set; } = new Dictionary<BuildingType, List<Building>>();

    private HexGridManager manager;
    private NodeManager nodeManager;
    private PathManager pathManager;
    private WorkerManager workerManager;

    public void Initialise(HexGridManager manager)
    {
        this.manager = manager;

        nodeManager = manager.NodeManager;
        pathManager = manager.PathManager;
        workerManager = manager.WorkerManager;

        TryPlaceBuilding(manager.HqStartingNode, BuildingType.HQ, out _);
    }

    public Dictionary<BuildingType, Dictionary<StockResourceType, int>> BuildingCosts = new()
    {
        { BuildingType.WoodCuttersHut, new Dictionary<StockResourceType, int>
            {
                { StockResourceType.Wood, 2 }
            }
        },
        { BuildingType.Storehouse, new Dictionary<StockResourceType, int>
            {
                { StockResourceType.Wood, 2 },
                { StockResourceType.Stone, 2 }
            }
        },
        // Add more buildingGameObject costs here
    };

    // Check if the buildingGameObject can be placed at the vertex
    public bool TryPlaceBuilding(int centralVertexIndex, BuildingType buildingType, out int entranceVertexIndex)
    {
        entranceVertexIndex = -1;

        if (!CanPlaceBuilding(centralVertexIndex, buildingType))
        {
            Debug.LogWarning($"Cannot place {buildingType} at vertex {centralVertexIndex}.");
            return false;
        }

        BuildingSize size = BuildingSizes[buildingType];
        List<int> reservedNodes = GetReservedNodes(centralVertexIndex, size);
        entranceVertexIndex = nodeManager.GetNeighborInDirection(centralVertexIndex, Direction.Southeast);

         //Instantiate and initialize building
        GameObject buildingPrefab = DetermineBuildingObject(buildingType);
        GameObject buildingObj = Instantiate(buildingPrefab, nodeManager.GlobalVertices[centralVertexIndex], Quaternion.identity);
        buildingObj.SetActive(true);Building buildingScript = buildingObj.GetComponent<Building>();
        buildingScript.Initialise(this, nodeManager, pathManager, workerManager, buildingType, centralVertexIndex, entranceVertexIndex, reservedNodes);

        bool canAfford = CanAffordBuilding(buildingType);
        //if (buildingType != BuildingType.HQ && canAfford)
        //{
        //    CompleteConstruction(buildingType); // Deduct resources and complete if affordable
        //    buildingScript.SetConstructed(true);
        //}
        //else if (buildingType == BuildingType.HQ)
        //{
        //    CompleteConstruction(buildingType); // HQ construction is free
        //    buildingScript.SetConstructed(true);
        //}
        //else if (!canAfford && buildingType != BuildingType.HQ)
        //{
        //    Debug.Log($"Site marked for {buildingType} at {centralVertexIndex}, awaiting resources.");
        //}

        // Create path visuals from CentralNode to EntranceNode
        List<int> buildingPath = new() { centralVertexIndex, entranceVertexIndex };
        manager.PathVisualsGenerator.DrawPath(buildingPath);

        AddBuilding(buildingScript);
        return true;
    }

    // Check if the resources are available to build the buildingGameObject
    public bool CanAffordBuilding(BuildingType buildingType)
    {
        if (BuildingCosts.TryGetValue(buildingType, out Dictionary<StockResourceType, int> cost))
        {
            foreach (var kvp in cost)
            {
                StockResourceType resource = kvp.Key;
                int amount = kvp.Value;
                if (GetStockResourceAmount(resource) < amount)
                {
                    Debug.Log($"Not enough {resource}({amount}) to build {buildingType}. Only {GetStockResourceAmount(resource)} left!");
                    return false;
                }
            }
            return true;
        }
        return false;
    }

    public void CompleteConstruction(BuildingType buildingType)
    {
        if (buildingType == BuildingType.HQ)
        {
            Debug.Log("HQ construction completed (no resource cost).");
            return;
        }

        if (BuildingCosts.TryGetValue(buildingType, out Dictionary<StockResourceType, int> cost))
        {
            foreach (var kvp in cost)
            {
                if (!RemoveStockResource(kvp.Key, kvp.Value))
                {
                    Debug.LogError($"Failed to remove {kvp.Value} {kvp.Key} for {buildingType}");
                    return; // Shouldn’t happen due to CanAffordBuilding check
                }
            }
            Debug.Log($"Construction completed for {buildingType} with resources deducted.");
        }
    }

    // Get the buildingGameObject object based on the buildingGameObject type
    private GameObject DetermineBuildingObject(BuildingType buildingType)
    {
        int buildingIndex = (int)buildingType - 1; // -1 to skip None
        if (buildingIndex < 0 || buildingIndex >= buildingGameObjects.Length || buildingGameObjects[buildingIndex] == null)
        {
            Debug.LogError($"No prefab assigned for {buildingType} at index {buildingIndex}");
            return null;
        }
        return buildingGameObjects[buildingIndex];
    }

    // Get the reserved nodes for the buildingGameObject based on the buildingGameObject size
    private List<int> GetReservedNodes(int centralVertexIndex, BuildingSize size)
    {
        List<int> reservedNodes = new List<int> { centralVertexIndex };
        if (size != BuildingSize.Large) return reservedNodes;

        Dictionary<Direction, int> directionNodes = new Dictionary<Direction, int>();
        int west = nodeManager.GetNeighborInDirection(centralVertexIndex, Direction.West);
        if (west != -1) directionNodes[Direction.West] = west;

        int northwest = nodeManager.GetNeighborInDirection(centralVertexIndex, Direction.Northwest);
        if (northwest != -1 && northwest != west) directionNodes[Direction.Northwest] = northwest;

        int northeast = nodeManager.GetNeighborInDirection(centralVertexIndex, Direction.Northeast);
        if (northeast != -1 && northeast != west && northeast != northwest) directionNodes[Direction.Northeast] = northeast;

        if (directionNodes.ContainsKey(Direction.West) && IsReservedNodeValid(directionNodes[Direction.West]))
            reservedNodes.Add(directionNodes[Direction.West]);
        if (directionNodes.ContainsKey(Direction.Northwest) && IsReservedNodeValid(directionNodes[Direction.Northwest]))
            reservedNodes.Add(directionNodes[Direction.Northwest]);
        if (directionNodes.ContainsKey(Direction.Northeast) && IsReservedNodeValid(directionNodes[Direction.Northeast]))
            reservedNodes.Add(directionNodes[Direction.Northeast]);

        return reservedNodes;
    }

    // Check if the reserved node is valid
    private bool IsReservedNodeValid(int reservedNode)
    {
        NodeData nodeData = nodeManager.GetNodeData(reservedNode);
        return nodeData != null && !nodeData.HasBuilding && !nodeData.HasFlag && !nodeData.HasObstacle && !nodeData.HasResource &&
               nodeData.TerrainType != TerrainType.Water && nodeData.TerrainType != TerrainType.Mountain && nodeData.TerrainType != TerrainType.Marsh;
    }

    // Check if the buildingGameObject can be placed at the vertex
    private bool CanPlaceBuilding(int vertexIndex, BuildingType buildingType)
    {
        if (vertexIndex == -1) return false;

        NodeData centralData = nodeManager.GetNodeData(vertexIndex);
        if (centralData == null || centralData.HasBuilding || centralData.HasFlag || centralData.HasObstacle)
            return false;

        BuildingSize size = BuildingSizes[buildingType];

        List<int> reservedNodes = GetReservedNodes(vertexIndex, size);
        foreach (int node in reservedNodes)
        {
            NodeData nodeData = nodeManager.GetNodeData(node);
            if (nodeData == null || nodeData.HasBuilding || nodeData.HasFlag || nodeData.HasObstacle)
                return false;
        }

        if (size == BuildingSize.Large && reservedNodes.Count != 4)
        {
            Debug.LogWarning("Plot not large enough for this building.");
            return false;
        }

        int entranceNode = nodeManager.GetNeighborInDirection(vertexIndex, Direction.Southeast);

        if (entranceNode == -1 || reservedNodes.Contains(entranceNode))
        {
            Debug.LogWarning("Invalid entrance node.");
            return false;
        }

        // Only allow for 1 HQ
        if (buildingType == BuildingType.HQ && AllBuildings.ContainsKey(BuildingType.HQ))
        {
            Debug.LogWarning("HQ already exists.");
            manager.HexGridInteraction.SetBuildingType(BuildingType.None);
            return false;
        }

        if (entranceNode != -1)
        {
            if (reservedNodes.Contains(entranceNode)) return false;
            if (nodeManager.HasNodeGotFlag(entranceNode)) return true;
            if (!nodeManager.CanPlaceFlag(entranceNode)) return false;
            NodeData southEastData = nodeManager.GetNodeData(entranceNode);
            if (southEastData == null || southEastData.HasBuilding || southEastData.HasFlag || southEastData.HasObstacle)
                return false;
        }

        return true;
    }

    // Add biulding to dictionary and increment count
    public void AddBuilding(Building building)
    {
        BuildingType type = building.BuildingType;
        if (!AllBuildings.ContainsKey(type))
        {
            AllBuildings[type] = new List<Building>();
            Debug.Log($"Added {type} to AllBuildings dictionary.");
        }
        AllBuildings[type].Add(building);
    }

    public Building GetBuildingFromNode(int node)
    {
        foreach (var buildings in AllBuildings.Values)
        {
            foreach (var building in buildings)
            {
                if (building.CentralNode == node)
                {
                    return building;
                }
            }
        }
        return null;
    }
}
