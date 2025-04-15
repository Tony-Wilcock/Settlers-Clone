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
            gridManager.ResourceManager.OnResourceRequestSubmitted += HandleResourceRequestSubmitted;
            gridManager.PathManager.OnPathCreationCompleted += ProcessResourceQueue;
        }

        public override Character GetCharacterInstance(int spawnNodeIndex = -1)
        {
            GameObject prefab = characterPrefabs.characterPrefabs[(int)ManagedType];
            if (prefab == null)
            {
                Debug.LogError($"Prefab for {ManagedType} not found!");
                return null;
            }

            int storehouseNode = gridManager.BuildingManager.GetStorehouseNode(); // Get once
            Vector3 initialPosition = gridManager.NodeManager.GetNodePosition(storehouseNode);
            
            GameObject characterGO = GameObject.Instantiate(prefab);
            characterGO.transform.position = initialPosition;

            if (typeSpecificParentTransform != null) characterGO.transform.SetParent(typeSpecificParentTransform);
            else Debug.LogWarning($"Parent transform for {ManagedType} not set. Character '{characterGO.name}' will be at scene root.", characterGO);
            if (!characterGO.TryGetComponent<StorehousePorter>(out StorehousePorter porter))
            {
                Debug.LogError($"Prefab for {ManagedType} is missing StorehousePorter component!");
                GameObject.Destroy(characterGO); // Clean up unusable instance
                return null;
            }

            porter.InitialiseCharacter(ManagedType, storehouseNode);
            porter.SetWorkNodeIndex(storehouseNode);

            return porter;
        }

        public override void ReturnCharacterInstance(Character character)
        {
            throw new System.NotImplementedException();
        }

        public override void InstantlyReturnCharacterInstance(Character character)
        {
            throw new System.NotImplementedException();
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

            //if (waitingQueue.Count > 0)
            //    Debug.Log($"[ResourceManager] {waitingQueue.Count} resources remain in awaiting queue after processing.");
            //else
            //    Debug.Log("[ResourceManager] Awaiting resource queue is now empty.");
        }

        public override void Unsubscribe()
        {
            gridManager.ResourceManager.OnResourceRequestSubmitted -= HandleResourceRequestSubmitted;
            gridManager.PathManager.OnPathCreationCompleted -= ProcessResourceQueue;
        }
    }
}
