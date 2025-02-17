// --- GridManager.cs ---
using UnityEngine;
using System.Collections.Generic;
using TGS;
using System.IO;
using static CellTypes;

public class GridManager : MonoBehaviour
{
    private TerrainGridSystem tgs;
    private Dictionary<Cell, CellData> cellData = new Dictionary<Cell, CellData>();
    [SerializeField] private string saveFileName = "save.json";

    [SerializeField] private NodeManager nodeManager;
    private PathManager pathManager;

    private const float WaterHeight = 0.2f;
    private const float MarshHeight = 0.4f;
    private const float GrassHeight = 0.6f;
    private const float DesertHeight = 0.8f;

    public void OnEnable()
    {
        tgs = TerrainGridSystem.instance;
        nodeManager = FindFirstObjectByType<NodeManager>();
        pathManager = FindFirstObjectByType<PathManager>();

        InitializeGrid();
        LoadGame();
    }

    public void InitializeGrid()
    {
        if (tgs != null)
        {
            tgs.Init();
        }
        if (nodeManager != null)
        {
            nodeManager.InitializeNodes(tgs);
        }
        InitializeCells();
    }

    public void InitializeCells()
    {
        cellData.Clear();
        ClearFlags();

        if (tgs == null || tgs.cells == null)
        {
            return;
        }

        foreach (Cell cell in tgs.cells)
        {
            TerrainType terrain;
            float height = cell.center.y;

            if (height < WaterHeight)
            {
                terrain = TerrainType.Water;
            }
            else if (height < MarshHeight)
            {
                terrain = TerrainType.Marsh;
            }
            else if (height < GrassHeight)
            {
                terrain = TerrainType.Grass;
            }
            else if (height < DesertHeight)
            {
                terrain = TerrainType.Desert;
            }
            else
            {
                terrain = TerrainType.Mountain;
            }

            cellData.Add(cell, new CellData(terrain));
            UpdateCellPathability(cell);
        }
    }

    public void SetCellData(Cell cell, CellData data)
    {
        if (cellData.ContainsKey(cell))
        {
            cellData[cell] = data;
        }
        else
        {
            cellData.Add(cell, data);
        }
        UpdateCellPathability(cell);
    }

    public CellData GetCellData(Cell cell)
    {
        if (cellData.TryGetValue(cell, out CellData data))
        {
            return data;
        }
        return new CellData(TerrainType.Grass);
    }

    public void SetCellTerrainType(Cell cell, TerrainType type)
    {
        CellData data = GetCellData(cell);
        data.terrainType = type;
        SetCellData(cell, data);
    }

    public void SetCellBuildingType(Cell cell, BuildingType type, int buildingId = -1)
    {
        CellData data = GetCellData(cell);
        data.buildingType = type;
        data.buildingID = buildingId;
        SetCellData(cell, data);
    }

    public void SetCellObstacle(Cell cell, bool hasObstacle)
    {
        CellData data = GetCellData(cell);
        data.hasObstacle = hasObstacle;
        SetCellData(cell, data);
    }

    public void SetCellResourceType(Cell cell, ResourceType type, int amount = 0)
    {
        CellData data = GetCellData(cell);
        data.resourceType = type;
        data.resourceAmount = amount;
        SetCellData(cell, data);
    }

    public void SetCellFlag(Cell cell, bool hasFlag)
    {
        CellData data = GetCellData(cell);
        data.hasFlag = hasFlag;
        SetCellData(cell, data);
    }

    public void SetCellPath(Cell cell, bool hasPath)
    {
        CellData data = GetCellData(cell);
        data.hasPath = hasPath;
        SetCellData(cell, data);
    }

    private void UpdateCellPathability(Cell cell)
    {
        CellData data = GetCellData(cell);

        bool canWalk = true;
        switch (data.terrainType)
        {
            case TerrainType.Water:
            case TerrainType.Mountain:
                canWalk = false;
                break;
        }
        if (!data.hasPath)
        {
            if (data.buildingType != BuildingType.None) canWalk = false;
            if (data.hasObstacle) canWalk = false;
        }

        int cellIndex = tgs.CellGetIndex(cell);
        tgs.CellSetTag(cellIndex, canWalk ? 0 : 1);
    }

    public void SaveGame()
    {
        SaveData saveData = new SaveData();
        foreach (var kvp in cellData)
        {
            int cellIndex = tgs.CellGetIndex(kvp.Key);
            saveData.cellIndices.Add(cellIndex);
            saveData.cellData.Add(kvp.Value);
        }

        string json = JsonUtility.ToJson(saveData);
        File.WriteAllText(Application.persistentDataPath + "/" + saveFileName, json);
        Debug.Log("Game Saved");
    }

