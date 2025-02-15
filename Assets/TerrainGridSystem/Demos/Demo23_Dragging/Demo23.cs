using UnityEngine;
using UnityEngine.UI;

using TGS;

namespace TGSDemos {

    public class Demo23 : MonoBehaviour {

        TerrainGridSystem tgs;
        GUIStyle labelStyle;
        public Text status;

        void Start() {
            // setup GUI styles
            labelStyle = new GUIStyle();
            labelStyle.alignment = TextAnchor.MiddleCenter;
            labelStyle.normal.textColor = Color.black;

            // Get a reference to Terrain Grid System's API
            tgs = TerrainGridSystem.instance;

            // Color some random cells
            for (int k = 0; k < 10; k++) {
                int cellIndex = Random.Range(0, tgs.cellCount);
                tgs.CellSetColor(cellIndex, Random.ColorHSV());
            }

            // Setup drag & drop events
            tgs.OnCellDragStart += Tgs_OnCellDragStart;
            tgs.OnCellDrag += Tgs_OnCellDrag;
            tgs.OnCellDragEnd += Tgs_OnCellDragEnd;

        }

        private void Tgs_OnCellDragStart(TerrainGridSystem tgs, int cellIndex) {
            status.text = "Drag starts at cell " + cellIndex;
        }

        private void Tgs_OnCellDrag(TerrainGridSystem tgs, int cellOriginIndex, int cellTargetIndex) {
            if (cellTargetIndex != cellOriginIndex) {
                status.text = "Dragging from cell " + cellOriginIndex + ". Current cell: " + cellTargetIndex;
            }
        }

        private void Tgs_OnCellDragEnd(TerrainGridSystem tgs, int cellOriginIndex, int cellTargetIndex) {
            if (cellTargetIndex < 0) {
                status.text = "Dragged out of grid";
            } else {
                status.text = "Drag ends at cell " + cellTargetIndex;
                Color sourceColor = tgs.CellGetColor(cellOriginIndex);
                Color targetColor = tgs.CellGetColor(cellTargetIndex);
                tgs.CellSetColor(cellOriginIndex, targetColor);
                tgs.CellSetColor(cellTargetIndex, sourceColor);
            }
        }

    }
}
