// --- NodeSelection.cs ---
using UnityEngine;
using TGS;
using System.Collections.Generic;

public class NodeSelection : MonoBehaviour
{
    [Tooltip("The color to use when highlighting a node.")]
    public Color highlightColor = Color.yellow;

    private NodeManager nodeManager;
    private GridManager gridManager; // Add reference to GridManager
    private GameObject nearestNode = null;
    private GameObject previousNearestNode = null;
    private Vector3 lastMousePosition = Vector3.negativeInfinity;

    public void Initialize(NodeManager nodeManager)
    {
        this.nodeManager = nodeManager;
        gridManager = FindFirstObjectByType<GridManager>(); // Get GridManager reference
    }

    public GameObject NearestNode => nearestNode;

    public void HighlightNode(Cell currentHexCell)
    {
        if (Input.mousePosition != lastMousePosition)
        {
            lastMousePosition = Input.mousePosition;
            FindNearestNode(lastMousePosition); // Pass in last mouse position.
        }

        if (nearestNode != previousNearestNode)
        {

            if (previousNearestNode != null)
            {
                ResetNodeColor(previousNearestNode);
            }

            if (nearestNode != null)
            {
                Cell currentCell = nodeManager.GetCellFromNode(nearestNode);
                if (currentCell != null)
                {
                    SetNodeColor(nearestNode, highlightColor);
                    nodeManager.SetNodeVisibility(nearestNode, true); // Ensure visibility
                }
            }

            previousNearestNode = nearestNode;
        }
    }

    private void FindNearestNode(Vector3 mousePosition)
    {
        nearestNode = null; // Always reset

        Cell currentHexCell = TerrainGridSystem.instance.CellGetAtMousePosition(); // Get the Cell

        if (currentHexCell != null)
        {
            nearestNode = nodeManager.GetCellNodeMap().GetValueOrDefault(currentHexCell); //Simplified
        }
    }

    public void SetNodeColor(GameObject node, Color color)
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

    private void ResetNodeColor(GameObject node)
    {
        if (node != null)
        {
            // ALWAYS reset to default color, then re-apply path color if needed.
            SetNodeColor(node, nodeManager.defaultColor);

            Cell cell = nodeManager.GetCellFromNode(node);
            if (cell != null)
            {
                CellData data = gridManager.GetCellData(cell);
                // If it's part of a path, make it red.  This overrides the default color.
                if (data.hasPath)
                {
                    SetNodeColor(node, Color.red);
                    nodeManager.SetNodeVisibility(node, true, Color.red); // Ensure path nodes are visible
                }
                else if (!data.hasFlag) // Only hide if it's NOT a path and NOT a flag
                {
                    nodeManager.SetNodeVisibility(node, false, nodeManager.defaultColor);
                }
            }
        }
    }
}