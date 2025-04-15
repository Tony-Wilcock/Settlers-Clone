using UnityEngine;
using TMPro;
using System.Collections;
using System;

namespace PunkyFruitBat
{
    public enum UIPanel { BuildingPanel, DebugPanel } // Enum for panel states

    public class UIManager : MonoBehaviour
    {
        private HexGridManager manager;

        // DEBUG TEXT
        [SerializeField] private TMP_Text pathText;
        [SerializeField] private TMP_Text flagText;
        [SerializeField] private TMP_Text fpsText;
        [SerializeField] private TMP_Text liveNodeText;
        [SerializeField] private TMP_Text selectedNodeText;

        // PANELS
        [SerializeField] private GameObject debugPanel;
        [SerializeField] private GameObject buildingPanel;

        // TABS
        [SerializeField] private GameObject flagTab;
        [SerializeField] private GameObject pathTab;
        [SerializeField] private GameObject createSmallBuildingTab;
        [SerializeField] private GameObject createMediumBuildingTab;
        [SerializeField] private GameObject createLargeBuildingTab;

        // PAGES
        // FLAG PAGE
        [SerializeField] private GameObject flagPage;
        [SerializeField] private GameObject createFlagIcon;
        [SerializeField] private GameObject removeFlagIcon;

        // PATH PAGE
        [SerializeField] private GameObject pathPage;
        [SerializeField] private GameObject createPathIcon;
        [SerializeField] private GameObject removePathIcon;

        // SMALL BUILDING PAGE
        [SerializeField] private GameObject createSmallBuildingPage;

        // MEDIUM BUILDING PAGE
        [SerializeField] private GameObject createMediumBuildingPage;

        // LARGE BUILDING PAGE
        [SerializeField] private GameObject createLargeBuildingPage;

        // HOVER TEXT
        [SerializeField] private TMP_Text buildingPanelHoverText;

        // TABS BACKGROUND
        [SerializeField] private Sprite tabBackgroundFrame_Default;
        [SerializeField] private Sprite tabBackgroundFrame_Selected;

        // TABS SIZE
        private Vector3 tabSize_Default, tabSize_Selected;

        private void Awake()
        {
            HideAllPanels();
            ValidateTextReferences(); // Ensure all text fields are assigned
            manager = HexGridManager.Instance; // Cache HexGridManager instance
        }

        private void Start()
        {
            manager.NodeManager.OnLiveNodeUpdated += UpdateNearestVertexText;
            manager.NodeSelector.OnNodeSelected += UpdateSelectedNodeIndex;

            tabSize_Default = Vector3.one;
            tabSize_Selected = Vector3.one * 1.1f;

            StartCoroutine(UpdateFpsText()); // Start FPS text update coroutine
        }

        private void UpdateNearestVertexText(int node)
        {
            UpdateUIText("Live", $"Live: {node}");
        }

        private void UpdateSelectedNodeIndex(int nodeIndex)
        {
            UpdateUIText("Selected", $"Selected: {nodeIndex}");
        }

        private IEnumerator UpdateFpsText()
        {
            while (true)
            {
                yield return WaitForSecondsFactory.WaitCoroutine(0.1f);
                if (fpsText != null) fpsText.text = $"FPS: {Mathf.RoundToInt(1f / Time.deltaTime)}"; // Update FPS text
            }
        }

        private void ValidateTextReferences()
        {
            if (pathText == null) Debug.LogError("UIManager: pathText is not assigned!");
            if (flagText == null) Debug.LogError("UIManager: flagText is not assigned!");
            if (liveNodeText == null) Debug.LogError("UIManager: liveNodeText is not assigned!");
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
                case "Live":
                    if (liveNodeText != null) liveNodeText.text = value;
                    else Debug.LogWarning("UIManager: liveNodeText is null, cannot update Debug.");
                    break;
                case "Selected":
                    if (selectedNodeText != null) selectedNodeText.text = value;
                    else Debug.LogWarning("UIManager: selectedNodeText is null, cannot update Debug.");
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
                case UIPanel.BuildingPanel:
                    if (buildingPanel != null) buildingPanel.SetActive(true);
                    DetermineTabsToShow(manager.LiveNode);
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
            return IsPanelActive(UIPanel.BuildingPanel);
        }

        public void HideAllPanels()
        {
            HidePanel(UIPanel.BuildingPanel);
        }

