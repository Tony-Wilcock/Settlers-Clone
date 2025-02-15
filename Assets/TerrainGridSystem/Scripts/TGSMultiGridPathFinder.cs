using UnityEngine;
using System.Collections.Generic;

namespace TGS {
    
    public class TGSMultiGridPathFinder {
        Dictionary<(TerrainGridSystem, int, TerrainGridSystem, int), float> connections = 
            new Dictionary<(TerrainGridSystem, int, TerrainGridSystem, int), float>();
        
        List<TerrainGridSystem> grids;

        public TGSMultiGridPathFinder(TerrainGridSystem[] grids) {
            this.grids = new List<TerrainGridSystem>(grids);
        }

        /// <summary>
        /// Adds a connection between two cells in different grids
        /// </summary>
        public void AddConnection(TerrainGridSystem grid1, int cellIndex1, TerrainGridSystem grid2, int cellIndex2, float cost = 1f) {
            connections[(grid1, cellIndex1, grid2, cellIndex2)] = cost;
            connections[(grid2, cellIndex2, grid1, cellIndex1)] = cost;
        }

        /// <summary>
        /// Adds a connection between two world positions. Returns true if a valid connection was created.
        /// </summary>
        public bool AddConnection(Vector3 pointA, Vector3 pointB, float cost = 1f) {
            TerrainGridSystem grid1 = null;
            TerrainGridSystem grid2 = null;
            Cell cell1 = null;
            Cell cell2 = null;

            foreach (TerrainGridSystem grid in grids) {
                if (grid1 == null) {
                    cell1 = grid.CellGetAtWorldPosition(pointA);
                    if (cell1 != null) {
                        grid1 = grid;
                    }
                }
                if (grid2 == null) {
                    cell2 = grid.CellGetAtWorldPosition(pointB);
                    if (cell2 != null) {
                        grid2 = grid;
                    }
                }
                if (grid1 != null && grid2 != null) {
                    break;
                }
            }

            if (grid1 != null && grid2 != null && grid1 != grid2 && cell1 != null && cell2 != null) {
                AddConnection(grid1, cell1.index, grid2, cell2.index, cost);
                return true;
            }

            return false;
        }


        /// <summary>
        /// Finds a path between two cells that can be in different grids with specified options
        /// </summary>
        public int FindPath(TerrainGridSystem startGrid, Cell startCell, 
                           TerrainGridSystem endGrid, Cell endCell,
                           List<(TerrainGridSystem grid, int cellIndex)> path,
                           out float totalCost,
                           FindPathOptions options = null) {
            totalCost = 0;
            path.Clear();

            // If cells are in same grid, use that grid's pathfinding directly
            if (startGrid == endGrid) {
                List<int> indices = new List<int>();
                startGrid.FindPath(startCell.index, endCell.index, indices, out totalCost, options);
                foreach (int index in indices) {
                    path.Add((startGrid, index));
                }
                return path.Count;
            }

            // Track visited states to avoid cycles
            var visited = new HashSet<(TerrainGridSystem grid, int cellIndex)>();
            var bestPath = new List<(TerrainGridSystem grid, int cellIndex)>();
            float bestCost = float.MaxValue;

            // Try each connection from start grid
            foreach (var conn in connections) {
                var (fromGrid, fromCellIndex, toGrid, toCellIndex) = conn.Key;
                
                if (fromGrid == startGrid) {
                    visited.Clear();
                    var currentPath = new List<(TerrainGridSystem grid, int cellIndex)>();
                    float currentCost = 0;

                    // First find path to connection point in start grid
                    List<int> pathToConnection = new List<int>();
                    startGrid.FindPath(startCell.index, fromCellIndex, pathToConnection, out float costToConnection, options);

                    if (pathToConnection.Count > 0) {
                        // Add path to connection point
                        foreach (int index in pathToConnection) {
                            currentPath.Add((startGrid, index));
                        }
                        currentCost += costToConnection;

                        // Check if we've exceeded maxSearchCost
                        if (options != null && options.maxSearchCost > 0 && currentCost > options.maxSearchCost) continue;

                        // Add connection cost
                        currentCost += connections[conn.Key];

                        // Now find path from connection to end
                        if (toGrid == endGrid) {
                            // Direct connection to destination grid
                            List<int> pathFromConnection = new List<int>();
                            toGrid.FindPath(toCellIndex, endCell.index, pathFromConnection, out float costFromConnection, options);

                            if (pathFromConnection.Count > 0) {
                                foreach (int index in pathFromConnection) {
                                    currentPath.Add((toGrid, index));
                                }
                                currentCost += costFromConnection;

                                // Check if this is the best path so far
                                if (currentCost < bestCost) {
                                    bestCost = currentCost;
                                    bestPath = new List<(TerrainGridSystem grid, int cellIndex)>(currentPath);
                                }
                            }
                        } else {
                            // Need to find path through intermediate grid
                            visited.Add((fromGrid, fromCellIndex));
                            visited.Add((toGrid, toCellIndex));

                            // Recursively find path from this point
                            List<(TerrainGridSystem grid, int cellIndex)> remainingPath = new List<(TerrainGridSystem grid, int cellIndex)>();
                            FindPath(toGrid, toGrid.cells[toCellIndex], endGrid, endCell, remainingPath, out float remainingCost, options);

                            if (remainingPath.Count > 0) {
                                currentPath.AddRange(remainingPath);
                                currentCost += remainingCost;
                                if (currentCost < bestCost) {
                                    bestCost = currentCost;
                                    bestPath = new List<(TerrainGridSystem grid, int cellIndex)>(currentPath);
                                }
                            }
                        }
                    }
                }
            }

            if (bestPath.Count > 0) {
                path.AddRange(bestPath);
                totalCost = bestCost;
                return path.Count;
            }

            return 0;
        }

        public void ClearConnections() {
            connections.Clear();
        }
    }
} 