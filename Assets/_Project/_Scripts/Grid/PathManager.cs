// --- PathManager.cs ---
using System.Collections.Generic;
using System.Linq;
using TGS;
using UnityEngine;
using static CellTypes;

// --- PathManager.cs ---
public class PathManager : MonoBehaviour
{
    [SerializeField] private GameObject tempPathPrefab;
    [SerializeField] private GameObject testSphere;
    [SerializeField] private Transform hut;
    [SerializeField] private int maxPathLength = 20;

    private TerrainGridSystem tgs;
    private GridNodeManager gridNodeManager;
    private NodeManager nodeManager;
    private GridManager gridManager;
    private PathGenerator pathGenerator;

    private bool isPathingMode = false;
    private GameObject startFlag = null;
    private Cell startCell = null;
    private List<List<Cell>> allPaths = new List<List<Cell>>();
    private List<Cell> tempPath = new List<Cell>();

    public bool IsPathingMode => isPathingMode;

    public IReadOnlyList<List<Cell>> AllPaths => allPaths.AsReadOnly();

    public void Initialize(
        GridNodeManager gridNodeManager,
        TerrainGridSystem tgs,
        NodeManager nodeManager,
        GridManager gridManager,
        PathGenerator pathGenerator)
    {
        this.gridNodeManager = gridNodeManager ?? throw new System.ArgumentNullException(nameof(gridNodeManager));
        this.tgs = tgs ?? throw new System.ArgumentNullException(nameof(tgs));
        this.nodeManager = nodeManager ?? throw new System.ArgumentNullException(nameof(nodeManager));
        this.gridManager = gridManager ?? throw new System.ArgumentNullException(nameof(gridManager));
        this.pathGenerator = pathGenerator ?? throw new System.ArgumentNullException(nameof(pathGenerator));

        allPaths.Clear();
        SubscribeToEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromEvents();
    }

    private void SubscribeToEvents()
    {
        if (gridNodeManager != null)
        {
            gridNodeManager.OnStartPathCreation += StartPathCreation;
        }
        else
        {
            Debug.LogError("PathManager: GridNodeManager is null during initialization!");
        }
    }

    private void UnsubscribeFromEvents()
    {
        if (gridNodeManager != null)
        {
            gridNodeManager.OnStartPathCreation -= StartPathCreation;
        }
    }

    public void StartPathCreation(GameObject startFlag)
    {
        if (!CanStartPathCreation(startFlag)) return;

        isPathingMode = true;
        this.startFlag = startFlag;
        startCell = nodeManager.GetCellFromNode(startFlag);

        tempPath.Clear();
        tempPath.Add(startCell);
    }

    private bool CanStartPathCreation(GameObject startFlag)
    {
        if (startFlag == null || nodeManager == null)
        {
            Debug.LogError("PathManager.StartPathCreation: Missing required references (startFlag or nodeManager)!");
            return false;
        }
        return true;
    }

    public void TryAddPathToEndNode(GameObject endNode)
    {
        if (!isPathingMode || endNode == null) return;

        Cell endCell = nodeManager.GetCellFromNode(endNode);
        if (endCell == null) return;

        CellData endCellData = gridManager.GetCellData(endCell);
        // Try to get the flag component from the new endNode, if it has one, then we know it has a flag.
        if (endCellData?.HasFlag == true)
        {
            FinalisePath(endCell);
        }
        else
        {
            ExtendPath(endCell);
        }
    }

    private void ExtendPath(Cell endCell)
    {
        if (!CanExtendPath(endCell)) return;

        Cell currentStartCell = tempPath.Last();
        List<Cell> pathSegment = GeneratePathSegment(currentStartCell, endCell);
        if (pathSegment == null)
        {
            CancelPathCreation();
            return;
        }

        tempPath.AddRange(pathSegment);
        if (IsValidPath(tempPath))
        {
            VisualizeTempPath();
        }
        else
        {
            tempPath.RemoveRange(tempPath.Count - pathSegment.Count, pathSegment.Count);
        }
    }

    private void FinalisePath(Cell endCell)
    {
        Cell currentStartCell = tempPath.Last();
        List<Cell> pathSegment = GeneratePathSegment(currentStartCell, endCell);
        if (pathSegment == null)
        {
            CancelPathCreation();
            return;
        }

        tempPath.AddRange(pathSegment);
        if (!tempPath.Contains(endCell))
        {
            tempPath.Add(endCell);
        }

        if (IsValidPath(tempPath))
        {
            SaveAndVisualizeFinalPath();
            EndPathCreation();
        }
        else
        {
            CancelPathCreation();
        }
    }

    private List<Cell> GeneratePathSegment(Cell startCell, Cell endCell)
    {
        List<int> cellIndices = tgs.FindPath(startCell.index, endCell.index);
        if (cellIndices == null)
        {
            Debug.Log($"No path found between {startCell.index} and {endCell.index}");
            return null;
        }

        List<Cell> segment = new();
        foreach (int index in cellIndices)
        {
            segment.Add(tgs.cells[index]);
        }
        return segment;
    }

    private void SaveAndVisualizeFinalPath()
    {
        allPaths.Add(new List<Cell>(tempPath)); // Store a copy
        Vector3[] points = tempPath.Select(GetCellWorldPosition).ToArray();

        foreach (Cell cell in tempPath)
        {
            gridManager.SetCellPath(cell, true);
            //if (nodeManager.GetCellNodeMap().TryGetValue(cell, out GameObject pathNode))
            //{
            //    nodeManager.SetNodeVisibility(pathNode, true, Color.red); // Configurable color could be added
            //}
        }

        pathGenerator.CreatePath(points);
    }

