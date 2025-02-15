using UnityEngine;
using System.Collections.Generic;
using TGS.PathFinding;

namespace TGS {

    public enum CanCrossCheckType {
        Default = 0,
        IgnoreCanCrossCheckOnAllCells = 1,
        IgnoreCanCrossCheckOnStartAndEndCells = 2,
        IgnoreCanCrossCheckOnStartCell = 3,
        IgnoreCanCrossCheckOnEndCell = 4,
        IgnoreCanCrossCheckOnAllCellsExceptStartAndEndCells = 5,
    }

    public class FindPathOptions {
        public float maxSearchCost;
        public int maxSteps;
        public int cellGroupMask = -1;
        public bool cellGroupMaskExactComparison;
        public CanCrossCheckType canCrossCheckType = CanCrossCheckType.Default;
        public bool ignoreCellCosts;
        public bool includeInvisibleCells = true;
        public int minClearance = 1;
        public float maxCellCrossCost = float.MaxValue;
        public PathFindingEvent OnPathFindingCrossCell;
        public object OnPathFindingCrossCellData;
    }


    public partial class TerrainGridSystem : MonoBehaviour {


        [SerializeField]
        HeuristicFormula
            _pathFindingHeuristicFormula = HeuristicFormula.EuclideanNoSQR;

        /// <summary>
        /// The path finding heuristic formula to estimate distance from current position to destination
        /// </summary>
        public PathFinding.HeuristicFormula pathFindingHeuristicFormula {
            get { return _pathFindingHeuristicFormula; }
            set {
                if (value != _pathFindingHeuristicFormula) {
                    _pathFindingHeuristicFormula = value;
                    isDirty = true;
                }
            }
        }

        [SerializeField]
        int
            _pathFindingMaxSteps = 2000;

        /// <summary>
        /// The maximum number of steps that a path can return.
        /// </summary>
        public int pathFindingMaxSteps {
            get { return _pathFindingMaxSteps; }
            set {
                if (value != _pathFindingMaxSteps) {
                    _pathFindingMaxSteps = value;
                    isDirty = true;
                }
            }
        }

        [SerializeField]
        float
            _pathFindingMaxCost = 200000;

        /// <summary>
        /// The maximum search cost of the path finding execution.
        /// </summary>
        public float pathFindingMaxCost {
            get { return _pathFindingMaxCost; }
            set {
                if (value != _pathFindingMaxCost) {
                    _pathFindingMaxCost = value;
                    isDirty = true;
                }
            }
        }

        [SerializeField]
        bool
            _pathFindingUseDiagonals = true;

        /// <summary>
        /// If path can include diagonals between cells
        /// </summary>
        public bool pathFindingUseDiagonals {
            get { return _pathFindingUseDiagonals; }
            set {
                if (value != _pathFindingUseDiagonals) {
                    _pathFindingUseDiagonals = value;
                    isDirty = true;
                }
            }
        }

        [SerializeField]
        float
            _pathFindingHeavyDiagonalsCost = 1.4f;

        /// <summary>
        /// The cost for crossing diagonals.
        /// </summary>
        public float pathFindingHeavyDiagonalsCost {
            get { return _pathFindingHeavyDiagonalsCost; }
            set {
                if (value != _pathFindingHeavyDiagonalsCost) {
                    _pathFindingHeavyDiagonalsCost = value;
                    isDirty = true;
                }
            }
        }



        [SerializeField]
        bool
            _pathFindingIncludeInvisibleCells = true;

        /// <summary>
        /// If true, the path will include invisible cells as well.
        /// </summary>
        public bool pathFindingIncludeInvisibleCells {
            get { return _pathFindingIncludeInvisibleCells; }
            set {
                if (value != _pathFindingIncludeInvisibleCells) {
                    _pathFindingIncludeInvisibleCells = value;
                    isDirty = true;
                }
            }
        }


        #region Public Path Finding functions

        readonly FindPathOptions defaultOptions = new();

        /// <summary>
        /// Returns an optimal path from startPosition to endPosition with options.
        /// </summary>
        /// <returns>The route consisting of a list of cell indexes.</returns>
        /// <param name="maxSearchCost">Maximum search cost for the path finding algorithm. A value of 0 will use the global default defined by pathFindingMaxCost</param>
        /// <param name="maxSteps">Maximum steps for the path. A value of 0 will use the global default defined by pathFindingMaxSteps</param>
        /// <param name="maxCellCrossCost">The maximum allowed crossing cost of any cell</param>
        public List<int> FindPath(int cellIndexStart, int cellIndexEnd, float maxSearchCost = 0, int maxSteps = 0, int cellGroupMask = -1, CanCrossCheckType canCrossCheckType = CanCrossCheckType.Default, bool ignoreCellCosts = false, bool includeInvisibleCells = true, int minClearance = 1, float maxCellCrossCost = float.MaxValue, bool cellGroupMaskExactComparison = false) {
            return FindPath(cellIndexStart, cellIndexEnd, out _, maxSearchCost, maxSteps, cellGroupMask, canCrossCheckType, ignoreCellCosts, includeInvisibleCells, minClearance, maxCellCrossCost, cellGroupMaskExactComparison);
        }

