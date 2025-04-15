using UnityEngine;

namespace PunkyFruitBat
{
    public class HexColorChanger : MonoBehaviour
    {
        private HexGridManager hexGridManager;

        void Start()
        {
            // Get references (ensure HexGridManager is ready)
            hexGridManager = HexGridManager.Instance;

            if (hexGridManager == null)
            {
                Debug.LogError("HexGridManager not found!");
                this.enabled = false; // Disable script if manager is missing
            }
        }

        void Update()
        {
            // Check for left mouse button click
            if (Input.GetMouseButtonDown(0)) // 0 is the left mouse button
            {
                HandleClick();
            }

            // Example: Change color using nearest node and a key press (like 'Y')
            if (Input.GetKeyDown(KeyCode.Y))
            {
                int nearestNode = hexGridManager.LiveNode;
                if (nearestNode >= 0 && hexGridManager.EditableVerticesIndices[nearestNode].IsCentreNode)
                {
                    //Debug.Log($"Changing color of hex centered at node {nearestNode} to Yellow.");
                    hexGridManager.VertexManipulator.SetHexColor(nearestNode, Color.yellow);
                }
            }
            // Example: Change back to white using 'U' key
            if (Input.GetKeyDown(KeyCode.U))
            {
                int nearestNode = hexGridManager.LiveNode;
                if (nearestNode >= 0 && hexGridManager.EditableVerticesIndices[nearestNode].IsCentreNode)
                {
                    //Debug.Log($"Changing color of hex centered at node {nearestNode} to White.");
                    hexGridManager.VertexManipulator.SetHexColor(nearestNode, Color.white);
                }
            }
        }

        void HandleClick()
        {
            // Use the existing LiveNode logic from HexGridManager/NodeManager
            int targetNodeIndex = hexGridManager.LiveNode;

            if (targetNodeIndex >= 0) // Check if a valid node is under the mouse
            {
                // Optional: Check if the clicked node is actually a hex center
                if (hexGridManager.EditableVerticesIndices != null &&
                    targetNodeIndex < hexGridManager.EditableVerticesIndices.Length &&
                    hexGridManager.EditableVerticesIndices[targetNodeIndex] != null &&
                    hexGridManager.EditableVerticesIndices[targetNodeIndex].IsCentreNode)
                {
                    //Debug.Log($"Clicked on hex center node: {targetNodeIndex}. Setting color to Yellow.");
                    // Call the method in VertexManipulator to set the color
                    hexGridManager.VertexManipulator.SetHexColor(targetNodeIndex, Color.yellow);
                }
                else
                {
                    // Debug.Log($"Clicked on node {targetNodeIndex}, but it's not a hex center.");
                }
            }
            // else { Debug.Log("Clicked, but no node found nearby."); } // Optional
        }
    }
}
