using System.Collections.Generic;
using System;
using UnityEngine;
using System.Linq;

public class Carrier : Character
{
    private int pathId; // Field for the current path ID
    private StockResourceType carryingResource;
    private int carryingAmount;
    private bool atMidpoint = false;

    public event Action<int, StockResourceType, int> OnResourceAdded;

    public override void Initialize(WorkerManager workerManager, BuildingManager buildingManager, PathManager pathManager, int startNode)
    {
        base.Initialize(workerManager, buildingManager, pathManager, startNode);
        CharacterType = CharacterType.Carrier;
        carryingResource = StockResourceType.None;
        carryingAmount = 0;
        atMidpoint = false;
        pathId = -1; // Reset path assignment
        OnResourceAdded -= HandleResourceAdded; // Clear previous subscription
        OnResourceAdded += HandleResourceAdded; // Re-subscribe
    }

    public void MoveToPathMidpoint(int pathId, int startNode, int startFlag, int endFlag, List<int> pathToMidpoint, int midpoint)
    {
        if (pathToMidpoint == null || pathToMidpoint.Count == 0)
        {
            Debug.LogWarning($"[Carrier] No path to midpoint {midpoint}, returning to HQ");
            pathManager.ReturnCarrierToHQ(this);
            return;
        }

        this.pathId = pathId; // Set the path ID
        Path currentPath = pathManager.GetPathById(pathId);
        if (currentPath == null)
        {
            Debug.LogWarning($"[Carrier] Path ID {pathId} not found, returning to HQ");
            pathManager.ReturnCarrierToHQ(this);
            return;
        }

        // Move to midpoint and stay there using AssignPath with callback
        AssignPath(pathToMidpoint, () =>
        {
            CurrentNode = midpoint; // Set position to midpoint
            atMidpoint = true; // Mark as at midpoint
            onPathComplete = null; // Clear callback to prevent returning to HQ
            pathManager.pathCarriers[pathId] = this; // Ensure carrier is assigned to path
        });
    }

    public void NotifyResourceAdded(int flagId, StockResourceType resource, int amount)
    {
        OnResourceAdded?.Invoke(flagId, resource, amount);
    }

    private Path GetAssignedPath()
    {
        return pathManager.GetPathById(pathId);
    }

    private void HandleResourceAdded(int flagId, StockResourceType resource, int amount)
    {
        Path path = GetAssignedPath();
        if (path == null) return;
        if (flagId != path.StartFlag && flagId != path.EndFlag) return;

        if (CurrentNode != path.Midpoint || !atMidpoint)
        {
            List<int> pathToMidpoint = GetSubPath(path.Nodes, CurrentNode, path.Midpoint);
            if (pathToMidpoint.Count > 1)
            {
                AssignPath(pathToMidpoint, () => HandleResourceAtMidpoint(flagId));
            }
            return;
        }

        HandleResourceAtMidpoint(flagId);
    }

    private void HandleResourceAtMidpoint(int flagId)
    {
        Path path = GetAssignedPath();
        if (path == null) return;
        if (flagId == path.StartFlag && TryPickUpResource())
        {
            List<int> pathToEnd = GetSubPath(path.Nodes, path.Midpoint, path.EndFlag);
            if (pathToEnd.Count > 1)
            {
                AssignPath(pathToEnd);
            }
        }
    }

    protected override void StartTask()
    {
        if (!atMidpoint) return;

        Path path = GetAssignedPath();
        if (path == null) return;
        if (CurrentNode == path.EndFlag)
        {
            if (carryingAmount > 0)
            {
                DropOffResource();
            }
            else if (TryPickUpResourceFromEnd())
            {
                List<int> pathToStart = GetSubPath(path.Nodes, path.EndFlag, path.StartFlag);
                if (pathToStart.Count > 1)
                {
                    AssignPath(pathToStart);
                    return;
                }
            }

            List<int> pathBack = GetSubPath(path.Nodes, path.EndFlag, path.Midpoint);
            if (pathBack.Count > 1)
            {
                AssignPath(pathBack);
            }
        }
        else if (CurrentNode == path.StartFlag && carryingAmount > 0)
        {
            DropOffResource();
            List<int> pathBack = GetSubPath(path.Nodes, path.StartFlag, path.Midpoint);
            if (pathBack.Count > 1)
            {
                AssignPath(pathBack);
            }
        }
    }

    private List<int> GetSubPath(List<int> fullPath, int fromNode, int toNode)
    {
        int fromIndex = fullPath.IndexOf(fromNode);
        int toIndex = fullPath.IndexOf(toNode);

        if (fromIndex == -1 || toIndex == -1)
        {
            Debug.LogWarning($"Node {fromNode} or {toNode} not found in path ID {pathId}");
            return new List<int>();
        }

        return fromIndex < toIndex
            ? fullPath.GetRange(fromIndex, toIndex - fromIndex + 1)
            : fullPath.GetRange(toIndex, fromIndex - toIndex + 1).AsEnumerable().Reverse().ToList();
    }

    private bool TryPickUpResource()
    {
        Path path = GetAssignedPath();
        if (path == null) return false;
        if (pathManager.TryGetResource(path.StartFlag, out StockResourceType resource, out int amount))
        {
            carryingResource = resource;
            carryingAmount = amount;
            return true;
        }
        return false;
    }

    private bool TryPickUpResourceFromEnd()
    {
        Path path = GetAssignedPath();
        if (path == null) return false;
        if (pathManager.TryGetResource(path.EndFlag, out StockResourceType resource, out int amount))
        {
            carryingResource = resource;
            carryingAmount = amount;
            return true;
        }
        return false;
    }

    private void DropOffResource()
    {
        Path path = GetAssignedPath();
        if (path == null) return;
        if (CurrentNode == path.EndFlag)
        {
            pathManager.AddResourceToQueue(path.EndFlag, carryingResource, carryingAmount);
        }
        else if (CurrentNode == path.StartFlag)
        {
            pathManager.AddResourceToQueue(path.StartFlag, carryingResource, carryingAmount);
        }
        carryingAmount = 0;
    }
}