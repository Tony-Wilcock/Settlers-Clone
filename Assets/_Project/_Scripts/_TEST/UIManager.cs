using UnityEngine;
using TMPro;

public enum UIPanel { None, PathPanel, FlagPanel, DebugPanel } // Enum for panel states

public class UIManager : MonoBehaviour
{
    public TMP_Text pathText;
    public TMP_Text flagText;
    public TMP_Text debugText;

    private UIPanel currentPanel = UIPanel.None; // Track current panel state
    public UIPanel CurrentPanel => currentPanel; // Public read-only access

    [SerializeField] private GameObject pathPanel;
    [SerializeField] private GameObject flagPanel;
    [SerializeField] private GameObject debugPanel;

    private void Awake()
    {
        HideAllPanels();
        ValidateTextReferences(); // Ensure all text fields are assigned
    }

    private void ValidateTextReferences()
    {
        if (pathText == null) Debug.LogError("UIManager: pathText is not assigned!");
        if (flagText == null) Debug.LogError("UIManager: flagText is not assigned!");
        if (debugText == null) Debug.LogError("UIManager: debugText is not assigned!");
    }

    public void UpdateUIText(string key, string value)
    {
        switch (key)
        {
            case "Paths":
                if (pathText != null) pathText.text = value;
                else Debug.LogWarning("UIManager: pathText is null, cannot update Paths.");
                break;
            case "Flags":
                if (flagText != null) flagText.text = value;
                else Debug.LogWarning("UIManager: flagText is null, cannot update Flags.");
                break;
            case "Debug":
                if (debugText != null) debugText.text = value;
                else Debug.LogWarning("UIManager: debugText is null, cannot update Debug.");
                break;
            default:
                Debug.LogWarning($"UIManager: Unknown UI text key: {key}");
                break;
        }
    }

    public void ShowPanelsForNode(HexGridManager manager, int node, NodeData nodeData)
    {
        if (nodeData.HasFlag)
        {
            TryShowPathPanel(manager, node);
        }
        else
        {
            TryShowFlagPanel(manager, node);
        }
    }

    private void TryShowFlagPanel(HexGridManager manager, int node)
    {
        if (manager.NodeManager.CanPlaceFlag(node))
        {
            manager.UIManager.ShowPanel(UIPanel.FlagPanel);
        }
    }

    private void TryShowPathPanel(HexGridManager manager, int node)
    {
        NodeData nodeData = manager.NodeManager.nodeDataDictionary[node]; // Access NodeData from dictionary
        if (nodeData.HasFlag)
        {
            manager.UIManager.ShowPanel(UIPanel.PathPanel);
        }
        else
        {
            manager.PathFindingManager.IsInPathCreationMode = false;
        }
    }

    public void ShowPanel(UIPanel panelType) // Centralized ShowPanel method
    {
        HideAllPanels();
        currentPanel = panelType; // Update current panel state

        switch (panelType)
        {
            case UIPanel.PathPanel:
                if (pathPanel != null) pathPanel.SetActive(true);
                break;
            case UIPanel.FlagPanel:
                if (flagPanel != null) flagPanel.SetActive(true);
                break;
            case UIPanel.DebugPanel:
                if (debugPanel != null) debugPanel.SetActive(true);
                break;
            case UIPanel.None:
                break;
            default:
                Debug.LogWarning($"UIManager: Unknown panel type: {panelType}");
                currentPanel = UIPanel.None;
                break;
        }
    }

    public void HideAllPanels()
    {
        if (pathPanel != null) pathPanel.SetActive(false);
        if (flagPanel != null) flagPanel.SetActive(false);
        if (debugPanel != null) debugPanel.SetActive(false);
        currentPanel = UIPanel.None;
    }

    public bool IsPanelActive(UIPanel panelType) // Generic IsPanelActive
    {
        return currentPanel == panelType;
    }

    public bool AreAnyPanelsActive() => currentPanel != UIPanel.None; // Check currentPanel state


    // Example methods to be called by UI Buttons (if you have buttons directly controlling panels)
    public void OnShowPathPanelButton() => ShowPanel(UIPanel.PathPanel);
    public void OnShowFlagPanelButton() => ShowPanel(UIPanel.FlagPanel);
    public void OnShowDebugPanelButton() => ShowPanel(UIPanel.DebugPanel);
    public void OnHideAllPanelsButton() => HideAllPanels();
}