        /// <summary>
		/// Returns an optimal path from startPosition to endPosition with options.
		/// </summary>
		/// <returns>The route consisting of a list of cell indexes.</returns>
		/// <param name="totalCost">The total accumulated cost for the path</param>
		/// <param name="maxSearchCost">Maximum search cost for the path finding algorithm. A value of 0 will use the global default defined by pathFindingMaxCost</param>
		/// <param name="maxSteps">Maximum steps for the path. A value of 0 will use the global default defined by pathFindingMaxSteps</param>
        /// <param name="maxCellCrossCost">The maximum allowed crossing cost of any cell</param>
		public List<int> FindPath(int cellIndexStart, int cellIndexEnd, out float totalCost, float maxSearchCost = 0, int maxSteps = 0, int cellGroupMask = -1, CanCrossCheckType canCrossCheckType = CanCrossCheckType.Default, bool ignoreCellCosts = false, bool includeInvisibleCells = true, int minClearance = 1, float maxCellCrossCost = float.MaxValue, bool cellGroupMaskExactComparison = false) {
            List<int> results = new();
            FindPath(cellIndexStart, cellIndexEnd, results, out totalCost, maxSearchCost, maxSteps, cellGroupMask, canCrossCheckType, ignoreCellCosts, includeInvisibleCells, minClearance, maxCellCrossCost, cellGroupMaskExactComparison);
            return results;
        }


        /// <summary>
        /// Returns an optimal path from startPosition to endPosition with options.
        /// </summary>
        /// <returns>The route consisting of a list of cell indexes.</returns>
        /// <param name="cellIndices">User provided list to fill with path indices</param>
        /// <param name="totalCost">The total accumulated cost for the path</param>
        /// <param name="maxSearchCost">Maximum search cost for the path finding algorithm. A value of 0 will use the global default defined by pathFindingMaxCost</param>
        /// <param name="maxSteps">Maximum steps for the path. A value of 0 will use the global default defined by pathFindingMaxSteps</param>
        /// <param name="maxCellCrossCost">The maximum allowed crossing cost of any cell</param>
        public int FindPath(int cellIndexStart, int cellIndexEnd, List<int> cellIndices, out float totalCost, float maxSearchCost = 0, int maxSteps = 0, int cellGroupMask = -1, CanCrossCheckType canCrossCheckType = CanCrossCheckType.Default, bool ignoreCellCosts = false, bool includeInvisibleCells = true, int minClearance = 1, float maxCellCrossCost = float.MaxValue, bool cellGroupMaskExactComparison = false) {
            defaultOptions.maxSearchCost = maxSearchCost;
            defaultOptions.maxSteps = maxSteps;
            defaultOptions.cellGroupMask = cellGroupMask;
            defaultOptions.cellGroupMaskExactComparison = cellGroupMaskExactComparison;
            defaultOptions.canCrossCheckType = canCrossCheckType;
            defaultOptions.ignoreCellCosts = ignoreCellCosts;
            defaultOptions.includeInvisibleCells = includeInvisibleCells;
            defaultOptions.minClearance = minClearance;
            defaultOptions.maxCellCrossCost = maxCellCrossCost;
            defaultOptions.OnPathFindingCrossCell = null;
            defaultOptions.OnPathFindingCrossCellData = null;
            return FindPath(cellIndexStart, cellIndexEnd, cellIndices, out totalCost, defaultOptions);
        }

        public FindPathOptions GetDefaultOptions() {
            defaultOptions.maxSearchCost = 0;
            defaultOptions.maxSteps = 0;
            defaultOptions.cellGroupMask = -1;
            defaultOptions.cellGroupMaskExactComparison = false;
            defaultOptions.canCrossCheckType = CanCrossCheckType.Default;
            defaultOptions.ignoreCellCosts = false;
            defaultOptions.includeInvisibleCells = true;
            defaultOptions.minClearance = 1;
            defaultOptions.maxCellCrossCost = float.MaxValue;
            defaultOptions.OnPathFindingCrossCell = null;
            defaultOptions.OnPathFindingCrossCellData = null;            
            return defaultOptions;
        }


