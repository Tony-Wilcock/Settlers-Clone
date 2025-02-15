// --- GridNodeManager.cs ---
using UnityEngine;
using TGS;
using UnityEngine.Events;


public class GridNodeManager : MonoBehaviour
{
    [SerializeField] private Input_SO input;
    [SerializeField] private GameObject flagPrefab;

    private NodePlacer nodePlacer;
    private NodeHighlighter nodeHighlighter;
    private PathManager pathManager;
    private TerrainGridSystem tgs;

    public event UnityAction<GameObject> OnStartPathCreation;


    private void Awake()
    {
        nodePlacer = GetComponent<NodePlacer>();
        nodeHighlighter = GetComponent<NodeHighlighter>();
        pathManager = GetComponent<PathManager>();
        tgs = TerrainGridSystem.instance;

        if (nodePlacer == null) Debug.LogError("nodePlacer component not found.");
        if (nodeHighlighter == null) Debug.LogError("nodeHighlighter component not found.");
        if (pathManager == null) Debug.LogError("PathManager component not found.");
        if (input == null) Debug.LogError("Input_SO is not assigned.");
    }

    void Start()
    {
        input.OnInteractAction += HandleInteractAction;
        input.OnAlternateInteractAction += HandleAlternateInteractAction;
        nodeHighlighter.Initialize(nodePlacer);
    }

    private void Update()
    {
        if (tgs != null)
        {
            nodeHighlighter.HighlightNode(tgs.CellGetAtMousePosition());
            if (Input.GetKeyDown(KeyCode.P))
            {
                StartPathCreation();
            }
        }
    }

    private void HandleInteractAction()
    {
        GameObject nearestNode = nodeHighlighter.NearestNode;

        if (nearestNode != null)
        {
            if (!pathManager.IsPathingMode)
            {
                if (nodePlacer.CanPlaceFlag(nearestNode, tgs))
                {
                    GameObject newFlag = Instantiate(flagPrefab, nearestNode.transform.position, Quaternion.identity, nearestNode.transform);
                    if (newFlag.GetComponent<Flag>() == null)
                    {
                        newFlag.AddComponent<Flag>();
                    }
                    nodeHighlighter.SetNodeColor(nearestNode, nodePlacer.defaultColor);
                    Cell cell = nodePlacer.GetCellFromNode(nearestNode);
                    FindFirstObjectByType<GridManager>().SetCellFlag(cell, true);
                }
            }
            else
            {
                pathManager.TryAddNodeToPath(nearestNode);
            }
        }
    }

    private void HandleAlternateInteractAction()
    {
        pathManager.CancelPathCreation();
    }

    public void StartPathCreation()
    {
        Debug.Log("StartPathCreation button clicked!");

        if (nodeHighlighter.NearestNode != null &&
            nodeHighlighter.NearestNode.transform.childCount > 0 &&
            nodeHighlighter.NearestNode.transform.GetChild(0).GetComponent<Flag>())
        {
            OnStartPathCreation?.Invoke(nodeHighlighter.NearestNode);
        }
        else
        {
            Debug.Log("No flag selected to start the path.");
        }
    }

    private void OnDestroy()
    {
        input.OnInteractAction -= HandleInteractAction;
        input.OnAlternateInteractAction -= HandleAlternateInteractAction;
    }

    public GameObject GetFlagPrefab()
    {
        return flagPrefab;
    }
}