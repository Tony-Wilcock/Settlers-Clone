// --- NodeSelection.cs ---
using UnityEngine;
using TGS;
using System.Collections.Generic;

public enum IconIndex
{
    None = 0,
    Flag = 1,
    SmallBuilding = 2,
    MediumBuilding = 3,
    LargeBuilding = 4,
    Resource = 5
}

public class NodeSelection : MonoBehaviour
{
    #region Fields

    [Tooltip("The color to use when highlighting a node.")]
    [SerializeField] private Color highlightColor = Color.yellow;
    [Tooltip("Prefabs for node icons, corresponding to IconIndex enum.")]
    [SerializeField] private GameObject[] nodeIconPrefabs = new GameObject[6]; // Sized to match enum

    private GameObject[] instantiatedNodeIcons; // Instances of the icons in the scene
    private NodeManager nodeManager;
    private GridManager gridManager;
    private TerrainGridSystem tgs;
    private PathManager pathManager;

    private GameObject nearestNode;
    private GameObject previousNearestNode;
    private Vector3 lastMousePosition = Vector3.negativeInfinity;
    private int currentIconIndex = (int)IconIndex.Flag;

    #endregion

    #region Properties

    public GameObject NearestNode => nearestNode;

    #endregion

    #region Initialization

    public void Initialize(NodeManager nodeManager, GridManager gridManager, TerrainGridSystem tgs, PathManager pathManager)
    {
        this.nodeManager = nodeManager ?? throw new System.ArgumentNullException(nameof(nodeManager));
        this.gridManager = gridManager ?? throw new System.ArgumentNullException(nameof(gridManager));
        this.tgs = tgs ?? throw new System.ArgumentNullException(nameof(tgs));
        this.pathManager = pathManager ?? throw new System.ArgumentNullException(nameof(pathManager));

        ValidateAndInstantiateIcons();
    }

    private void ValidateAndInstantiateIcons()
    {
        if (nodeIconPrefabs.Length != System.Enum.GetNames(typeof(IconIndex)).Length)
        {
            Debug.LogError("NodeSelection: nodeIconPrefabs length does not match IconIndex enum!");
            return;
        }

        instantiatedNodeIcons = new GameObject[nodeIconPrefabs.Length];
        for (int i = 0; i < nodeIconPrefabs.Length; i++)
        {
            if (nodeIconPrefabs[i] != null)
            {
                instantiatedNodeIcons[i] = Instantiate(nodeIconPrefabs[i]);
                instantiatedNodeIcons[i].SetActive(false);
            }
            else
            {
                Debug.LogError($"NodeSelection: nodeIconPrefabs[{i}] is null!");
            }
        }
    }

    #endregion

    #region Node Highlighting

    public void HighlightNode()
    {
        if (!HasMouseMoved()) return;

        UpdateNearestNode();
        if (nearestNode != previousNearestNode)
        {
            HighlightCurrentNode();
            previousNearestNode = nearestNode;
        }
    }

    private bool HasMouseMoved()
    {
        if (Input.mousePosition == lastMousePosition) return false;
        lastMousePosition = Input.mousePosition;
        return true;
    }

    private void UpdateNearestNode()
    {
        nearestNode = null;
        Cell currentCell = tgs?.CellGetAtMousePosition();
        if (currentCell != null)
        {
            nearestNode = nodeManager.GetCellNodeMap().GetValueOrDefault(currentCell);
        }
    }

    private void HighlightCurrentNode()
    {
        if (nearestNode == null) return;

        Cell currentCell = nodeManager.GetCellFromNode(nearestNode);
        if (currentCell != null)
        {
            int iconIndex = DetermineIconIndex(currentCell);
            SetActiveIcon(iconIndex);
        }
    }

    #endregion

    #region Icon Management

    private int DetermineIconIndex(Cell cell)
    {
        CellData cellData = gridManager.GetCellData(cell);
        if (cellData == null || cellData.HasFlag || pathManager.IsPathingMode)
        {
            return (int)IconIndex.None;
        }

        if (cellData.HasPath && nodeManager.CanPlaceFlag(nearestNode, tgs))
        {
            return (int)IconIndex.Flag;
        }

        return cellData.HasPath ? (int)IconIndex.None : (int)IconIndex.SmallBuilding;
    }

    public void SetActiveIcon(int iconIndex)
    {
        if (!IsValidIconIndex(iconIndex))
        {
            Debug.LogError($"NodeSelection: Invalid icon index: {iconIndex}");
            return;
        }

        DeactivateAllIcons();
        currentIconIndex = iconIndex;

        if (nearestNode != null && instantiatedNodeIcons[currentIconIndex] != null)
        {
            ActivateAndPositionIcon(currentIconIndex);
        }
    }

    private bool IsValidIconIndex(int index) => index >= 0 && index < instantiatedNodeIcons.Length;

    private void ActivateAndPositionIcon(int index)
    {
        GameObject icon = instantiatedNodeIcons[index];
        icon.SetActive(true);
        icon.transform.position = nearestNode.transform.position + Vector3.up * 0.5f;
    }

    private void DeactivateAllIcons()
    {
        foreach (GameObject icon in instantiatedNodeIcons)
        {
            icon?.SetActive(false);
        }
    }

    #endregion

    #region Node Color Utilities (Deprecated but Kept for Reference)

    [System.Obsolete("Use NodeManager.SetNodeVisibility instead for consistency.")]
    private void SetNodeColor(GameObject node, Color color)
    {
        if (node != null)
        {
            SpriteRenderer spriteRenderer = node.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.color = color;
            }
        }
    }

    #endregion
}