        private void DetermineTabsToShow(int nodeIndex)
        {
            if (nodeIndex < 0 || nodeIndex >= manager.globalVertices.Length) return; // Validate node index
            Node node = manager.NodeManager.GetNode(nodeIndex);
            HideAllTabs();

            if (manager.NodeManager.CanPlaceBuilding(nodeIndex, BuildingSize.Large))
            {
                ShowTabOrPage(createLargeBuildingTab);
                ShowTabOrPage(createMediumBuildingTab);
                ShowTabOrPage(createSmallBuildingTab);

                if (manager.NodeManager.CanPlaceFlag(nodeIndex))
                {
                    ShowTabOrPage(flagTab);
                    SetActiveBuildingPage(flagPage);
                    ShowTabOrPage(createFlagIcon);
                    HideTabOrPage(removeFlagIcon);
                }
                else
                {
                    SetActiveBuildingPage(createSmallBuildingPage);
                }
            }
            else if (manager.NodeManager.CanPlaceBuilding(nodeIndex, BuildingSize.Medium))
            {
                ShowTabOrPage(createMediumBuildingTab);
                ShowTabOrPage(createSmallBuildingTab);

                if (manager.NodeManager.CanPlaceFlag(nodeIndex))
                {
                    ShowTabOrPage(flagTab);
                    SetActiveBuildingPage(flagPage);
                    ShowTabOrPage(createFlagIcon);
                    HideTabOrPage(removeFlagIcon);
                }
                else
                {
                    SetActiveBuildingPage(createSmallBuildingPage);
                }
            }
            else if (manager.NodeManager.CanPlaceBuilding(nodeIndex, BuildingSize.Small))
            {
                ShowTabOrPage(createSmallBuildingTab);

                if (manager.NodeManager.CanPlaceFlag(nodeIndex))
                {
                    ShowTabOrPage(flagTab);
                    SetActiveBuildingPage(flagPage);
                    ShowTabOrPage(createFlagIcon);
                    HideTabOrPage(removeFlagIcon);
                }
                else
                {
                    SetActiveBuildingPage(createSmallBuildingPage);
                }
            }
            else if (manager.NodeManager.CanPlaceFlag(nodeIndex))
            {
                ShowTabOrPage(flagTab);
                if (node.HasPath)
                {
                    ShowTabOrPage(pathTab);
                    ShowTabOrPage(removePathIcon);
                    HideTabOrPage(createPathIcon);
                }

                SetActiveBuildingPage(flagPage);
                ShowTabOrPage(createFlagIcon);
                HideTabOrPage(removeFlagIcon);
            }
            else if (node.HasFlag)
            {
                ShowTabOrPage(flagTab);

                Flag flag = manager.FlagManager.TryGetFlag(nodeIndex);
                if (flag != null)
                {
                    if (flag.PathsAttachedToFlag.Count < 6)
                    {
                        ShowTabOrPage(pathTab);
                        ShowTabOrPage(createPathIcon);
                        HideTabOrPage(removePathIcon);
                    }
                }

                SetActiveBuildingPage(flagPage);
                ShowTabOrPage(removeFlagIcon);
                HideTabOrPage(createFlagIcon);
            }
            else if (!node.HasFlag && node.HasPath)
            {
                ShowTabOrPage(pathTab);

                SetActiveBuildingPage(pathPage);
                ShowTabOrPage(removePathIcon);
                HideTabOrPage(createPathIcon);
            }
        }

        private void ShowTabOrPage(GameObject obj)
        {
            if (obj != null && !obj.activeSelf) obj.SetActive(true);
        }

        private void HideTabOrPage(GameObject obj)
        {
            if (obj != null && obj.activeSelf) obj.SetActive(false);
        }

        private void HideAllTabs()
        {
            HideTabOrPage(flagTab);
            HideTabOrPage(pathTab);
            HideTabOrPage(createSmallBuildingTab);
            HideTabOrPage(createMediumBuildingTab);
            HideTabOrPage(createLargeBuildingTab);
        }

        private void HideAllPages()
        {
            HideTabOrPage(flagPage);
            HideTabOrPage(pathPage);
            HideTabOrPage(createSmallBuildingPage);
            HideTabOrPage(createMediumBuildingPage);
            HideTabOrPage(createLargeBuildingPage);
        }

        public void SetBuildingPanelHoverText(string text)
        {
            if (buildingPanelHoverText != null) buildingPanelHoverText.text = text;
            else Debug.LogWarning("UIManager: buildingPanelHoverText is null, cannot set hover text.");
        }

        public void SetActiveBuildingPage(GameObject page)
        {
            if (page != null)
            {
                HideAllPages();
                ShowTabOrPage(page);
            }
            else
            {
                Debug.LogWarning("UIManager: Attempted to set a null page as active.");
            }
        }
    }
}
