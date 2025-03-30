using System.Collections.Generic;
using UnityEngine;

namespace PunkyFruitBat
{
    public class CarrierManager : BaseSpecificCharacterManager // Inherit from the base
    {
        public override CharacterType ManagedType => CharacterType.Carrier;

        private Queue<Carrier> carrierPool = new();

        // Override Initialise to add specific setup if needed, like pool creation
        public override void Initialise(CharacterManager mainManager, HexGridManager gridManager, CharacterPrefabs_SO characterPrefabs, Transform parentTransform)
        {
            base.Initialise(mainManager, gridManager, characterPrefabs, parentTransform); // Call base initialisation
                                                                                          // Note: Pool initialisation is now tied to HandleGridComplete

            gridManager.PathManager.OnPathCreationCompleted += HandlePathCreationOrConnectionChange;
            gridManager.PathManager.OnPathRemoved += HandlePathRemoval;
        }

        // --- Pooling Logic (Moved from CharacterManager) ---
        private void InitialiseCarrierPool()
        {
            carrierPool = new Queue<Carrier>();
            IncreaseCarrierPool(100); // Or read from config
        }

        private void IncreaseCarrierPool(int amount)
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

                if (!characterGO.TryGetComponent<Carrier>(out Carrier carrier))
                {
                    Debug.LogError($"Prefab for {ManagedType} is missing Carrier component!");
                    GameObject.Destroy(characterGO); // Clean up unusable instance
                    continue;
                }

