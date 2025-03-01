using System.Collections.Generic;
using UnityEngine;
using static NodeTypes;

public class BuildingManager : MonoBehaviour
{
    private NodeManager2 nodeManager;
    private PathFindingManager pathFindingManager;

    public void Initialise(NodeManager2 nodeManager, PathFindingManager pathFindingManager)
    {
        this.nodeManager = nodeManager;
        this.pathFindingManager = pathFindingManager;
    }

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

        if (size == BuildingSize.Large && reservedNodes.Count != 4)
        {
            Debug.LogWarning($"Invalid number of reserved nodes for {buildingType} at vertex {centralVertexIndex}.");
            return false;
        }

        entranceVertexIndex = nodeManager.GetNeighborInDirection(centralVertexIndex, Direction.Southeast);
        if (entranceVertexIndex == -1 || reservedNodes.Contains(entranceVertexIndex))
        {
            Debug.LogWarning($"No valid Southeast entrance for {buildingType} at vertex {centralVertexIndex}.");
            return false;
        }

        // Reserve nodes
        Debug.Log($"Placing {buildingType} at central node {centralVertexIndex}");
        NodeData centralData = nodeManager.GetNodeData(centralVertexIndex);
        nodeManager.SetNodeBuildingType(centralVertexIndex, buildingType);
        centralData.HasBuilding = true;
        SetBuildingID(centralVertexIndex, centralData);

        foreach (int node in reservedNodes)
        {
            if (node != centralVertexIndex)
            {
                Debug.Log($"Reserving node {node} for {buildingType} as {(reservedNodes.IndexOf(node) == 1 ? "West" : reservedNodes.IndexOf(node) == 2 ? "Northwest" : "Northeast")}");
                nodeManager.SetNodeBuildingType(node, BuildingType.None);
                NodeData nodeData = nodeManager.GetNodeData(node);
                nodeData.HasBuilding = true;
                nodeData.HasObstacle = true;  // Obstacle to prevent pathfinding through building
                SetBuildingID(centralVertexIndex, nodeData);
            }
        }

