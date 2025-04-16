using System;
using System.Collections.Generic;
using UnityEngine;

namespace PunkyFruitBat
{
    public partial class BuilderManager : BaseSpecificCharacterManager<Builder>
    {
        public event Action<Building> OnBuildingConstructionComplete;

        public override CharacterType ManagedType => CharacterType.Builder;

        private Queue<Building> BuildingJobs { get; set; } = new();

        public override void HandleGridComplete()
        {
            base.InitialisePool(5);

            gridManager.BuildingManager.OnBuildingRequestSubmitted += HandleBuildingRequest;
            gridManager.PathManager.OnPathCreationCompleted += ProcessBuildingsAwaitingBuilderQueue;
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

            Path path = gridManager.PathManager.GetPathAtNode(building.EntranceIndex);

            if (path == null || !gridManager.PathManager.PathFinder.IsPathConnectedToStorehouse(path))
            {
                if (!BuildingJobs.Contains(building)) // Avoid adding duplicates
                {
                    BuildingJobs.Enqueue(building);
                }
                return; // Not reachable yet
            }

            // Request resources BEFORE assigning builder
            bool requested = RequestBuildingResources(building);
            if (!requested)
            {
                Debug.LogWarning($"Could not request resources for {building.name}. Builder not assigned yet.");
                if (!BuildingJobs.Contains(building)) BuildingJobs.Enqueue(building); // Requeue if resources failed
                return;
            }

            if (characterPool.Count == 0)
            {
                if (!BuildingJobs.Contains(building)) BuildingJobs.Enqueue(building);
                return;
            }

            // Send out a builder to build the building
            Character baseChar = base.GetCharacterInstance(); // Get from generic pool
            if (baseChar is Builder builder) // Safely cast
            {
                building.AssignedBuilder = builder;
                builder.AssignConstructionTask(building);
                builder.StartCoroutine(builder.PerformConstruction(building));
                Debug.Log($"Assigned Builder {builder.GetInstanceID()} to {building.name}");
            }
            else
            {
                Debug.LogError("Dequeued character was not a Builder or was null!");
                if (baseChar != null) base.InstantlyReturnCharacterInstance(baseChar); // Return wrong type instantly
                if (!BuildingJobs.Contains(building)) BuildingJobs.Enqueue(building); // Re-queue the job
            }
        }

        private bool RequestBuildingResources(Building building)
        {
            if (building == null || building.BuildingCost == null) return false;

            bool allRequestsSuccessful = true; // Assume success initially

            foreach (var costPair in building.BuildingCost)
            {
                ResourceType type = costPair.Key;
                int amountNeeded = costPair.Value;

                if (amountNeeded <= 0) continue; // Skip if no cost for this type

                // Check if enough resources ALREADY requested/delivered (optional optimization)
                building.ResourcesOnSite.TryGetValue(type, out int amountHave);
                int stillNeeded = amountNeeded - amountHave;

                if (stillNeeded <= 0) continue; // Already have enough

                for (int i = 0; i < stillNeeded; i++)
                {
                    // Check availability *before* requesting (crucial!)
                    if (gridManager.ResourceManager.GetResourceAmount(type) > 0)
                    {
                        gridManager.ResourceManager.RequestResources(type, building);
                        // NOTE: RequestResources likely *removes* from central storage immediately.
                        // If it only queues, the logic here needs adjustment.
                    }
                    else
                    {
                        Debug.LogWarning($"Not enough {type} in central storage to request for {building.name}. Needed {stillNeeded}, have 0.");
                        allRequestsSuccessful = false;
                        // Decide if you want to stop requesting other types if one fails
                        // break; // Uncomment to stop requesting other resources if one type is unavailable
                    }
                }
            }
            return allRequestsSuccessful; // Return true only if ALL required requests could be initiated
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

            // Use a temporary list to avoid issues with modifying the queue while iterating
            List<Building> buildingsToProcess = new(BuildingJobs);
            BuildingJobs.Clear(); // Clear original queue

            foreach (Building buildingToCheck in buildingsToProcess)
            {
                // Re-validate building before processing
                if (buildingToCheck == null || buildingToCheck.CenterIndex == -1)
                {
                    Debug.LogWarning($"[BuilderManager] Building {buildingToCheck?.CenterIndex ?? -1} in queue is invalid/destroyed. Skipping.");
                    continue; // Skip invalid/destroyed buildings
                }
                if (buildingToCheck.IsConstructed || buildingToCheck.CurrentStage == Building.ConstructionStage.Complete)
                {
                    Debug.Log($"[BuilderManager] Building {buildingToCheck.CenterIndex} in queue already constructed. Skipping.");
                    continue; // Skip already constructed buildings
                }
                if (buildingToCheck.AssignedBuilder != null)
                {
                    // Debug.Log($"[BuilderManager] Building {buildingToCheck.CenterIndex} in queue already has a builder. Skipping.");
                    continue;
                }

                // Retry assignment logic. This will re-queue if still unconnected or blocked.
                TryAssignBuilderToBuilding(buildingToCheck);
            }
        }

        public override void Unsubscribe()
        {
            // Unsubscribe from Builder-specific events
            if (gridManager?.BuildingManager != null)
                gridManager.BuildingManager.OnBuildingRequestSubmitted -= HandleBuildingRequest;
            if (gridManager?.PathManager != null)
                gridManager.PathManager.OnPathCreationCompleted -= ProcessBuildingsAwaitingBuilderQueue;

            BuildingJobs.Clear(); // Clear specific queue
            base.Unsubscribe(); // Call base if it ever does anything
        }
    }
}