    private bool CanExtendPath(Cell endCell)
    {
        CellData data = gridManager.GetCellData(endCell);
        if (data?.HasPath == true)
        {
            Debug.Log("Cannot extend path: Cell already in an existing path.");
            return false;
        }
        return true;
    }

    private bool IsValidPath(List<Cell> path)
    {
        if (path.Count > maxPathLength)
        {
            Debug.Log($"Path exceeds max length of {maxPathLength}");
            return false;
        }

        foreach (Cell cell in path)
        {
            CellData data = gridManager.GetCellData(cell);
            if (!IsCellValidForPath(cell, data))
            {
                return false;
            }
        }

        return !HasInvalidOverlap(path);
    }

    private bool IsCellValidForPath(Cell cell, CellData data)
    {
        if (data == null) return true; // Assume valid if no data

        if (data.TerrainType is TerrainType.Water or TerrainType.MountainTop or TerrainType.Marsh)
        {
            Debug.Log($"Path blocked by terrain: {data.TerrainType}");
            return false;
        }
        if (data.HasObstacle)
        {
            Debug.Log("Path blocked by obstacle");
            return false;
        }
        if (data.BuildingType != BuildingType.None)
        {
            Debug.Log("Path blocked by building");
            return false;
        }
        return true;
    }

    private bool HasInvalidOverlap(List<Cell> path)
    {
        foreach (List<Cell> existingPath in allPaths)
        {
            foreach (Cell cell in path)
            {
                CellData data = gridManager.GetCellData(cell);
                if (existingPath.Contains(cell) && data?.HasFlag != true)
                {
                    Debug.Log("Path overlaps with existing path.");
                    return true;
                }
            }
        }
        return false;
    }

    public void SplitPathAt(Cell splitCell)
    {
        List<List<Cell>> newPaths = new();
        List<List<Cell>> pathsToRemove = allPaths.Where(path => path.Contains(splitCell)).ToList();

        foreach (List<Cell> path in pathsToRemove)
        {
            int splitIndex = path.IndexOf(splitCell);
            if (splitIndex >= 0)
            {
                AddSplitPaths(path, splitIndex, newPaths);
            }
            allPaths.Remove(path);
        }

        allPaths.AddRange(newPaths);
        VisualizeAllPaths();
    }

    private void AddSplitPaths(List<Cell> path, int splitIndex, List<List<Cell>> newPaths)
    {
        List<Cell> firstPart = path.GetRange(0, splitIndex + 1);
        if (firstPart.Count > 1) newPaths.Add(firstPart);

        List<Cell> secondPart = path.GetRange(splitIndex, path.Count - splitIndex);
        if (secondPart.Count > 1) newPaths.Add(secondPart);
    }

    public void EndPathCreation()
    {
        isPathingMode = false;
        ClearTempPath();
        ResetPathCreationState();
    }

    public void CancelPathCreation()
    {
        isPathingMode = false;
        if (startCell != null && !allPaths.Any(path => path.Contains(startCell)))
        {
            ResetStartNode();
        }
        ResetPathCreationState();
        VisualizeAllPaths();
    }

    private void ResetStartNode()
    {
        //nodeManager.SetNodeVisibility(startFlag, false, nodeManager.DefaultColor);
        gridManager.SetCellPath(startCell, false);
    }

    private void ResetPathCreationState()
    {
        startFlag = null;
        startCell = null;
        ClearTempPath();
    }

    public void ClearAllPaths()
    {
        allPaths.Clear();
        ClearTempPath();
        VisualizeAllPaths();
    }

    public void RemovePathsContainingCell(Cell cell)
    {
        List<List<Cell>> pathsToRemove = allPaths.Where(path => path.Contains(cell)).ToList();
        foreach (List<Cell> path in pathsToRemove)
        {
            allPaths.Remove(path);
        }
    }

    public void AddPath(List<Cell> path) => allPaths.Add(path);

    public IReadOnlyList<List<Cell>> GetAllPaths() => allPaths.AsReadOnly();

    public List<Cell> GetAllPathCells() => allPaths.SelectMany(path => path).Distinct().ToList();

    private void VisualizeTempPath()
    {
        ClearTempPathVisuals();
        foreach (Cell cell in tempPath)
        {
            GameObject tempNode = Instantiate(tempPathPrefab, GetCellWorldPosition(cell) + Vector3.up * nodeManager.NodeHeightOffset, Quaternion.identity, transform);
            if (tempNode.TryGetComponent<SpriteRenderer>(out var renderer))
            {
                renderer.color = Color.cyan;
            }
        }
    }

    private void ClearTempPathVisuals()
    {
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }
    }

    private void ClearTempPath()
    {
        ClearTempPathVisuals();
        tempPath.Clear();
    }

    public void VisualizeAllPaths()
    {
        //ResetAllNodes();
        VisualizeTempPath();
    }

    //private void ResetAllNodes()
    //{
    //    foreach (var kvp in nodeManager.GetCellNodeMap())
    //    {
    //        Cell cell = kvp.Key;
    //        GameObject node = kvp.Value;
    //        CellData data = gridManager.GetCellData(cell);
    //        //if (data?.HasFlag != true)
    //        //{
    //        //    nodeManager.SetNodeVisibility(node, false, nodeManager.DefaultColor);
    //        //}
    //    }
    //}

    public Vector3 GetCellWorldPosition(Cell cell) => tgs.CellGetCentroid(cell.index);
}