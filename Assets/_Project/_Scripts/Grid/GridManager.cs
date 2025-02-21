// --- GridManager.cs ---
using UnityEngine;
using System.Collections.Generic;
using TGS;
using System.IO;
using static CellTypes;

public class GridManager : MonoBehaviour
{
    [SerializeField] private string saveFileName = "save.json";

    private NodeManager nodeManager;
    private TerrainGridSystem tgs;
    private PathManager pathManager;

    private Dictionary<Cell, CellData> cellData = new Dictionary<Cell, CellData>();

    [SerializeField] private float waterHeight = 0.2f;
    [SerializeField] private float marshHeight = 0.4f;
    [SerializeField] private float grassHeight = 0.6f;
    [SerializeField] private float desertHeight = 0.8f;
    [SerializeField] private float mountainHeight = 1.0f;

    public TerrainType GetTerrainType(Cell cell) => GetCellData(cell)?.TerrainType ?? TerrainType.None;

    public List<Cell> GetAllCells() => tgs != null ? tgs.cells : null ?? new List<Cell>();

    public void Initialise(NodeManager nodeManager, TerrainGridSystem tgs, PathManager pathManager)
    {
        this.nodeManager = nodeManager;
        this.tgs = tgs;
        this.pathManager = pathManager;
        
        if (!AreDependenciesValid())
        {
            Debug.LogError("GridManager: Missing dependencies. Initialization aborted.");
            return;
        }

        InitializeGrid();
        LoadGame();
    }

    private bool AreDependenciesValid() => nodeManager != null && tgs != null && pathManager != null;

    public void InitializeGrid()
    {
        tgs?.Init();
        nodeManager?.InitializeNodes(tgs);
        SetTerrainData();
    }

    public void SetTerrainData()
    {
        cellData.Clear();
        ClearFlags();

        if (tgs == null || tgs.cells == null)
        {
            return;
        }

        foreach (Cell cell in tgs.cells)
        {
            float height = GetCellHeight(cell);
            TerrainType terrain = DetermineTerrainType(height);
            SetCellTerrainType(cell, terrain);
        }
    }
    private float GetCellHeight(Cell cell) => tgs.CellGetCentroid(cell.index).y - nodeManager.NodeHeightOffset;

    private TerrainType DetermineTerrainType(float height)
    {
        if (height < waterHeight) return TerrainType.Water;
        if (height < marshHeight) return TerrainType.Marsh;
        if (height < grassHeight) return TerrainType.Grass;
        if (height < desertHeight) return TerrainType.Desert;
        if (height < mountainHeight) return TerrainType.Mountain;
        return TerrainType.MountainTop;
    }

    public CellData GetCellData(Cell cell)
    {
        return cellData.TryGetValue(cell, out CellData data) ? data : null;
    }

    public void SetCellData(Cell cell, CellData data)
    {
        if (cell == null || data == null)
        {
            Debug.LogWarning("GridManager: Attempted to set null cell or data.");
            return;
        }
        cellData[cell] = data; // Overwrites if exists, adds if not
    }

    private void SetCellTerrainType(Cell cell, TerrainType type)
    {
        CellData data = EnsureCellData(cell);
        data.TerrainType = type;
    }

    public void SetCellBuildingType(Cell cell, BuildingType type, int buildingId = -1)
    {
        CellData data = EnsureCellData(cell);
        data.BuildingType = type;
        data.BuildingID = buildingId;
    }

    public void SetCellObstacle(Cell cell, bool hasObstacle)
    {
        CellData data = EnsureCellData(cell);
        data.HasObstacle = hasObstacle;
    }

    public void SetCellResourceType(Cell cell, ResourceType type, int amount = 0)
    {
        CellData data = EnsureCellData(cell);
        data.ResourceType = type;
        data.ResourceAmount = amount;
    }

    public void SetCellFlag(Cell cell, bool hasFlag)
    {
        CellData data = EnsureCellData(cell);
        data.HasFlag = hasFlag;
    }

    public void SetCellPath(Cell cell, bool hasPath)
    {
        CellData data = EnsureCellData(cell);
        data.HasPath = hasPath;
    }

    private CellData EnsureCellData(Cell cell)
    {
        CellData data = GetCellData(cell);
        if (data == null)
        {
            data = new CellData();
            SetCellData(cell, data);
        }
        return data;
    }

