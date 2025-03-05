using System;
using System.Text;
using UnityEngine;
using static NodeTypes;

public enum IconIndex
{
    None = 0,
    Flag = 1,
    SmallBuilding = 2,
    MediumBuilding = 3,
    LargeBuilding = 4,
    Resource = 5
}

[Serializable]
public class HexGridInteraction : MonoBehaviour
{
    [Tooltip("Prefabs for node icons, corresponding to IconIndex enum.")]
    [SerializeField] private GameObject[] nodeIconPrefabs; // Array of node icon prefabs

    private HexGridManager manager;
    private PathManager pathManager;

    private GameObject[] instantiatedNodeIcons;
    private int currentIconIndex = (int)IconIndex.None;

    [SerializeField] private BuildingType selectedBuildingType = BuildingType.Storehouse;

    public void Initialise(HexGridManager manager, PathManager pathManager)
    {
        this.manager = manager;
        this.pathManager = pathManager;

        instantiatedNodeIcons = new GameObject[nodeIconPrefabs.Length];
        for (int i = 0; i < nodeIconPrefabs.Length; i++)
        {
            if (nodeIconPrefabs[i] != null)
            {
                instantiatedNodeIcons[i] = Instantiate(nodeIconPrefabs[i]);
                instantiatedNodeIcons[i].transform.SetParent(manager.nodeIconsTransform);
                instantiatedNodeIcons[i].SetActive(false);
            }
            else
            {
                Debug.LogError($"NodeSelection: nodeIconPrefabs[{i}] is null!");
            }
        }

        manager.Input_SO.OnInteractAction += HandleNodeInteraction;
    }

    public void OnDestroy()
    {
        manager.Input_SO.OnInteractAction -= HandleNodeInteraction;
    }

    private void HandleNodeInteraction()
    {
        if (manager.UIManager.AreAnyPanelsActive() && !pathManager.IsInPathCreationMode) return; // Skip if any panel is active

        int node = manager.NodeManager.liveVertexIndex;
        if (node == -1) return;
        NodeData nodeData = manager.NodeManager.nodeDataDictionary[node]; // Access NodeData from dictionary
        manager.NodeManager.heldVertexIndex = node;

        if (manager.isDebugModeActive)
        {
            LogVertexData(node);
            DebugActions(node);
            return;
        }

        if (pathManager.IsInPathCreationMode)
        {
            pathManager.TryAddPathToEndNode(node);
        }
        else if (selectedBuildingType != BuildingType.None)
        {
            if (manager.BuildingManager.TryPlaceBuilding(node, selectedBuildingType, out int entranceNode))
            {
                Debug.Log($"Placed {selectedBuildingType} at {node} with entrance at {entranceNode}");
                selectedBuildingType = BuildingType.None;
            }
        }
        else
        {
            manager.UIManager.ShowPanelsForNode(manager, node, nodeData);
        }
    }

    // Example method to set building type from UI
    public void SetBuildingType(BuildingType type)
    {
        selectedBuildingType = type;
    }

    public void LogVertexData(int vertexIndex)
    {
        foreach (var cellEntry in manager.cellVertexMap) // Access cellVertexMap from manager
        {
            int localVertexIndex = cellEntry.Value.IndexOf(vertexIndex);
            if (localVertexIndex != -1)
            {
                int cellIndex = cellEntry.Key.y + cellEntry.Key.x * manager.settings.height; // Access height via manager
                return;
            }
        }
        Debug.LogWarning($"Vertex {vertexIndex} not found in any cell!");
    }

    private void DebugActions(int node)
    {
        if (Input.GetKey(KeyCode.LeftShift)) // Check for Shift key for obstacle placement
        {
            if (node != -1)
            {
                if (node != -1)
                {
                    NodeData nodeData = manager.NodeManager.nodeDataDictionary[node];
                    manager.NodeManager.SetNodeObstacle(node, !nodeData.HasObstacle);
                    Debug.Log(nodeData.HasObstacle
                        ? $"Vertex {node} set as obstacle."
                        : $"Vertex {node} removed as obstacle.");
                }
            }
        }
        else if (Input.GetKey(KeyCode.DownArrow)) // Down Arrow for lowering height
        {
            manager.NodeManager.LowerHeight(node);
        }
        else if (Input.GetKey(KeyCode.UpArrow) && node != -1 && !manager.EdgeVertices.Contains(node)) // Up Arrow for raising height
        {
            manager.NodeManager.RaiseHeight();
        }
        else if (Input.GetKey(KeyCode.C)) // C key for Cell selection
        {
            (int cellRow, int cellCol) = manager.NodeManager.GetCellAtMousePosition(Input.mousePosition);
            manager.UIManager.debugText.text = BuildCellDebugInfo(node, manager.NodeManager.nodeDataDictionary[node]);
        }
        else if (Input.GetKey(KeyCode.N)) // N key for neighbours of selected vertex
        {
            foreach (int neighbour in manager.NodeManager.GetNodeNieghbors(node))
            {
                Debug.Log("Neighbours:" + neighbour.ToString());
            }
        }
    }


