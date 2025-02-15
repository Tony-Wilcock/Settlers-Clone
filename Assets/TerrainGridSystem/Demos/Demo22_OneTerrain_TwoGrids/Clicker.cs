using UnityEngine;
using TGS;

namespace TGSDemos {

    public class Clicker : MonoBehaviour {
        TerrainGridSystem[] grids;

        void Start() {
            grids = Misc.FindObjectsOfType<TerrainGridSystem>();
            foreach (TerrainGridSystem grid in grids) {
                grid.OnCellClick += OnCellClick;
            }

        }

        private void OnCellClick(TerrainGridSystem grid, int cellIndex, int buttonIndex) {
            Debug.Log("Clicked on Grid " + grid.name + " on cell " + cellIndex);
        }

    }

}