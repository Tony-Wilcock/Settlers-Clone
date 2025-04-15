using System;
using System.Collections.Generic;
using UnityEngine;

namespace PunkyFruitBat
{
    public partial class BuilderManager : BaseSpecificCharacterManager<Builder>
    {
        public event Action<Building> OnBuildingConstructionComplete;

        public override CharacterType ManagedType => CharacterType.Builder;

        private Queue<Builder> builderPool = new();

        private Queue<Building> BuildingJobs { get; set; } = new();

        public override void HandleGridComplete()
        {
            gridManager.BuildingManager.OnBuildingRequestSubmitted += HandleBuildingRequest;
            gridManager.PathManager.OnPathCreationCompleted += ProcessBuildingsAwaitingBuilderQueue;

            InitialiseBuilderPool();
        }

        // --- Pooling Logic (Moved from CharacterManager) ---
        private void InitialiseBuilderPool()
        {
            builderPool = new Queue<Builder>();
            IncreaseBuilderPool(5); // Or read from config
        }

        private void IncreaseBuilderPool(int amount)
        {
            GameObject prefab = characterPrefabs.characterPrefabs[(int)ManagedType];
            if (prefab == null)
            {
                Debug.LogError($"Prefab for {ManagedType} not found!");
                return;
            }

            int storehouseNode = gridManager.BuildingManager.GetStorehouseNode(); // Get once
            Vector3 initialPosition = gridManager.NodeManager.GetNodePosition(storehouseNode);

            for (int i = 0; i < amount; i++)
            {
                GameObject characterGO = GameObject.Instantiate(prefab);
                characterGO.transform.position = initialPosition;

                if (typeSpecificParentTransform != null) characterGO.transform.SetParent(typeSpecificParentTransform);
                else Debug.LogWarning($"Parent transform for {ManagedType} not set. Character '{characterGO.name}' will be at scene root.", characterGO);

                if (!characterGO.TryGetComponent<Builder>(out Builder builder))
                {
                    Debug.LogError($"Prefab for {ManagedType} is missing Builder component!");
                    GameObject.Destroy(characterGO); // Clean up unusable instance
                    continue;
                }

                builder.InitialiseCharacter(ManagedType, storehouseNode);
                characterGO.SetActive(false);
                builderPool.Enqueue(builder);
            }
        }

        public override Character GetCharacterInstance(int spawnNodeIndex = -1)
        {
            if (builderPool.Count == 0)
            {
                Debug.LogWarning("Builder pool empty, increasing size.");
                IncreaseBuilderPool(5); // Or read from config
            }

            if (builderPool.Count == 0) // Check again after trying to increase
            {
                Debug.LogError("Failed to increase builder pool or pool still empty. Cannot get builder.");
                return null;
            }

            Builder builder = builderPool.Dequeue();

            if (spawnNodeIndex != -1) builder.transform.position = gridManager.NodeManager.GetNodePosition(spawnNodeIndex);

            builder.gameObject.SetActive(true);
            return builder;
        }

        public override void ReturnCharacterInstance(Character character)
        {
            if (character is not Builder builder)
            {
                Debug.LogError($"Tried to return non-Builder character to Builder pool: {character.name}");
                return;
            }

            if (builder == null || !builder.gameObject.activeInHierarchy) return; // Already returned

            builder.StopAllCoroutines(); // Stop any running coroutines

            builder.StartCoroutine(builder.MoveCharacter(builder.WorkNodeIndex, () =>
            {
                builder.gameObject.SetActive(false);
                builderPool.Enqueue(builder);
            }));
        }

        public override void InstantlyReturnCharacterInstance(Character character)
        {
            if (character is not Builder builder)
            {
                Debug.LogError($"Tried to instantly return non-Builder: {character?.name}");
                return;
            }
            if (builder == null) return;

            Debug.Log($"Instantly returning builder {builder.GetInstanceID()} to pool");
            builder.StopAllCoroutines();
            builder.ClearTask(); // Ensure task is cleared

            builder.gameObject.SetActive(false);
            // Reset position?
            builder.transform.position = gridManager.NodeManager.GetNodePosition(builder.WorkNodeIndex); // Assuming WorkNodeIndex is storehouse

            // Avoid double-adding
            if (!builderPool.Contains(builder))
            {
                builderPool.Enqueue(builder);
            }
            else
            {
                Debug.LogWarning($"Builder {builder.GetInstanceID()} already in pool during instant return?");
            }

            // Process queue as a builder is now free
            ProcessBuildingsAwaitingBuilderQueue();
        }

