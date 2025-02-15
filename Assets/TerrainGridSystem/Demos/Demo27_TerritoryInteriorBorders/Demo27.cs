using UnityEngine;
using TGS;

namespace TGSDemos {

    public class Demo27 : MonoBehaviour {

        TerrainGridSystem tgs;

        void Start() {
            tgs = TerrainGridSystem.instance;

            tgs.TerritoryHideInteriorBorders();
            tgs.OnTerritoryEnter += Tgs_OnTerritoryEnter;
            tgs.OnTerritoryExit += Tgs_OnTerritoryExit;

            tgs.OnCellClick += Tgs_OnCellClick;
        }

        int side = 0;
        private void Tgs_OnCellClick(TerrainGridSystem tgs, int cellIndex, int buttonIndex) {
            Debug.Log(cellIndex);
            tgs.CellSetColor(cellIndex, Color.gray);

            // Draw a segment of the border with each click
            tgs.DrawLine(cellIndex, (CELL_SIDE)side, Color.blue, 4);
            side++;
        }

        private void Tgs_OnTerritoryExit(TerrainGridSystem tgs, int territoryIndex) {
            tgs.TerritoryHideInteriorBorders();
        }

        private void Tgs_OnTerritoryEnter(TerrainGridSystem tgs, int territoryIndex) {

            tgs.TerritoryHideInteriorBorders();
            Territory territory = tgs.territories[territoryIndex];
            Color darkerColor = territory.fillColor;
            darkerColor.r *= 0.37f;
            darkerColor.g *= 0.85f;
            darkerColor.b *= 0.35f;
            tgs.TerritoryDrawInteriorBorder(territory, color: darkerColor, secondColor: territory.fillColor, animationSpeed: 2f, thickness: 5f);
        }

    }

}