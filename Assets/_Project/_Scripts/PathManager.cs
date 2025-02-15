// --- PathManager.cs ---
using UnityEngine;
using System.Collections.Generic;
using TGS;

public class PathManager : MonoBehaviour
{
    [SerializeField] private GameObject pathPrefab;

    private TerrainGridSystem _tgs;
    private NodePlacer _nodePlacer;
    private GridManager _gridManager;
    private bool _isPathingMode = false;
    private GameObject _startFlag = null;
    private Cell _startCell = null;
    private List<GameObject> _currentPathVisuals = new List<GameObject>();
    private List<List<Cell>> _allPaths = new List<List<Cell>>();

    public bool IsPathingMode => _isPathingMode;

    private void OnEnable()
    {
        Initialize(TerrainGridSystem.instance, FindFirstObjectByType<NodePlacer>());
    }

    public void Initialize(TerrainGridSystem tgs, NodePlacer nodePlacer)
    {
        Debug.Log("PathManager Initialize called!");
        _tgs = tgs;
        _nodePlacer = nodePlacer;
        _gridManager = FindFirstObjectByType<GridManager>();
        _tgs.cells.ForEach(cell => cell.canCross = true);

        FindFirstObjectByType<GridNodeManager>().OnStartPathCreation += StartPathCreation;
    }

    private void OnDisable()
    {
        if (FindFirstObjectByType<GridNodeManager>() != null)
        {
            FindFirstObjectByType<GridNodeManager>().OnStartPathCreation -= StartPathCreation;
        }
    }

    public void StartPathCreation(GameObject startFlag)
    {
        Debug.Log("PathManager.StartPathCreation called!");

        _isPathingMode = true;
        _startFlag = startFlag;

        if (_nodePlacer == null)
        {
            Debug.LogError("PathManager.StartPathCreation: _nodePlacer is NULL!");
        }
        else
        {
            Debug.Log("_nodePlacer is not null");
        }

        if (_startFlag == null)
        {
            Debug.LogError("PathManager.StartPathCreation: _startFlag is NULL!");
        }
        else
        {
            Debug.Log("_startFlag is not null: " + _startFlag.name);
        }

        _startCell = _nodePlacer.GetCellFromNode(_startFlag);

        if (_startCell == null)
        {
            Debug.LogError("PathManager.StartPathCreation: _startCell is NULL (GetCellFromNode returned null)!");
        }

        ClearPathVisuals();
        Debug.Log("Starting Path Creation");
        FindFirstObjectByType<NodeHighlighter>().enabled = true; // Re-enable highlighter
    }

    public void TryAddNodeToPath(GameObject newNode)
    {
        Debug.Log("TryAddNodeToPath called with: " + (newNode != null ? newNode.name : "null"));

        if (!_isPathingMode)
        {
            Debug.Log("Not in pathing mode. Returning.");
            return;
        }

        Cell newCell = _nodePlacer.GetCellFromNode(newNode);
        if (newCell == null)
        {
            Debug.Log("newCell is null. Returning.");
            return;
        }

        // Check if the cell has a flag AND it's NOT the start flag
        if (newNode.transform.childCount > 0 && newNode.transform.GetChild(0).GetComponent<Flag>())
        {
            Debug.Log("Clicked node has a flag.");
            if (newNode != _startFlag)
            {
                Debug.Log("Clicked node is NOT the start flag.");
                // Proceed with path creation.
                List<int> cellIndices = _tgs.FindPath(_startCell.index, newCell.index);
                if (cellIndices != null)
                {
                    Debug.Log("Path found!");
                    List<Cell> finalPath = new List<Cell>();
                    for (int i = 0; i < cellIndices.Count; i++)
                    {
                        finalPath.Add(_tgs.cells[cellIndices[i]]);
                    }

                    VisualizeFinalPath(finalPath);
                    _allPaths.Add(finalPath);
                    EndPathCreation(finalPath);
                    FindFirstObjectByType<NodeHighlighter>().enabled = false; // Disable highlighter

                }
                else
                {
                    Debug.Log("No path found to the destination flag.");
                    CancelPathCreation(); // No path possible
                }
            }
            else
            {
                Debug.Log("Cannot create a path to the starting flag.");
                return; // Exit if it IS the start flag
            }
        }
        else
        {
            Debug.Log("Clicked node does not have a flag, or is the starting flag.  Ignoring.");
            return; // Exit if not a valid end flag
        }
    }


    public void UpdatePathVisualization(GameObject currentHoverNode)
    {
        // Not used.
    }

    private void DrawPathSegment(Cell cell1, Cell cell2)
    {
        if (cell1 == null || cell2 == null) return;

        Vector3 pos1 = _nodePlacer.GetHexCellCenterNodes()[cell1].transform.position;
        Vector3 pos2 = _nodePlacer.GetHexCellCenterNodes()[cell2].transform.position;
        Vector3 direction = (pos2 - pos1).normalized;
        Quaternion rotation = Quaternion.LookRotation(direction);

        GameObject pathVisual = Instantiate(pathPrefab, (pos1 + pos2) / 2f, rotation);
        _currentPathVisuals.Add(pathVisual);
    }
    private void ClearPathVisuals()
    {
        foreach (GameObject visual in _currentPathVisuals)
        {
            Destroy(visual);
        }
        _currentPathVisuals.Clear();
    }

    public void EndPathCreation(List<Cell> finalPath = null)
    {
        Debug.Log("Ending Path Creation");
        _isPathingMode = false;

        if (finalPath != null)
        {
            foreach (Cell cell in finalPath)
            {
                if (!_gridManager.GetCellData(cell).hasFlag)
                {
                    _gridManager.SetCellPath(cell, true);
                }
            }
            Debug.Log("Path Added with " + finalPath.Count + " points.");
        }

        _startFlag = null;
        _startCell = null;
        ClearPathVisuals();

    }

    public void CancelPathCreation()
    {
        _isPathingMode = false;
        _startFlag = null;
        _startCell = null;
        ClearPathVisuals();
    }
    public List<List<Cell>> GetAllPaths()
    {
        return _allPaths;
    }
    public void VisualizeAllPaths()
    {
        ClearPathVisuals();

        foreach (List<Cell> path in _allPaths)
        {
            VisualizeFinalPath(path, false);
        }
    }

    private void VisualizeFinalPath(List<Cell> path, bool clearExisting = true)
    {
        if (clearExisting)
        {
            ClearPathVisuals();
        }


        for (int i = 0; i < path.Count - 1; i++)
        {
            DrawPathSegment(path[i], path[i + 1]);
        }
        foreach (Cell cell in path)
        {
            if (_nodePlacer.GetHexCellCenterNodes().TryGetValue(cell, out GameObject node))
            {
                _nodePlacer.SetNodeVisibility(node, true, Color.red);
            }
        }
    }
    public List<Cell> GetAllPathCells()
    {
        List<Cell> allPathCells = new List<Cell>();
        foreach (List<Cell> path in _allPaths)
        {
            foreach (Cell cell in path)
            {
                allPathCells.Add(cell);
            }
        }
        return allPathCells;
    }
}