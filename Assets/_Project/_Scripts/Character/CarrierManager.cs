using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PunkyFruitBat
{
    // Define a simple structure/class to hold transport request details
    public struct TransportRequest
    {
        public Resource Resource;
        public Flag CurrentFlag;
        public Flag NextFlag; // Calculated target for this leg
        public Building DestinationBuilding; // Final destination
    }

    public class CarrierManager : BaseSpecificCharacterManager<Carrier> // Inherit from the base
    {
        public override CharacterType ManagedType => CharacterType.Carrier;

        //private Queue<Carrier> carrierPool = new();

        // Queue for resources waiting for transport between flags
        private Queue<TransportRequest> transportRequestQueue = new();

        // Override the grid complete handler to initialise the pool
        public override void HandleGridComplete()
        {
            base.InitialisePool(100);
            gridManager.PathManager.OnPathCreationCompleted += HandlePathCreationOrChange;
            gridManager.PathManager.OnPathRemoved += HandlePathRemovalUnassignCarrier;
            //InitialiseCarrierPool();
        }

        // --- Pooling Logic (Moved from CharacterManager) ---
        //private void InitialiseCarrierPool()
        //{
        //    carrierPool = new Queue<Carrier>();
        //    IncreaseCarrierPool(100); // Or read from config
        //}

        //private void IncreaseCarrierPool(int amount)
        //{
        //    GameObject prefab = characterPrefabs.characterPrefabs[(int)ManagedType];
        //    if (prefab == null)
        //    {
        //        Debug.LogError($"Prefab for {ManagedType} not found!");
        //        return;
        //    }

        //    int storehouseNode = gridManager.BuildingManager.GetStorehouseNode(); // Get once
        //    Vector3 initialPosition = gridManager.NodeManager.GetNodePosition(storehouseNode);

        //    for (int i = 0; i < amount; i++)
        //    {
        //        GameObject characterGO = GameObject.Instantiate(prefab);
        //        characterGO.transform.position = initialPosition;

        //        if (typeSpecificParentTransform != null) characterGO.transform.SetParent(typeSpecificParentTransform);
        //        else Debug.LogWarning($"Parent transform for {ManagedType} not set. Character '{characterGO.name}' will be at scene root.", characterGO);

        //        if (!characterGO.TryGetComponent<Carrier>(out Carrier carrier))
        //        {
        //            Debug.LogError($"Prefab for {ManagedType} is missing Carrier component!");
        //            GameObject.Destroy(characterGO); // Clean up unusable instance
        //            continue;
        //        }

        //        carrier.InitialiseCharacter(ManagedType, storehouseNode);
        //        characterGO.SetActive(false);
        //        carrierPool.Enqueue(carrier);
        //    }
        //}

        //public override Character GetCharacterInstance(int spawnNodeIndex = -1)
        //{
        //    if (carrierPool.Count == 0)
        //    {
        //        Debug.Log("Carrier pool empty, increasing size.");
        //        IncreaseCarrierPool(50); // Or read from config
        //    }

        //    if (carrierPool.Count == 0) // Check again after trying to increase
        //    {
        //        Debug.LogError("Failed to increase carrier pool or pool still empty. Cannot get carrier.");
        //        return null;
        //    }

        //    Carrier carrier = carrierPool.Dequeue();

        //    if (spawnNodeIndex != -1) carrier.transform.position = gridManager.NodeManager.GetNodePosition(spawnNodeIndex);

        //    carrier.gameObject.SetActive(true);
        //    return carrier;
        //}

        //public override void ReturnCharacterInstance(Character character)
        //{
        //    if (character is not Carrier carrier)
        //    {
        //        Debug.LogError($"Tried to return a non-carrier character ({character.GetType().Name}) to CarrierManager.");
        //        return;
        //    }

        //    if (carrier == null || !carrier.gameObject.activeInHierarchy) return; // Already returned or destroyed

        //    // Logic specific to returning a carrier (e.g., send back to storehouse)
        //    carrier.StopAllCoroutines(); // Stop current task
        //    // Start movement back to the storehouse (using the node index)
        //    int storehouseNode = gridManager.BuildingManager.GetStorehouseNode();
        //    carrier.SetWorkNodeIndex(storehouseNode); // Reset home node
        //    carrier.StartCoroutine(carrier.MoveCharacter(carrier.WorkNodeIndex, () =>
        //    {
        //        // This callback executes *after* movement is complete
        //        carrier.SetAssignedPath(null);
        //        carrier.gameObject.SetActive(false); // Deactivate only after reaching storehouse
        //        carrierPool.Enqueue(carrier);
        //    }));
        //}

        //public override void InstantlyReturnCharacterInstance(Character character)
        //{
        //    if (character is not Carrier carrier)
        //    {
        //        Debug.LogError($"Tried to instantly return a non-carrier character ({character.GetType().Name}) to CarrierManager.");
        //        return;
        //    }

        //    if (carrier == null) return;

        //    Debug.Log($"Instantly returning carrier {carrier.GetInstanceID()} to pool");
        //    carrier.StopAllCoroutines(); // Ensure no movement coroutines continue
        //    carrier.gameObject.SetActive(false);
        //    // Optionally reset position to storehouse instantly
        //    int storehouseNode = gridManager.BuildingManager.GetStorehouseNode();
        //    carrier.transform.position = gridManager.NodeManager.GetNodePosition(storehouseNode);

        //    // Avoid duplicate enqueuing if already in pool (though SetActive(false) should prevent issues)
        //    if (!carrierPool.Contains(carrier))
        //    {
        //        carrierPool.Enqueue(carrier);
        //    }
        //    else
        //    {
        //        Debug.LogWarning($"Carrier {carrier.GetInstanceID()} was already in the pool?");
        //    }

        //    Debug.Log($"Carrier pool size: {carrierPool.Count}");
        //}


        // --- Carrier-Specific Event Handling ---

        // --- Path Event Handlers ---

        /// <summary>
        /// Handles new paths or changes that might affect connectivity or assignment.
        /// </summary>
        private void HandlePathCreationOrChange(Path path)
        {
            // 1. Try to assign a carrier to THIS path (includes connectivity check & queuing)
            TryAssignCarrierToPath(path, true); // Pass 'true' to indicate it's a new/changed path

            // 2. Process the queue of paths waiting for assignment, as this change might have connected them.
            ProcessPathsAwaitingCarrierAssignmentQueue();

            // 3. Process the transport queue, as assigning a carrier might fulfill a request.
            ProcessTransportQueue();
        }

        private void HandlePathRemovalUnassignCarrier(Path path)
        {
            UnassignCarrierFromPath(path); // Returns carrier to pool if path removed
        }

        private void TryAssignCarrierToPath(Path path, bool isNewPathEvent = false)
        {
            // Basic validation
            if (path == null || path.Id == -1 || path.HasCarrier) return;

            // --- REINSTATE CONNECTIVITY CHECK ---
            if (!gridManager.PathManager.PathFinder.IsPathConnectedToStorehouse(path))
            {
                // Not connected yet. Add to the waiting queue if it's not already there.
                // Use PathManager's queue (assuming its purpose aligns)
                Queue<Path> waitingQueue = gridManager.PathManager.UnconnectedPaths;
                if (!waitingQueue.Contains(path)) // Avoid duplicates
                {
                    Debug.Log($"[CarrierManager] Path {path.Id} is not connected to storehouse. Queuing for carrier assignment.");
                    waitingQueue.Enqueue(path);
                }
                else if (isNewPathEvent)
                {
                    // Path might have changed but is still not connected. Log is optional.
                    // Debug.Log($"[CarrierManager] Path {path.Id} changed but still not connected.");
                }
                return; // Don't assign carrier yet
            }
            // ------------------------------------

            // Path IS connected! Check if we have an idle carrier in the POOL.
            if (characterPool.Count > 0)
            {
                Carrier carrier = characterPool.Dequeue() as Carrier;
                if (carrier != null)
                {
                    //carrier.AssignTaskStart();
                    path.SetCarrier(carrier);
                    carrier.SetAssignedPath(path);
                    carrier.gameObject.SetActive(true);

                    // Move carrier to path center, notify when idle there
                    carrier.StartCoroutine(carrier.MoveCharacter(path.CenterNode));
                }
                else
                {
                    Debug.LogError("Carrier pool error: Count > 0 but Dequeue failed or was wrong type.");
                }
            }
            else
            {
                Debug.Log($"[CarrierManager] Path {path.Id} is connected, but no idle carriers in pool. Path remains without carrier for now.");
                // Add to waiting queue? Or just wait for a carrier to become free?
                // Let's add to queue so it gets re-checked when a carrier returns.
                Queue<Path> waitingQueue = gridManager.PathManager.UnconnectedPaths;
                if (!waitingQueue.Contains(path))
                {
                    waitingQueue.Enqueue(path);
                }
            }
        }

        /// <summary>
        /// Processes the queue of paths waiting for connection and carrier assignment.
        /// </summary>
        private void ProcessPathsAwaitingCarrierAssignmentQueue()
        {
            // Use PathManager's queue
            Queue<Path> waitingQueue = gridManager.PathManager.UnconnectedPaths;
            int currentQueueSize = waitingQueue.Count;
            if (currentQueueSize == 0) return;

            Debug.Log($"[CarrierManager] Processing {currentQueueSize} paths awaiting carrier assignment...");

            List<Path> pathsToProcess = new(waitingQueue);
            waitingQueue.Clear(); // Clear original queue

            int processedCount = 0;
            foreach (Path pathToCheck in pathsToProcess)
            {
                processedCount++;

                // Re-validate
                if (pathToCheck == null || pathToCheck.Id == -1) continue;
                if (pathToCheck.HasCarrier) continue; // Already got one somehow

                // RETRY assignment logic - this includes the connectivity check again
                TryAssignCarrierToPath(pathToCheck);
                // TryAssign will re-queue if still not connected or no carrier available
            }

            int remainingQueueSize = waitingQueue.Count;
            if (remainingQueueSize > 0)
                Debug.Log($"[CarrierManager] {remainingQueueSize} paths remain in assignment queue after processing.");
            else
                Debug.Log("[CarrierManager] Assignment queue empty after processing cycle.");
        }

        // Modified Unassign - returns character via base manager method
        private void UnassignCarrierFromPath(Path path)
        {
            if (path != null && path.HasCarrier)
            {
                Carrier carrier = path.Carrier;
                if (carrier.IsBusy)
                {
                    return; // Don't unassign busy carriers
                }

                ReturnCharacterInstance(carrier);
                path.RemoveCarrier(); // Clear path's reference
            }
        }
        
        // --- Resource Transport Coordination ---

        /// <summary>
        /// Called by Flags when a resource arrives. Determines the next step and queues transport.
        /// </summary>
        public void ResourceArrivedAtFlag(Flag currentFlag, Resource resource)
        {
            if (resource == null || currentFlag == null) return;

            // 1. Check if this flag is the final destination (connected to the building)
            Building destinationBuilding = currentFlag.GetBuildingAtFlag();
            if (destinationBuilding != null && destinationBuilding.CenterIndex == resource.DestinationNodeIndex)
            {
                // Deliver to building
                destinationBuilding.AddResourceToSite(resource.ResourceType, 1); // Assumes amount is 1
                // TODO: Pool or destroy the resource GameObject
                DestroyResource(resource);
                return; // Transport complete
            }

            // 2. Dynamically find the NEXT flag on the route from currentFlag towards the resource's destination
            Flag nextFlag = FindNextFlagOnRouteTowards(currentFlag, resource.DestinationNodeIndex);

            if (nextFlag == null)
            {
                Debug.LogWarning($"[CarrierManager] Cannot find next flag on route from Flag {currentFlag.Id} towards destination {resource.DestinationNodeIndex} for {resource.ResourceType}. Transport stuck!", resource);
                // Just leave it at the current flag and queue for later check?

                //return;
            }

            // 3. Create and queue the request for this specific leg
            TransportRequest request = new()
            {
                Resource = resource,
                CurrentFlag = currentFlag,
                NextFlag = nextFlag,
                DestinationBuilding = null // Not needed for intermediate steps
            };

            transportRequestQueue.Enqueue(request);

            // 4. Try processing the queue immediately
            ProcessTransportQueue();
        }

        // --- Helper Method --- (Needs corresponding PathFinder method)
        public Flag FindNextFlagOnRouteTowards(Flag startFlag, int finalDestinationBuildingIndex)
        {
            if (startFlag == null || finalDestinationBuildingIndex < 0) return null;

            // We need the entrance node of the final building to pathfind towards
            Building finalBuilding = gridManager.BuildingManager.GetBuildingAtNode(finalDestinationBuildingIndex);
            if (finalBuilding == null)
            {
                Debug.LogError($"[CarrierManager] Cannot find final destination building with index {finalDestinationBuildingIndex} for routing.");
                return null;
            }
            int finalEntranceNodeIndex = finalBuilding.EntranceIndex;


            // Ask PathFinder for the next flag from startFlag.Id towards finalEntranceNodeIndex
            return gridManager.PathManager.PathFinder.FindNextFlagOnRoute(startFlag.Id, finalEntranceNodeIndex);
        }

        /// <summary>
        /// Processes the queue of pending transport requests, assigning tasks to available carriers.
        /// </summary>
        public void ProcessTransportQueue()
        {
            if (transportRequestQueue.Count == 0)
            {
                return;
            }

            int processedThisCycle = 0;
            // Limit processing per cycle to prevent potential infinite loop if no carriers ever become free
            int maxToProcess = transportRequestQueue.Count;

            while (transportRequestQueue.Count > 0 && processedThisCycle < maxToProcess)
            {
                TransportRequest request = transportRequestQueue.Peek(); // Peek first to check validity

                // Basic validation of request components
                if (request.Resource == null || request.CurrentFlag == null)
                {
                    Debug.LogWarning($"[CarrierManager] Resource: {request.Resource} | CurrentFlag: {request.CurrentFlag}.");
                    processedThisCycle++;
                    continue;
                }

                if (request.NextFlag == null)
                {
                    Debug.LogWarning($"[CarrierManager] NextFlag: {request.NextFlag}.");
                    Resource resource = request.Resource;
                    Flag currentFlag = request.CurrentFlag;
                    Flag nextFlag = FindNextFlagOnRouteTowards(request.CurrentFlag, request.Resource.DestinationNodeIndex);

                    transportRequestQueue.Dequeue(); // Remove invalid request

                    TransportRequest newRequest = new()
                    {
                        Resource = resource,
                        CurrentFlag = currentFlag,
                        NextFlag = nextFlag,
                        DestinationBuilding = null // Not needed for intermediate steps
                    };

                    transportRequestQueue.Enqueue(newRequest);

                    return;
                }

                // Find the path connecting the current and next flags
                Path path = FindPathBetweenFlags(request.CurrentFlag, request.NextFlag);

                if (path == null)
                {
                    Debug.LogWarning($"[CarrierManager] No path found between flags {request.CurrentFlag.Id} and {request.NextFlag.Id}. Request remains queued.");
                    gridManager.StartCoroutine(TryGetNewFlag(request));
                    //transportRequestQueue.Dequeue();
                    return;
                }

                // Check if the path has a carrier assigned
                Carrier carrier = path.Carrier;
                if (carrier == null)
                {
                    // Debug.Log($"[CarrierManager] Path {path.Id} has no carrier assigned for request ({request.CurrentFlag.Id} -> {request.NextFlag.Id}). Request remains queued.");
                    // Cannot process this request now, break the loop and wait for carrier assignment
                    break;
                }

                // Check if the assigned carrier is busy
                if (carrier.IsBusy)
                {
                    // Cannot process this request now, break the loop and wait for carrier to become idle
                    break;
                }

                // --- Carrier is available! Assign the task ---
                TransportRequest actualRequest = transportRequestQueue.Dequeue(); // Now actually dequeue
                carrier.StopAllCoroutines(); // Stop any previous tasks
                bool assigned = carrier.AssignTransportTask(actualRequest.Resource, actualRequest.CurrentFlag, actualRequest.NextFlag);
                if (!assigned)
                {
                    // Should not happen if IsBusy check passed, but handle anyway
                    Debug.LogError($"[CarrierManager] Failed to assign task to supposedly idle carrier {carrier.GetInstanceID()}! Re-queuing.");
                    transportRequestQueue.Enqueue(actualRequest); // Re-queue on failure
                }

                processedThisCycle++; // Count successful or failed attempts for this cycle
            }
        }

        private IEnumerator TryGetNewFlag(TransportRequest request)
        {
            yield return null;

            Flag nextFlag = FindNextFlagOnRouteTowards(request.CurrentFlag, request.Resource.DestinationNodeIndex);
            
            TransportRequest newRequest = new()
            {
                Resource = request.Resource,
                CurrentFlag = request.CurrentFlag,
                NextFlag = nextFlag,
                DestinationBuilding = null
            };

            transportRequestQueue.Enqueue(newRequest);

            ProcessTransportQueue();
        }

        /// <summary>
        /// Called by Carriers when they become idle (e.g., after dropping off resource and returning to center).
        /// </summary>
        public void NotifyCarrierIdle(Carrier carrier, Path path)
        {
            if (carrier == null) return;
            // An idle carrier might be able to take a job, process the queue.
            //carrier.ResetCarrierState();
            ProcessTransportQueue();
        }


        // Helper to find the path connecting two flags
        private Path FindPathBetweenFlags(Flag flag1, Flag flag2)
        {
            if (flag1 == null || flag2 == null) return null;

            List<Path> paths = new();

            // Iterate through all known paths
            foreach (Path path in gridManager.PathManager.GetAllPaths.Values) // Use the IReadOnlyDictionary property
            {
                if ((path.Flag1 == flag1 && path.Flag2 == flag2) ||
                    (path.Flag1 == flag2 && path.Flag2 == flag1))
                {
                    paths.Add(path);
                }
            }

            Path pathToReturn = null;

            for (int i = 0; i < paths.Count; i++)
            {
                // Get the path with the fewest nodes
                if (i == 0 || paths[i].Nodes.Count < paths[i - 1].Nodes.Count)
                {
                    pathToReturn = paths[i];
                }
            }
            return pathToReturn; // Return the path with the fewest nodes
        }

        // Helper to destroy resource GameObject (implement pooling later if needed)
        private void DestroyResource(Resource resource)
        {
            if (resource != null)
            {
                GameObject.Destroy(resource.gameObject);
            }
        }

        public override void Unsubscribe()
        {
            if (gridManager?.PathManager != null)
            {
                gridManager.PathManager.OnPathCreationCompleted -= HandlePathCreationOrChange;
                gridManager.PathManager.OnPathRemoved -= HandlePathRemovalUnassignCarrier;
            }
            // Clear queue? Reset state?
            transportRequestQueue.Clear();
        }
    }
}