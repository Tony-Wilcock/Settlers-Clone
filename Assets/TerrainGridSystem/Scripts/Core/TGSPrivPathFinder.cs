using UnityEngine;
using TGS.PathFinding;

namespace TGS
{

    public partial class TerrainGridSystem : MonoBehaviour {

        IPathFinder finder;
        bool needRefreshRouteMatrix = true;

        bool clearanceComputed;
        int clearanceCellGroupMask;

        void ComputeRouteMatrix() {

            // prepare matrix
            if (!needRefreshRouteMatrix)
                return;

            needRefreshRouteMatrix = false;

            if (finder == null) {
                if (_gridTopology == GridTopology.Irregular) {
                    finder = new PathFinderFastIrregular(cells.ToArray());
                } else {
                    if ((_cellColumnCount & (_cellColumnCount - 1)) == 0) { // is power of two?
                        finder = new PathFinderFast(cells.ToArray(), _cellColumnCount, _cellRowCount);
                    } else {
                        finder = new PathFinderFastNonSQR(cells.ToArray(), _cellColumnCount, _cellRowCount);
                    }
                }
            } else {
                finder.SetCalcMatrix(cells.ToArray());
            }
        }


        /// <summary>
        /// Updates clearance data for each cell. Clearance is used with FindPath method (minClearance parameter) and it's used to specify the minimum width of a path
        /// </summary>
        public void ComputeClearance(int cellGroupMask) {

            if (clearanceComputed && clearanceCellGroupMask == cellGroupMask) return;

            clearanceComputed = true;
            clearanceCellGroupMask = cellGroupMask;

            int cellsCount = cells.Count;
            // clear clearance
            for (int k = 0; k < cellsCount; k++) {
                cells[k].clearance = 0;
            }

            int maxDim = Mathf.Max(rowCount, columnCount);
            // uses true clearance
            for (int j = rowCount - 1; j >= 0; j--) {
                for (int k = 0; k < columnCount; k++) {
                    Cell cell = CellGetAtPosition(k, j);
                    if (cell == null) continue;
                    for (int maxClearance = 2; maxClearance < maxDim; maxClearance++) {
                        bool blocked = false;
                        int maxIter = maxClearance * maxClearance;
                        for (int i = 1; i < maxIter; i++) {
                            int nj = j - (i / maxClearance);
                            int nk = k + (i % maxClearance);
                            if (nj < 0 || nk >= columnCount) {
                                blocked = true;
                                break;
                            }
                            Cell neighbour = CellGetAtPosition(nk, nj);
                            if (neighbour == null || (neighbour.group & cellGroupMask) == 0 || !neighbour.canCross) {
                                blocked = true;
                                break;
                            }
                        }
                        if (blocked) {
                            cell.clearance = (byte)(maxClearance - 1);
                            break;
                        }
                    }
                }
            }
        }
    }

}