// --- GridManager.cs ---
using UnityEngine;
using System.Collections.Generic;
using TGS;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using static CellTypes;

public class GridManager : MonoBehaviour
{
    private TerrainGridSystem _tgs;
    private Dictionary<Cell, CellData> _cellData = new Dictionary<Cell, CellData>();
    public string saveFileName = "save.json";

    private NodePlacer _nodePlacer;

    public void OnEnable()
    {
        _tgs = TerrainGridSystem.instance;
        _nodePlacer = FindFirstObjectByType<NodePlacer>();

        InitializeGrid();
        LoadGame();
    }

    public void InitializeGrid()
    {
        if (_tgs != null)
        {
            _tgs.Init();
        }
        if (_nodePlacer != null)
        {
            _nodePlacer.InitializeNodes(_tgs);
        }
    }

    public void InitializeCells()
    {
        _cellData.Clear();
        ClearFlags();
        foreach (Cell cell in _tgs.cells)
        {
            TerrainType terrain;
            float height = cell.center.y;
            if (height < 0.2f)
            {
                terrain = TerrainType.Water;
            }
            else if (height < 0.4f)
            {
                terrain = TerrainType.Marsh;
            }
            else if (height < 0.6f)
            {
                terrain = TerrainType.Grass;
            }
            else if (height < 0.8f)
            {
                terrain = TerrainType.Desert;
            }
            else
            {
                terrain = TerrainType.Mountain;
            }

            _cellData.Add(cell, new CellData(terrain));
            UpdateCellWalkability(cell);
        }
    }
    public void SetCellData(Cell cell, CellData data)
    {
        if (_cellData.ContainsKey(cell))
        {
            _cellData[cell] = data;
        }
        else
        {
            _cellData.Add(cell, data);
        }
        UpdateCellWalkability(cell);
    }
    public CellData GetCellData(Cell cell)
    {
        if (_cellData.TryGetValue(cell, out CellData data))
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
    private void UpdateCellWalkability(Cell cell)
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
            if (data.hasFlag) canWalk = false;
        }


        int cellIndex = _tgs.CellGetIndex(cell);
        _tgs.CellSetTag(cellIndex, canWalk ? 0 : 1);
    }


    public void SaveGame()
    {
        SaveData saveData = new SaveData();
        foreach (var kvp in _cellData)
        {
            int cellIndex = _tgs.CellGetIndex(kvp.Key);
            saveData.cellIndices.Add(cellIndex);
            saveData.cellData.Add(kvp.Value);
        }

        string json = JsonUtility.ToJson(saveData);

        using (StreamWriter writer = new StreamWriter(Application.persistentDataPath + "/" + saveFileName))
        {
            writer.Write(json);
        }
        Debug.Log("Game Saved");
    }

    public void LoadGame()
    {
        string savePath = Application.persistentDataPath + "/" + saveFileName;

        if (File.Exists(savePath))
        {
            ClearFlags();

            string json;
            using (StreamReader reader = new StreamReader(savePath))
            {
                json = reader.ReadToEnd();
            }

            SaveData saveData = JsonUtility.FromJson<SaveData>(json);

            _cellData.Clear();

            for (int i = 0; i < saveData.cellIndices.Count; i++)
            {
                int cellIndex = saveData.cellIndices[i];
                if (cellIndex >= 0 && cellIndex < _tgs.cells.Count)
                {
                    _cellData[_tgs.cells[cellIndex]] = saveData.cellData[i];
                    UpdateCellWalkability(_tgs.cells[cellIndex]);
                }
                else
                {
                    Debug.LogWarning($"Loaded cell index out of bounds: {cellIndex}");
                }
            }

            RestoreFlags();
            FindFirstObjectByType<PathManager>().VisualizeAllPaths();
            Debug.Log("Game Loaded");
        }
        else
        {
            Debug.Log("No save file found. Initializing new grid.");
            InitializeCells();
        }
    }
    private void RestoreFlags()
    {
        GameObject flagPrefab = FindFirstObjectByType<GridNodeManager>().GetFlagPrefab();

        foreach (var kvp in _cellData)
        {
            Cell cell = kvp.Key;
            CellData data = kvp.Value;

            if (data.hasFlag)
            {
                if (_nodePlacer.GetHexCellCenterNodes().TryGetValue(cell, out GameObject node))
                {
                    _nodePlacer.SetNodeVisibility(node, true);
                    GameObject newFlag = Instantiate(flagPrefab, node.transform.position, Quaternion.identity, node.transform);
                    if (newFlag.GetComponent<Flag>() == null)
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
        return _tgs.cells;
    }

    [System.Serializable]
    private class SaveData
    {
        public List<int> cellIndices = new List<int>();
        public List<CellData> cellData = new List<CellData>();
    }
}