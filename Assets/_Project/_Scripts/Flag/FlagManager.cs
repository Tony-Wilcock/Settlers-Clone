using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PunkyFruitBat
{
    public class FlagManager
    {
        public event Action OnFlagPlaced;
        public event Action OnFlagRemoved;

        private HexGridManager manager;

        private Flag flagPrefab;
        private readonly int poolSize = 100; // Initial pool size (adjust as needed)
        private Queue<Flag> flagPool; // Object pool for flags
        private readonly Dictionary<int, Flag> allFlags = new(); // To store all flags

        public void Initialise(HexGridManager manager, Flag flag)
        {
            this.manager = manager;
            flagPrefab = flag;

            manager.OnCreateFlagButtonPressed += PlaceFlag;
            manager.OnRemoveFlagButtonPressed += RemoveFlag;

            InitialisePool();
        }

        public void Unsubscribe()
        {
            manager.OnCreateFlagButtonPressed -= PlaceFlag;
            manager.OnRemoveFlagButtonPressed -= RemoveFlag;
        }

        private void InitialisePool()
        {
            flagPool = new Queue<Flag>();
            IncreaseFlagPool(poolSize);
        }

        private Flag GetFlagFromPool()
        {
            if (flagPool.Count == 0)
            {
                IncreaseFlagPool(50);
            }
            Flag flag = flagPool.Dequeue();
            flag.gameObject.SetActive(true);
            return flag;
        }

        private void ReturnFlagToPool(Flag flag)
        {
            flag.gameObject.SetActive(false);
            flagPool.Enqueue(flag);
        }

        private void IncreaseFlagPool(int amount)
        {
            for (int i = 0; i < amount; i++)
            {
                Flag flag = GameObject.Instantiate(flagPrefab, Vector3.zero, Quaternion.identity);
                if (manager.FlagsTransform != null) flag.transform.SetParent(manager.FlagsTransform);
                flag.gameObject.SetActive(false);
                flagPool.Enqueue(flag);
            }
        }

        public void PlaceFlag(int vertexIndex)
        {
            if (!CanPlaceFlag(vertexIndex))
            {
                manager.UIManager.HideAllPanels();
                return;
            }

            Flag flag = GetFlagFromPool();
            flag.transform.position = manager.NodeManager.GetNodePosition(vertexIndex);
            flag.name = $"Flag_{vertexIndex}";
            allFlags.Add(vertexIndex, flag);
            flag.SetFlagId(vertexIndex);

            // Check if the flag is connected to a building
            int northwestIndex = manager.NodeManager.GetNeighbourInDirection(vertexIndex, Direction.Northwest);
            if (northwestIndex != -1)
            {
                Node northwestNode = manager.NodeManager.GetNode(northwestIndex);
                if (northwestNode != null && northwestNode.HasBuilding)
                {
                    flag.SetFlagAttachedToBuilding(true);
                }
            }

            Node node = manager.NodeManager.GetNode(vertexIndex);
            node.SetFlagOnNode(flag);

            manager.UIManager.UpdateUIText("Flags", $"Flags: {allFlags.Count}");

            if (node.HasPath)
            {
                manager.PathManager.SplitPathAtNode(vertexIndex);
            }

            manager.UIManager.HideAllPanels();

            OnFlagPlaced?.Invoke();
        }

        private void RemoveFlag()
        {
            int vertexIndex = manager.SelectedNode;
            if (allFlags.ContainsKey(vertexIndex))
            {
                Flag flag = allFlags[vertexIndex];
                if (IsFlagConnectedToBuilding(flag))
                {
                    // TODO: Ask user to confirm removal of flag connected to building as it will remove the building
                    Debug.LogWarning("Cannot remove flag connected to building.");
                }
                else
                {
                    ReturnFlagToPool(flag);
                    flag.name = "Flag";
                    allFlags.Remove(vertexIndex);
                    manager.NodeManager.GetNode(vertexIndex).RemoveFlagOnNode();
                    JoinOrRemovePaths(vertexIndex);
                }                    
            }

            manager.UIManager.HideAllPanels();

            manager.UIManager.UpdateUIText("Flags", $"Flags: {allFlags.Count}");

            OnFlagRemoved?.Invoke();
        }

        public bool CanPlaceFlag(int vertexIndex)
        {
            if (vertexIndex < 0)
            {
                return false;
            }

            if (HasFlag(vertexIndex))
            {
                return false;
            }

            if (HasNeighborGotFlag(vertexIndex))
            {
                return false;
            }

            Node node = manager.NodeManager.GetNode(vertexIndex);
            if (node.HasBuilding)
            {
                return false;
            }

            return true;
        }

        public bool HasFlag(int vertexIndex)
        {
            return allFlags.ContainsKey(vertexIndex);
        }

        public Flag TryGetFlag(int vertexIndex)
        {
            return HasFlag(vertexIndex) ? allFlags[vertexIndex] : null;
        }

        public bool IsFlagConnectedToBuilding(Flag flag)
        {
            return flag.IsFlagAttachedToBuilding;
        }

        public Flag[] GetBothFlagsFromPath(List<int> pathNodes)
        {
            Flag[] flags = new Flag[2];
            for (int i = 0; i < pathNodes.Count; i++)
            {
                int vertexIndex = pathNodes[i];
                if (HasFlag(vertexIndex))
                {
                    if (flags[0] == null)
                    {
                        flags[0] = TryGetFlag(vertexIndex);
                    }
                    else
                    {
                        flags[1] = TryGetFlag(vertexIndex);
                    }
                }
            }
            return flags;
        }

        private bool HasNeighborGotFlag(int vertexIndex)
        {
            List<int> neighbours = manager.NodeManager.GetNodeNieghbors(vertexIndex);
            for (int i = 0; i < neighbours.Count; i++)
            {
                int neighbor = neighbours[i];
                if (allFlags.ContainsKey(neighbor))
                {
                    return true;
                }
            }
            return false;
        }

        private void JoinOrRemovePaths(int vertexIndex)
        {
            if (GetPathCountAtVertexIndex(vertexIndex) == 0) return;

            if (GetPathCountAtVertexIndex(vertexIndex) == 1)
            {
                // Remove path
                Path path = manager.PathManager.GetPathAtNode(vertexIndex);
                if (path.Nodes.Contains(vertexIndex))
                {
                    manager.PathManager.RemovePath(path);
                }
            }
            else if (GetPathCountAtVertexIndex(vertexIndex) == 2)
            {
                // Join paths
                manager.PathManager.JoinPathAtNode(vertexIndex);
            }
            else
            {
                // Remove paths
                List<Path> pathsToRemove = manager.PathManager.GetAllPaths.Values.Where(p => p.Nodes.Contains(vertexIndex)).ToList();

                for (int i = 0; i < pathsToRemove.Count; i++)
                {
                    manager.PathManager.RemovePath(pathsToRemove[i]);
                }
            }
        }

        // Calculate how many paths are attached to a flag
        private int GetPathCountAtVertexIndex(int vertexIndex)
        {
            int count = 0;
            foreach (var path in manager.PathManager.GetAllPaths.Values)
            {
                if (path.Nodes.Contains(vertexIndex))
                {
                    count++;
                }
            }
            return count;
        }

        public int GetPathCountAtFlag(Flag flag)
        {
            int count = 0;
            foreach (var path in manager.PathManager.GetAllPaths.Values)
            {
                if (path.Nodes.Contains(flag.Id))
                {
                    count++;
                }
            }
            return count;
        }
    }
}
