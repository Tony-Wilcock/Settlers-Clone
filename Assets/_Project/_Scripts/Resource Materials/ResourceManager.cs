using System.Collections;
using System.Collections.Generic;
using System.IO;
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
        private HexGridManager manager;
        private ResourcePrefabs_SO ResourcePrefabs;

        private StorehousePorter storehousePorter;

        private readonly Dictionary<ResourceType, int> allResources = new();

        public void Initialise(HexGridManager manager, ResourcePrefabs_SO ResourcePrefabs)
        {
            this.manager = manager;
            this.ResourcePrefabs = ResourcePrefabs;

            allResources.Clear();

            foreach (ResourceType type in System.Enum.GetValues(typeof(ResourceType)))
            {
                allResources.Add(type, 0);

                AddResource(type, 2);
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

        public IEnumerator RequestResources(Building building)
        {
            int woodCost = building.GetBuildingCostByResourceType(ResourceType.Wood);
            int stoneCost = building.GetBuildingCostByResourceType(ResourceType.Stone);
            if (woodCost == 0 && stoneCost == 0)
            {
                Debug.LogWarning($"Building {building.CenterIndex} has no resource cost.");
                yield break;
            }

            Debug.Log($"Building {building.CenterIndex} requires {woodCost} wood and {stoneCost} stone.");

            if (GetResourceAmount(ResourceType.Wood) >= woodCost)
            {
                int startPosition = manager.BuildingManager.HQ.EntranceIndex;
                int endPosition = building.EntranceIndex;
                List<int> route = manager.PathManager.PathFinder.FindWalkableRouteThroughPaths(startPosition, endPosition);
                List<Flag> flags = manager.PathManager.FlagsAlongRoute(route);

                if (storehousePorter == null) storehousePorter = manager.BuildingManager.HQ.AssignedWorker as StorehousePorter;

                Flag hqFlag = manager.FlagManager.TryGetFlag(manager.BuildingManager.HQ.EntranceIndex);
                for (int i = 0; i < woodCost; i++)
                {
                    GameObject woodPrefabSource = ResourcePrefabs.ResourcePrefabs[(int)ResourceType.Wood];
                    GameObject woodInstance = GameObject.Instantiate(
                        woodPrefabSource,
                        storehousePorter.transform.position,
                        Quaternion.identity,
                        storehousePorter.transform);

                    Resource woodComponent = woodInstance.GetComponent<Resource>();
                    woodComponent.SetResource(
                        storehousePorter.CurrentNodeIndex,
                        building.CenterIndex,
                        flags
                        );

                    yield return WaitForSecondsFactory.WaitCoroutine(2);
                    yield return storehousePorter.StartCoroutine(storehousePorter.MoveCharacter(manager.BuildingManager.HQ.EntranceIndex));
                    woodComponent.transform.parent = null;
                    hqFlag.AddResourceToFlag(woodComponent);
                    yield return storehousePorter.StartCoroutine(storehousePorter.MoveCharacter(manager.BuildingManager.HQ.CenterIndex));
                }
            }
        }
    }
}
