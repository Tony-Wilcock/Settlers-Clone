using System;
using System.Collections.Generic;
using UnityEngine;

namespace PunkyFruitBat
{
    public enum ResourceType
    {
        Wood,
        Stone,
    }

    public class ResourceManager
    {
        public event Action<Resource> OnResourceRequestSubmitted;

        private HexGridManager manager;
        private ResourcePrefabs_SO ResourcePrefabs;

        private readonly Dictionary<ResourceType, int> allResources = new();

        public void Initialise(HexGridManager manager, ResourcePrefabs_SO ResourcePrefabs)
        {
            this.manager = manager;
            this.ResourcePrefabs = ResourcePrefabs;

            allResources.Clear();

            foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType)))
            {
                allResources.Add(type, 0);

                AddResource(type, 200);
            }
        }

        public void AddResource(ResourceType type, int amount)
        {
            if (allResources.ContainsKey(type) && amount > 0)
            {
                allResources[type] += amount;
            }
        }

        public void RemoveResource(ResourceType type, int amount)
        {
            if (allResources.ContainsKey(type) && amount > 0)
            {
                allResources[type] -= amount;
            }
        }

        public int GetResourceAmount(ResourceType type)
        {
            return allResources.ContainsKey(type) ? allResources[type] : 0;
        }

        public void RequestResources(ResourceType resourceTypeNeeded, Building building)
        {
            if (GetResourceAmount(resourceTypeNeeded) > 0)
            {
                int hqCenterIndex = manager.BuildingManager.HQ.CenterIndex;
                int hqEntranceIndex = manager.BuildingManager.HQ.EntranceIndex; // Where porter drops off
                int buildingEntranceIndex = building.EntranceIndex; // Where pathfinding targets
                int buildingCenterIndex = building.CenterIndex;     // The Resource's final destination ID

                List<int> initialRouteCheck = manager.PathManager.PathFinder.FindWalkableRouteThroughPaths(hqEntranceIndex, buildingEntranceIndex);
                if (initialRouteCheck == null || initialRouteCheck.Count == 0)
                {
                    Debug.LogWarning($"No initial route found from HQ entrance {hqEntranceIndex} to building entrance {buildingEntranceIndex}. Cannot request resource.");
                    // Maybe queue the building's resource need? For now, just stop.
                    return;
                }

                Vector3 hqCenterPosition = manager.NodeManager.GetNode(hqCenterIndex).Position;
                GameObject resourcePrefab = ResourcePrefabs.ResourcePrefabs[(int)resourceTypeNeeded];
                GameObject resourceInstance = GameObject.Instantiate(
                    resourcePrefab,
                    hqCenterPosition,
                    Quaternion.identity,
                    manager.BuildingManager.HQ.transform);

                Resource resourceComponent = resourceInstance.GetComponent<Resource>();
                if (resourceComponent == null)
                {
                    Debug.LogError("Resource prefab missing Resource component!", resourceInstance);
                    GameObject.Destroy(resourceInstance);
                    return;
                }

                // Initialise with only Start and Destination (Building Center Index)
                resourceComponent.InitialiseResource(
                    hqCenterIndex,
                    buildingCenterIndex, // Use CenterIndex!
                    resourceTypeNeeded
                );

                // Remove resource from central storage
                RemoveResource(resourceTypeNeeded, 1); // Assuming amount is 1

                // Invoke event - the StorehousePorterManager will pick this up
                OnResourceRequestSubmitted?.Invoke(resourceComponent);
            }
            else
            {
                Debug.LogWarning($"Not enough {resourceTypeNeeded} in storage to fulfill request for {building.name}");
                // TODO: Queue resource need?
            }
        }
    }
}
