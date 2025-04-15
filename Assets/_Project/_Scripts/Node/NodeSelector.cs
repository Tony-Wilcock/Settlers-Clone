using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PunkyFruitBat
{
    public class NodeSelector
    {
        public event Action<int> OnNodeSelected; // Event to notify when a node is selected

        public int selectedNode = -1;

        private HexGridManager manager;
        private NodeManager nodeManager;

        public void Initialise(HexGridManager manager)
        {
            this.manager = manager;
            nodeManager = manager.NodeManager;

            manager.Input_SO.OnInteractAction += SetSelectedNodeIndex; // Subscribe to the interact action
            manager.FlagManager.OnFlagPlaced += ResetSelectedNodeIndex; // Subscribe to the flag placed action
            manager.FlagManager.OnFlagRemoved += ResetSelectedNodeIndex; // Subscribe to the flag removed action
        }

        private void SetSelectedNodeIndex()
        {
            manager.StartCoroutine(SetSelectedNodeIndexCoroutine());
        }

        private IEnumerator SetSelectedNodeIndexCoroutine()
        {
            yield return null; // Wait for the end of the frame
            if (manager.UIManager.AreAnyPanelsActive() && EventSystem.current.IsPointerOverGameObject())
            {
                // If it is over a UI element, do nothing further.
                yield break;
            }
            if (manager.LiveNode < 0) yield break; // If no node is selected, return

            if (ShouldShowAndUpdateSelectedNode(manager.LiveNode))
            {
                selectedNode = manager.LiveNode; // Set the selected node to the nearest node
                nodeManager.SelectedNodeObject.SetActive(true); // Activate the selected node prefab
                nodeManager.SelectedNodeObject.transform.position = manager.globalVertices[selectedNode] + Vector3.up * 0.5f; // Set the position of the selected node prefab to the nearest node
                OnNodeSelected?.Invoke(selectedNode); // Invoke the node selected event
            }
            else yield break; // If the node is not valid, return

            if (manager.PathManager.IsInPathCreationMode) // If in Path Creation Mode
            {
                manager.PathManager.TryAddPathToEndNode(manager.LiveNode); // If in Path Creation Mode, try to add the node to the path
            }
            else if (selectedNode >= 0 && selectedNode < manager.globalVertices.Length) // If a node is selected
            {
                Node node = nodeManager.GetNode(selectedNode); // Get the node at the selected index
                if (manager.IconPicker.GetCurrentIconIndex() != (int)NodeIconIndex.None)
                    manager.UIManager.ShowPanel(UIPanel.BuildingPanel); // Show the Building Panel
                else if (manager.IconPicker.GetCurrentIconIndex() == (int)NodeIconIndex.None && (node.HasFlag || node.HasPath))
                    manager.UIManager.ShowPanel(UIPanel.BuildingPanel); // Show the Building Panel
            }
        }

        private bool ShouldShowAndUpdateSelectedNode(int nodeIndex)
        {
            if (nodeIndex < 0 || nodeIndex >= manager.globalVertices.Length)
            {
                return false; // Invalid node index
            }
            bool canPlaceFlag = manager.NodeManager.CanPlaceFlag(nodeIndex); // Check if the node can place a flag
            bool canPlaceBuilding = manager.NodeManager.CanPlaceBuilding(nodeIndex, BuildingSize.Small); // Check if the node can place a building
            Node node = nodeManager.GetNode(nodeIndex);
            return node != null && (node.HasFlag || node.HasPath || canPlaceFlag || canPlaceBuilding); // Check if the node has a flag or path
        }

        public void ResetSelectedNodeIndex()
        {
            selectedNode = -1; // Set the selected node to -1
            nodeManager.SelectedNodeObject.SetActive(false); // Deactivate the selected node prefab
            OnNodeSelected?.Invoke(selectedNode); // Invoke the node selected event
        }

        public void Unsubscribe()
        {
            manager.Input_SO.OnInteractAction -= SetSelectedNodeIndex; // Unsubscribe from the interact action
            manager.FlagManager.OnFlagPlaced -= ResetSelectedNodeIndex; // Unsubscribe from the flag placed action
            manager.FlagManager.OnFlagRemoved -= ResetSelectedNodeIndex; // Unsubscribe from the flag removed action
        }
    }
}