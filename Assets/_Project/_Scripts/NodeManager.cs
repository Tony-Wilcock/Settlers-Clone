// --- NodeManager.cs ---
using System.Collections.Generic;
using TGS;
using UnityEngine;
using static CellTypes;

public class NodeManager : MonoBehaviour
{
    [SerializeField] private GameObject nodePrefab;
    [Tooltip("The height offset to place nodes above the terrain.")]
    [SerializeField] private float nodeHeightOffset = 0.1f;
    [Tooltip("The maximum distance to raycast to find the terrain.")]
    [SerializeField] private float raycastDistance = 1000f;
    [Tooltip("The LayerMask to use for the terrain.  Make sure your terrain is on this layer.")]
    [SerializeField] public LayerMask terrainLayerMask;

    [HideInInspector] public Color defaultColor;

    private readonly Dictionary<Cell, GameObject> cellToNodeMap = new();
    private SpriteRenderer spriteRenderer;
    private GridManager gridManager; // Add reference


    private void Awake()
    {
        if (nodePrefab == null)
        {
            Debug.LogError("NodePlacer: nodePrefab is not assigned in the Inspector!");
            enabled = false;
            return;
        }

        gridManager = FindFirstObjectByType<GridManager>(); // Get GridManager reference

        if (gridManager == null)
        {
            Debug.LogError("NodePlacer could not find GridManager!");
        }
    }

    public void InitializeNodes(TerrainGridSystem tgs)
    {
        if (tgs == null)
        {
            Debug.LogError("NodePlacer.InitializeNodes: tgs is null!");
            return;
        }

        spriteRenderer = nodePrefab.GetComponent<SpriteRenderer>();

        if (spriteRenderer != null)
        {
            defaultColor = spriteRenderer.color;
        }
        else
        {
            Debug.LogError("Sprite Renderer does not exists on the node prefab");
        }

        cellToNodeMap.Clear();

        for (int k = 0; k < tgs.cells.Count; k++)
        {
            Vector3 worldSpaceCenter = tgs.CellGetCentroid(k);

            Vector3 raycastStart = new Vector3(worldSpaceCenter.x, 1000f, worldSpaceCenter.z);
            RaycastHit hit;

            if (Physics.Raycast(raycastStart, Vector3.down, out hit, raycastDistance, terrainLayerMask))
            {
                GameObject centerNode = Instantiate(nodePrefab);
                centerNode.name = "Center Node: " + tgs.cells[k].index;
                centerNode.transform.position = hit.point + Vector3.up * nodeHeightOffset;
                cellToNodeMap[tgs.cells[k]] = centerNode;
                SetNodeVisibility(centerNode, false, defaultColor); // Set visibility and default color
            }
            else
            {
                Debug.LogWarning($"Could not find terrain for cell {tgs.cells[k].coordinates}. Placing node at grid level.");
                GameObject centerNode = Instantiate(nodePrefab);
                centerNode.name = "Center Node: " + tgs.cells[k].index;
                centerNode.transform.position = worldSpaceCenter + Vector3.up * nodeHeightOffset;

                cellToNodeMap[tgs.cells[k]] = centerNode;
                SetNodeVisibility(centerNode, false, defaultColor); // Set visibility and default color
            }
        }
    }

    public void SetNodeVisibility(GameObject node, bool visible, Color? color = null)
    {
        if (node != null)
        {
            Renderer renderer = node.GetComponent<Renderer>();
            if (renderer != null)
            {
                SpriteRenderer spriteRenderer = node.GetComponent<SpriteRenderer>();
                if (spriteRenderer != null) // Check for SpriteRenderer *before* accessing .color
                {
                    renderer.enabled = visible;
                    if (color.HasValue)
                    {
                        spriteRenderer.color = color.Value;
                    }
                }
                else // CRITICAL: Log if we *expect* a SpriteRenderer but don't find one.
                {
                    Debug.LogError($"SetNodeVisibility: Node {node.name} has no SpriteRenderer (but a color was provided)!");
                }
            }
            else
            {
                Debug.LogError($"SetNodeVisibility: Node {node.name} has no Renderer component!");
            }
        }
        else
        {
            Debug.LogError("SetNodeVisibility: node is NULL!");
        }
    }

    public bool CanPlaceFlag(GameObject node, TerrainGridSystem tgs)
    {
        Cell clickedCell = GetCellFromNode(node);
        if (clickedCell == null) return false;

        CellData data = gridManager.GetCellData(clickedCell);

        if (data.hasFlag || data.buildingType != BuildingType.None || data.hasObstacle)
        {
            return false;
        }

        List<Cell> neighbors = tgs.CellGetNeighbours(clickedCell);
        foreach (Cell neighbor in neighbors)
        {
            CellData neighborData = gridManager.GetCellData(neighbor);
            //Now checks for paths in neighbor cells
            if (neighborData.hasFlag)
            {
                return false;
            }
        }

        return true;
    }

    public Cell GetCellFromNode(GameObject node)
    {
        foreach (var kvp in cellToNodeMap)
        {
            if (kvp.Value == node)
            {
                return kvp.Key;
            }
        }
        return null;
    }

    public Dictionary<Cell, GameObject> GetCellNodeMap() => cellToNodeMap;
}