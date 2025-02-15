// --- NodePlacer.cs ---
using System.Collections.Generic;
using TGS;
using UnityEngine;
using static CellTypes;

public class NodePlacer : MonoBehaviour
{
    [SerializeField] private GameObject nodePrefab;
    [Tooltip("The height offset to place nodes above the terrain.")]
    [SerializeField] private float nodeHeightOffset = 0.1f;
    [Tooltip("The maximum distance to raycast to find the terrain.")]
    [SerializeField] private float raycastDistance = 1000f;
    [Tooltip("The LayerMask to use for the terrain.  Make sure your terrain is on this layer.")]
    [SerializeField] public LayerMask terrainLayerMask;

    [HideInInspector] public Color defaultColor;

    private readonly Dictionary<Cell, GameObject> _cellToNodeMap = new();
    private List<Vector3> _centerNodePositions = new List<Vector3>();
    private List<GameObject> _centerNodes = new List<GameObject>();
    private SpriteRenderer _spriteRenderer;
    private GridManager _gridManager; // Add reference


    private void Awake()
    {
        if (nodePrefab == null)
        {
            Debug.LogError("NodePlacer: nodePrefab is not assigned in the Inspector!");
            enabled = false;
            return;
        }

        _spriteRenderer = nodePrefab.GetComponent<SpriteRenderer>();
        if (_spriteRenderer != null)
        {
            defaultColor = _spriteRenderer.color;
        }
        else
        {
            Debug.LogError("Sprite Renderer does not exists on the node prefab");
        }
        _gridManager = FindFirstObjectByType<GridManager>(); // Get GridManager reference
        if (_gridManager == null)
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

        _cellToNodeMap.Clear();
        _centerNodePositions.Clear();
        _centerNodes.Clear();

        for (int k = 0; k < tgs.cells.Count; k++)
        {
            Vector3 worldSpaceCenter = tgs.CellGetCentroid(k);

            Vector3 raycastStart = new Vector3(worldSpaceCenter.x, 1000f, worldSpaceCenter.z);
            RaycastHit hit;

            if (Physics.Raycast(raycastStart, Vector3.down, out hit, raycastDistance, terrainLayerMask))
            {
                GameObject centerNode = Instantiate(nodePrefab);
                centerNode.name = "Center Node: " + tgs.cells[k].coordinates;
                centerNode.transform.position = hit.point + Vector3.up * nodeHeightOffset;
                _cellToNodeMap[tgs.cells[k]] = centerNode;
                _centerNodePositions.Add(centerNode.transform.position);
                _centerNodes.Add(centerNode);
                //SetNodeVisibility(centerNode, false); //Removed for now
            }
            else
            {
                Debug.LogWarning($"Could not find terrain for cell {tgs.cells[k].coordinates}. Placing node at grid level.");
                GameObject centerNode = Instantiate(nodePrefab);
                centerNode.name = "Center Node: " + tgs.cells[k].coordinates;
                centerNode.transform.position = worldSpaceCenter + Vector3.up * nodeHeightOffset;

                _cellToNodeMap[tgs.cells[k]] = centerNode;
                _centerNodePositions.Add(centerNode.transform.position);
                _centerNodes.Add(centerNode);
                //SetNodeVisibility(centerNode, false); //Removed for now
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
                Debug.Log($"SetNodeVisibility: Node={node.name}, Visible={visible}, Current Enabled={renderer.enabled}"); // Add this
                renderer.enabled = visible;
                if (color.HasValue)
                {
                    SpriteRenderer spriteRenderer = node.GetComponent<SpriteRenderer>();
                    if (spriteRenderer != null)
                    {
                        Debug.Log("Setting Node Color" + color.Value);
                        spriteRenderer.color = color.Value;
                    }
                    else
                    {
                        renderer.material.color = color.Value;
                    }
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

        CellData data = _gridManager.GetCellData(clickedCell);

        if (data.hasFlag || data.buildingType != BuildingType.None || data.hasObstacle)
        {
            return false;
        }

        List<Cell> neighbors = tgs.CellGetNeighbours(clickedCell);
        foreach (Cell neighbor in neighbors)
        {
            CellData neighborData = _gridManager.GetCellData(neighbor);
            //Now checks for paths in neighbor cells
            if (neighborData.hasFlag || neighborData.hasPath)
            {
                return false;
            }
        }

        return true;
    }

    public Cell GetCellFromNode(GameObject node)
    {
        foreach (var kvp in _cellToNodeMap)
        {
            if (kvp.Value == node)
            {
                return kvp.Key;
            }
        }
        return null;
    }

    public List<Vector3> GetCenterNodePositions() => _centerNodePositions;
    public List<GameObject> GetCenterNodes() => _centerNodes;
    public Dictionary<Cell, GameObject> GetHexCellCenterNodes() => _cellToNodeMap;
}