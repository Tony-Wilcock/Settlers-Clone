using UnityEngine;
using System.Collections.Generic;
using TGS;

namespace TGS.Demos {
    
    public class Demo33 : MonoBehaviour {
        [System.Serializable]
        public class GridConnection {
            public Transform pointA;
            public Transform pointB;
        }

        public List<GridConnection> gridConnections;
        public float connectionCost = 1f;

        TerrainGridSystem[] grids;
        TGSMultiGridPathFinder pathFinder;
        Cell startCell;
        TerrainGridSystem startGrid;
        bool isSelectingStart;
        int cellStartIndex;
        List<(TerrainGridSystem grid, int cellIndex)> currentPath;

        void Start () {
            // Get all grid systems in the scene
            grids = Misc.FindObjectsOfType<TerrainGridSystem>();

            // Create pathfinder
            pathFinder = new TGSMultiGridPathFinder(grids);
            currentPath = new List<(TerrainGridSystem grid, int cellIndex)>();

            // Setup the multi-grid connections
            foreach (var connection in gridConnections) {
                bool success = pathFinder.AddConnection(connection.pointA.position, connection.pointB.position, connectionCost);
                if (!success) {
                    Debug.LogWarning($"Failed to create connection between points {connection.pointA.position} and {connection.pointB.position}. Make sure both points are over valid grid cells.");
                }
            }

            isSelectingStart = true;

            // Hook into cell click event to handle path building
            foreach (var grid in grids) {
                grid.OnCellClick += (g, cellIndex, buttonIndex) => BuildPath(g, cellIndex);
            }
        }

        void BuildPath(TerrainGridSystem grid, int clickedCellIndex) {
            Cell cell = grid.cells[clickedCellIndex];
            Debug.Log("Clicked on cell# " + clickedCellIndex + " (row=" + cell.row + ", col=" + cell.column + ")");

            if (isSelectingStart) {
                // Selects start cell
                startCell = cell;
                startGrid = grid;
                cellStartIndex = clickedCellIndex;
                grid.CellToggleRegionSurface(cellStartIndex, true, Color.yellow);
            } else {
                // Clicked on the end cell, then show the path
                // First clear color of start cell
                startGrid.CellToggleRegionSurface(cellStartIndex, false, Color.white);

                // Get Path
                currentPath.Clear();
                int pathCount = pathFinder.FindPath(startGrid, startCell, grid, cell, currentPath, out float totalCost);

                // Color the path
                if (pathCount > 0) {
                    foreach (var (pathGrid, cellIndex) in currentPath) {
                        pathGrid.CellFadeOut(cellIndex, Color.green, 1f);
                    }
                }
            }
            isSelectingStart = !isSelectingStart;
        }
    }
}