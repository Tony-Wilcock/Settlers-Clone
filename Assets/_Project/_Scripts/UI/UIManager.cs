using UnityEngine;
using TMPro;

namespace PunkyFruitBat
{
    public enum UIPanel { NodePanel, BuildingPanel, DebugPanel } // Enum for panel states

    public class UIManager : MonoBehaviour
    {
        private HexGridManager manager;

        public TMP_Text pathText;
        public TMP_Text flagText;
        public TMP_Text debugText;

        [SerializeField] private GameObject nodePanel;
        [SerializeField] private GameObject buildingPanel;
        [SerializeField] private GameObject debugPanel;

        private void Awake()
        {
            HideAllPanels();
            ValidateTextReferences(); // Ensure all text fields are assigned
            manager = HexGridManager.Instance; // Cache HexGridManager instance
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

        public void ShowPanel(UIPanel panelType) // Centralized ShowPanel method
        {
            switch (panelType)
            {
                case UIPanel.NodePanel:
                    if (nodePanel != null) nodePanel.SetActive(true);
                    break;
                case UIPanel.BuildingPanel:
                    if (buildingPanel != null) buildingPanel.SetActive(true);
                    break;
                case UIPanel.DebugPanel:
                    if (debugPanel != null) debugPanel.SetActive(true);
                    break;
                default:
                    Debug.LogWarning($"UIManager: Unknown panel type: {panelType}");
                    break;
            }
        }

        private void HidePanel(UIPanel panelType) // Generic HidePanel
        {
            switch (panelType)
            {
                case UIPanel.NodePanel:
                    if (nodePanel != null && IsPanelActive(UIPanel.NodePanel)) nodePanel.SetActive(false);
                    break;
                case UIPanel.BuildingPanel:
                    if (buildingPanel != null && IsPanelActive(UIPanel.BuildingPanel)) buildingPanel.SetActive(false);
                    break;
                case UIPanel.DebugPanel:
                    if (debugPanel != null && IsPanelActive(UIPanel.DebugPanel)) debugPanel.SetActive(false);
                    break;
                default:
                    Debug.LogWarning($"UIManager: Unknown panel type: {panelType}");
                    break;
            }
        }

        public bool IsPanelActive(UIPanel panelType) // Generic IsPanelActive
        {
            switch (panelType)
            {
                case UIPanel.NodePanel:
                    return nodePanel != null && nodePanel.activeSelf;
                case UIPanel.BuildingPanel:
                    return buildingPanel != null && buildingPanel.activeSelf;
                case UIPanel.DebugPanel:
                    return debugPanel != null && debugPanel.activeSelf;
                default:
                    Debug.LogWarning($"UIManager: Unknown panel type: {panelType}");
                    return false;
            }
        }

        public bool AreAnyPanelsActive()
        {
            return IsPanelActive(UIPanel.NodePanel) || IsPanelActive(UIPanel.BuildingPanel);
        }

        public void HideAllPanels()
        {
            HidePanel(UIPanel.NodePanel);
            HidePanel(UIPanel.BuildingPanel);
        }

        public void HideNodePanel() => HidePanel(UIPanel.NodePanel);
        public void HideBuildingPanel() => HidePanel(UIPanel.BuildingPanel);
    }
}
