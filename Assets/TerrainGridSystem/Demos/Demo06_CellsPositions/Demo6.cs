using UnityEngine;
using TGS;

namespace TGSDemos {

    public class Demo6 : MonoBehaviour {

        public GameObject yellowSpherePrefab;
        public GameObject redSpherePrefab;
        public GameObject prefab;

        TerrainGridSystem tgs;
        GUIStyle labelStyle;

        void Start() {
            // setup GUI styles
            labelStyle = new GUIStyle();
            labelStyle.alignment = TextAnchor.MiddleCenter;
            labelStyle.normal.textColor = Color.black;

            // Get a reference to Terrain Grid System's API
            tgs = TerrainGridSystem.instance;

            // Iterate each cell and draws a sphere on the center of the cell as well as on the vertices
            for (int k = 0; k < tgs.cells.Count; k++) {

                // Add a yellow circle on the center of the cell
                Vector3 worldSpaceCenter = tgs.CellGetCentroid(k);
                GameObject yellowSphere = Instantiate(yellowSpherePrefab);
                yellowSphere.transform.position = worldSpaceCenter + Vector3.up * 0.1f;

                // For each vertex of the cell, add a red circle
                int vertexCount = tgs.CellGetVertexCount(k);
                for (int p = 0; p < vertexCount; p++) {
                    Vector3 worldSpacePosition = tgs.CellGetVertexPosition(k, p);
                    GameObject redSphere = Instantiate(redSpherePrefab);
                    redSphere.transform.position = worldSpacePosition + Vector3.up * 0.1f; // note: "up" is -z (grid is x,y) - displaced to avoid z-fighting
                }

            }

            tgs.OnCellClick += Tgs_OnCellClick;
        }

        private void Tgs_OnCellClick(TerrainGridSystem tgs, int cellIndex, int buttonIndex) {
            GameObject go = Instantiate(prefab);
            go.transform.position = tgs.CellGetPosition(cellIndex);
        }

        void OnGUI() {
            GUIResizer.AutoResize();
            GUI.Label(new Rect(10, 10, 160, 30), "Click on any cell to instantiate a prefab", labelStyle);
        }





    }
}