    public void SaveGame()
    {
        SaveData saveData = CreateSaveData();
        string json = JsonUtility.ToJson(saveData);
        string savePath = GetSavePath();
        File.WriteAllText(savePath, json);
        Debug.Log("Game Saved");
    }

    public void DeleteSavedData()
    {
        string savePath = GetSavePath();
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
        SetTerrainData();
        //ResetNodeVisibility();

        string savePath = GetSavePath();
        if (!File.Exists(savePath))
        {
            Debug.Log("No save file found. Initializing new grid.");
            return;
        }

        LoadFromSaveFile(savePath);
        RestoreFlags();
        RestorePaths();
        pathManager.ClearAllPaths();
        pathManager.VisualizeAllPaths();
        Debug.Log("Game Loaded");
    }

    private SaveData CreateSaveData()
    {
        SaveData saveData = new SaveData();
        foreach (var kvp in cellData)
        {
            int cellIndex = tgs.CellGetIndex(kvp.Key);
            saveData.cellIndices.Add(cellIndex);
            saveData.cellData.Add(kvp.Value);
        }
        return saveData;
    }

    private string GetSavePath() => Path.Combine(Application.persistentDataPath, saveFileName);

    private void LoadFromSaveFile(string savePath)
    {
        string json = File.ReadAllText(savePath);
        SaveData saveData = JsonUtility.FromJson<SaveData>(json);

        for (int i = 0; i < saveData.cellIndices.Count; i++)
        {
            int cellIndex = saveData.cellIndices[i];
            if (IsValidCellIndex(cellIndex))
            {
                cellData[tgs.cells[cellIndex]] = saveData.cellData[i];
            }
            else
            {
                Debug.LogWarning($"Loaded cell index out of bounds: {cellIndex}");
            }
        }
    }

    //private void ResetNodeVisibility()
    //{
    //    foreach (var kvp in nodeManager.GetCellNodeMap())
    //    {
    //        nodeManager.SetNodeVisibility(kvp.Value, false, nodeManager.DefaultColor);
    //    }
    //}

    private bool IsValidCellIndex(int index) => index >= 0 && index < tgs.cells.Count;

    private void RestoreFlags()
    {
        GameObject flagPrefab = FindFirstObjectByType<GridNodeManager>()?.GetFlagPrefab;
        if (flagPrefab == null)
        {
            Debug.LogError("GridManager: Could not find GridNodeManager or flag prefab for flag restoration.");
            return;
        }

        foreach (var kvp in cellData)
        {
            if (kvp.Value.HasFlag)
            {
                RestoreFlag(kvp.Key, flagPrefab);
            }
        }
    }

    private void RestoreFlag(Cell cell, GameObject flagPrefab)
    {
        if (nodeManager.GetCellNodeMap().TryGetValue(cell, out GameObject node))
        {
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

    private void RestorePaths()
    {
        // Rebuild paths from cell data
        foreach (var kvp in cellData)
        {
            if (kvp.Value.HasPath)
            {
                SetCellPath(kvp.Key, true); // Correctly set the path
            }
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

    public void RemovePath(Cell cell)
    {
        CellData data = GetCellData(cell);
        if (data?.HasPath != true) return;

        SetCellPath(cell, false);
        RemovePathFromPathManager(cell);
        pathManager.VisualizeAllPaths();
    }

    private void RemovePathFromPathManager(Cell cell)
    {
        PathManager pm = FindFirstObjectByType<PathManager>();
        if (pm == null)
        {
            Debug.LogError("GridManager: Could not find PathManager to remove path!");
            return;
        }
        pm.RemovePathsContainingCell(cell);
    }

    private void FindPathRecursive(Cell currentCell, List<Cell> currentPath, HashSet<Cell> processedCells)
    {
        currentPath.Add(currentCell);
        processedCells.Add(currentCell);

        List<Cell> neighbors = tgs.CellGetNeighbours(currentCell);
        foreach (Cell neighbor in neighbors)
        {
            if (neighbor != null && CanProcessNeighbor(neighbor, processedCells))
            {
                FindPathRecursive(neighbor, currentPath, processedCells);
            }
        }
    }

    private bool CanProcessNeighbor(Cell neighbor, HashSet<Cell> processedCells)
    {
        CellData neighborData = GetCellData(neighbor);
        return neighborData.HasPath && !processedCells.Contains(neighbor);
    }

    [System.Serializable]
    private class SaveData
    {
        public List<int> cellIndices = new List<int>();
        public List<CellData> cellData = new List<CellData>();
    }
}