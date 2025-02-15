using UnityEngine;
using System.Collections.Generic;
using TGS;

namespace TGSDemos {


    /// <summary>
    /// Marks random cells as unpassable
    /// </summary>
    public class Obstacles : MonoBehaviour {

        TerrainGridSystem tgs;

        // Use this for initialization
        void Start() {
            tgs = TerrainGridSystem.instance;
            Color blockedColor = new Color(0.2f, 0.2f, 0.2f, 1f);
            for (int k = 0; k < 1500; k++) {
                int cellIndex = Random.Range(0, tgs.cellCount);
                tgs.CellToggleRegionSurface(cellIndex, true, blockedColor);
                tgs.CellSetCanCross(cellIndex, false);
            }

            // show neigbhour cells around clicked cell
            tgs.OnCellClick += Tgs_OnCellClick;
        }

        private void Tgs_OnCellClick(TerrainGridSystem tgs, int cellIndex, int buttonIndex) {
            List<int> indices = tgs.CellGetNeighbours(cellIndex, 1, canCrossCheckType: CanCrossCheckType.IgnoreCanCrossCheckOnAllCells);
            tgs.CellFlash(indices, Color.yellow);
        }
    }
}




