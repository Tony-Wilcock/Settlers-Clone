// --- PathManager.cs ---
using UnityEngine;
using System.Collections.Generic;
using TGS;
using System.Linq;

public class PathManager : MonoBehaviour
{
    [SerializeField] private int maxPathLength = 20;// Not sure if I need this yet

    private TerrainGridSystem tgs;
    GridNodeManager gridNodeManager;
    private NodeManager nodeManager;
    private GridManager gridManager;
    private bool isPathingMode = false;
    private GameObject startFlag = null;
    private Cell startCell = null;
    private List<List<Cell>> allPaths = new List<List<Cell>>();
    private List<Cell> currentPath = new List<Cell>();

    public bool IsPathingMode => isPathingMode;

    private void OnEnable()
    {
        tgs = TerrainGridSystem.instance;
        nodeManager = GetComponent<NodeManager>();
        Initialize(tgs, nodeManager);
    }

    public void Initialize(TerrainGridSystem tgs, NodeManager nodeManager)
    {
        this.tgs = tgs;
        this.nodeManager = nodeManager;
        gridManager = GetComponent<GridManager>();
        gridNodeManager = GetComponent<GridNodeManager>();

        allPaths.Clear();

        if (gridNodeManager != null)
        {
            gridNodeManager.OnStartPathCreation += StartPathCreation;
        }
        else
        {
            Debug.LogError("PathManager: Could not find GridNodeManager!");
        }
    }

    private void OnDisable()
    {
        if (gridNodeManager != null)
        {
            gridNodeManager.OnStartPathCreation -= StartPathCreation;
        }
    }

    public void StartPathCreation(GameObject startFlag)
    {
        isPathingMode = true;
        this.startFlag = startFlag;
        startCell = nodeManager.GetCellFromNode(startFlag);

        if (!nodeManager || !startFlag || startCell == null)
        {
            Debug.LogError("PathManager.StartPathPlacement: Initialization error.");
            return;
        }

        currentPath.Clear();
        currentPath.Add(startCell);

        Debug.Log("Starting Path Creation");
    }

    public void TryAddNodeToPath(GameObject node)
    {
        if (!isPathingMode)
        {
            Debug.Log("Not in pathing mode. Returning.");
            return;
        }

        Cell newCell = nodeManager.GetCellFromNode(node);
        if (newCell == null)
        {
            Debug.Log("newCell is null. Returning.");
            return;
        }

        // Try to get the flag component from the new node, if it has one, then we know it has a flag.
        if (node.GetComponentInChildren<Flag>() != null)
        {
            if (node != startFlag)
            {
                // Clicked on a flag (that is NOT the start flag).  Finalize the path.
                FinalisePath(newCell);
            }
        }
        else
        {
            ExtendPath(newCell);
        }
    }

    private void ExtendPath(Cell newCell)
    {
        if (!CanExtendPath(newCell)) return;

        Cell lastCell = currentPath[currentPath.Count - 1];

        List<int> cellIndices = tgs.FindPath(lastCell.index, newCell.index);

        if (cellIndices != null)
        {
            List<Cell> potentialPathSegment = new List<Cell>();
            for (int i = 0; i < cellIndices.Count; i++) // Don't include the start cell
            {
                potentialPathSegment.Add(tgs.cells[cellIndices[i]]);
            }

            currentPath.AddRange(potentialPathSegment);
            VisualizeCurrentPath();
        }
        else
        {
            Debug.Log("No path found between " + lastCell.index + " and " + newCell.index);
            CancelPathCreation();
        }

            Debug.Log("Extending path from " + lastCell.index + " to " + newCell.index);
    }

    private bool CanExtendPath(Cell newCell)
    {
        // Check if the new cell is already part of an existing path or the current path.
        foreach (List<Cell> existingPath in allPaths)
        {
            if (existingPath.Contains(newCell))
            {
                Debug.Log("Cannot extend path: Cell already in an existing path.");
                return false;
            }
        }

        if (currentPath.Contains(newCell))
        {
            Debug.Log("Cannot extend path: Cell already in the current path.");
            return false;
        }
        if (currentPath.Count + 1 > maxPathLength) //+1 since cell not added to the path yet
        {
            Debug.Log("Max path length reached");
            return false;
        }

        return true;
    }

    private void FinalisePath(Cell newCell)
    {
        Cell lastCell = currentPath[currentPath.Count - 1];
        List<int> cellIndices = tgs.FindPath(lastCell.index, newCell.index);

        if (cellIndices != null)
        {
            List<Cell> potentialPath = new List<Cell>();
            for (int i = 0; i < cellIndices.Count; i++) // Don't include last cell
            {
                potentialPath.Add(tgs.cells[cellIndices[i]]);
            }
            potentialPath.Add(lastCell); // Add the last cell to the path
            currentPath.AddRange(potentialPath);
            Debug.Log(potentialPath.Contains(lastCell));

            if (IsValidPath(potentialPath))
            {
                // Add to path list, and set the cells
                allPaths.Add(potentialPath);
                foreach (Cell cell in potentialPath)
                {
                    gridManager.SetCellPath(cell, true);

                    if (nodeManager.GetCellNodeMap().TryGetValue(cell, out GameObject pathNode))
                    {
                        nodeManager.SetNodeVisibility(pathNode, true, Color.red);
                    }
                }
                EndPathCreation(); // No need to pass path, already added
            }
            else
            {
                CancelPathCreation();
            }
        }
        else // ADDED THIS: Handle case where no path is found
        {
            Debug.Log("No path found between flags.");
            CancelPathCreation();
        }
    }

