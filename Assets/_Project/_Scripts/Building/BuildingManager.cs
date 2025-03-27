using System.Collections.Generic;
using UnityEngine;

namespace PunkyFruitBat
{
    /// <summary>
    /// Represents the type of building that can be placed on a cell.
    /// </summary>
    public enum BuildingType
    {
        None = 0,
        HQ = 1,
        Storehouse = 2,
        WoodCuttersHut = 3,
        GrainFarm = 4,
        ForestersHut = 5,
        Sawmill = 6,
        Quarry = 7,
        Well = 8,
        FishingHut = 9,
        HuntersHut = 10,
        Windmill = 11,
        Bakery = 12,
        PigFarm = 13,
        Slaughterhouse = 14,
        Mine = 15,  // Generic mine, could be specialized later
        Military = 16,
        Blacksmith = 17,
        ChargingStation = 18, // For robots
        RobotFactory = 19,
    }

    public enum BuildingSize
    {
        Small,
        Medium,
        Large
    }

    [System.Serializable]
    public class BuildingManager
    {
        private HexGridManager manager;

        private BuildingPrefabs_SO buildingPrefabs;

        [SerializeField] private int hqIndex = 50;
        public Building_HQ HQ { get; private set; }

        public void Initialise(HexGridManager manager, BuildingPrefabs_SO buildingPrefabs_SO)
        {
            this.manager = manager;
            buildingPrefabs = buildingPrefabs_SO;

            if (buildingPrefabs_SO == null || buildingPrefabs_SO.buildingPrefabs == null || buildingPrefabs_SO.buildingPrefabs.Length < 1)
            {
                Debug.LogError("BuildingPrefabs array is null! Please assign prefabs in the Inspector.");
                return; // Exit the constructor if BuildingPrefabs is null
            }

            manager.OnGridComplete += BuildHq;
            manager.OnCreateBuildingButtonPressed += TryBuildBuilding;
        }

        private void BuildHq()
        {
            TryBuildBuilding(hqIndex, BuildingType.HQ);
        }

        private void TryBuildBuilding(int vertexIndex, BuildingType buildingType)
        {
            if (!CanPlaceBuilding(vertexIndex, buildingType)) return;

            GameObject newBuilding = GameObject.Instantiate(buildingPrefabs.buildingPrefabs[(int)buildingType], manager.NodeManager.GetNodePosition(vertexIndex), Quaternion.identity);
            newBuilding.transform.SetParent(manager.BuildingTransform);
            Building building = newBuilding.GetComponent<Building>();
            building.InitialiseBuild(manager, this, buildingType, vertexIndex);

            if (buildingType == BuildingType.HQ) HQ = (Building_HQ)building;
        }

        public bool CanPlaceBuilding(int vertexIndex, BuildingType buildingType)
        {
            if (buildingType == BuildingType.None || (buildingType == BuildingType.HQ && HQ != null)) return false;
            Node vertexIndexNodeData = manager.NodeManager.GetNode(vertexIndex);
            if (vertexIndexNodeData == null || vertexIndexNodeData.HasBuilding || vertexIndexNodeData.HasFlag || vertexIndexNodeData.HasObstacle || vertexIndexNodeData .IsEdgeNode) return false;
            int entranceIndex = manager.NodeManager.GetNeighbourInDirection(vertexIndex, Direction.Southeast);
            Node entranceNodeData = manager.NodeManager.GetNode(entranceIndex);
            if (!manager.FlagManager.CanPlaceFlag(entranceIndex) && !entranceNodeData.HasFlag) return false;
            BuildingSize BuildingSize = GetBuildingSize(buildingType);
            int[] reservedNodes = GetReservedNodes(vertexIndex, BuildingSize);
            if (BuildingSize == BuildingSize.Large && reservedNodes.Length < 4) return false;

            return true;
        }

