using UnityEngine;

namespace PunkyFruitBat
{
    public class VertexHeightChanger : MonoBehaviour
    {
        HexGridManager hexGridManager;
        VertexManipulator vertexManipulator;

        private void Awake()
        {
            hexGridManager = HexGridManager.Instance;
            vertexManipulator = hexGridManager.VertexManipulator;
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                vertexManipulator.AdjustVertexHeight(hexGridManager.LiveNode, hexGridManager.Settings.movementAmount);
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                vertexManipulator.AdjustVertexHeight(hexGridManager.LiveNode, -hexGridManager.Settings.movementAmount);
            }
        }
    }
}
