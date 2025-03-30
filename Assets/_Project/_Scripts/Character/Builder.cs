using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PunkyFruitBat
{
    public class Builder : Character // Make sure Character is not abstract if Builder derives directly
    {
        private Building currentBuildingTask = null;
        private bool isConstructing = false; // Simple state
        private float constructionTime = 5.0f;

        protected override void Awake()
        {
            base.Awake();
            // characterType = CharacterType.Builder; // Already set in base via InitialiseCharacter
        }

        /// <summary>
        /// Assigns a construction task to this builder.
        /// </summary>
        public void AssignConstructionTask(Building building)
        {
            if (building != null && !isConstructing)
            {
                currentBuildingTask = building;
                Debug.Log($"Builder {GetInstanceID()} assigned to build {building.BuildingType} at {building.CenterIndex}");
            }
            else
            {
                Debug.LogWarning($"Builder {GetInstanceID()} cannot be assigned task. Current task: {currentBuildingTask?.BuildingType}, IsConstructing: {isConstructing}");
            }
        }

        /// <summary>
        /// Clears the current construction task.
        /// </summary>
        public void ClearTask()
        {
            currentBuildingTask = null;
            isConstructing = false;
            Debug.Log($"Builder {GetInstanceID()} task cleared.");
        }

        /// <summary>
        /// Coroutine to move to the building site and perform staged construction.
        /// </summary>
        public IEnumerator PerformConstruction(Building building)
        {
            if (building == null)
            {
                Debug.LogError("PerformConstruction called with null building!", this);
                ClearTask(); // Clear invalid task
                             // Notify manager? Or just return builder instantly?
                characterManager.InstantlyReturnCharacter(this); // Safer return
                yield break;
            }

            isConstructing = true; // Mark as busy

            // 1. Move to the building entrance
            int buildingEntrance = building.EntranceIndex;
            Debug.Log($"Builder {GetInstanceID()} moving to entrance {buildingEntrance} for building {building.BuildingType}");
            yield return StartCoroutine(MoveCharacter(buildingEntrance)); // Use base class movement

            // Check if we actually reached the entrance
            if (CurrentNodeIndex != building.EntranceIndex)
            {
                Debug.LogError($"Builder {GetInstanceID()} failed to reach entrance {building.EntranceIndex}! Current node: {CurrentNodeIndex}. Aborting construction.", this);
                isConstructing = false;
                ClearTask();
                // Should the building be re-queued by the manager? Maybe fire an event?
                // For now, just return the builder.
                characterManager.ReturnCharacter(this); // Normal return to pool
                yield break;
            }

            Debug.Log($"Builder {GetInstanceID()} arrived at entrance {CurrentNodeIndex}. Starting construction checks.");

            // 2. Construction Loop (Simplified - assumes resources are magically present)
            while (building.CurrentStage != Building.ConstructionStage.Complete && currentBuildingTask == building)
            {
                switch (building.CurrentStage)
                {
                    case Building.ConstructionStage.AwaitingWood:
                        if (building.HasEnoughResourcesForStage(building.CurrentStage)) // Check if resources are "delivered"
                        {
                            building.StartWoodConstruction();
                            Debug.Log($"Builder {GetInstanceID()} starting wood construction for {building.BuildingType}.");
                        }
                        else
                        {
                            Debug.Log($"Builder {GetInstanceID()} waiting for wood for {building.BuildingType}.");
                            // TODO: Implement resource request/fetching logic if needed
                            // For now, just wait a bit and re-check
                            yield return StartCoroutine(WaitForSecondsFactory.WaitCoroutine(1.0f));
                        }
                        break;

                    case Building.ConstructionStage.ConstructingWood:
                        Debug.Log($"Builder {GetInstanceID()} constructing wood for {building.BuildingType}...");
                        // TODO: Play animation?
                        int amountOfWoodNeeded = building.GetBuildingCostByResourceType(ResourceType.Wood);
                        yield return StartCoroutine(WaitForSecondsFactory.WaitCoroutine(constructionTime * amountOfWoodNeeded)); // Use a getter for time
                        building.CompleteWoodConstruction();
                        Debug.Log($"Builder {GetInstanceID()} finished wood stage for {building.BuildingType}.");
                        break;

                    case Building.ConstructionStage.AwaitingStone:
                        if (building.HasEnoughResourcesForStage(building.CurrentStage)) // Check if resources are "delivered"
                        {
                            building.StartStoneConstruction();
                            Debug.Log($"Builder {GetInstanceID()} starting stone construction for {building.BuildingType}.");
                        }
                        else
                        {
                            Debug.Log($"Builder {GetInstanceID()} waiting for stone for {building.BuildingType}.");
                            // TODO: Implement resource request/fetching logic if needed
                            yield return StartCoroutine(WaitForSecondsFactory.WaitCoroutine(1.0f));
                        }
                        break;

                    case Building.ConstructionStage.ConstructingStone:
                        Debug.Log($"Builder {GetInstanceID()} constructing stone for {building.BuildingType}...");
                        // TODO: Play animation?
                        int amountOfStoneNeeded = building.GetBuildingCostByResourceType(ResourceType.Stone);
                        yield return StartCoroutine(WaitForSecondsFactory.WaitCoroutine(constructionTime * amountOfStoneNeeded)); // Use a getter for time
                        building.CompleteStoneConstruction();
                        Debug.Log($"Builder {GetInstanceID()} finished stone stage for {building.BuildingType}.");
                        break;

                    default:
                        // Should not happen if logic is correct
                        Debug.LogError($"Builder {GetInstanceID()} encountered unexpected building stage: {building.CurrentStage}");
                        // Force completion or abort? Abort safer.
                        goto ConstructionEnd; // Use goto sparingly, but useful for breaking out here
                }
                yield return null; // Wait a frame between checks/stages
            }

        ConstructionEnd: // Label for breaking out on error/completion

            Debug.Log($"Builder {GetInstanceID()} finished construction process for {building.BuildingType}. Current Stage: {building.CurrentStage}");

            // 3. Cleanup and Return
            bool wasTaskCompleted = building.CurrentStage == Building.ConstructionStage.Complete;
            ClearTask(); // Clear internal task reference

            // Trigger the manager's event ONLY if construction actually finished successfully
            if (wasTaskCompleted)
            {
                // The BuildingManager already subscribes to this event from BuilderManager
                var mainManager = characterManager.GetSpecificManager(CharacterType.Builder) as BuilderManager;
                mainManager.InvokeConstructionCompleteEvent(building);
                // OR: Fire an event from the Building itself when MarkConstructionComplete is called.
            }
            else
            {
                Debug.LogWarning($"Construction for {building.BuildingType} did not complete successfully.");
                // Should the building be re-queued? Requires more logic/event handling.
            }

            // Return builder to pool (handled by BuilderManager usually)
            // This signals the BuilderManager this builder is free.
            characterManager.ReturnCharacter(this);
        }

        // Need helper methods in Building to get construction times
        // e.g., in Building.cs: public float GetWoodConstructionTime() => woodConstructionTime;
    }
    // Helper in BuilderManager to invoke the event
    public partial class BuilderManager // Assuming partial class or add directly
    {
        private Dictionary<CharacterType, ICharacterTypeManager> typeManagers; // Add this field

        public void InvokeConstructionCompleteEvent(Building building)
        {
            OnBuildingConstructionComplete?.Invoke(building);
        }

        // Helper to get typed manager
        public T GetSpecificManager<T>(CharacterType type) where T : class, ICharacterTypeManager
        {
            if (typeManagers.TryGetValue(type, out var manager) && manager is T typedManager)
            {
                return typedManager;
            }
            Debug.LogError($"Could not find or cast manager of type {typeof(T)} for CharacterType {type}.");
            return null;
        }
    }
}