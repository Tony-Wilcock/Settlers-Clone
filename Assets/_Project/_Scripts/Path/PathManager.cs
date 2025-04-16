using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using UnityEngine;

namespace PunkyFruitBat
{
    [Serializable]
    public class PathManager
    {
        public event Action<Path> OnPathCreationCompleted;
        public event Action<Path> OnPathRemoved;

        private HexGridManager manager;
        private PathFinder pathFinder;
        private PathBuilder pathBuilder;

        public GameObject PathVisual { get; private set; }
        private readonly int pathVisualPoolSize = 100; // Initial pool size (adjust as needed)
        private Queue<GameObject> pathVisualPool;
        public GameObject TempPathVisual { get; private set; }
        private readonly int tempPathVisualPoolSize = 20; // Initial pool size (adjust as needed)
        private Queue<GameObject> tempPathVisualPool;

        public PathFinder PathFinder => pathFinder;
        public PathBuilder PathBuilder => pathBuilder;

        private Dictionary<int, Path> AllPaths { get; } = new Dictionary<int, Path>();
        public IReadOnlyDictionary<int, Path> GetAllPaths => new ReadOnlyDictionary<int, Path>(AllPaths);

        public Queue<Path> UnconnectedPaths { get; set; } = new Queue<Path>();

        public int PathId { get; private set; } = 0;

        public bool IsInPathCreationMode { get; private set; } = false;

        public void Initialise(HexGridManager manager, GameObject pathVisual, GameObject tempPathVisual)
        {
            this.manager = manager;
            this.PathVisual = pathVisual;
            this.TempPathVisual = tempPathVisual;
            pathFinder = new PathFinder(this, manager);
            pathBuilder = new PathBuilder(this, manager);

            InitialisePools();

            pathBuilder.OnPathCancelled += () => IsInPathCreationMode = false;
        }

        private void InitialisePools()
        {
            pathVisualPool = new Queue<GameObject>();
            IncreasePathVisualPool(pathVisualPoolSize);

            tempPathVisualPool = new Queue<GameObject>();
            IncreaseTempPathVisualPool(tempPathVisualPoolSize);
        }

        private void IncreasePathVisualPool(int amount)
        {
            for (int i = 0; i < amount; i++)
            {
                GameObject pathVisual = GameObject.Instantiate(PathVisual, Vector3.zero, Quaternion.identity);
                if (manager.PathVisualsTransform != null) pathVisual.transform.SetParent(manager.PathVisualsTransform);
                pathVisual.SetActive(false);
                pathVisualPool.Enqueue(pathVisual);
            }
        }

        private void IncreaseTempPathVisualPool(int amount)
        {
            for (int i = 0; i < amount; i++)
            {
                GameObject pathVisual = GameObject.Instantiate(TempPathVisual, Vector3.zero, Quaternion.identity);
                if (manager.TempPathTransform != null) pathVisual.transform.SetParent(manager.TempPathTransform);
                pathVisual.SetActive(false);
                tempPathVisualPool.Enqueue(pathVisual);
            }
        }

        public GameObject GetPathVisualsFromPool()
        {
            if (pathVisualPool.Count == 0)
            {
                IncreasePathVisualPool(100);
            }
            GameObject pathVisual = pathVisualPool.Dequeue();
            pathVisual.SetActive(true);
            return pathVisual;
        }

        public GameObject GetTempPathVisualsFromPool()
        {
            if (tempPathVisualPool.Count == 0)
            {
                IncreaseTempPathVisualPool(20);
            }
            GameObject pathVisual = tempPathVisualPool.Dequeue();
            pathVisual.SetActive(true);
            return pathVisual;
        }

        public void ReturnPathVisualsToPool(GameObject pathVisual)
        {
            pathVisual.SetActive(false);
            pathVisualPool.Enqueue(pathVisual);
        }

        public void ReturnTempPathVisualsToPool(GameObject pathVisual)
        {
            pathVisual.SetActive(false);
            tempPathVisualPool.Enqueue(pathVisual);
        }

        public void StartPathPlacement()
        {
            int startNode = manager.SelectedNode;
            if (startNode < 0) return;

            Node startNodeData = manager.NodeManager.GetNode(startNode);
            if (startNodeData == null) return;
            if (startNodeData.HasFlag)
            {
                IsInPathCreationMode = true;
                pathBuilder.CreatePath(startNode);
                manager.UIManager.HideAllPanels();
            }
        }

        public void TryAddPathToEndNode(int endNode)
        {
            if (!IsInPathCreationMode || endNode == -1) return;

            Node endNodeData = manager.NodeManager.GetNode(endNode);
            if (endNodeData == null) return;

            if (endNodeData.HasFlag)
            {
                pathBuilder.FinalisePath(endNode);
            }
            else
            {
                pathBuilder.ExtendPath(endNode);
            }
        }

