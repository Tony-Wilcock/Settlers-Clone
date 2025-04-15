using System.Collections.Generic;
using UnityEngine;

namespace PunkyFruitBat
{
    public class Flag : MonoBehaviour
    {
        [field: SerializeField] public int Id { get; private set; }
        [field: SerializeField] public bool IsFlagAttachedToBuilding { get; private set; } = false;
        [field: SerializeField] public List<Path> PathsAttachedToFlag { get; private set; } = new();
        [field: SerializeField] public List<Resource> ResourcesAtFlag { get; private set; } = new();

        private CarrierManager carrierManager;

        private void Start()
        {
            // Cache the CarrierManager instance for efficiency
            // Ensure HexGridManager and its managers are ready
            if (HexGridManager.Instance != null && HexGridManager.Instance.CharacterManager != null)
            {
                carrierManager = HexGridManager.Instance.CharacterManager.GetSpecificManager(CharacterType.Carrier) as CarrierManager;
                if (carrierManager == null)
                {
                    Debug.LogError($"Flag {Id} could not find CarrierManager!", this);
                }
            }
            else
            {
                Debug.LogError($"HexGridManager or CharacterManager not ready when Flag {Id} started!", this);
            }
        }

        public void SetFlagId(int id)
        {
            // Set the id to the manager selected vertex
            Id = id;
        }

        public void SetFlagAttachedToBuilding(bool isAttached)
        {
            IsFlagAttachedToBuilding = isAttached;
        }

        public void AddPathToFlag(Path path)
        {
            PathsAttachedToFlag.Add(path);
        }

        /// <summary>
        /// Called when a resource (from Porter or another Carrier) arrives at this flag.
        /// Adds the resource to the list and notifies the CarrierManager to handle the next step.
        /// </summary>
        /// <param name="resource">The resource that arrived.</param>
        public void AddResourceToFlag(Resource resource)
        {
            if (resource == null) return;

            if (!ResourcesAtFlag.Contains(resource)) // Avoid duplicates
            {
                ResourcesAtFlag.Add(resource);
                resource.transform.SetParent(this.transform); // Parent resource to flag visually
                resource.transform.localPosition = Vector3.up * 0.5f; // Example offset
                resource.CurrentFlag = this; // Set the current flag for the resource

                // Notify the central manager - CRITICAL CHANGE
                carrierManager?.ResourceArrivedAtFlag(this, resource);
            }
            else
            {
                Debug.LogWarning($"Resource {resource.ResourceType} already present at Flag {Id}.", this);
            }
        }

        public void RemoveResourceFromFlag(Resource resource)
        {
            if (resource != null) ResourcesAtFlag.Remove(resource);
        }

        /// <summary>
        /// Finds the building directly associated with this flag (if any).
        /// Assumes building center is NW neighbour of the flag (entrance).
        /// </summary>
        /// <returns>The Building component or null.</returns>
        public Building GetBuildingAtFlag()
        {
            if (!IsFlagAttachedToBuilding)
            {
                Debug.LogWarning($"Flag {Id}: GetBuildingAtFlag() failed - IsFlagAttachedToBuilding is false."); // Log 1
                return null;
            }
            if (HexGridManager.Instance?.NodeManager == null || HexGridManager.Instance?.BuildingManager == null)
            {
                Debug.LogError($"Flag {Id}: GetBuildingAtFlag() failed - Managers not ready."); // Log 2
                return null;
            }

            int buildingIndex = HexGridManager.Instance.NodeManager.GetNeighbourInDirection(Id, Direction.Northwest);

            if (buildingIndex == -1)
            {
                Debug.LogWarning($"Flag {Id}: GetBuildingAtFlag() - Could not find neighbour NW."); // Log 4
                return null;
            }

            Building building = HexGridManager.Instance.BuildingManager.GetBuildingAtNode(buildingIndex);

            return building;
        }

        // Helper to clean up resources if flag is destroyed
        void OnDestroy()
        {
            // Destroy or return resources to pool if necessary
            foreach (var resource in ResourcesAtFlag)
            {
                if (resource != null) Destroy(resource.gameObject);
            }
            ResourcesAtFlag.Clear();
        }
    }
}