        public BuildingSize GetBuildingSize(BuildingType buildingType)
        {
            return buildingType switch
            {
                BuildingType.HQ => BuildingSize.Large,
                BuildingType.Storehouse => BuildingSize.Medium,
                BuildingType.WoodCuttersHut => BuildingSize.Small,
                BuildingType.ForestersHut => BuildingSize.Small,
                BuildingType.Sawmill => BuildingSize.Medium,
                BuildingType.Quarry => BuildingSize.Small,
                BuildingType.Well => BuildingSize.Small,
                BuildingType.FishingHut => BuildingSize.Small,
                BuildingType.HuntersHut => BuildingSize.Small,
                BuildingType.GrainFarm => BuildingSize.Large,
                BuildingType.Windmill => BuildingSize.Medium,
                BuildingType.Bakery => BuildingSize.Medium,
                BuildingType.PigFarm => BuildingSize.Large,
                BuildingType.Slaughterhouse => BuildingSize.Medium,
                BuildingType.Mine => BuildingSize.Small,
                BuildingType.Military => BuildingSize.Small,
                BuildingType.Blacksmith => BuildingSize.Medium,
                _ => BuildingSize.Small,
            };
        }

        public int[] GetReservedNodes(int centralNodeIndex, BuildingSize buildingSize)
        {
            List<int> reservedNodes = new() { centralNodeIndex };
            if (buildingSize != BuildingSize.Large) return reservedNodes.ToArray();

            int west = manager.NodeManager.GetNeighbourInDirection(centralNodeIndex, Direction.West);
            if (west != 0 && IsReservedNodeValid(west))
            {
                int westNeighbourNode = manager.NodeManager.GetNeighbourInDirection(west, Direction.West);
                if (westNeighbourNode != 0 && IsReservedNodeValid(westNeighbourNode))
                    reservedNodes.Add(west);
            }

            int northwest = manager.NodeManager.GetNeighbourInDirection(centralNodeIndex, Direction.Northwest);
            if (northwest != 0 && IsReservedNodeValid(northwest))
            {
                int westNeighbourNode = manager.NodeManager.GetNeighbourInDirection(northwest, Direction.West);
                int northeastNeighbourNode = manager.NodeManager.GetNeighbourInDirection(northwest, Direction.Northeast);
                if (westNeighbourNode != 0 && IsReservedNodeValid(westNeighbourNode) && northeastNeighbourNode != 0 && IsReservedNodeValid(northeastNeighbourNode))
                    reservedNodes.Add(northwest);
            }

            int northeast = manager.NodeManager.GetNeighbourInDirection(centralNodeIndex, Direction.Northeast);
            if (northeast != 0 && IsReservedNodeValid(northeast))
            {
                int northeastNeighbourNode = manager.NodeManager.GetNeighbourInDirection(northeast, Direction.Northeast);
                int eastNeighbourNode = manager.NodeManager.GetNeighbourInDirection(northeast, Direction.East);
                if (northeastNeighbourNode != 0 && IsReservedNodeValid(northeastNeighbourNode) && eastNeighbourNode != 0 && IsReservedNodeValid(eastNeighbourNode))
                    reservedNodes.Add(northeast);
            }

            return reservedNodes.ToArray();
        }

        // Check if the reserved node is valid
        public bool IsReservedNodeValid(int reservedNode)
        {
            Node nodeData = manager.NodeManager.GetNode(reservedNode);
            return nodeData != null && !nodeData.HasBuilding && !nodeData.HasFlag && !nodeData.HasObstacle && !nodeData.IsEdgeNode;
        }

        public int GetStorehouseNode()
        {
            return HQ.CenterIndex;
        }

        public int GetStorehouseEntranceNode()
        {
            return HQ.EntranceIndex;
        }

        public Building GetBuildingAtNode(int nodeIndex)
        {
            Node node = manager.NodeManager.GetNode(nodeIndex);
            return node.GetBuildingOnNode();
        }

        public int GetBuildingNode(Building building)
        {
            return building.CenterIndex;
        }

        public int GetEntranceNode(Building building)
        {
            return building.EntranceIndex;
        }

        public void Unsubscribe()
        {
            manager.OnCreateBuildingButtonPressed -= TryBuildBuilding;
            manager.OnGridComplete -= BuildHq;
        }
    }
}
