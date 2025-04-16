using System.Collections.Generic;
using UnityEngine;

namespace PunkyFruitBat
{
    public class StorehousePorterManager : BaseSpecificCharacterManager<StorehousePorter>
    {
        public override CharacterType ManagedType => CharacterType.StorehousePorter;

        StorehousePorter hqPorter;

        private Queue<Resource> resourceQueue = new();

        public override void HandleGridComplete()
        {
            base.InitialisePool(5);

            gridManager.ResourceManager.OnResourceRequestSubmitted += HandleResourceRequestSubmitted;
            gridManager.PathManager.OnPathCreationCompleted += ProcessResourceQueue;
        }

        private void HandleResourceRequestSubmitted(Resource request)
        {
            // If the request is for a resource this manager can provide, add it to the queue
            if (request != null)
            {
                TryAssignPorter(request);
            }

            ProcessResourceQueue();
        }

        private void TryAssignPorter(Resource request)
        {
            if (hqPorter == null) hqPorter = gridManager.BuildingManager.HQ.AssignedWorker as StorehousePorter;
            if (hqPorter == null || hqPorter.IsBusy)
            {
                resourceQueue.Enqueue(request);
                return;
            }

            hqPorter.AssignResourceTask(request);
            hqPorter.StartCoroutine(hqPorter.PerformResourceTask(request, this));
        }

        public void ProcessResourceQueue(Path path = null)
        {
            if (resourceQueue.Count == 0) return;

            Queue<Resource> waitingQueue = resourceQueue;

            // Use a temporary list to avoid issues with modifying the queue while iterating
            List<Resource> resourcesToProcess = new(waitingQueue);
            waitingQueue.Clear(); // Clear original queue

            int processedCount = 0;
            foreach (Resource resourceToCheck in resourcesToProcess)
            {
                processedCount++;

                // Re-validate resource before processing
                if (resourceToCheck == null)
                {
                    Debug.LogWarning($"[ResourceManager] Resource {resourceToCheck} in queue is invalid/destroyed. Skipping.");
                    continue; // Skip invalid/destroyed buildings
                }

                // Retry assignment logic. This will re-queue if still unconnected or blocked.
                TryAssignPorter(resourceToCheck);
            }
        }

        public override void Unsubscribe()
        {
            if (gridManager?.ResourceManager != null)
                gridManager.ResourceManager.OnResourceRequestSubmitted -= HandleResourceRequestSubmitted;
            if (gridManager?.PathManager != null)
                gridManager.PathManager.OnPathCreationCompleted -= ProcessResourceQueue;

            resourceQueue.Clear();
            base.Unsubscribe();
        }
    }
}