        // --- Builder-Specific Event Handling ---

        private void HandleBuildingRequest(Building building)
        {
            if (building == null) return;
            
            TryAssignBuilderToBuilding(building);
            ProcessBuildingsAwaitingBuilderQueue();
        }

        private void TryAssignBuilderToBuilding(Building building)
        {
            if (building.AssignedBuilder != null || building == null || building.IsConstructed)
            {
                Debug.Log($"Building {building.CenterIndex} already has a builder assigned.");
                return;
            }

            if (builderPool.Count == 0)
            {
                BuildingJobs.Enqueue(building);
                return;
            }

            Path path = gridManager.PathManager.GetPathAtNode(building.EntranceIndex);

            if (path == null || !gridManager.PathManager.PathFinder.IsPathConnectedToStorehouse(path))
            {
                BuildingJobs.Enqueue(building);
                return;
            }

            // Request resources for the building
            // Get the wood amount and stone amount required for the building
            int woodCost = building.GetBuildingCostByResourceType(ResourceType.Wood);
            int stoneCost = building.GetBuildingCostByResourceType(ResourceType.Stone);
            if (woodCost > 0)
            {
                for (int i = 0; i < woodCost; i++)
                    gridManager.ResourceManager.RequestResources(ResourceType.Wood, building);
            }
            if (stoneCost > 0)
            {
                for (int i = 0; i < stoneCost; i++)
                    gridManager.ResourceManager.RequestResources(ResourceType.Stone, building);
            }

            // Send out a builder to build the building
            Builder builder = GetCharacterInstance() as Builder;
            if (builder == null)
            {
                Debug.LogError("Builder pool error: Count > 0 but GetCharacterInstance returned null!");
                BuildingJobs.Enqueue(building); // Re-queue
                return;
            }

            building.AssignedBuilder = builder;
            builder.AssignConstructionTask(building);
            builder.StartCoroutine(builder.PerformConstruction(building));
        }

        /// <summary>
        /// Processes the queue of buildings waiting for a builder assignment.
        /// It iterates through buildings currently in the BuildingJobs queue,
        /// attempts to assign an available builder if connectivity requirements are met,
        /// and re-queues buildings that cannot be assigned yet.
        /// </summary>
        private void ProcessBuildingsAwaitingBuilderQueue(Path path = null)
        {
            if (BuildingJobs.Count <= 0) return;
            Queue<Building> waitingQueue = BuildingJobs;

            // Use a temporary list to avoid issues with modifying the queue while iterating
            List<Building> buildingsToProcess = new(waitingQueue);
            waitingQueue.Clear(); // Clear original queue

            int processedCount = 0;
            foreach (Building buildingToCheck in buildingsToProcess)
            {
                processedCount++;

                // Re-validate building before processing
                if (buildingToCheck == null || buildingToCheck.CenterIndex == -1)
                {
                    Debug.LogWarning($"[BuilderManager] Building {buildingToCheck?.CenterIndex ?? -1} in queue is invalid/destroyed. Skipping.");
                    continue; // Skip invalid/destroyed buildings
                }
                if (buildingToCheck.IsConstructed)
                {
                    Debug.Log($"[BuilderManager] Building {buildingToCheck.CenterIndex} in queue already constructed. Skipping.");
                    continue; // Skip already constructed buildings
                }

                // Retry assignment logic. This will re-queue if still unconnected or blocked.
                TryAssignBuilderToBuilding(buildingToCheck);
            }
        }

        public override void Unsubscribe()
        {
            gridManager.BuildingManager.OnBuildingRequestSubmitted -= HandleBuildingRequest;
        }
    }
}
