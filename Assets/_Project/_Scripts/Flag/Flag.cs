using Mono.Cecil;
using System;
using System.Collections;
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
            Debug.Log($"Adding path: {path.Id}. Paths attached = {PathsAttachedToFlag.Count}");
        }

        public void AddResourceToFlag(Resource resource)
        {
            ResourcesAtFlag.Add(resource);
            StartCoroutine(DoThis(resource));
        }

        private IEnumerator DoThis(Resource resource)
        {
            resource.UpdateResource();

            foreach (Path path in PathsAttachedToFlag)
            {
                Debug.Log($"Path: {path.Id}");
                // Chech each path attached to the flag to see if the flags are the same as the resource current flag and resource next flag
                if (path.Flag1 == this && path.Flag2 == resource.NextFlag ||
                    path.Flag2 == this && path.Flag1 == resource.NextFlag)
                {
                    Carrier carrier = path.Carrier;
                    if (carrier == null || carrier.IsBusy)
                    {
                        Debug.LogWarning("No carrier on this path yet OR carrier is busy.");
                    }
                    else
                    {
                        carrier.StopAllCoroutines();
                        // Get the carriers other flag
                        yield return StartCoroutine(carrier.RetrieveResource(resource, path, this));
                    }
                }
                else
                {
                    Debug.LogWarning("No path found for this resource.");
                }
            }
        }

        public void RemoveResourceFromFlag(Resource resource)
        {
            ResourcesAtFlag.Remove(resource);
        }

        public Building GetBuildingAtFlag()
        {
            if (!IsFlagAttachedToBuilding)
            {
                Debug.LogWarning("No building attached to this flag.");
                return null;
            }

            int buildingIndex = HexGridManager.Instance.NodeManager.GetNeighbourInDirection(Id, Direction.Northwest);

            Building building = HexGridManager.Instance.BuildingManager.GetBuildingAtNode(buildingIndex);

            Debug.Log($"Building at flag: {building.BuildingType}");

            return building;
        }
    }
}