                carrier.InitialiseCharacter(ManagedType, storehouseNode);
                characterGO.SetActive(false);
                carrierPool.Enqueue(carrier);
            }
        }

        public override Character GetCharacterInstance(int spawnNodeIndex = -1)
        {
            if (carrierPool.Count == 0)
            {
                Debug.Log("Carrier pool empty, increasing size.");
                IncreaseCarrierPool(50); // Or read from config
            }

            if (carrierPool.Count == 0) // Check again after trying to increase
            {
                Debug.LogError("Failed to increase carrier pool or pool still empty. Cannot get carrier.");
                return null;
            }

            Carrier carrier = carrierPool.Dequeue();

            if (spawnNodeIndex != -1) carrier.transform.position = gridManager.NodeManager.GetNodePosition(spawnNodeIndex);

            carrier.gameObject.SetActive(true);
            return carrier;
        }

        public override void ReturnCharacterInstance(Character character)
        {
            if (character is not Carrier carrier)
            {
                Debug.LogError($"Tried to return a non-carrier character ({character.GetType().Name}) to CarrierManager.");
                return;
            }

            if (carrier == null || !carrier.gameObject.activeInHierarchy) return; // Already returned or destroyed

            // Logic specific to returning a carrier (e.g., send back to storehouse)
            carrier.StopAllCoroutines(); // Stop current task
            // Start movement back to the storehouse (using the node index)
            int storehouseNode = gridManager.BuildingManager.GetStorehouseNode();
            carrier.SetHomeNodeIndex(storehouseNode); // Reset home node
            carrier.StartCoroutine(carrier.MoveCharacter(carrier.HomeNodeIndex, () =>
            {
                // This callback executes *after* movement is complete
                carrier.gameObject.SetActive(false); // Deactivate only after reaching storehouse
                carrierPool.Enqueue(carrier);
                Debug.Log($"Carrier {carrier.GetInstanceID()} returned to pool. Pool size: {carrierPool.Count}");
                ProcessPathsAwaitingCarrierQueue(); // Check if this returned carrier can fulfil a waiting path request
            }));
        }

        public override void InstantlyReturnCharacterInstance(Character character)
        {
            if (character is not Carrier carrier)
            {
                Debug.LogError($"Tried to instantly return a non-carrier character ({character.GetType().Name}) to CarrierManager.");
                return;
            }

            if (carrier == null) return;

            Debug.Log($"Instantly returning carrier {carrier.GetInstanceID()} to pool");
            carrier.StopAllCoroutines(); // Ensure no movement coroutines continue
            carrier.gameObject.SetActive(false);
            // Optionally reset position to storehouse instantly
            int storehouseNode = gridManager.BuildingManager.GetStorehouseNode();
            carrier.transform.position = gridManager.NodeManager.GetNodePosition(storehouseNode);

            // Avoid duplicate enqueuing if already in pool (though SetActive(false) should prevent issues)
            if (!carrierPool.Contains(carrier))
            {
                carrierPool.Enqueue(carrier);
            }
            else
            {
                Debug.LogWarning($"Carrier {carrier.GetInstanceID()} was already in the pool?");
            }

            Debug.Log($"Carrier pool size: {carrierPool.Count}");
            ProcessPathsAwaitingCarrierQueue(); // Check if this returned carrier can fulfil a waiting path request
        }

        // Override the grid complete handler to initialise the pool
        public override void HandleGridComplete()
        {
            InitialiseCarrierPool();
        }


        // --- Carrier-Specific Event Handling ---

        private void HandlePathCreationOrConnectionChange(Path path)
        {
            TryAssignCarrierToPath(path);
            ProcessPathsAwaitingCarrierQueue(); // Check queue whenever a path changes
        }

        private void HandlePathRemoval(Path path)
        {
            UnassignCarrierFromPath(path);
            // Also, remove the path from the waiting queue if it's there
            if (gridManager.PathManager.UnconnectedPaths.Contains(path))
            {
                // Queues don't have a direct Remove. Need to rebuild or use a different structure (e.g., List/HashSet + Queue logic) if efficient removal is critical.
                // For simplicity now, we'll rely on the check within ProcessPathsAwaitingCarrierQueue
                Debug.Log($"Path {path.Id} removed, need to ensure it's not processed from waiting queue.");
            }
        }

        // --- Carrier Path Assignment Logic (Moved from CharacterManager) ---
        private void TryAssignCarrierToPath(Path path)
        {
            if (path == null || path.Id == -1 || path.HasCarrier)
            {
                return;
            }

            if (!gridManager.PathManager.PathFinder.IsPathConnectedToStorehouse(path))
            {
                if (!gridManager.PathManager.UnconnectedPaths.Contains(path))
                {
                    Debug.Log($"[CarrierManager] Path {path.Id} is not connected. Queuing.");
                    gridManager.PathManager.UnconnectedPaths.Enqueue(path);
                }
                return;
            }

            int storehouseEntranceNode = gridManager.BuildingManager.GetStorehouseEntranceNode();
            if (storehouseEntranceNode == -1)
            {
                Debug.LogError("[CarrierManager] Storehouse entrance node not found!");
                return;
            }

            // Now try to get a carrier *only if* a route exists
            Carrier carrier = GetCharacterInstance() as Carrier; // Get from pool (already handles increase if needed)

            if (carrier != null)
            {
                // IMPORTANT: Set HasCarrier flag BEFORE starting movement
                path.SetCarrier(carrier);

                carrier.StartCoroutine(carrier.MoveCharacter(path.CenterNode));

                if (gridManager.PathManager.UnconnectedPaths.Contains(path))
                {
                    gridManager.PathManager.UnconnectedPaths.Dequeue();
                }
            }
            else
            {
                // Pool was empty *even after trying to increase it*
                Debug.LogWarning($"[CarrierManager] Path {path.Id} is walkable, but no carriers available in pool. Queuing.");
                if (!gridManager.PathManager.UnconnectedPaths.Contains(path))
                {
                    gridManager.PathManager.UnconnectedPaths.Enqueue(path);
                }
            }
        }

        private void ProcessPathsAwaitingCarrierQueue()
        {
            int currentQueueSize = gridManager.PathManager.UnconnectedPaths.Count;
            if (currentQueueSize == 0) return;

            Debug.Log($"[CarrierManager] Processing {currentQueueSize} paths awaiting carrier...");

            // Use a temporary list to avoid issues with modifying the queue while iterating
            List<Path> pathsToProcess = new(gridManager.PathManager.UnconnectedPaths);
            gridManager.PathManager.UnconnectedPaths.Clear(); // Clear original queue

            int processedCount = 0;
            foreach (Path pathToCheck in pathsToProcess)
            {
                processedCount++;
                Debug.Log($"[CarrierManager] Re-checking path {pathToCheck.Id} ({processedCount}/{currentQueueSize}) from waiting queue.");

                // Re-validate path before processing
                if (pathToCheck == null || pathToCheck.Id == -1)
                {
                    Debug.LogWarning($"[CarrierManager] Path {pathToCheck?.Id ?? -1} in queue is invalid/destroyed. Skipping.");
                    continue; // Skip invalid/destroyed paths
                }
                if (pathToCheck.HasCarrier)
                {
                    Debug.Log($"[CarrierManager] Path {pathToCheck.Id} in queue already has carrier. Skipping.");
                    continue; // Skip already assigned paths
                }

                // Retry assignment logic. This will re-queue if still unconnected or blocked.
                TryAssignCarrierToPath(pathToCheck);
            }

            if (gridManager.PathManager.UnconnectedPaths.Count > 0)
                Debug.Log($"[CarrierManager] {gridManager.PathManager.UnconnectedPaths.Count} paths remain in awaiting queue after processing.");
            else
                Debug.Log("[CarrierManager] Awaiting carrier queue is now empty.");
        }

        private void UnassignCarrierFromPath(Path path)
        {
            if (path != null && path.HasCarrier)
            {
                Carrier carrier = path.Carrier;
                ReturnCharacterInstance(carrier);
                path.RemoveCarrier();
            }
        }

        public override void Unsubscribe()
        {
            gridManager.PathManager.OnPathCreationCompleted -= HandlePathCreationOrConnectionChange;
            gridManager.PathManager.OnPathRemoved -= HandlePathRemoval;
        }
    }
}