    private string BuildCellDebugInfo(int node, NodeData nodeData)
    {
        StringBuilder sb = new();
        sb.AppendLine($"Node Height: {manager.NodeManager.GetVertexHeight(node)}");

        if (nodeData.HasFlag) sb.AppendLine("Has Flag");
        if (nodeData.HasPath) sb.AppendLine($"Has Path with id: {manager.PathManager.GetPathId(node)}");
        if (nodeData.HasObstacle) sb.AppendLine("Has Obstacle");
        if (nodeData.HasBuilding) sb.AppendLine($"Building ID: {nodeData.BuildingID}");
        if (nodeData.HasResource) sb.AppendLine($"Has Resource: {nodeData.ResourceType} - Amount: {nodeData.ResourceAmount}");
        if (nodeData.BuildingType != BuildingType.None) sb.AppendLine($"Building Type: {nodeData.BuildingType}");
        if (nodeData.ResourceType != WorldResourceType.None) sb.AppendLine($"Resource Type: {nodeData.ResourceType}");
        if (nodeData.ResourceAmount > 0) sb.AppendLine($"Resource Amount: {nodeData.ResourceAmount}");
        if (nodeData.TerrainType != TerrainType.None) sb.AppendLine($"Terrain Type: {nodeData.TerrainType}");

        return sb.ToString();
    }

    public void HighlightNode()
    {
        int vertexIndex = manager.NodeManager.GetNearestNode(Input.mousePosition);
        if (!CanHighlightNode(vertexIndex)) return;

        manager.NodeManager.liveVertexIndex = vertexIndex;
        int iconIndex = DetermineIconIndex(vertexIndex);
        SetActiveIcon(iconIndex);
    }

    private bool CanHighlightNode(int vertexIndex)
    {
        if (vertexIndex == -1)
        {
            for (int i = 0; i < instantiatedNodeIcons.Length; i++)
            {
                instantiatedNodeIcons[i].SetActive(false); // Deactivate all icons
            }
            manager.NodeManager.liveVertexIndex = -1; // Reset selected vertex
            return false;
        }

        if (manager.NodeManager.liveVertexIndex == vertexIndex) return false; // Skip if same vertex

        return true;
    }

    private int DetermineIconIndex(int vertexIndex)
    {
        NodeData nodeData = manager.NodeManager.nodeDataDictionary[vertexIndex]; // Access NodeData from dictionary
        if (nodeData == null || !manager.NodeManager.CanPlaceFlag(vertexIndex))
        {
            return (int)IconIndex.None;
        }

        return (int)IconIndex.Flag; // Default to None
    }

    private void SetActiveIcon(int iconIndex)
    {
        if (!IsValidIconIndex(iconIndex))
        {
            Debug.LogError($"NodeSelection: Invalid icon index: {iconIndex}");
            return;
        }

        DeactivateAllIcons();
        currentIconIndex = iconIndex;

        ActivateAndPositionIcon(currentIconIndex);
    }

    private bool IsValidIconIndex(int index) => index >= 0 && index < instantiatedNodeIcons.Length;

    private void DeactivateAllIcons()
    {
        for (int i = 0; i < instantiatedNodeIcons.Length; i++)
        {
            instantiatedNodeIcons[i]?.SetActive(false); // Deactivate all icons
        }
    }

    private void ActivateAndPositionIcon(int index)
    {
        GameObject icon = instantiatedNodeIcons[index];
        icon?.SetActive(true);
        icon.transform.position = manager.globalVertices[manager.NodeManager.liveVertexIndex];
    }
}