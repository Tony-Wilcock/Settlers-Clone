using System.Collections.Generic;
using UnityEngine;

namespace PunkyFruitBat
{
    public abstract class Building : MonoBehaviour
    {
        [field: SerializeField] protected BuildingType buildingType;
        [field: SerializeField] protected BuildingSize buildingSize;
        [field: SerializeField] protected Transform buildingGFXTransform;
        [field: SerializeField] protected int centerIndex;
        [field: SerializeField] protected int entranceIndex;
        [field: SerializeField] protected Flag entranceFlag;
        [field: SerializeField] protected int[] reservedNodes;
        [field: SerializeField] protected Dictionary<ResourceType, int> buildingCost;
        [field: SerializeField] protected bool isConstructed = false;
        [field: SerializeField] public Builder AssignedBuilder { get; set; }
        [field: SerializeField] public Character AssignedWorker { get; protected set; }

        public BuildingType BuildingType => buildingType;
        public BuildingSize BuildingSize => buildingSize;
        public Transform BuildingGFXTransform => buildingGFXTransform;
        public int CenterIndex => centerIndex;
        public int EntranceIndex => entranceIndex;
        public Flag EntranceFlag => entranceFlag;
        public int[] ReservedNodes => reservedNodes;
        public bool IsConstructed
        {
            get => isConstructed;
            set => isConstructed = value;
        }
        public Dictionary<ResourceType, int> BuildingCost => buildingCost;
        
        // --- Construction Progress ---
        public enum ConstructionStage
        {
            Planned,      // Initial state
            AwaitingWood,
            ConstructingWood,
            AwaitingStone,
            ConstructingStone,
            Complete
        }
        [field: SerializeField] public ConstructionStage CurrentStage { get; protected set; } = ConstructionStage.Planned;

        // Track resources delivered TO the site (simplification, assumes delivery happens instantly or builder fetches)
        protected Dictionary<ResourceType, int> resourcesOnSite = new();
        public Dictionary<ResourceType, int> ResourcesOnSite => resourcesOnSite;

        protected HexGridManager manager;
        protected BuildingManager buildingManager;

        public void InitialiseBuild(HexGridManager manager, BuildingManager buildingManager, BuildingType buildingType, int centerIndex)
        {
            this.manager = manager;
            this.buildingManager = buildingManager;
            this.buildingType = buildingType;
            this.buildingSize = buildingManager.GetBuildingSize(buildingType);
            this.buildingGFXTransform = transform.GetChild(0);
            this.centerIndex = centerIndex;
            this.entranceIndex = manager.NodeManager.GetNeighbourInDirection(centerIndex, Direction.Southeast);
            this.reservedNodes = buildingManager.GetReservedNodes(centerIndex, buildingSize);

            AssignBuildingCost();
            Build();

            //manager.OnGridComplete += DetermineInitialConstructionStage;
            DetermineInitialConstructionStage();
        }

        protected void DetermineInitialConstructionStage()
        {
            // Ensure costs are set BEFORE this
            if (buildingCost == null || buildingCost.Count == 0)
            {
                Debug.LogError($"BuildingCost not set for {buildingType}!", this);
                CurrentStage = ConstructionStage.Complete; // Or some error state
                return;
            }

            if (buildingCost.ContainsKey(ResourceType.Wood) && buildingCost[ResourceType.Wood] > 0)
            {
                CurrentStage = ConstructionStage.AwaitingWood;
            }
            else if (buildingCost.ContainsKey(ResourceType.Stone) && buildingCost[ResourceType.Stone] > 0)
            {
                CurrentStage = ConstructionStage.AwaitingStone;
            }
            else
            {
                CurrentStage = ConstructionStage.Complete; // No cost? Instantly built (like HQ?)
                MarkConstructionComplete(true); // Mark as complete immediately
            }
        }

        public bool HasEnoughResourcesForStage(ConstructionStage stage)
        {
            if (buildingCost == null) return true; // No cost defined

            switch (stage)
            {
                case ConstructionStage.AwaitingWood:
                case ConstructionStage.ConstructingWood:
                    if (buildingCost.TryGetValue(ResourceType.Wood, out int woodNeeded))
                    {
                        resourcesOnSite.TryGetValue(ResourceType.Wood, out int woodHave);
                        return woodHave >= woodNeeded;
                    }
                    return true; // No wood cost defined for this building

                case ConstructionStage.AwaitingStone:
                case ConstructionStage.ConstructingStone:
                    if (buildingCost.TryGetValue(ResourceType.Stone, out int stoneNeeded))
                    {
                        resourcesOnSite.TryGetValue(ResourceType.Stone, out int stoneHave);
                        return stoneHave >= stoneNeeded;
                    }
                    return true; // No stone cost defined

                default:
                    return true; // Other stages don't check resources here
            }
        }

        // Method for builder/carrier to add resources (placeholder)
        public void AddResourceToSite(ResourceType type, int amount = 1)
        {
            if (!resourcesOnSite.ContainsKey(type)) resourcesOnSite[type] = 0;
            resourcesOnSite[type] += amount;
        }

