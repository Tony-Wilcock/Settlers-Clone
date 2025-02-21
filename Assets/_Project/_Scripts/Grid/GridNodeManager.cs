// --- GridNodeManager.cs ---
using UnityEngine;
using TGS;
using UnityEngine.Events;
using TMPro;
using System.Collections.Generic;
using static CellTypes;
using System.Text;

public class GridNodeManager : MonoBehaviour
{
    #region Variables

    [SerializeField] private Input_SO input;
    [SerializeField] private GameObject flagPrefab;
    [SerializeField] private GameObject flagPanel;
    [SerializeField] private GameObject pathPanel;
    [SerializeField] private TMP_Text debugText;
    [SerializeField] private GameObject characterPrefab;

    private NodeManager nodeManager;
    private NodeSelection nodeSelection;
    private PathManager pathManager;
    private TerrainGridSystem tgs;
    private GridManager gridManager;
    private PathGenerator pathGenerator;

    private GameObject nearestNode = null;

    public event UnityAction<GameObject> OnStartPathCreation;

    #endregion Variables

    private void Awake()
    {
        InitialiseComponents();

        if (!AreComponentsValid())
        {
            Debug.LogError("GridNodeManager: Missing required components or assignments.");
            enabled = false; // Disable the script if dependencies are missing.
            return;
        }

        InitialiseManagers();
    }

    void Start()
    {
        input.OnInteractAction += HandleNodeInteraction;
        input.OnCheckCellAction += CheckCell;
    }

    private void Update()
    {
        if (tgs == null) return;

        nodeSelection.HighlightNode();

        if (Input.GetMouseButtonDown(1) && pathManager.IsPathingMode) CancelPathPlacement();
    }

    private void OnDestroy()
    {
        input.OnInteractAction -= HandleNodeInteraction;
        input.OnCheckCellAction -= CheckCell;
    }

    private void InitialiseComponents()
    {
        nodeManager = GetComponent<NodeManager>();
        nodeSelection = GetComponent<NodeSelection>();
        pathManager = GetComponent<PathManager>();
        tgs = TerrainGridSystem.instance;
        gridManager = GetComponent<GridManager>();
        pathGenerator = GetComponent<PathGenerator>();
    }

    private bool AreComponentsValid()
    {
        return nodeManager != null && nodeSelection != null && pathManager != null &&
               input != null && gridManager != null && tgs != null && pathGenerator != null;
    }

    private void InitialiseManagers()
    {
        nodeSelection.Initialize(nodeManager, gridManager, tgs, pathManager);
        pathManager.Initialize(this, tgs, nodeManager, gridManager, pathGenerator);
        gridManager.Initialise(nodeManager, tgs, pathManager);
        nodeManager.Initialise(gridManager);
    }

    public void HandleNodeInteraction()
    {
        GameObject node = nodeSelection.NearestNode;
        if (node == null) return;

        nearestNode = node;

        if (!pathManager.IsPathingMode)
        {
            TryShowFlagPanel(node);
            TryShowPathPanel(node);
        }
        else
        {
            pathManager.TryAddPathToEndNode(node);
        }
    }

    private void TryShowFlagPanel(GameObject node)
    {
        if (nodeManager.CanPlaceFlag(node, tgs))
        {
            flagPanel.SetActive(true);
        }
    }

    private void TryShowPathPanel(GameObject node)
    {
        Cell cell = nodeManager.GetCellFromNode(node);
        CellData cellData = gridManager.GetCellData(cell);
        if (cellData.HasFlag)
        {
            pathPanel.SetActive(true);
        }
    }

    public void PlaceFlag()
    {
        if (!CanPlaceFlag())
        {
            Debug.Log("Cannot place flag here.");
            return;
        }

        GameObject newFlag = CreateFlag(nearestNode);
        UpdateNodeForFlag(nearestNode);
        ResetFlagPlacement();
    }
    private bool CanPlaceFlag()
    {
        return nearestNode != null && nodeManager.CanPlaceFlag(nearestNode, tgs);
    }
    private GameObject CreateFlag(GameObject node)
    {
        GameObject newFlag = Instantiate(flagPrefab, node.transform.position, Quaternion.identity, node.transform);
        if (!newFlag.TryGetComponent(out Flag _))
        {
            newFlag.AddComponent<Flag>();
        }
        return newFlag;
    }
    private void UpdateNodeForFlag(GameObject node)
    {
        //nodeManager.SetNodeVisibility(node, true, nodeManager.DefaultColor);
        Cell cell = nodeManager.GetCellFromNode(node);
        gridManager.SetCellFlag(cell, true);

        CellData cellData = gridManager.GetCellData(cell);
        if (cellData.HasPath)
        {
            pathManager.SplitPathAt(cell);
        }
    }

    private void ResetFlagPlacement()
    {
        flagPanel.SetActive(false);
        nearestNode = null;
    }

    public void CancelFlagPlacement()
    {
        flagPanel.SetActive(false);
        nearestNode = null;
    }

    public void InstantiateCharacter(CharacterType characterType, Vector3 target)
    {
        GameObject newCharacter = Instantiate(characterPrefab);
        Character character = newCharacter.GetComponent<Character>();
        character.Initialise(
            this,
            gridManager,
            nodeManager,
            pathManager,
            nodeSelection
        );
        character.CharacterType = characterType;

        List<Vector3> targets = new()
        {
            character.HQ.transform.position,
            target
        };
        character.SetTarget(targets);
    }

    private void CheckCell()
    {
        Cell cell = tgs.CellGetAtMousePosition();
        if (cell == null) return;

        CellData cellData = gridManager.GetCellData(cell);
        debugText.text = BuildCellDebugInfo(cell, cellData);
    }
    private string BuildCellDebugInfo(Cell cell, CellData cellData)
    {
        StringBuilder sb = new();
        sb.AppendLine($"Cell Height: {tgs.CellGetCentroid(cell.index).y - nodeManager.NodeHeightOffset}");

        if (cellData.HasFlag) sb.AppendLine("Has Flag");
        if (cellData.HasPath) sb.AppendLine("Has Path");
        if (cellData.HasObstacle) sb.AppendLine("Has Obstacle");
        if (cellData.BuildingType != BuildingType.None) sb.AppendLine($"Building Type: {cellData.BuildingType}");
        if (cellData.ResourceType != ResourceType.None) sb.AppendLine($"Resource Type: {cellData.ResourceType}");
        if (cellData.ResourceAmount > 0) sb.AppendLine($"Resource Amount: {cellData.ResourceAmount}");
        if (cellData.TerrainType != TerrainType.None) sb.AppendLine($"Terrain Type: {cellData.TerrainType}");

        return sb.ToString();
    }

    public void StartPathPlacement()
    {
        if (!CanStartPath()) return;

        Cell cell = nodeManager.GetCellFromNode(nearestNode);
        CellData cellData = gridManager.GetCellData(cell);
        if (cellData.HasFlag)
        {
            OnStartPathCreation?.Invoke(nearestNode);
            pathPanel.SetActive(false);
        }
    }

    private bool CanStartPath()
    {
        if (nearestNode == null)
        {
            Debug.Log("No node selected to start the path.");
            return false;
        }
        return true;
    }

    public void CancelPathPlacement()
    {
        pathPanel.SetActive(false);
        if (!pathManager.IsPathingMode) return;
        pathManager.CancelPathCreation();
        nearestNode = null;
    }

    public GameObject GetFlagPrefab => flagPrefab;
}