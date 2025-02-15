using UnityEngine;
using System.Collections.Generic;
using TGS;

namespace TGSDemos {

    public class Demo11 : MonoBehaviour {

        TerrainGridSystem tgs;
        readonly List<int> cellIndices = new List<int>();
        int currentTerritoryIndex = -1;
        int currentRegionIndex;

        void Start() {

            // Get a reference to Terrain Grid System's API
            tgs = TerrainGridSystem.instance;

            // listen to cell click event
            tgs.OnCellClick += Tgs_OnCellClick;

        }


        private void Tgs_OnCellClick(TerrainGridSystem tgs, int cellIndex, int buttonIndex) {

            // get the territory index of the clicked cell
            int territoryIndex = tgs.CellGetTerritoryIndex(cellIndex);
            int regionIndex = tgs.CellGetTerritoryRegionIndex(cellIndex);

            // if there's no territory selected, select it
            if (currentTerritoryIndex < 0) {
                SelectTerritoryRegion(territoryIndex, regionIndex);
                return;
            }

            // if same territory, unselect territory
            if (territoryIndex == currentTerritoryIndex && regionIndex == currentRegionIndex) {
                DeselectTerritory();
                return;
            }

            // if right button is pressed and the cell is on the frontier, add it to the currently selected territory
            if (buttonIndex == 1) {
                tgs.TerritoryGetAdjacentCells(currentTerritoryIndex, cellIndices, currentRegionIndex);
                if (cellIndices.Contains(cellIndex)) {
                    tgs.CellSetTerritory(cellIndex, currentTerritoryIndex);
                    UpdateBorders();
                    return;
                }
            }

            // select the other territory
            SelectTerritoryRegion(territoryIndex, regionIndex);
        }

        void DeselectTerritory() {
            tgs.TerritoryHideInteriorBorders();
            currentTerritoryIndex = -1;
        }

        void SelectTerritoryRegion(int territoryIndex, int regionIndex) {

            currentTerritoryIndex = territoryIndex;
            currentRegionIndex = regionIndex;

            UpdateBorders();
        }

        void UpdateBorders() { 

            // clear existing interior borders
            tgs.TerritoryHideInteriorBorders();

            // draw the interior border for the current territory
            tgs.TerritoryDrawInteriorBorder(currentTerritoryIndex, padding: 1f, thickness: 5f, color: Color.gray, regionIndex: currentRegionIndex, animationSpeed: 0, includeEnclaves: true);

            // flash frontier cells
            tgs.TerritoryGetAdjacentCells(currentTerritoryIndex, cellIndices, currentRegionIndex);
            tgs.CellFlash(cellIndices, Color.red, 2f);

        }


    }
}