        public void SplitPathAtNode(int splitNode) // Split 1 path into 2 by adding a flag
        {
            Path path = GetPathAtNode(splitNode);
            if (path == null) return;
            int splitIndex = path.Nodes.IndexOf(splitNode);
            if (splitIndex == 0 || splitIndex == path.Nodes.Count - 1) return;

            Flag[] flags = manager.FlagManager.GetBothFlagsFromPath(path.Nodes);
            Flag newFlag = manager.FlagManager.TryGetFlag(splitNode);

            List<int> firstPart = path.Nodes.GetRange(0, splitIndex + 1);
            List<int> secondPart = path.Nodes.GetRange(splitIndex, path.Nodes.Count - splitIndex);

            Carrier carrier = path.Carrier;

            RemovePath(path);

            Path firstPath = new(flags[0], newFlag, firstPart, PathId);
            if (carrier)
            {
                firstPath.SetCarrier(carrier);

                if (carrier.IsBusy)
                {
                    carrier.newPath = firstPath;
                }
                else
                {
                    carrier.StopAllCoroutines();
                    manager.StartCoroutine(carrier.MoveCharacter(firstPath.CenterNode));
                }
            }
            AddToAllPaths(firstPath);

            Path secondPath = new(newFlag, flags[1], secondPart, PathId);
            AddToAllPaths(secondPath);
        }

        public void JoinPathAtNode(int joinNode) // Join two paths at a node after removing a flag
        {
            List<Path> PathsToJoin = AllPaths.Values.Where(p => p.Nodes.Contains(joinNode)).ToList();
            if (PathsToJoin.Count != 2) return;

            Path path1 = PathsToJoin[0];
            Path path2 = PathsToJoin[1];

            List<int> joinedNodes = CombinePaths(path1, path2, joinNode);

            if (joinedNodes == null) return;

            Carrier carrier1 = path1.Carrier;
            Carrier carrier2 = path2.Carrier;
            Carrier carrierToKeep = carrier1; // Get the carrierToKeep from carrier1

            Flag[] flags = manager.FlagManager.GetBothFlagsFromPath(joinedNodes);

            Path joinedPath = new(flags[0], flags[1], joinedNodes, PathId);

            RemovePath(path1);
            RemovePath(path2);

            if (joinedPath.Flag1 == joinedPath.Flag2) // Check if the flags are the same. If they are, remove the path and return out of this method
            {
                Debug.LogWarning($"Path starts and ends with the same flag: {joinedPath.Flag1 == joinedPath.Flag2}");
                RemovePath(joinedPath);
                return;
            }

            joinedPath.SetCarrier(carrierToKeep);

            if (carrierToKeep.IsBusy)
            {
                if (carrierToKeep.CurrentResource == null)
                {
                    Debug.Log($"Carrier to keep {carrierToKeep.GetInstanceID()} is busy and has no resource: {carrierToKeep.CurrentResource == null}"); // Check if the resource is null
                    Resource resource = carrierToKeep.ResourceToPickup;
                    Flag pickupFlag = resource.CurrentFlag;
                    Flag dropoffFlag = joinedPath.Flag1 == pickupFlag ? joinedPath.Flag2 : joinedPath.Flag1;
                    carrier2.AssignTransportTask(resource, pickupFlag, dropoffFlag);
                    carrier2.StopAllCoroutines();
                    carrier2.ResetCarrierState();
                }
                else
                {
                    if (carrierToKeep.AssignedPath.Flag1.gameObject.activeSelf)
                    {
                        Debug.Log($"Carrier to keep {carrierToKeep.GetInstanceID()} is busy and assigned to flag 1: {carrierToKeep.AssignedPath.Flag1.gameObject.activeSelf}");
                        carrierToKeep.StartCoroutine(carrierToKeep.DropoffResource(carrierToKeep.CurrentResource, carrierToKeep.AssignedPath.Flag1));
                    }
                    else if (carrierToKeep.AssignedPath.Flag2.gameObject.activeSelf)
                    {
                        Debug.Log($"Carrier to keep {carrierToKeep.GetInstanceID()} is busy and assigned to flag 2: {carrierToKeep.AssignedPath.Flag2.gameObject.activeSelf}");
                        carrierToKeep.StartCoroutine(carrierToKeep.DropoffResource(carrierToKeep.CurrentResource, carrierToKeep.AssignedPath.Flag2));
                    }
                }
            }
            else
            {
                Debug.Log($"Carrier to keep {carrierToKeep.GetInstanceID()} is not busy: {carrierToKeep.IsBusy}"); // Check if the carrierToKeep is busy
            }

            if (carrier2.IsBusy)
            {
                if (carrier2.CurrentResource == null)
                {
                    Debug.Log($"Carrier 2 is busy and has no resource: {carrier2.CurrentResource == null}"); // Check if the resource is null
                    Resource resource = carrier2.ResourceToPickup;
                    Flag pickupFlag = resource.CurrentFlag;
                    Flag dropoffFlag = joinedPath.Flag1 == pickupFlag ? joinedPath.Flag2 : joinedPath.Flag1;
                    carrierToKeep.AssignTransportTask(resource, pickupFlag, dropoffFlag);
                    carrier2.StopAllCoroutines();
                    carrier2.ResetCarrierState();
                }
                else
                {
                    if (carrier2.AssignedPath.Flag1.gameObject.activeSelf)
                    {
                        Debug.Log($"Carrier 2 {carrier2.GetInstanceID()} is busy and assigned to flag 1: {carrier2.AssignedPath.Flag1.gameObject.activeSelf}");
                        carrier2.StartCoroutine(carrier2.DropoffResource(carrier2.CurrentResource, carrier2.AssignedPath.Flag1));
                    }
                    else if (carrier2.AssignedPath.Flag2.gameObject.activeSelf)
                    {
                        Debug.Log($"Carrier 2 {carrier2.GetInstanceID()} is busy and assigned to flag 2: {carrier2.AssignedPath.Flag2.gameObject.activeSelf}");
                        carrier2.StartCoroutine(carrier2.DropoffResource(carrier2.CurrentResource, carrier2.AssignedPath.Flag2));
                    }
                }
            }
            else
            {
                Debug.Log($"Carrier 2 {carrier2.GetInstanceID()} is not busy: {!carrier2.IsBusy}"); // Check if the carrierToKeep is busy
            }

            if (carrierToKeep.IsBusy)
            {
                carrierToKeep.newPath = joinedPath;
            }
            else
            {
                carrierToKeep.StopAllCoroutines();
                manager.StartCoroutine(carrierToKeep.MoveCharacter(joinedPath.CenterNode));
            }

            AddToAllPaths(joinedPath);
        }