    private bool IsValidPath(List<Cell> path)
    {
        if (path.Count > maxPathLength)
        {
            Debug.Log("Path is too long. Max length is " + maxPathLength);
            return false;
        }

        foreach (List<Cell> existingPath in allPaths)
        {
            foreach (Cell cell in path)
            {
                if (cell == startCell || cell == path[path.Count - 1]) continue;
                CellData cellData = gridManager.GetCellData(cell);

                if (existingPath.Contains(cell) && !cellData.hasFlag)
                {
                    Debug.Log("Path overlaps with existing path.");
                    return false;
                }
            }
        }
        return true;
    }

    public void SplitPathAt(Cell splitCell)
    {
        List<List<Cell>> pathsToRemove = new List<List<Cell>>();
        List<List<Cell>> pathsToAdd = new List<List<Cell>>();

        foreach (List<Cell> path in allPaths)
        {
            if (path.Contains(splitCell))
            {
                pathsToRemove.Add(path);

                int splitIndex = path.IndexOf(splitCell);

                // Create the first part of the split path
                List<Cell> path1 = new List<Cell>();
                for (int i = 0; i <= splitIndex; i++)
                {
                    path1.Add(path[i]);
                }
                if (path1.Count > 1) pathsToAdd.Add(path1);

                // Create the second part of the split path
                List<Cell> path2 = new List<Cell>();
                for (int i = splitIndex; i < path.Count; i++)
                {
                    path2.Add(path[i]);
                }
                if (path2.Count > 1) pathsToAdd.Add(path2);
            }
        }
        //Remove old paths
        foreach (List<Cell> path in pathsToRemove)
        {
            allPaths.Remove(path);
        }
        //Add new paths
        foreach (List<Cell> path in pathsToAdd)
        {
            allPaths.Add(path);
        }

        VisualizeAllPaths(); // Update visuals after splitting
    }

    public void EndPathCreation(List<Cell> finalPath = null)
    {
        Debug.Log("Ending Path Creation");
        isPathingMode = false;

        if (finalPath != null)
        {
            foreach (Cell cell in finalPath)
            {
                gridManager.SetCellPath(cell, true);
            }
            Debug.Log("Path Added with " + finalPath.Count + " points.");
            currentPath.Clear();
            VisualizeAllPaths();
        }

        startFlag = null;
        startCell = null;
    }

    public void CancelPathCreation()
    {
        isPathingMode = false;

        if (startCell != null && !allPaths.Any(path => path.Contains(startCell)))
        {
            nodeManager.SetNodeVisibility(startFlag, false, nodeManager.defaultColor);
            gridManager.SetCellPath(startCell, false);
        }

        startFlag = null;
        startCell = null;
        currentPath.Clear();
        VisualizeAllPaths();
    }

    public void ClearAllPaths()
    {
        allPaths.Clear();
        currentPath.Clear();
        VisualizeAllPaths();
    }

    public void AddPath(List<Cell> path)
    {
        allPaths.Add(path);
    }

    public List<List<Cell>> GetAllPaths()
    {
        return allPaths;
    }

    private void VisualizeCurrentPath()
    {
        // Visualize the temporary path (orange)
        foreach (Cell cell in currentPath)
        {
            if (nodeManager.GetCellNodeMap().TryGetValue(cell, out GameObject node))
            {
                nodeManager.SetNodeVisibility(node, true, Color.yellow); // Use yellow for temp path
            }
        }
    }

    public void VisualizeAllPaths()
    {
        // First, reset all nodes to default state
        foreach (var kvp in nodeManager.GetCellNodeMap())
        {
            Cell cell = kvp.Key;
            GameObject node = kvp.Value;
            CellData data = gridManager.GetCellData(cell);

            // Only reset if NOT a flag.  Flags keep their visibility.
            if (!data.hasFlag)
            {
                nodeManager.SetNodeVisibility(node, false, nodeManager.defaultColor);
            }
        }


        // Then, visualize the paths.  This correctly handles flags and paths.
        foreach (List<Cell> path in allPaths)
        {
            VisualizeFinalPath(path, false);
        }
    }

    private void VisualizeFinalPath(List<Cell> path, bool clearExisting = true)
    {
        foreach (Cell cell in path)
        {
            if (nodeManager.GetCellNodeMap().TryGetValue(cell, out GameObject node))
            {
                nodeManager.SetNodeVisibility(node, true, Color.red);
            }
        }
    }

    public List<Cell> GetAllPathCells()
    {
        List<Cell> allPathCells = new List<Cell>();
        foreach (List<Cell> path in allPaths)
        {
            allPathCells.AddRange(path);
        }
        return allPathCells;
    }
}