        /// <summary>
        /// Returns an optimal path from startPosition to endPosition with options.
        /// </summary>
        /// <returns>The route consisting of a list of cell indexes.</returns>
        /// <param name="cellIndices">User provided list to fill with path indices</param>
        /// <param name="totalCost">The total accumulated cost for the path</param>
        /// <param name="options">A FindPathOptions object that contains options for the path finding execution</param>
        public int FindPath(int cellIndexStart, int cellIndexEnd, List<int> cellIndices, out float totalCost, FindPathOptions options) {
            totalCost = 0;
            if (cellIndices == null) return 0;
            if (options == null) options = GetDefaultOptions();
            cellIndices.Clear();
            if (cellIndexStart == cellIndexEnd || cellIndexStart < 0 || cellIndexEnd < 0 || cellIndexStart >= cells.Count || cellIndexEnd >= cells.Count) return 0;
            Cell startCell = cells[cellIndexStart];
            Cell endCell = cells[cellIndexEnd];
            if (startCell == null || endCell == null) return 0;
            bool startCellCanCross = startCell.canCross;
            bool endCellCanCross = endCell.canCross;
            if (options.canCrossCheckType != CanCrossCheckType.IgnoreCanCrossCheckOnAllCells) {
                switch (options.canCrossCheckType) {
                    case CanCrossCheckType.IgnoreCanCrossCheckOnStartAndEndCells:
                        startCell.canCross = endCell.canCross = true;
                        break;
                    case CanCrossCheckType.IgnoreCanCrossCheckOnStartCell:
                        if (!endCell.canCross) return 0;
                        startCell.canCross = true;
                        break;
                    case CanCrossCheckType.IgnoreCanCrossCheckOnEndCell:
                        if (!startCell.canCross) return 0;
                        endCell.canCross = true;
                        break;
                    default:
                        if (!startCell.canCross || !endCell.canCross)
                            return 0;
                        break;
                }
            }
            if (options.minClearance > 1 && (needRefreshRouteMatrix || !clearanceComputed)) {
                ComputeClearance(options.cellGroupMask);
            }
            ComputeRouteMatrix();

            finder.Formula = _pathFindingHeuristicFormula;
            finder.MaxSteps = options.maxSteps > 0 ? options.maxSteps : _pathFindingMaxSteps;
            finder.Diagonals = _pathFindingUseDiagonals;
            finder.HeavyDiagonalsCost = _pathFindingHeavyDiagonalsCost;
            switch (_gridTopology) {
                case GridTopology.Irregular: finder.CellShape = CellType.Irregular; break;
                case GridTopology.Hexagonal: finder.CellShape = _pointyTopHexagons ? CellType.PointyTopHexagon : CellType.FlatTopHexagon; break;
                default: finder.CellShape = CellType.Box; break;
            }
            finder.MaxSearchCost = options.maxSearchCost > 0 ? options.maxSearchCost : _pathFindingMaxCost;
            finder.CellGroupMask = options.cellGroupMask;
            finder.CellGroupMaskExactComparison = options.cellGroupMaskExactComparison;
            finder.IgnoreCanCrossCheck = options.canCrossCheckType == CanCrossCheckType.IgnoreCanCrossCheckOnAllCells || options.canCrossCheckType == CanCrossCheckType.IgnoreCanCrossCheckOnAllCellsExceptStartAndEndCells;
            finder.IgnoreCellCost = options.ignoreCellCosts;
            finder.IncludeInvisibleCells = options.includeInvisibleCells;
            finder.MinClearance = options.minClearance;
            finder.MaxCellCrossCost = options.maxCellCrossCost;
            finder.Data = options.OnPathFindingCrossCellData;
            if (options.OnPathFindingCrossCell != null) {
                finder.OnCellCross = options.OnPathFindingCrossCell;
            } else if (OnPathFindingCrossCell != null) { 
                finder.OnCellCross = OnPathFindingCrossCell;
            } else {
                finder.OnCellCross = null;
            }
            List<PathFinderNode> route = finder.FindPath(this, startCell, endCell, out totalCost, _evenLayout);
            startCell.canCross = startCellCanCross;
            endCell.canCross = endCellCanCross;
            if (route != null) {
                int routeCount = route.Count;
                if (_gridTopology == GridTopology.Irregular) {
                    for (int r = routeCount - 2; r >= 0; r--) {
                        cellIndices.Add(route[r].PX);
                    }
                } else {
                    for (int r = routeCount - 2; r >= 0; r--) {
                        int cellIndex = route[r].PY * _cellColumnCount + route[r].PX;
                        cellIndices.Add(cellIndex);
                    }
                }
                cellIndices.Add(cellIndexEnd);
            } else {
                return 0;    // no route available
            }
            return cellIndices.Count;
        }


        #endregion



    }
}