    public void DeleteSavedData()
    {
        string savePath = Application.persistentDataPath + "/" + saveFileName;
        if (File.Exists(savePath))
        {
            File.Delete(savePath);
            Debug.Log("Save file deleted.");
        }
        else
        {
            Debug.Log("No save file found.");
        }
    }

    public void LoadGame()
    {
        InitializeCells(); // Clears existing cell data and flag GameObjects

        // IMPORTANT: Reset node visibility *before* restoring anything.
        foreach (var kvp in nodeManager.GetCellNodeMap())
        {
            nodeManager.SetNodeVisibility(kvp.Value, false, nodeManager.defaultColor);
        }

        string savePath = Application.persistentDataPath + "/" + saveFileName;

        if (File.Exists(savePath))
        {
            string json = File.ReadAllText(savePath);

            SaveData saveData = JsonUtility.FromJson<SaveData>(json);

            for (int i = 0; i < saveData.cellIndices.Count; i++)
            {
                int cellIndex = saveData.cellIndices[i];
                if (cellIndex >= 0 && cellIndex < tgs.cells.Count)
                {
                    cellData[tgs.cells[cellIndex]] = saveData.cellData[i];
                    UpdateCellPathability(tgs.cells[cellIndex]);
                }
                else
                {
                    Debug.LogWarning($"Loaded cell index out of bounds: {cellIndex}");
                }
            }

            RestoreFlags();
            RestorePaths(); // Now correctly restores path data
            Debug.Log("Game Loaded");
        }
        else
        {
            Debug.Log("No save file found. Initializing new grid.");
        }
        pathManager.ClearAllPaths();  //Clear all paths

        // Visualize paths *after* everything is loaded.
        pathManager.VisualizeAllPaths();
    }

    private void RestoreFlags()
    {
        GameObject flagPrefab = FindFirstObjectByType<GridNodeManager>().GetFlagPrefab;

        foreach (var kvp in cellData)
        {
            Cell cell = kvp.Key;
            CellData data = kvp.Value;

            if (data.hasFlag)
            {
                if (nodeManager.GetCellNodeMap().TryGetValue(cell, out GameObject node))
                {
                    // Don't just set visibility, *instantiate* the flag:
                    GameObject newFlag = Instantiate(flagPrefab, node.transform.position, Quaternion.identity, node.transform);
                    if (!newFlag.TryGetComponent<Flag>(out _))
                    {
                        newFlag.AddComponent<Flag>();
                    }
                }
                else
                {
                    Debug.LogError($"Could not find node for cell {cell} during flag restoration.");
                }
            }
        }
    }

    private void RestorePaths()
    {
        // Rebuild paths from cell data
        foreach (var kvp in cellData)
        {
            if (kvp.Value.hasPath)
            {
                SetCellPath(kvp.Key, true); // Correctly set the path
            }
        }
    }

    private void FindPathRecursive(Cell currentCell, List<Cell> currentPath, HashSet<Cell> processedCells)
    {
        currentPath.Add(currentCell);
        processedCells.Add(currentCell);
        // Check neighbours
        List<Cell> neighbors = tgs.CellGetNeighbours(currentCell);
        foreach (Cell neighbor in neighbors)
        {
            if (neighbor != null)
            {
                CellData neighborData = GetCellData(neighbor);
                // If neighbor is on path and not processed, add it.
                if (neighborData.hasPath && !processedCells.Contains(neighbor))
                {
                    FindPathRecursive(neighbor, currentPath, processedCells);
                }
            }
        }
    }

    public void RemovePath(Cell cell)
    {
        if (GetCellData(cell).hasPath)
        {
            SetCellPath(cell, false); // Remove the path flag

            // Find and remove the path from PathManager's allPaths
            List<List<Cell>> pathsToRemove = new List<List<Cell>>();
            foreach (List<Cell> path in FindFirstObjectByType<PathManager>().GetAllPaths())
            {
                if (path.Contains(cell))
                {
                    pathsToRemove.Add(path);
                }
            }

            foreach (List<Cell> pathToRemove in pathsToRemove)
            {
                FindFirstObjectByType<PathManager>().GetAllPaths().Remove(pathToRemove);
            }

            // Visualize after changes.
            FindFirstObjectByType<PathManager>().VisualizeAllPaths();
        }
    }

    private void ClearFlags()
    {
        Flag[] flags = FindObjectsByType<Flag>(FindObjectsSortMode.None);
        foreach (Flag flag in flags)
        {
            Destroy(flag.gameObject);
        }
    }

    public List<Cell> GetAllCells()
    {
        return tgs.cells;
    }

    [System.Serializable]
    private class SaveData
    {
        public List<int> cellIndices = new List<int>();
        public List<CellData> cellData = new List<CellData>();
    }
}