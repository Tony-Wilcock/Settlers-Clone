using System.Collections;
using UnityEngine;

namespace PunkyFruitBat
{
    public class StorehousePorter : Character
    {
        private Resource currentResourceTask = null;
        public bool IsBusy { get; private set; } = false; // Simple state
        private int buildingIndex;
        private int entranceIndex;
        private Flag buildingFlag;

        override protected void Awake()
        {
            base.Awake();
        }

        public void SetWorkingLocation(int buildingIndex, int entranceIndex, Flag flag)
        {
            this.buildingIndex = buildingIndex;
            this.entranceIndex = entranceIndex;
            this.buildingFlag = flag;
        }

        public void AssignResourceTask(Resource resource)
        {
            if (resource != null && !IsBusy)
            {
                currentResourceTask = resource;
            }
            else
            {
                Debug.LogWarning($"Porter {GetInstanceID()} cannot be assigned task. Current task: {currentResourceTask?.ResourceType}, IsBusy: {IsBusy}");
            }
        }

        /// <summary>
        /// Clears the current construction task.
        /// </summary>
        public void ClearTask()
        {
            currentResourceTask = null;
            IsBusy = false;
        }

        /// <summary>
        /// Coroutine to move to the resource and perform staged resource collection and delivery.
        /// </summary>
        public IEnumerator PerformResourceTask(Resource resource, StorehousePorterManager porterManager)
        {
            if (resource == null)
            {
                Debug.LogError("PerformResourceTask called with null resource!", this);
                ClearTask(); // Clear invalid task
                             // Notify manager? Or just return porter instantly?
                characterManager.InstantlyReturnCharacter(this); // Safer return
                yield break;
            }

            IsBusy = true; // Mark as busy
            // Parent the resource to the porter
            resource.transform.position = transform.position;
            resource.transform.SetParent(transform);
            yield return WaitForSecondsFactory.WaitCoroutine(2f);

            yield return StartCoroutine(MoveCharacter(entranceIndex));
            // Check if we actually reached the entrance
            if (CurrentNodeIndex == entranceIndex) resource.transform.SetParent(null);
            buildingFlag.AddResourceToFlag(resource);
            // Move back to the storehouse
            yield return StartCoroutine(MoveCharacter(buildingIndex));
            // Clear task
            ClearTask();
            porterManager.ProcessResourceQueue();
        }
    }
}
