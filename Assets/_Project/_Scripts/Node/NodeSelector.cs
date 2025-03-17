using System;

namespace PunkyFruitBat
{
    public class NodeSelector
    {
        public event Action<int> OnNodeSelected;

        private HexGridManager manager;

        public void Initialise(HexGridManager manager)
        {
            this.manager = manager;

            manager.Input_SO.OnInteractAction += SetCurrentVertexIndex;
        }

        private void SetCurrentVertexIndex()
        {
            if (manager.UIManager.AreAnyPanelsActive() && !manager.PathManager.IsInPathCreationMode) return; // If any panels are active, or not in Path Creation Mode return
            if (manager.NearestNode < 0) return; // If no node is selected, return
            OnNodeSelected?.Invoke(manager.NearestNode); // Invoke the OnNodeSelected event
            if (manager.PathManager.IsInPathCreationMode) // If in Path Creation Mode
            {
                manager.PathManager.TryAddPathToEndNode(manager.NearestNode); // If in Path Creation Mode, try to add the node to the path
            }
            else
            {
                manager.UIManager.ShowPanel(UIPanel.NodePanel); // If not in Path Creation Mode, show the Node Panel
                manager.UIManager.ShowPanel(UIPanel.BuildingPanel); // If not in Path Creation Mode, show the Building Panel
            }
        }

        public void Unsubscribe()
        {
            manager.Input_SO.OnInteractAction -= SetCurrentVertexIndex; // Unsubscribe from the OnMouseMoved event
        }
    }
}