        private List<int> CombinePaths(Path path1, Path path2, int joinNode)
        {
            int path1JoinIndex = path1.Nodes.IndexOf(joinNode);
            int path2JoinIndex = path2.Nodes.IndexOf(joinNode);
            bool path1StartsAtJoin = path1JoinIndex == 0;
            bool path1EndsAtJoin = path1JoinIndex == path1.Nodes.Count - 1;
            bool path2StartsAtJoin = path2JoinIndex == 0;
            bool path2EndsAtJoin = path2JoinIndex == path2.Nodes.Count - 1;

            List<int> joinedNodes = new();
            if (path1EndsAtJoin && path2StartsAtJoin)
            {
                joinedNodes.AddRange(path1.Nodes);
                joinedNodes.AddRange(path2.Nodes.Skip(1));
            }
            else if (path1StartsAtJoin && path2EndsAtJoin)
            {
                joinedNodes.AddRange(path2.Nodes);
                joinedNodes.AddRange(path1.Nodes.Skip(1));
            }
            else if (path1StartsAtJoin && path2StartsAtJoin)
            {
                List<int> reversedPath1 = new(path1.Nodes);
                reversedPath1.Reverse();
                joinedNodes.AddRange(reversedPath1);
                joinedNodes.AddRange(path2.Nodes.Skip(1));
            }
            else if (path1EndsAtJoin && path2EndsAtJoin)
            {
                joinedNodes.AddRange(path1.Nodes);
                List<int> reversedPath2 = new(path2.Nodes);
                reversedPath2.Reverse();
                joinedNodes.AddRange(reversedPath2.Skip(1));
            }
            else
            {
                return null;
            }

            return joinedNodes;
        }

        public void AddToAllPaths(Path newPath)
        {
            AllPaths[newPath.Id] = newPath;
            PathId++;
            foreach (int node in newPath.Nodes)
            {
                manager.NodeManager.GetNode(node).SetPathOnNode(newPath);
            }

            manager.UIManager.UpdateUIText("Paths", $"Paths: {AllPaths.Count}");

            OnPathCreationCompleted?.Invoke(newPath);
        }

        public void RemovePath(Path path)
        {
            for (int i = 0; i < path.Nodes.Count; i++)
            {
                Node node = manager.NodeManager.GetNode(path.Nodes[i]);
                if (node != null)
                {
                    node.RemovePathOnNode();
                }
            }

            AllPaths.Remove(path.Id);

            OnPathRemoved?.Invoke(path);

            path.OnPathRemoved();

            manager.UIManager.UpdateUIText("Paths", $"Paths: {AllPaths.Count}");
        }

        public Path GetPathAtNode(int nodeIndex)
        {
            foreach (Path path in AllPaths.Values)
            {
                if (path.Nodes.Contains(nodeIndex))
                {
                    return path;
                }
            }
            return null;
        }

        public List<Flag> FlagsAlongRoute(List<int> route)
        {
            List<Flag> flags = new();
            foreach (int node in route)
            {
                Node nodeData = manager.NodeManager.GetNode(node);
                if (nodeData == null) continue;
                if (nodeData.HasFlag)
                {
                    Flag flag = manager.FlagManager.TryGetFlag(node);
                    if (flag != null)
                    {
                        flags.Add(flag);
                    }
                }
            }
            return flags;
        }

        public void Unsubscribe()
        {
            pathBuilder.OnPathCancelled -= () => IsInPathCreationMode = false;
        }
    }
}