        // Set entrance node
        Debug.Log($"Setting entrance at node {entranceVertexIndex}");
        nodeManager.heldVertexIndex = entranceVertexIndex; // Set entrance as held vertex
        nodeManager.PlaceFlag(); // Place flag at entrance
        nodeManager.heldVertexIndex = centralVertexIndex; // Set central node as held vertex
        pathFindingManager.StartPathPlacement(); // Start path placement
        pathFindingManager.TryAddPathToEndNode(entranceVertexIndex); // Add entrance node to path
        nodeManager.heldVertexIndex = -1; // Reset held vertex
        return true;
    }

    private void SetBuildingID(int vertexIndex, NodeData nodeData)
    {
        nodeData.BuildingID = vertexIndex;
    }

    private List<int> GetReservedNodes(int centralVertexIndex, BuildingSize size)
    {
        List<int> reservedNodes = new List<int> { centralVertexIndex };

        switch (size)
        {
            case BuildingSize.Small:
            case BuildingSize.Medium:
                // Only central node (Southeast handled separately as entrance)
                break;

            case BuildingSize.Large:
                // Central + West + Northwest + Northeast in that order
                Dictionary<Direction, int> directionNodes = new Dictionary<Direction, int>();

                int west = nodeManager.GetNeighborInDirection(centralVertexIndex, Direction.West);
                if (west != -1) directionNodes[Direction.West] = west;

                int northwest = nodeManager.GetNeighborInDirection(centralVertexIndex, Direction.Northwest);
                if (northwest != -1 && northwest != west) directionNodes[Direction.Northwest] = northwest;

                int northeast = nodeManager.GetNeighborInDirection(centralVertexIndex, Direction.Northeast);
                if (northeast != -1 && northeast != west && northeast != northwest) directionNodes[Direction.Northeast] = northeast;

                // Add in desired order
                if (directionNodes.ContainsKey(Direction.West))
                {
                    int westNode = directionNodes[Direction.West];
                    if (IsReservedNodeValid(westNode, Direction.West)) reservedNodes.Add(westNode);
                }

                if (directionNodes.ContainsKey(Direction.Northwest))
                {
                    int northwestNode = directionNodes[Direction.Northwest];
                    if (IsReservedNodeValid(northwestNode, Direction.Northwest)) reservedNodes.Add(northwestNode);
                }

                if (directionNodes.ContainsKey(Direction.Northeast))
                {
                    int northeastNode = directionNodes[Direction.Northeast];
                    if (IsReservedNodeValid(northeastNode, Direction.Northeast)) reservedNodes.Add(northeastNode);
                }

                break;
        }

        return reservedNodes;
    }

    private bool IsReservedNodeValid(int reservedNode, Direction direction) // Check if reserved nodes are valid
    {
        List<int> directions = new List<int>();
        int west = -1;
        int northwest = -1;
        int northeast = -1;
        int southeast = -1;
        int southwest = -1;
        int east = -1;

        switch (direction)
        {
            case Direction.West:
                southeast = nodeManager.GetNeighborInDirection(reservedNode, Direction.Southeast);
                directions.Add(southeast);
                southwest = nodeManager.GetNeighborInDirection(reservedNode, Direction.Southwest);
                directions.Add(southwest);
                west = nodeManager.GetNeighborInDirection(reservedNode, Direction.West);
                directions.Add(west);
                northwest = nodeManager.GetNeighborInDirection(reservedNode, Direction.Northwest);
                directions.Add(northwest);
                return CheckValidity(directions);

            case Direction.Northwest:
                
                west = nodeManager.GetNeighborInDirection(reservedNode, Direction.West);
                directions.Add(west);
                northwest = nodeManager.GetNeighborInDirection(reservedNode, Direction.Northwest);
                directions.Add(northwest);
                northeast = nodeManager.GetNeighborInDirection(reservedNode, Direction.Northeast);
                directions.Add(northeast);
                return CheckValidity(directions);

            case Direction.Northeast:
                northwest = nodeManager.GetNeighborInDirection(reservedNode, Direction.Northwest);
                directions.Add(northwest);
                northeast = nodeManager.GetNeighborInDirection(reservedNode, Direction.Northeast);
                directions.Add(northeast);
                east = nodeManager.GetNeighborInDirection(reservedNode, Direction.East);
                directions.Add(east);
                southeast = nodeManager.GetNeighborInDirection(reservedNode, Direction.Southeast);
                directions.Add(southeast);
                return CheckValidity(directions);

            default:
                return false;
        }
    }

    private bool CheckValidity(List<int> directions)
    {
        foreach (int node in directions)
        {
            NodeData nodeData = nodeManager.GetNodeData(node);
            if (nodeData == null || nodeData.HasBuilding || nodeData.HasFlag || nodeData.HasObstacle || nodeData.HasResource ||
                nodeData.TerrainType == TerrainType.Water || nodeData.TerrainType == TerrainType.Mountain || nodeData.TerrainType == TerrainType.Marsh)
                return false;
        }

        return true;
    }

    private bool CanPlaceBuilding(int vertexIndex, BuildingType buildingType)
    {
        if (vertexIndex == -1) return false;

        NodeData centralData = nodeManager.GetNodeData(vertexIndex);
        if (centralData == null || centralData.HasBuilding || centralData.HasFlag || centralData.HasObstacle)
            return false;

        List<int> reservedNodes = GetReservedNodes(vertexIndex, BuildingSizes[buildingType]);
        foreach (int node in reservedNodes)
        {
            NodeData nodeData = nodeManager.GetNodeData(node);
            if (nodeData == null || nodeData.HasBuilding || nodeData.HasFlag || nodeData.HasObstacle)
                return false;
        }

        int southEast = nodeManager.GetNeighborInDirection(vertexIndex, Direction.Southeast);
        if (southEast == -1 || reservedNodes.Contains(southEast)) return false; // Check for valid entrance
        if (!nodeManager.CanPlaceFlag(southEast)) return false; // Check for valid flag placement
        NodeData southEastData = nodeManager.GetNodeData(southEast);
        if (southEastData == null || southEastData.HasBuilding || southEastData.HasFlag || southEastData.HasObstacle)
            return false;

        return true;
    }
}
