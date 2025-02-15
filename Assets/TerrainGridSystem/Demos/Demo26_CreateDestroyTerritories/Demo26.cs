using System.Collections.Generic;
using UnityEngine;
using TGS;

namespace TGSDemos {


    public class Demo26 : MonoBehaviour {
        TerrainGridSystem tgs;

        void Start() {
            tgs = TerrainGridSystem.instance;

            tgs.OnCellClick += OnCellClick;
            tgs.OnTerritoryClick += OnTerritoryClick;
        }

        private void OnTerritoryClick(TerrainGridSystem tgs, int territoryIndex, int regionIndex, int buttonIndex) {
            if (buttonIndex == 1) {
                tgs.TerritoryDestroy(territoryIndex);
            }
        }

        private void OnCellClick(TerrainGridSystem tgs, int cellIndex, int buttonIndex) {
            if (buttonIndex != 0) return;

            // check if current cell belongs to a territory
            Cell cell = tgs.cells[cellIndex];
            if (cell.territoryIndex != -1) return;

            // check if clicked cell is adjacent to territory
            List<Cell> neighbours = tgs.CellGetNeighbours(cellIndex);
            foreach (var neighbour in neighbours) {
                if (neighbour.territoryIndex != -1) {
                    // found an adjacent territory, make it grow with this new cell
                    tgs.CellSetTerritory(cellIndex, neighbour.territoryIndex);
                    return;
                }
            }

            // create territory with one cell at click position
            Territory territory = tgs.TerritoryCreate(cellIndex);
            territory.fillColor = new Color(Random.value * 0.75f + 0.25f, Random.value * 0.75f + 0.25f, Random.value * 0.75f + 0.25f, 1f);

        }
    }
}