        // Methods for the Builder to call
        public void StartWoodConstruction()
        {
            if (CurrentStage == ConstructionStage.AwaitingWood && HasEnoughResourcesForStage(CurrentStage))
            {
                CurrentStage = ConstructionStage.ConstructingWood;
            }
            else
            {
                Debug.LogWarning($"Cannot start wood construction for {buildingType}. Stage: {CurrentStage}, HasResources: {HasEnoughResourcesForStage(ConstructionStage.AwaitingWood)}");
            }
        }

        public void CompleteWoodConstruction()
        {
            if (CurrentStage == ConstructionStage.ConstructingWood)
            {
                // Check if stone is needed next
                if (buildingCost != null && buildingCost.ContainsKey(ResourceType.Stone) && buildingCost[ResourceType.Stone] > 0)
                {
                    CurrentStage = ConstructionStage.AwaitingStone;
                }
                else
                {
                    MarkConstructionComplete(true); // Wood was the last stage
                }
            }
        }

        public void StartStoneConstruction()
        {
            if (CurrentStage == ConstructionStage.AwaitingStone && HasEnoughResourcesForStage(CurrentStage))
            {
                CurrentStage = ConstructionStage.ConstructingStone;
            }
            else
            {
                Debug.LogWarning($"Cannot start stone construction for {buildingType}. Stage: {CurrentStage}, HasResources: {HasEnoughResourcesForStage(ConstructionStage.AwaitingStone)}");
            }
        }

        public void CompleteStoneConstruction()
        {
            if (CurrentStage == ConstructionStage.ConstructingStone)
            {
                // Assume stone is last stage for now
                MarkConstructionComplete(true);
            }
        }

        // Call this when ALL stages are done
        private void MarkConstructionComplete(bool success)
        {
            if (success)
            {
                CurrentStage = ConstructionStage.Complete;
                IsConstructed = true; // Use the property setter
                buildingGFXTransform.gameObject.SetActive(true); // Show final graphics

                // Assign specific worker based on building type
                AssignWorkerBasedOnBuildingType();
            }
            else
            {
                // Handle failure? Reset stage?
            }
            AssignedBuilder = null; // Clear assignment
        }

        protected virtual void AssignWorkerBasedOnBuildingType()
        {
            CharacterType workerType = RequiredWorkerType;

            Character worker = manager.CharacterManager.GetCharacter(workerType);

            if (worker != null)
            {
                AssignedWorker = worker;
                AssignedWorker.SetWorkNodeIndex(centerIndex);
                StartCoroutine(worker.MoveCharacter(centerIndex));
            }
            else
            {
                Debug.LogWarning($"No available worker of type {workerType} for building {buildingType} at index {centerIndex}.");
            }
        }

        private void Build()
        {
            buildingGFXTransform.gameObject.SetActive(false);
            manager.FlagManager.PlaceFlag(entranceIndex);
            Flag flag = manager.FlagManager.TryGetFlag(entranceIndex);
            if (!flag.IsFlagAttachedToBuilding)
            {
                flag.SetFlagAttachedToBuilding(true);
            }
            entranceFlag = flag;
            gameObject.name = $"{buildingType}_{centerIndex}";

            DrawPathVisual();
            manager.UIManager.HideAllPanels();

            foreach (int nodeIndex in reservedNodes)
            {
                Node node = manager.NodeManager.GetNode(nodeIndex);
                node.SetBuildingOnNode(this);
            }
        }

        private void DrawPathVisual()
        {
            Node entranceNode = manager.NodeManager.GetNode(entranceIndex);
            Node centerNode = manager.NodeManager.GetNode(centerIndex);
            // Get the Y angle between the entranceNode and the centerNode with 0 being the forward direction
            float angle = Vector3.SignedAngle(Vector3.forward, centerNode.transform.position - entranceNode.transform.position, Vector3.up);

            Vector3 position = entranceNode.transform.position;

            // Get a path visual from the pool and spawn at the start position with the angle between the start and end nodes
            GameObject visual = manager.PathManager.GetPathVisualsFromPool();
            visual.transform.SetPositionAndRotation(position, Quaternion.Euler(0, angle, 0));
            visual.transform.SetParent(transform);
        }

        public Building GetBuildingTypeAtNode(int nodeIndex)
        {
            Node node = manager.NodeManager.GetNode(nodeIndex);
            if (node == null || !node.HasBuilding) return null;
            Building building = node.transform.GetChild(0).GetComponent<Building>();
            return building;
        }

        public int GetBuildingCostByResourceType(ResourceType type)
        {
            if (buildingCost.TryGetValue(type, out int cost))
            {
                return cost;
            }
            return 0;
        }

        /// <summary>
        /// Derived classes must specify the type of worker this building requires.
        /// Return a default or 'None' value if no worker is needed.
        /// </summary>
        protected abstract CharacterType RequiredWorkerType { get; }

        /// <summary>
        /// Derived classes must specify the wood cost of the building.
        /// </summary>
        protected abstract int WoodCost { get; }

        /// <summary>
        /// Derived classes must specify the stone cost of the building.
        /// </summary>
        protected abstract int StoneCost { get; }

        /// <summary>
        /// Derived classes must implement this method to assign the building cost.
        /// </summary>
        public abstract void AssignBuildingCost();

        protected void SetBuildingCost(int woodCost, int stoneCost)
        {
            buildingCost = new Dictionary<ResourceType, int>
            {
                { ResourceType.Wood, woodCost },
                { ResourceType.Stone, stoneCost }
            };
        }
    }
}
