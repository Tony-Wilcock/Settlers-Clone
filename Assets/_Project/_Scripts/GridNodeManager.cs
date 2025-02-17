// --- GridNodeManager.cs ---
using UnityEngine;
using TGS;
using UnityEngine.Events;
using TMPro;

public class GridNodeManager : MonoBehaviour
{
    [SerializeField] private Input_SO input;
    [SerializeField] private GameObject flagPrefab;

    [SerializeField] private GameObject flagPanel;
    [SerializeField] private GameObject pathPanel;
    [SerializeField] private TMP_Text debugText;

    private NodeManager nodeManager;
    private NodeSelection nodeSelection;
    private PathManager pathManager;
    private TerrainGridSystem tgs;
    private GridManager gridManager;

    private GameObject nearestNode = null;

    public event UnityAction<GameObject> OnStartPathCreation;

    private void Awake()
    {
        nodeManager = GetComponent<NodeManager>();
        nodeSelection = GetComponent<NodeSelection>();
        pathManager = GetComponent<PathManager>();
        tgs = TerrainGridSystem.instance;
        gridManager = GetComponent<GridManager>();

        if (!nodeManager || !nodeSelection || !pathManager || !input || !gridManager)
        {
            Debug.LogError("GridNodeManager: Missing required components or assignments.");
            enabled = false; // Disable the script if dependencies are missing.
            return;
        }

        nodeSelection.Initialize(nodeManager);
    }

    void Start()
    {
        input.OnInteractAction += HandleNodeInteraction;
    }

    private void Update()
    {
        debugText.text = pathManager.GetAllPaths().Count.ToString();
        if (tgs != null)
        {
            if (tgs)
            {
                nodeSelection.HighlightNode(tgs.CellGetAtMousePosition());
            }

            if (Input.GetKeyDown(KeyCode.C)) CheckCell();
            if (Input.GetMouseButtonDown(1) && pathManager.IsPathingMode) CancelPathPlacement();
        }
    }

    public void HandleNodeInteraction()
    {
        GameObject node = nodeSelection.NearestNode;
        if (!node) return;

        nearestNode = node;

        Cell cell = nodeManager.GetCellFromNode(nearestNode);
        CellData cellData = gridManager.GetCellData(cell);

        if (nodeManager.CanPlaceFlag(nearestNode, tgs) && !pathManager.IsPathingMode)
        {
            flagPanel.SetActive(true);
        }

        if (cellData.hasFlag && !pathManager.IsPathingMode)
        {
            pathPanel.SetActive(true);
        }

        if (pathManager.IsPathingMode)
        {
            pathManager.TryAddNodeToPath(nearestNode);
        }
    }

    public void PlaceFlag()
    {
        if (!nodeManager.CanPlaceFlag(nearestNode, tgs))
        {
            Debug.Log("Cannot place flag here.");
            return;
        }

        GameObject newFlag = Instantiate(flagPrefab, nearestNode.transform.position, Quaternion.identity, nearestNode.transform);
        if (!newFlag.TryGetComponent(out Flag _))
        {
            newFlag.AddComponent<Flag>();
        }

        nodeManager.SetNodeVisibility(nearestNode, true, nodeManager.defaultColor); // Use NodeManager
        Cell cell = nodeManager.GetCellFromNode(nearestNode);
        gridManager.SetCellFlag(cell, true);

        CellData cellData = gridManager.GetCellData(cell);

        if (cellData.hasPath)
        {
            pathManager.SplitPathAt(cell); // Call the new PathManager method
            nodeManager.SetNodeVisibility(nearestNode, true, Color.red);
        }

        flagPanel.SetActive(false);
        nearestNode = null;
    }

    public void CancelFlagPlacement()
    {
        flagPanel.SetActive(false);
        nearestNode = null;
    }

    private void CheckCell()
    {
        if (nodeSelection.NearestNode != null)
        {
            Cell cell = nodeManager.GetCellFromNode(nodeSelection.NearestNode);
            if (cell != null)
            {
                // Check if the cell has a flag
                bool hasFlag = gridManager.GetCellData(cell).hasFlag;

                // Check if the cell has a path
                bool hasPath = gridManager.GetCellData(cell).hasPath;

                Debug.Log($"\"Cell: {cell.index} - Flag?: {hasFlag} | Path?: {hasPath}");
            }
        }
    }

    public void StartPathPlacement()
    {
        if (nearestNode != null &&
            nearestNode.transform.childCount > 0 &&
            nearestNode.transform.GetChild(0).GetComponent<Flag>())
        {
            OnStartPathCreation?.Invoke(nearestNode);
        }
        else
        {
            Debug.Log("No flag selected to start the path.");
        }

        pathPanel.SetActive(false);
    }

    public void CancelPathPlacement()
    {
        pathPanel.SetActive(false);
        if (!pathManager.IsPathingMode) return;
        pathManager.CancelPathCreation();
        nearestNode = null;
    }

    private void OnDestroy()
    {
        input.OnInteractAction -= HandleNodeInteraction;
    }

    public GameObject GetFlagPrefab => flagPrefab;
}