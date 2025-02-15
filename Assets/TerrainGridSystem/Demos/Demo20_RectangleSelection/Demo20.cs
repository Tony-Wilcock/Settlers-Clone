using System.Collections.Generic;
using UnityEngine;
using TGS;

namespace TGSDemos {

    public class Demo20 : MonoBehaviour {

        public Sprite rectangleTexture;

        TerrainGridSystem tgs;

        void Start() {
            tgs = TerrainGridSystem.instance;
            tgs.enableRectangleSelection = true;
            tgs.rectangleSelection.rectangleSprite = rectangleTexture;
            tgs.OnRectangleSelection += SelectCells;
            tgs.OnRectangleDrag += HighlightRectangleArea;

            tgs.OnCellClick += (grid, cellIndex, buttonIndex) => PaintCell(cellIndex);
        }

        void PaintCell(int cellIndex) {
            tgs.CellSetColor(cellIndex, Color.red);
        }

        void SelectCells(TerrainGridSystem grid, Vector2 localPosStart, Vector2 localPosEnd) {
            List<int> cells = new List<int>();
            tgs.CellGetInArea(localPosStart, localPosEnd, cells);

            // Hides all surfaces (and clear cache)
            tgs.ClearAll();

            // Color selection
            tgs.CellSetColor(cells, Color.yellow);
        }

        void HighlightRectangleArea(TerrainGridSystem grid, Vector2 localPosStart, Vector2 localPosEnd) {

            if (!Input.GetKey(KeyCode.LeftShift)) return;

            List<int> cells = new List<int>();
            tgs.CellGetInArea(localPosStart, localPosEnd, cells);

            // Hides all surfaces (but keep them in cache)
            tgs.HideAll();

            // Color current selection
            tgs.CellSetColor(cells, new Color(0, 1, 0, 0.5f));
        }

    }

}