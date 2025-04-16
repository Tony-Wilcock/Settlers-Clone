using System;
using System.Collections.Generic;
using UnityEngine;

namespace PunkyFruitBat
{
    /// <summary>
    /// Represents the type of building that can be placed on a cell.
    /// </summary>
    public enum BuildingType // Ensure this matches the order in the inspector
    {
        // Large buildings
        GrainFarm,
        HQ,
        PigFarm,

        // Medium buildings
        Bakery, // 3
        Sawmill,
        Slaughterhouse, // 5
        Storehouse,
        Windmill, // 7

        // Small buildings
        FishingHut, // 8
        ForestersHut, // 9
        HuntersHut,
        Quarry,
        Well,
        WoodcuttersHut, // 13
    }

    public enum BuildingSize
    {
        Small,
        Medium,
        Large
    }

    [Serializable]
    public class BuildingManager
    {
        public event Action<Building> OnBuildingRequestSubmitted;

        private HexGridManager manager;
        private BuildingPrefabs_SO buildingPrefabs;

        [SerializeField] private int hqIndex = 50;
        public Building_HQ HQ { get; private set; }

        private Dictionary<int, Building> AllBuildings { get; } = new Dictionary<int, Building>();
        public IReadOnlyDictionary<int, Building> GetAllBuildings => AllBuildings;

        public void Initialise(HexGridManager manager, BuildingPrefabs_SO buildingPrefabs_SO)
        {
            this.manager = manager;
            buildingPrefabs = buildingPrefabs_SO;

            if (buildingPrefabs_SO == null || buildingPrefabs_SO.buildingPrefabs == null || buildingPrefabs_SO.buildingPrefabs.Length < 1)
            {
                Debug.LogError("BuildingPrefabs array is null! Please assign prefabs in the Inspector.");
                return; // Exit the constructor if BuildingPrefabs is null
            }

            manager.OnGridComplete += HandleOnGridComplete;
            //manager.OnCreateBuildingButtonPressed += TryBuildBuilding;
        }

        private void HandleOnGridComplete()
        {

            // Subscribe to BuilderManager's OnBuildingConstructionComplete event
            if (manager.CharacterManager.GetSpecificManager(CharacterType.Builder) is BuilderManager builderManager)
            {
                builderManager.OnBuildingConstructionComplete += HandleBuildingConstructionComplete;
            }
            manager.OnCreateBuildingButtonPressed += TryBuildBuilding;
            TryBuildBuilding(hqIndex, BuildingType.HQ);
        }

        private void TryBuildBuilding(int vertexIndex, BuildingType buildingType)
        {
            if (buildingType == BuildingType.HQ && HQ != null) return;
            BuildingSize BuildingSize = GetBuildingSize(buildingType);
            if (!CanPlaceBuilding(vertexIndex, BuildingSize)) return;

            GameObject newBuilding = GameObject.Instantiate(buildingPrefabs.buildingPrefabs[(int)buildingType], manager.NodeManager.GetNodePosition(vertexIndex), Quaternion.identity);
            newBuilding.transform.SetParent(manager.BuildingTransform);
            Building building = newBuilding.GetComponent<Building>();
            building.InitialiseBuild(manager, this, buildingType, vertexIndex);

            if (buildingType == BuildingType.HQ)
            {
                HQ = (Building_HQ)building;
                AddToAllBuildings(building);
                HandleBuildingConstructionComplete(building);
                return;
            }

            OnBuildingRequestSubmitted?.Invoke(building);
            AddToAllBuildings(building);
        }

        public bool CanPlaceBuilding(int vertexIndex, BuildingSize BuildingSize)
        {
            Node vertexIndexNodeData = manager.NodeManager.GetNode(vertexIndex);
            if (vertexIndexNodeData == null || vertexIndexNodeData.HasBuilding || vertexIndexNodeData.HasFlag || vertexIndexNodeData.HasObstacle || vertexIndexNodeData.IsEdgeNode || vertexIndexNodeData.HasPath) return false;
            int entranceIndex = manager.NodeManager.GetNeighbourInDirection(vertexIndex, Direction.Southeast);
            Node entranceNodeData = manager.NodeManager.GetNode(entranceIndex);
            if (!manager.FlagManager.CanPlaceFlag(entranceIndex) && !entranceNodeData.HasFlag) return false;
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
                BuildingType.WoodcuttersHut => BuildingSize.Small,
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

        private void HandleBuildingConstructionComplete(Building building)
        {
            building.BuildingGFXTransform.gameObject.SetActive(true);
            building.IsConstructed = true;
            building.AssignedBuilder = null;
        }

        public void AddToAllBuildings(Building building)
        {
            AllBuildings.Add(building.CenterIndex, building);
        }

        public void RemoveFromAllBuildings(Building building)
        {
            // TODO: Handle OnRemeved in Building
            AllBuildings.Remove(building.CenterIndex);
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
            manager.OnGridComplete -= HandleOnGridComplete;
        }
    }
}
