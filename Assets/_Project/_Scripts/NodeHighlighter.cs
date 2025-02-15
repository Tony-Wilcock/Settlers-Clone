// --- NodeHighlighter.cs ---
using UnityEngine;
using TGS;

public class NodeHighlighter : MonoBehaviour
{
    [Tooltip("The maximum distance for a node to be considered for highlighting.")]
    [SerializeField] private float highlightDistanceThreshold = 20f;
    [Tooltip("The color to use when highlighting a node.")]
    public Color highlightColor = Color.yellow;

    private GameObject _nearestNode = null;
    private GameObject _previousNearestNode = null;
    private Vector3 _lastMousePosition = Vector3.negativeInfinity;

    private NodePlacer _nodePlacer;
    private GridManager _gridManager; // Add reference to GridManager

    public void Initialize(NodePlacer nodePlacer)
    {
        _nodePlacer = nodePlacer;
        _gridManager = FindFirstObjectByType<GridManager>(); // Get GridManager reference
    }

    public GameObject NearestNode => _nearestNode;

    public void HighlightNode(Cell currentHexCell)
    {
        //Debug.Log("HighlightNode called"); // Uncomment if needed

        if (Input.mousePosition != _lastMousePosition)
        {
            _lastMousePosition = Input.mousePosition;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, Mathf.Infinity, _nodePlacer.terrainLayerMask))
            {
                // Debug.Log("Raycast hit: " + hit.collider.gameObject.name); // Keep for now
                FindNearestNode(hit.point);
            }
            else
            {
                ResetHighlighting();
            }
        }

        if (_nearestNode != _previousNearestNode)
        {
            //Debug.Log("Nearest node changed!"); // Add this

            if (_previousNearestNode != null)
            {
                ResetNodeColor(_previousNearestNode);
            }

            if (_nearestNode != null)
            {
                Cell currentCell = _nodePlacer.GetCellFromNode(_nearestNode);
                if (currentCell != null)
                {
                    CellData currentData = _gridManager.GetCellData(currentCell);
                    if (!currentData.hasPath)
                    {
                        SetNodeColor(_nearestNode, highlightColor);
                    }
                }
            }

            _previousNearestNode = _nearestNode;
        }
    }
    private void FindNearestNode(Vector3 mousePosition)
    {
        //Debug.Log("FindNearestNode called"); // Add this
        _nearestNode = null;
        float minDistanceSq = highlightDistanceThreshold * highlightDistanceThreshold;

        foreach (GameObject node in _nodePlacer.GetCenterNodes())
        {
            //Debug.Log("Checking node: " + node.name); // Add this
            float distanceSq = (mousePosition - node.transform.position).sqrMagnitude;
            if (distanceSq < minDistanceSq)
            {
                Cell cell = _nodePlacer.GetCellFromNode(node);
                if (cell != null)
                {
                    CellData data = _gridManager.GetCellData(cell);
                    if (!data.hasPath)
                    {
                        minDistanceSq = distanceSq;
                        _nearestNode = node;
                        //Debug.Log("New nearest node: " + _nearestNode.name); // Add this
                    }
                }
            }
        }
    }

    public void SetNodeColor(GameObject node, Color color)
    {
        if (node != null)
        {
            SpriteRenderer _spriteRenderer = node.GetComponent<SpriteRenderer>();
            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = color;
            }
        }
    }
    private void ResetNodeColor(GameObject node)
    {
        if (node != null)
        {
            Cell cell = _nodePlacer.GetCellFromNode(node);
            if (cell != null)
            {
                CellData data = _gridManager.GetCellData(cell);
                // Reset based on CellData:
                if (data.hasPath)
                {
                    //PathManager Visualize all paths sets visibilty and colour, so we don't need to do anything
                }
                else
                {
                    //Not part of a path
                    SetNodeColor(node, _nodePlacer.defaultColor);
                    //_nodePlacer.SetNodeVisibility(node, false); //Removed for now
                }
            }
        }
    }

    private void ResetHighlighting()
    {
        if (_previousNearestNode != null)
        {
            ResetNodeColor(_previousNearestNode); // Use ResetNodeColor here
        }
        _nearestNode = null;
        _previousNearestNode = null;
    }
}