using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PunkyFruitBat
{
    public class PathBuilder
    {
        public event Action OnPathCancelled;

        private readonly PathManager pathManager;
        private readonly HexGridManager manager;

        private readonly List<int> currentPath = new();
        public List<int> CurrentPath => currentPath;

        public PathBuilder(PathManager pathManager, HexGridManager manager)
        {
            this.pathManager = pathManager;
            this.manager = manager;
        }

        public void CreatePath(int startNode)
        {
            if (startNode == -1) return;

            currentPath.Clear();
            currentPath.Add(startNode);
        }

        public void FinalisePath(int endNode)
        {
            int startNode = currentPath.Last();
            if (startNode == endNode)
            {
                Debug.LogWarning("Start and end nodes are the same");
                CancelPath();
                return;
            }

            Flag startFlag = manager.FlagManager.TryGetFlag(currentPath.First());
            Flag endFlag = manager.FlagManager.TryGetFlag(endNode);

            if (startFlag == null || endFlag == null || startFlag == endFlag)
            {
                Debug.LogWarning("Start or end flag is either null, or the same flag!");
                CancelPath();
                return;
            }

            List<int> pathSegments = pathManager.PathFinder.FindPath(startNode, endNode);
            if (pathSegments == null)
            {
                Debug.LogWarning("Path segments are null");
                CancelPath();
                return;
            }

            currentPath.Remove(startNode);
            currentPath.AddRange(pathSegments);
            if (!currentPath.Contains(endNode))
            {
                currentPath.Add(endNode);
            }

            if (IsValidTemporaryPath(currentPath))
            {
                SaveAndVisualiseFinalPath();
            }
            else
            {
                Debug.LogWarning("Invalid temporary path");
            }
            
            CancelPath();
        }

        public void ExtendPath(int endNode)
        {
            if (endNode < 0) return;
            Node endNodeData = manager.NodeManager.GetNode(endNode);
            if (endNodeData == null || endNodeData.hasPath || endNodeData.hasObstacle) return;

            int startNode = currentPath.Last();
            List<int> pathSegments = pathManager.PathFinder.FindPath(startNode, endNode);
            if (pathSegments == null)
            {
                Debug.LogWarning("Path segments are null");
                CancelPath();
                return;
            }

            List<int> tempPath = new(currentPath);
            tempPath.RemoveAt(tempPath.Count - 1);
            tempPath.AddRange(pathSegments);

            if (IsValidTemporaryPath(tempPath))
            {
                currentPath.Clear();
                currentPath.AddRange(tempPath);
                VisualiseTempPath();
            }
            else
            {
                Debug.LogWarning("Invalid temporary path");
                CancelPath();                                                           
            }
        }

        private bool IsValidTemporaryPath(List<int> path)
        {
            if (path == null || path.Count < 2) return false;

            var seenNodes = new HashSet<int>();
            for (int i = 0; i < path.Count; i++)
            {
                int node = path[i];
                // If we have seen this node before and it is not the start or end node, then it is invalid
                if (seenNodes.Contains(node) && i != 0 && i != path.Count - 1) continue;

                foreach (var existingPath in pathManager.GetAllPaths.Values)
                {
                    int index = existingPath.Nodes.IndexOf(node);
                    // If the node is in the middle of an existing path
                    if (index != -1 && index != 0 && index != existingPath.Nodes.Count - 1 && i != 0 && i != path.Count - 1)
                    {
                        return false;
                    }
                }
                seenNodes.Add(node);
            }
            return true;
        }

        public void VisualiseTempPath()
        {
            ClearTempPathVisuals();

            foreach (int node in currentPath)
            {
                Node nodeData = manager.NodeManager.GetNode(node);
                if (nodeData == null) continue;
                GameObject visual = pathManager.GetTempPathVisualsFromPool();
                visual.transform.position = nodeData.transform.position;
                visual.transform.SetParent(manager.TempPathTransform);
            }
        }

        private void ClearTempPathVisuals()
        {
            foreach (Transform child in manager.TempPathTransform)
            {
                pathManager.ReturnTempPathVisualsToPool(child.gameObject);
            }
        }

        private void SaveAndVisualiseFinalPath()
        {
            Flag flag1 = manager.FlagManager.TryGetFlag(currentPath.First());
            Flag flag2 = manager.FlagManager.TryGetFlag(currentPath.Last());
            Path newPath = new(flag1, flag2, currentPath, pathManager.PathId);

            pathManager.AddToAllPaths(newPath);
        }

        private void CancelPath()
        {
            ClearTempPathVisuals();
            currentPath.Clear();
            manager.UIManager.HideAllPanels();
            OnPathCancelled?.Invoke();
        }
    }
}
