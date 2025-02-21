// --- NodeManager.cs ---
using System.Collections.Generic;
using TGS;
using UnityEngine;
using static CellTypes;

public class NodeManager : MonoBehaviour
{
    [SerializeField] private GameObject nodePrefab;
    [Tooltip("The height offset to place nodes above the terrain.")]
    [field: SerializeField] public float NodeHeightOffset { get; private set; } = 0.1f;
    [Tooltip("The LayerMask to use for the terrain.  Make sure your terrain is on this layer.")]
    [SerializeField] public LayerMask terrainLayerMask;

    private GridManager gridManager; // Add reference
    private Camera mainCamera;
    private readonly Dictionary<Cell, GameObject> cellToNodeMap = new();
    private Dictionary<GameObject, Cell> nodeToCellMap = new(); // Added for faster reverse lookups

    public Color DefaultColor { get; set; }

    public void Initialise(GridManager gridManager)
    {
        //if (nodePrefab == null)
        //{
        //    Debug.LogError("NodePlacer: nodePrefab is not assigned in the Inspector!");
        //    enabled = false;
        //    return;
        //}

        this.gridManager = gridManager ?? throw new System.ArgumentNullException(nameof(gridManager), "GridManager cannot be null!");

        mainCamera = Camera.main;

        if (mainCamera == null)
        {
            Debug.LogError("NodeManager: Could not find main camera!");
            enabled = false; // Disable the script if no camera is found.
            return;
        }
    }

    public void InitializeNodes(TerrainGridSystem tgs)
    {
        if (tgs == null)
        {
            Debug.LogError("NodePlacer.InitializeNodes: tgs is null!");
            return;
        }

        //SpriteRenderer spriteRenderer = nodePrefab.GetComponent<SpriteRenderer>();

        //if (spriteRenderer != null)
        //{
        //    DefaultColor = spriteRenderer.color;
        //}
        //else
        //{
        //    Debug.LogError("Sprite Renderer does not exists on the node prefab");
        //    return;
        //}

        cellToNodeMap.Clear();
        nodeToCellMap.Clear();

        foreach (Cell cell in tgs.cells)
        {
            Vector3 worldSpaceCenter = tgs.CellGetCentroid(cell.index);
            GameObject centerNode = Instantiate(nodePrefab, worldSpaceCenter + Vector3.up * NodeHeightOffset, Quaternion.identity);
            centerNode.name = $"Center Node: {cell.index}";
            cellToNodeMap[cell] = centerNode;
            nodeToCellMap[centerNode] = cell;
            //SetNodeVisibility(centerNode, false, DefaultColor);
        }
    }

    //public void SetNodeVisibility(GameObject node, bool visible, Color? color = null)
    //{
    //    if (node == null)
    //    {
    //        Debug.LogError("SetNodeVisibility: node is null!");
    //        return;
    //    }

    //    Renderer renderer = node.GetComponent<Renderer>();
    //    if (renderer == null)
    //    {
    //        Debug.LogError($"SetNodeVisibility: Node {node.name} has no Renderer component!");
    //        return;
    //    }

    //    renderer.enabled = visible;

    //    if (color.HasValue)
    //    {
    //        SpriteRenderer spriteRenderer = node.GetComponent<SpriteRenderer>();
    //        if (spriteRenderer != null)
    //        {
    //            spriteRenderer.color = color.Value;
    //        }
    //        else
    //        {
    //            Debug.LogError($"SetNodeVisibility: Node {node.name} has no SpriteRenderer to apply color!");
    //        }
    //    }
    //}

    public bool CanPlaceFlag(GameObject node, TerrainGridSystem tgs)
    {
        if (node == null || tgs == null || gridManager == null)
        {
            Debug.LogWarning("CanPlaceFlag: Missing required references (node, tgs, or gridManager)!");
            return false;
        }

        Cell cell = GetCellFromNode(node);
        if (cell == null)
        {
            return false;
        }

        CellData data = gridManager.GetCellData(cell);
        if (!IsCellEligibleForFlag(data))
        {
            return false;
        }

        List<Cell> neighbors = tgs.CellGetNeighbours(cell);
        return !HasFlagInNeighbors(neighbors);
    }

    private bool IsCellEligibleForFlag(CellData data)
    {
        if (data == null || data.HasFlag || data.BuildingType != BuildingType.None || data.HasObstacle)
        {
            return false;
        }

        return !IsInvalidTerrain(data.TerrainType);
    }

    private bool IsInvalidTerrain(TerrainType terrainType)
    {
        return terrainType == TerrainType.Water || terrainType == TerrainType.Marsh || terrainType == TerrainType.MountainTop;
    }

    private bool HasFlagInNeighbors(List<Cell> neighbors)
    {
        foreach (Cell neighbor in neighbors)
        {
            CellData neighborData = gridManager.GetCellData(neighbor);
            if (neighborData != null && neighborData.HasFlag)
            {
                return true;
            }
        }
        return false;
    }

    public Cell GetCellFromNode(GameObject node)
    {
        return nodeToCellMap.TryGetValue(node, out Cell cell) ? cell : null;
    }

    public Dictionary<Cell, GameObject> GetCellNodeMap() => cellToNodeMap;
}