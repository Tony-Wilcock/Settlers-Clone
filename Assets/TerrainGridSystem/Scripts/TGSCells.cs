using UnityEngine;
using System;
using System.Text;
using System.Collections.Generic;
using TGS.Geom;
using System.Globalization;

namespace TGS {

    public enum CELL_TEXTURE_MODE {
        InTile,
        Floating
    }

    public enum CELL_TILE_BACKGROUND_MODE {
        Opaque,
        Transparent
    }

    public partial class TerrainGridSystem : MonoBehaviour {

        /// <summary>
        /// Complete array of states and cells and the territory name they belong to.
        /// </summary>
        [NonSerialized]
        public List<Cell> cells;


        [SerializeField]
        int _numCells = 3;

        /// <summary>
        /// Gets or sets the desired number of cells in irregular topology.
        /// </summary>
        public int numCells {
            get { return _numCells; }
            set {
                if (_numCells != value) {
                    _numCells = Mathf.Clamp(value, 2, 20000);
                    needGenerateMap = true;
                    isDirty = true;
                }
            }
        }


        [SerializeField]
        bool _showCells = true;

        /// <summary>
        /// Toggle cells frontiers visibility.
        /// </summary>
        public bool showCells {
            get {
                return _showCells;
            }
            set {
                if (value != _showCells) {
                    _showCells = value;
                    isDirty = true;
                    if (cellLayer != null) {
                        cellLayer.SetActive(_showCells);
                        ClearLastOver();
                    }
                    else if (_showCells) {
                        Redraw();
                    }
                }
            }
        }


        [SerializeField]
        Vector2
            _cellSize;

        /// <summary>
        /// Gets current individual cell size or sets user defined cell size
        /// </summary>
        public Vector2 cellSize {
            get {
                return _cellSize;
            }
            set {
                if (value != _cellSize) {
                    _cellSize = value;
                    SetScaleByCellSize();
                    isDirty = true;
                    needGenerateMap = true;
                }
            }
        }


        [SerializeField]
        [ColorUsage(true, true)]
        Color
            _cellBorderColor = new Color(0, 1, 0, 1.0f);

        /// <summary>
        /// Cells border color
        /// </summary>
        public Color cellBorderColor {
            get {
                return _cellBorderColor;
            }
            set {
                if (value != _cellBorderColor) {
                    _cellBorderColor = value;
                    isDirty = true;
                    if (cellsThinMat != null && _cellBorderColor != cellsThinMat.color) {
                        cellsThinMat.color = _cellBorderColor;
                    }
                    if (cellsGeoMat != null && _cellBorderColor != cellsGeoMat.color) {
                        cellsGeoMat.color = _cellBorderColor;
                    }
                }
            }
        }

        [SerializeField]
        bool _cellCustomBorderThickness;

        /// <summary>
        /// Enables cells custom border thickness
        /// </summary>
        public bool cellCustomBorderThickness {
            get {
                return _cellCustomBorderThickness;
            }
            set {
                if (value != _cellCustomBorderThickness) {
                    _cellCustomBorderThickness = value;
                    UpdateMaterialThickness();
                    if (_showCells)
                        DrawCellBorders();
                    isDirty = true;
                }
            }
        }

        [SerializeField]
        float
            _cellBorderThickness = 1f;

        /// <summary>
        /// Cells border thickness
        /// </summary>
        public float cellBorderThickness {
            get {
                return _cellBorderThickness;
            }
            set {
                if (value != _cellBorderThickness) {
                    _cellCustomBorderThickness = true;
                    _cellBorderThickness = Mathf.Max(0.0001f, value);
                    UpdateMaterialThickness();
                    if (_showCells)
                        DrawCellBorders();
                    isDirty = true;
                }
            }
        }

        public float cellBorderAlpha {
            get {
                return _cellBorderColor.a;
            }
            set {
                if (_cellBorderColor.a != value) {
                    cellBorderColor = new Color(_cellBorderColor.r, _cellBorderColor.g, _cellBorderColor.b, Mathf.Clamp01(value));
                }
            }
        }


        [SerializeField]
        float _cellFillPadding;

        /// <summary>
        /// Padding applied to cell fill with color/texture
        /// </summary>
        public float cellFillPadding {
            get {
                return _cellFillPadding;
            }
            set {
                if (value != _cellFillPadding) {
                    _cellFillPadding = value;
                    if (_showCells) {
                        Redraw();
                    }
                    isDirty = true;
                }
            }
        }



        [SerializeField]
        CELL_TILE_BACKGROUND_MODE _cellTileBackgroundMode = CELL_TILE_BACKGROUND_MODE.Transparent;

        /// <summary>
        /// Returns the cells texture transparent mode when rendering in "Floating" mode.
        /// </summary>
        public CELL_TILE_BACKGROUND_MODE cellTileBackgroundMode {
            get {
                return _cellTileBackgroundMode;
            }
            set {
                if (value != _cellTileBackgroundMode) {
                    _cellTileBackgroundMode = value;
                    Redraw();
                    isDirty = true;
                }
            }
        }



        [SerializeField]
        CELL_TEXTURE_MODE _cellTextureMode = CELL_TEXTURE_MODE.InTile;

        /// <summary>
        /// Returns the cells texture mode. Usually texturing is done inside the cell shape or using a quad floating on top of the cell.
        /// </summary>
        public CELL_TEXTURE_MODE cellTextureMode {
            get {
                return _cellTextureMode;
            }
            set {
                if (value != _cellTextureMode) {
                    _cellTextureMode = value;
                    Redraw();
                    isDirty = true;
                }
            }
        }

        [SerializeField]
        Vector2 _cellTileScale = Misc.Vector2one;

        /// <summary>
        /// Returns the default scale for cell tiles
        /// </summary>
        public Vector2 cellTileScale {
            get {
                return _cellTileScale;
            }
            set {
                if (value != _cellTileScale) {
                    _cellTileScale = value;
                    Redraw();
                    isDirty = true;
                }
            }
        }


        [SerializeField]
        Vector2 _cellTileOffset;

        /// <summary>
        /// Returns the default scale for cell tiles
        /// </summary>
        public Vector2 cellTileOffset {
            get {
                return _cellTileOffset;
            }
            set {
                if (value != _cellTileOffset) {
                    _cellTileOffset = value;
                    Redraw();
                    isDirty = true;
                }
            }
        }

        [SerializeField]
        bool _cellAlphaClipping;

        /// <summary>
        /// Enables/disables alpha clipping
        /// </summary>
        public bool cellAlphaClipping {
            get {
                return _cellAlphaClipping;
            }
            set {
                if (value != _cellAlphaClipping) {
                    _cellAlphaClipping = value;
                    UpdatePreventOverdrawSettings();
                    isDirty = true;
                }
            }
        }

        float _cellAlphaTestThreshold = 0.5f;

        /// <summary>
        /// Sets/get cell alpha clipping threshold
        /// </summary>
        public float cellAlphaTestThreshold {
            get {
                return _cellAlphaTestThreshold;
            }
            set {
                if (value != _cellAlphaTestThreshold) {
                    _cellAlphaTestThreshold = value;
                    Redraw();
                    isDirty = true;
                }
            }
        }

        [SerializeField]
        [Tooltip("Instead of using the highlight color, use the cell color if it's set")]
        bool _cellHighlightUseCellColor;

        /// <summary>
        /// Instead of using the highlight color, use the cell color if it's set
        /// </summary>
        public bool cellHighlightUseCellColor {
            get {
                return _cellHighlightUseCellColor;
            }
            set {
                if (value != _cellHighlightUseCellColor) {
                    _cellHighlightUseCellColor = value;
                    isDirty = true;
                }
            }
        }


        [SerializeField]
        [ColorUsage(true, true)]
        [Tooltip("Fill color to use when the mouse hovers a cell's region")]
        Color
            _cellHighlightColor = new Color(1, 0, 0, 0.8f);

        /// <summary>
        /// Fill color to use when the mouse hovers a cell's region.
        /// </summary>
        public Color cellHighlightColor {
            get {
                return _cellHighlightColor;
            }
            set {
                if (value != _cellHighlightColor) {
                    _cellHighlightColor = value;
                    isDirty = true;
                    if (hudMatCellOverlay != null && _cellHighlightColor != hudMatCellOverlay.color) {
                        hudMatCellOverlay.color = _cellHighlightColor;
                    }
                    if (hudMatCellGround != null && _cellHighlightColor != hudMatCellGround.color) {
                        hudMatCellGround.color = _cellHighlightColor;
                    }
                }
            }
        }


        [SerializeField]
        [ColorUsage(true, true)]
        Color
            _cellHighlightColor2 = new Color(0, 1, 0, 0.8f);

        /// <summary>
        /// Alternate fill color to use when the mouse hovers a cell's region.
        /// </summary>
        public Color cellHighlightColor2 {
            get {
                return _cellHighlightColor2;
            }
            set {
                if (value != _cellHighlightColor2) {
                    _cellHighlightColor2 = value;
                    isDirty = true;
                    if (hudMatCellOverlay != null) {
                        hudMatCellOverlay.SetColor(ShaderParams.Color2, _cellHighlightColor2);
                    }
                    if (hudMatCellGround != null) {
                        hudMatCellGround.SetColor(ShaderParams.Color2, _cellHighlightColor2);
                    }
                }
            }
        }

        [SerializeField]
        [Range(0, 0.5f)]
        [Tooltip("Width of the highlight border(only box grids)")]
        float _cellHighlightBorderSize;

        /// <summary>
        /// Width of the highlight border (only box grids)
        /// </summary>
        public float cellHighlightBorderSize {
            get {
                return _cellHighlightBorderSize;
            }
            set {
                if (value != _cellHighlightBorderSize) {
                    _cellHighlightBorderSize = value;
                    isDirty = true;
                    UpdateMaterialHighlightBorder(hudMatCellGround);
                    UpdateMaterialHighlightBorder(hudMatCellGround);
                }
            }
        }


        [SerializeField]
        [ColorUsage(true, true)]
        Color
            _cellHighlightBorderColor = Color.white;

        /// <summary>
        /// Color for the highlight border
        /// </summary>
        public Color cellHighlightBorderColor {
            get {
                return _cellHighlightBorderColor;
            }
            set {
                if (value != _cellHighlightBorderColor) {
                    _cellHighlightBorderColor = value;
                    isDirty = true;
                    if (hudMatCellOverlay != null) {
                        hudMatCellOverlay.SetColor(ShaderParams.HighlightBorderColor, _cellHighlightBorderColor);
                    }
                    if (hudMatCellGround != null) {
                        hudMatCellGround.SetColor(ShaderParams.HighlightBorderColor, _cellHighlightBorderColor);
                    }
                }
            }
        }


        [SerializeField]
        bool _cellHighlightNonVisible = true;

        /// <summary>
        /// Gets or sets whether invisible cells should also be highlighted when pointer is over them
        /// </summary>
        public bool cellHighlightNonVisible {
            get { return _cellHighlightNonVisible; }
            set {
                if (_cellHighlightNonVisible != value) {
                    _cellHighlightNonVisible = value;
                    isDirty = true;
                }
            }
        }

        /// <summary>
        /// Returns Cell under mouse position or null if none.
        /// </summary>
        public Cell cellHighlighted { get { return _cellHighlighted; } }

        /// <summary>
        /// Returns current highlighted cell index.
        /// </summary>
        public int cellHighlightedIndex { get { return _cellHighlightedIndex; } }

        /// <summary>
        /// Returns Cell index which has been clicked
        /// </summary>
        public int cellLastClickedIndex { get { return _cellLastClickedIndex; } }


        [SerializeField]
        bool _cellsFlat;

        /// <summary>
        /// Gets or sets if cells are rendered as horizontal flat surfaces ignoring any terrain slope.
        /// </summary>
        public bool cellsFlat {
            get { return _cellsFlat; }
            set {
                if (_cellsFlat != value) {
                    _cellsFlat = value;
                    if (!Application.isPlaying) {
                        Redraw(true);
                    }
                    else {
                        issueRedraw = RedrawType.Full;
                    }
                }
            }
        }



        [SerializeField]
        Texture2D _gridFlatMask;

        /// <summary>
        /// Gets or sets the "flat" mask. The alpha component of this texture is used to determine if a cell should be rendered flat or adapt to terrain otherwise.
        /// </summary>
        public Texture2D gridFlatMask {
            get { return _gridFlatMask; }
            set {
                if (_gridFlatMask != value) {
                    _gridFlatMask = value;
                    isDirty = true;
                    ReloadFlatMask();
                }
            }
        }


        [SerializeField]
        float _cellsMaxSlope = 1f;

        /// <summary>
        /// Gets or sets the cells max slope. Cells with a greater slope will be hidden.
        /// </summary>
        /// <value>The cells max slope.</value>
        public float cellsMaxSlope {
            get { return _cellsMaxSlope; }
            set {
                if (_cellsMaxSlope != value) {
                    _cellsMaxSlope = value;
                    needUpdateTerritories = true;
                    if (!Application.isPlaying) {
                        Redraw(true);
                    }
                    else {
                        issueRedraw = RedrawType.Full;
                    }
                }
            }
        }


        [SerializeField]
        float _cellsMaxHeightDifference;

        /// <summary>
        /// Gets or sets the cells max height different between vertices. Cells with a greater difference than this threshold will be hidden.
        /// </summary>
        /// <value>The cells max height difference.</value>
        public float cellsMaxHeightDifference {
            get { return _cellsMaxHeightDifference; }
            set {
                if (_cellsMaxHeightDifference != value) {
                    _cellsMaxHeightDifference = Mathf.Max(0, value);
                    needUpdateTerritories = true;
                    if (!Application.isPlaying) {
                        Redraw(true);
                    }
                    else {
                        issueRedraw = RedrawType.Full;
                    }
                }
            }
        }


        [SerializeField]
        float _cellsMinimumAltitude;

        /// <summary>
        /// Gets or sets the minimum cell altitude. Useful to hide cells under certain altitude, for instance, under water.
        /// </summary>
        public float cellsMinimumAltitude {
            get { return _cellsMinimumAltitude; }
            set {
                if (_cellsMinimumAltitude != value) {
                    _cellsMinimumAltitude = value;
                    issueRedraw = RedrawType.Full;
                    Redraw(true);
                }
            }
        }


        [SerializeField]
        bool _cellsMinimumAltitudeClampVertices;

        /// <summary>
        /// Clamps vertices below the minimum altitude
        /// </summary>
        public bool cellsMinimumAltitudeClampVertices {
            get { return _cellsMinimumAltitudeClampVertices; }
            set {
                if (_cellsMinimumAltitudeClampVertices != value) {
                    _cellsMinimumAltitudeClampVertices = value;
                    issueRedraw = RedrawType.Full;
                    Redraw(true);
                }
            }
        }


        [SerializeField]
        float _cellsMaximumAltitude = 0f;

        /// <summary>
        /// Gets or sets the maximum cell altitude. Useful to hide cells above certain altitude.
        /// </summary>
        public float cellsMaximumAltitude {
            get { return _cellsMaximumAltitude; }
            set {
                if (_cellsMaximumAltitude != value) {
                    _cellsMaximumAltitude = value;
                    issueRedraw = RedrawType.Full;
                    Redraw(true);
                }
            }
        }


        [SerializeField]
        bool _cellsMaximumAltitudeClampVertices;

        /// <summary>
        /// Clamps vertices above the maximum altitude
        /// </summary>
        public bool cellsMaximumAltitudeClampVertices {
            get { return _cellsMaximumAltitudeClampVertices; }
            set {
                if (_cellsMaximumAltitudeClampVertices != value) {
                    _cellsMaximumAltitudeClampVertices = value;
                    issueRedraw = RedrawType.Full;
                    Redraw(true);
                }
            }
        }


        #region Public Cell Functions


        [NonSerialized]
        List<Vector2> _voronoiSites;

        /// <summary>
        /// Sets or gets a list of Voronoi sites. Full list will be completed with random number up to numCells amount.
        /// </summary>
        /// <value>The voronoi sites.</value>
        public List<Vector2> voronoiSites {
            get { return _voronoiSites; }
            set {
                if (_voronoiSites != value) {
                    _voronoiSites = value;
                    needGenerateMap = true;
                }
            }
        }



        [SerializeField]
        byte[] _voronoiSerializationData;

        /// <summary>
        /// Gets baked Voronoi cells
        /// </summary>
        public byte[] voronoiSerializationData {
            get { return _voronoiSerializationData; }
            set {
                if (_voronoiSerializationData != value) {
                    _voronoiSerializationData = value;
                    isDirty = true;
                }
            }
        }

        public bool hasBakedVoronoi {
            get {
                return _voronoiSerializationData != null && _voronoiSerializationData.Length > 0;
            }
        }

        /// <summary>
        /// Returns the index of a cell in the cells array by its reference.
        /// </summary>
        public int CellGetIndex (Cell cell) {
            if (cell == null)
                return -1;
            return cell.index;
        }

        /// <summary>
        /// Returns the index of a cell by its row and column numbers.
        /// </summary>
        /// <returns>The get index.</returns>
        /// <param name="row">Row.</param>
        /// <param name="column">Column.</param>
        /// <param name="clampToBorders">If set to <c>true</c> row and column values will be clamped inside current grid size (in case their values exceed the number of rows or columns). If set to false, it will wrap around edges.</param>
        public int CellGetIndex (int row, int column, bool clampToBorders = true) {
            if (_gridTopology != GridTopology.Box && _gridTopology != GridTopology.Hexagonal) {
                Debug.LogWarning("Grid topology does not support row/column indexing.");
                return -1;
            }

            if (clampToBorders) {
                row = Mathf.Clamp(row, 0, _cellRowCount - 1);
                column = Mathf.Clamp(column, 0, _cellColumnCount - 1);
            }
            else {
                row = (row + _cellRowCount) % _cellRowCount;
                column = (column + _cellColumnCount) % _cellColumnCount;
            }

            return row * _cellColumnCount + column;
        }


        /// <summary>
        /// Returns the cell index of a cell at a given position (in local or world space) in the grid
        /// </summary>
        public int CellGetIndex (Vector3 position, bool worldSpace = false) {
            Cell cell = GetCellAtPoint(position, worldSpace);
            if (cell == null) return -1;
            return CellGetIndex(cell);
        }


        /// <summary>
        /// Toggles visibility of the surface of a cell
        /// </summary>
        /// <param name="cellIndex"></param>
        /// <param name="visible"></param>
        public void CellToggleRegionSurface (int cellIndex, bool visible) {
            int cacheIndex = GetCacheIndexForCellRegion(cellIndex);
            surfaces.TryGetValue(cacheIndex, out GameObject surf);
            if (surf != null) {
                surf.SetActive(visible);
            }
        }

        /// <summary>
        /// Colorize specified region of a cell by indexes.
        /// </summary>
        /// <returns>The generated color surface positioned and oriented over the given cell.</returns>
        /// <param name="cellIndex">Cell index.</param>
        /// <param name="visible">If the colored surface is shown or not.</param>
        /// <param name="texture">Texture to be used.</param>
        /// <param name="isCanvasTexture">If true, the texture is assumed to fill the entire grid or canvas so only a portion of the texture would be visible in the cell</param>
        public GameObject CellToggleRegionSurface (int cellIndex, bool visible, Texture2D texture, bool isCanvasTexture = false) {
            return CellToggleRegionSurface(cellIndex, visible, Color.white, false, texture, Misc.Vector2one, Misc.Vector2zero, 0, true, localSpace: false, isCanvasTexture: isCanvasTexture);
        }

        /// <summary>
        /// Colorize specified region of a cell by indexes.
        /// </summary>
        /// <returns>The generated color surface positioned and oriented over the given cell.</returns>
        /// <param name="cellIndex">Cell index.</param>
        /// <param name="visible">If the colored surface is shown or not.</param>
        /// <param name="color">Color. Can be partially transparent.</param>
        /// <param name="refreshGeometry">If set to <c>true</c> any cached surface will be destroyed and regenerated. Usually you pass false to improve performance.</param>
        public GameObject CellToggleRegionSurface (int cellIndex, bool visible, Color color, bool refreshGeometry = false) {
            return CellToggleRegionSurface(cellIndex, visible, color, refreshGeometry, null, Misc.Vector2one, Misc.Vector2zero, 0, true, localSpace: false, isCanvasTexture: false);
        }

        /// <summary>
        /// Colorize specified region of a cell by indexes.
        /// </summary>
        /// <returns>The generated color surface positioned and oriented over the given cell.</returns>
        /// <param name="cellIndex">Cell index.</param>
        /// <param name="visible">If the colored surface is shown or not.</param>
        /// <param name="color">Color. Can be partially transparent.</param>
        /// <param name="refreshGeometry">If set to <c>true</c> any cached surface will be destroyed and regenerated. Usually you pass false to improve performance.</param>
        /// <param name="textureIndex">The index of the texture configured in the list of textures of the inspector.</param>
        /// <param name="isCanvasTexture">If true, the texture is assumed to fill the entire grid or canvas so only a portion of the texture would be visible in the cell</param>
        public GameObject CellToggleRegionSurface (int cellIndex, bool visible, Color color, bool refreshGeometry, int textureIndex, bool isCanvasTexture = false) {
            Texture2D texture = null;
            if (textureIndex >= 0 && textureIndex < textures.Length) {
                texture = textures[textureIndex];
            }
            return CellToggleRegionSurface(cellIndex, visible, color, refreshGeometry, texture, Misc.Vector2one, Misc.Vector2zero, 0, true, localSpace: false, isCanvasTexture: isCanvasTexture);
        }

        /// <summary>
        /// Colorize specified region of a cell by indexes.
        /// </summary>
        /// <returns>The generated color surface positioned and oriented over the given cell.</returns>
        /// <param name="cellIndex">Cell index.</param>
        /// <param name="visible">If the colored surface is shown or not.</param>
        /// <param name="color">Color. Can be partially transparent.</param>
        /// <param name="refreshGeometry">If set to <c>true</c> any cached surface will be destroyed and regenerated. Usually you pass false to improve performance.</param>
        /// <param name="texture">An optional texture. If you pass a color different than white, the texture will be tinted using that color.</param>
        /// <param name="isCanvasTexture">If true, the texture is assumed to fill the entire grid or canvas so only a portion of the texture would be visible in the cell</param>
        public GameObject CellToggleRegionSurface (int cellIndex, bool visible, Color color, bool refreshGeometry, Texture2D texture, bool isCanvasTexture = false, float textureScale = 1f) {
            return CellToggleRegionSurface(cellIndex, visible, color, refreshGeometry, texture, Misc.Vector2one * textureScale, Misc.Vector2zero, 0, localSpace: true, isCanvasTexture);
        }


        /// <summary>
        /// Colorize specified region of a cell by indexes.
        /// </summary>
        /// <returns>The generated color surface positioned and oriented over the given cell.</returns>
        /// <param name="cellIndex">Cell index.</param>
        /// <param name="visible">If the colored surface is shown or not.</param>
        /// <param name="color">Color. Can be partially transparent.</param>
        /// <param name="refreshGeometry">If set to <c>true</c> any cached surface will be destroyed and regenerated. Usually you pass false to improve performance.</param>
        /// <param name="textureIndex">The index of the texture configured in the list of textures of the inspector.</param>
        /// <param name="textureScale">Texture scale.</param>
        /// <param name="textureOffset">Texture offset.</param>
        /// <param name="isCanvasTexture">If true, the texture is assumed to fill the entire grid or canvas so only a portion of the texture would be visible in the cell</param>
        public GameObject CellToggleRegionSurface (int cellIndex, bool visible, Color color, bool refreshGeometry, int textureIndex, Vector2 textureScale, Vector2 textureOffset, bool isCanvasTexture = false) {
            Texture2D texture = null;
            if (textureIndex >= 0 && textureIndex < textures.Length) {
                texture = textures[textureIndex];
            }
            return CellToggleRegionSurface(cellIndex, visible, color, refreshGeometry, texture, textureScale, textureOffset, textureRotation: 0, localSpace: true, isCanvasTexture);
        }

        /// <summary>
        /// Colorize specified region of a cell by indexes.
        /// </summary>
        /// <returns>The generated color surface positioned and oriented over the given cell.</returns>
        /// <param name="cellIndex">Cell index.</param>
        /// <param name="visible">If the colored surface is shown or not.</param>
        /// <param name="color">Color. Can be partially transparent.</param>
        /// <param name="refreshGeometry">If set to <c>true</c> any cached surface will be destroyed and regenerated. Usually you pass false to improve performance.</param>
        /// <param name="texture">An optional texture. If you pass a color different than white, the texture will be tinted using that color.</param>
        /// <param name="textureScale">Texture scale.</param>
        /// <param name="textureOffset">Texture offset.</param>
        /// <param name="textureRotation">Texture rotation.</param>
        /// <param name="localSpace">Apply texture rotation & scale in local space (relative to the cell).</param>
        /// <param name="isCanvasTexture">If true, the texture is assumed to fill the entire grid or canvas so only a portion of the texture would be visible in the cell</param>
        public GameObject CellToggleRegionSurface (int cellIndex, bool visible, Color color, bool refreshGeometry, Texture2D texture, Vector2 textureScale, Vector2 textureOffset, float textureRotation = 0, bool localSpace = false, bool isCanvasTexture = false) {
            return CellToggleRegionSurface(cellIndex, visible, color, refreshGeometry, texture, textureScale, textureOffset, textureRotation, overlay: false, localSpace, isCanvasTexture);
        }

        /// <summary>
        /// Colorize specified region of a cell by indexes.
        /// </summary>
        /// <returns>The generated color surface positioned and oriented over the given cell.</returns>
        /// <param name="cell">Cell.</param>
        /// <param name="visible">If the colored surface is shown or not.</param>
        /// <param name="color">Color. Can be partially transparent.</param>
        /// <param name="refreshGeometry">If set to <c>true</c> any cached surface will be destroyed and regenerated. Usually you pass false to improve performance.</param>
        /// <param name="texture">An optional texture. If you pass a color different than white, the texture will be tinted using that color.</param>
        /// <param name="textureScale">Texture scale.</param>
        /// <param name="textureOffset">Texture offset.</param>
        /// <param name="textureRotation">Texture rotation.</param>
        /// <param name="overlay">If set to <c>true</c> the colored surface will be shown on top of objects.</param>
        /// <param name="localSpace">Apply texture rotation & scale in local space (relative to the cell).</param>
        /// <param name="isCanvasTexture">If true, the texture is assumed to fill the entire grid or canvas so only a portion of the texture would be visible in the cell</param>
        public GameObject CellToggleRegionSurface (Cell cell, bool visible, Color color, bool refreshGeometry, Texture2D texture, Vector2 textureScale, Vector2 textureOffset, float textureRotation, bool overlay, bool localSpace, bool isCanvasTexture = false) {
            int cellIndex = CellGetIndex(cell);
            return CellToggleRegionSurface(cellIndex, visible, color, refreshGeometry, texture, textureScale, textureOffset, textureRotation, overlay, localSpace, isCanvasTexture);
        }

        /// <summary>
        /// Colorize specified region of a cell by indexes.
        /// </summary>
        /// <returns>The generated color surface positioned and oriented over the given cell.</returns>
        /// <param name="cellIndex">Cell index.</param>
        /// <param name="visible">If the colored surface is shown or not.</param>
        /// <param name="color">Color. Can be partially transparent.</param>
        /// <param name="refreshGeometry">If set to <c>true</c> any cached surface will be destroyed and regenerated. Usually you pass false to improve performance.</param>
        /// <param name="texture">An optional texture. If you pass a color different than white, the texture will be tinted using that color.</param>
        /// <param name="textureScale">Texture scale.</param>
        /// <param name="textureOffset">Texture offset.</param>
        /// <param name="textureRotation">Texture rotation.</param>
        /// <param name="overlay">If set to <c>true</c> the colored surface will be shown on top of objects.</param>
        /// <param name="localSpace">Apply texture rotation & scale in local space (relative to the cell).</param>
        /// <param name="isCanvasTexture">If true, the texture is assumed to fill the entire grid or canvas so only a portion of the texture would be visible in the cell</param>
        public GameObject CellToggleRegionSurface (int cellIndex, bool visible, Color color, bool refreshGeometry, Texture2D texture, Vector2 textureScale, Vector2 textureOffset, float textureRotation, bool overlay, bool localSpace, bool isCanvasTexture) {

            FlushCellChanges();

            if (cellIndex < 0 || cells == null || cellIndex >= cells.Count || cells[cellIndex] == null)
                return null;

            if (!visible) {
                CellHideRegionSurface(cellIndex);
                return null;
            }

            int cacheIndex = GetCacheIndexForCellRegion(cellIndex);
            bool existsInCache = surfaces.TryGetValue(cacheIndex, out GameObject surf);
            if (existsInCache && surf == null) {
                surfaces.Remove(cacheIndex);
                existsInCache = false;
            }
            if (refreshGeometry && existsInCache) {
                surfaces.Remove(cacheIndex);
                DestroyImmediate(surf);
                surf = null;
            }
            Region region = cells[cellIndex].region;

            // Should the surface be recreated?
            if (surf != null) {
                if (texture != null && (textureScale != region.customTextureScale || textureOffset != region.customTextureOffset || textureRotation != region.customTextureRotation || region.customTextureScale.x == 0)) {
                    // we check if customTextureScale.x == 0 to ensure the material is of texture type so if it was colored before and now it's being textured, we need to regenerate UVs
                    surfaces.Remove(cacheIndex);
                    DestroyImmediate(surf);
                    surf = null;
                }
            }
            // If it exists, activate and check proper material, if not create surface
            bool isHighlighted = cellHighlightedIndex == cellIndex && _highlightEffect != HighlightEffect.None;
            Material surfMaterial;
            if (surf != null) {
                if (!surf.activeSelf) {
                    surf.SetActive(true);
                }
                surfMaterial = GetColoredTexturedMaterialForCell(color, texture, overlay);
                region.customMaterial = surfMaterial;
                if (!isHighlighted) {
                    ApplyMaterialToSurface(region, surfMaterial);
                }
            }
            else {
                surfMaterial = GetColoredTexturedMaterialForCell(color, texture, overlay);
                surf = GenerateCellRegionSurface(cellIndex, surfMaterial, textureScale, textureOffset, textureRotation, localSpace, _cellTextureMode, isCanvasTexture);
                if (surf == null)
                    return null;
                region.customMaterial = surfMaterial;
                if (texture != null) {
                    region.customTextureOffset = textureOffset;
                    region.customTextureRotation = textureRotation;
                    region.customTextureScale = textureScale;
                    region.customRotateInLocalSpace = localSpace;
                    region.customIsCanvasTexture = isCanvasTexture;
                }
                if (_cellTextureMode == CELL_TEXTURE_MODE.Floating) {
                    ApplyCellFloatingOffset(cellIndex, surf.transform);
                }
            }

            // If it was highlighted, highlight it again
            if (isHighlighted && region.customMaterial != null && _highlightedObj != null) {
                if (hudMatCell.HasProperty(ShaderParams.MainTex)) {
                    if (region.customMaterial != null) {
                        hudMatCell.mainTexture = region.customMaterial.mainTexture;
                    }
                    else {
                        hudMatCell.mainTexture = null;
                    }
                }
                surf.GetComponent<Renderer>().sharedMaterial = hudMatCell;
                _highlightedObj = surf;
            }

            // Optimization: if color alpha is zero, disable the entire surface
            if (color.a <= 0) {
                CellHideRegionSurface(cellIndex);
            }
            return surf;
        }


        /// <summary>
        /// Uncolorize/hide specified cell by index in the cells collection.
        /// </summary>
        public void CellHideRegionSurface (int cellIndex) {
            if (_cellHighlightedIndex != cellIndex || _highlightedObj == null) {
                int cacheIndex = GetCacheIndexForCellRegion(cellIndex);
                GameObject surf;
                if (surfaces.TryGetValue(cacheIndex, out surf)) {
                    if (surf == null) {
                        surfaces.Remove(cacheIndex);
                    }
                    else {
                        surf.SetActive(false);
                    }
                }
            }
            cells[cellIndex].region.customMaterial = null;
        }


        /// <summary>
        /// Uncolorize/hide specified all cells.
        /// </summary>
        public void CellHideRegionSurfaces () {
            int cellsCount = cells.Count;
            for (int k = 0; k < cellsCount; k++) {
                CellHideRegionSurface(k);
            }
        }

        /// <summary>
        /// Colors a cell and fades it out for "duration" in seconds.
        /// </summary>
        public void CellFadeOut (Cell cell, Color color, float duration = 2f) {
            int cellIndex = CellGetIndex(cell);
            CellFadeOut(cellIndex, color, duration);
        }

        /// <summary>
        /// Colors a cell and fades it out during "duration" in seconds.
        /// </summary>
        public void CellFadeOut (int cellIndex, Color color, float duration = 2f, int repetitions = 1) {
            CellAnimate(FaderStyle.FadeOut, cellIndex, Misc.ColorNull, color, duration, repetitions);
        }

        /// <summary>
        /// Fades out a list of cells with "color" and "duration" in seconds.
        /// </summary>
        public void CellFadeOut (List<int> cellIndices, Color color, float duration = 2f, int repetitions = 1) {
            if (cellIndices == null)
                return;
            int cellCount = cellIndices.Count;
            for (int k = 0; k < cellCount; k++) {
                CellAnimate(FaderStyle.FadeOut, cellIndices[k], Misc.ColorNull, color, duration, repetitions);
            }
        }

        /// <summary>
        /// Flashes a cell with "color" and "duration" in seconds.
        /// </summary>
        public void CellFlash (int cellIndex, Color color, float duration = 2f, int repetitions = 1) {
            CellAnimate(FaderStyle.Flash, cellIndex, Misc.ColorNull, color, duration, repetitions);
        }


        /// <summary>
        /// Flashes a cell with "color" and "duration" in seconds.
        /// </summary>
        public void CellFlash (Cell cell, Color color, float duration = 2f, int repetitions = 1) {
            int cellIndex = CellGetIndex(cell);
            CellAnimate(FaderStyle.Flash, cellIndex, Misc.ColorNull, color, duration, repetitions);
        }

        /// <summary>
        /// Flashes a list of cells with "color" and "duration" in seconds.
        /// </summary>
        public void CellFlash (List<int> cellIndices, Color color, float duration = 2f, int repetitions = 1) {
            if (cellIndices == null)
                return;
            int cellCount = cellIndices.Count;
            for (int k = 0; k < cellCount; k++) {
                CellAnimate(FaderStyle.Flash, cellIndices[k], Misc.ColorNull, color, duration, repetitions);
            }
        }

        /// <summary>
        /// Flashes a list of cells with "color" and "duration" in seconds.
        /// </summary>
        public void CellFlash (List<Cell> cells, Color color, float duration = 2f, int repetitions = 1) {
            if (cells == null)
                return;
            int cellCount = cells.Count;
            for (int k = 0; k < cellCount; k++) {
                CellAnimate(FaderStyle.Flash, cells[k].index, Misc.ColorNull, color, duration, repetitions);
            }
        }

        /// <summary>
        /// Temporarily colors a cell for "duration" in seconds.
        /// </summary>
        public void CellColorTemp (int cellIndex, Color color, float duration = 2f) {
            CellAnimate(FaderStyle.ColorTemp, cellIndex, Misc.ColorNull, color, duration, 1);
        }

        /// <summary>
        /// Temporarily colors a cell for "duration" in seconds.
        /// </summary>
        public void CellColorTemp (Cell cell, Color color, float duration = 2f, int repetitions = 1) {
            int cellIndex = CellGetIndex(cell);
            CellAnimate(FaderStyle.ColorTemp, cellIndex, Misc.ColorNull, color, duration, repetitions);
        }

        /// <summary>
        /// Temporarily colors a list of cells for "duration" in seconds.
        /// </summary>
        public void CellColorTemp (List<int> cellIndices, Color color, float duration = 2f, int repetitions = 1) {
            if (cellIndices == null)
                return;
            int cellCount = cellIndices.Count;
            for (int k = 0; k < cellCount; k++) {
                CellAnimate(FaderStyle.ColorTemp, cellIndices[k], Misc.ColorNull, color, duration, repetitions);
            }
        }

        /// <summary>
        /// Temporarily colors a list of cells for "duration" in seconds.
        /// </summary>
        public void CellColorTemp (List<Cell> cells, Color color, float duration = 2f, int repetitions = 1) {
            if (cells == null)
                return;
            int cellCount = cells.Count;
            for (int k = 0; k < cellCount; k++) {
                CellAnimate(FaderStyle.ColorTemp, cells[k].index, Misc.ColorNull, color, duration, repetitions);
            }
        }

        /// <summary>
        /// Blinks a cell with colors "color1" and "color2" and "duration" in seconds.
        /// </summary>
        public void CellBlink (Cell cell, Color color1, Color color2, float duration = 2f, int repetitions = 1) {
            int cellIndex = CellGetIndex(cell);
            CellAnimate(FaderStyle.Blink, cellIndex, color1, color2, duration, repetitions);
        }


        /// <summary>
        /// Blinks a cell with colors "color1" and "color2" and "duration" in seconds.
        /// </summary>
        public void CellBlink (int cellIndex, Color color1, Color color2, float duration = 2f, int repetitions = 1) {
            CellAnimate(FaderStyle.Blink, cellIndex, color1, color2, duration, repetitions);
        }

        /// <summary>
        /// Blinks a list of cells with colors "color1" and "color2" and "duration" in seconds.
        /// </summary>
        public void CellBlink (List<int> cellIndices, Color color1, Color color2, float duration = 2f, int repetitions = 1) {
            if (cellIndices == null)
                return;
            int cellCount = cellIndices.Count;
            for (int k = 0; k < cellCount; k++) {
                CellAnimate(FaderStyle.Blink, cellIndices[k], color1, color2, duration, repetitions);
            }
        }

        /// <summary>
        /// Blinks a list of cells with colors "color1" and "color2" and "duration" in seconds.
        /// </summary>
        public void CellBlink (List<Cell> cells, Color color1, Color color2, float duration = 2f, int repetitions = 1) {
            if (cells == null)
                return;
            int cellCount = cells.Count;
            for (int k = 0; k < cellCount; k++) {
                CellAnimate(FaderStyle.Blink, cells[k].index, color1, color2, duration, repetitions);
            }
        }

        /// <summary>
        /// Flashes a cell from "initialColor" to "color" and "duration" in seconds.
        /// </summary>
        public void CellFlash (Cell cell, Color initialColor, Color color, float duration = 2f, int repetitions = 1) {
            int cellIndex = CellGetIndex(cell);
            CellAnimate(FaderStyle.Flash, cellIndex, initialColor, color, duration, repetitions);
        }


        /// <summary>
        /// Flashes a cell from "initialColor" to "color" and "duration" in seconds.
        /// </summary>
        public void CellFlash (int cellIndex, Color initialColor, Color color, float duration = 2f, int repetitions = 1) {
            CellAnimate(FaderStyle.Flash, cellIndex, initialColor, color, duration, repetitions);
        }

        /// <summary>
        /// Flashes a list of cells from "initialColor" to "color" and "duration" in seconds.
        /// </summary>
        public void CellFlash (List<int> cellIndices, Color initialColor, Color color, float duration = 2f, int repetitions = 1) {
            if (cellIndices == null)
                return;

            int cellCount = cellIndices.Count;
            for (int k = 0; k < cellCount; k++) {
                CellAnimate(FaderStyle.Flash, cellIndices[k], initialColor, color, duration, repetitions);
            }
        }

        /// <summary>
        /// Flashes a list of cells from "initialColor" to "color" and "duration" in seconds.
        /// </summary>
        public void CellFlash (List<Cell> cells, Color initialColor, Color color, float duration = 2f, int repetitions = 1) {
            if (cells == null)
                return;

            int cellCount = cells.Count;
            for (int k = 0; k < cellCount; k++) {
                CellAnimate(FaderStyle.Flash, cells[k].index, initialColor, color, duration, repetitions);
            }
        }

        /// <summary>
        /// Blinks a cell with "color" and "duration" in seconds.
        /// </summary>
        public void CellBlink (Cell cell, Color color, float duration = 2f, int repetitions = 1) {
            int cellIndex = CellGetIndex(cell);
            CellAnimate(FaderStyle.Blink, cellIndex, Misc.ColorNull, color, duration, repetitions);
        }


        /// <summary>
        /// Blinks a cell with "color" and "duration" in seconds.
        /// </summary>
        public void CellBlink (int cellIndex, Color color, float duration = 2f, int repetitions = 1) {
            CellAnimate(FaderStyle.Blink, cellIndex, Misc.ColorNull, color, duration, repetitions);
        }

        /// <summary>
        /// Blinks a list of cells with "color" and "duration" in seconds.
        /// </summary>
        public void CellBlink (List<int> cellIndices, Color color, float duration = 2f, int repetitions = 1) {
            if (cellIndices == null)
                return;
            int cellCount = cellIndices.Count;
            for (int k = 0; k < cellCount; k++) {
                CellAnimate(FaderStyle.Blink, cellIndices[k], Misc.ColorNull, color, duration, repetitions);
            }
        }

        /// <summary>
        /// Blinks a list of cells with "color" and "duration" in seconds.
        /// </summary>
        public void CellBlink (List<Cell> cells, Color color, float duration = 2f, int repetitions = 1) {
            if (cells == null)
                return;
            int cellCount = cells.Count;
            for (int k = 0; k < cellCount; k++) {
                CellAnimate(FaderStyle.Blink, cells[k].index, Misc.ColorNull, color, duration, repetitions);
            }
        }


        /// <summary>
        /// Creates a gameobject with the border for the given cell.
        /// </summary>
        public GameObject CellDrawBorder (Cell cell, Color color = default(Color), float thickness = 0, float expand = 1f) {

            if (cell == null || cell.region == null) return null;
            return CellDrawBorder(cell.index, color, thickness, expand);
        }

        /// <summary>
        /// Creates a gameobject with the border for the given cell.
        /// </summary>
        public GameObject CellDrawBorder (int cellIndex, Color color = default(Color), float thickness = 0, float expand = 1f) {

            if (!ValidCellIndex(cellIndex)) return null;

            List<int> dummyList = new List<int>
            {
                cellIndex
            };
            Cell cell = cells[cellIndex];
            if (cell.region.customBorderGameObject != null) {
                DestroyImmediate(cell.region.customBorderGameObject);
            }
            cell.region.customBorderGameObject = CellDrawBorder(dummyList, color, thickness, expand);
            return cell.region.customBorderGameObject;
        }

        /// <summary>
        /// Destroys a custom border previous drawn using CellDrawBorder
        /// </summary>
        public void CellDestroyBorder (int cellIndex) {

            if (!ValidCellIndex(cellIndex)) return;
            Cell cell = cells[cellIndex];
            if (cell.region.customBorderGameObject != null) {
                DestroyImmediate(cell.region.customBorderGameObject);
            }
        }

        /// <summary>
        /// Destroys a cell surface and removes it from internal caches
        /// </summary>
        /// <param name="cellIndex"></param>
        public void CellDestroySurface (int cellIndex) {
            if (!ValidCellIndex(cellIndex)) return;

            // remove from surface cache
            int surfaceIndex = GetCacheIndexForCellRegion(cellIndex);
            if (surfaces.TryGetValue(surfaceIndex, out GameObject surf)) {
                if (surf != null) {
                    DestroyImmediate(surf);
                }
                surfaces.Remove(surfaceIndex);
            }

        }


        public GameObject CellDrawBorder (List<Cell> cells, Color color = default(Color), float thickness = 1f, float expand = 1f) {
            GetCellIndices(cells, tempListCells);
            return CellDrawBorder(tempListCells, color, thickness, expand);
        }

        readonly Dictionary<Segment, int> customBorderHit = new Dictionary<Segment, int>();

        public GameObject CellDrawBorder (List<int> cellIndices, Color color = default(Color), float thickness = 1f, float expand = 1f) {

            Region region;
            if (cellIndices == null) return null;

            if (cellIndices.Count > 1) {
                region = new Region(null, false);
                region.isFlat = _cellsFlat;
                customBorderHit.Clear();

                foreach (int cellIndex in cellIndices) {
                    if (!ValidCellIndex(cellIndex)) continue;
                    Cell cell = cells[cellIndex];
                    foreach (Segment segment in cell.region.segments) {
                        customBorderHit.TryGetValue(segment, out int count);
                        count++;
                        customBorderHit[segment] = count;
                    }
                }

                foreach (KeyValuePair<Segment, int> kvp in customBorderHit) {
                    if (kvp.Value == 1) {
                        region.segments.Add(kvp.Key);
                    }
                }

            }
            else {
                region = cells[cellIndices[0]].region;
            }

            if (expand != 1f) {
                // compute center
                Vector2 center = Vector2.zero;
                int count = 0;
                int segCount = region.segments.Count;
                for (int k = 0; k < segCount; k++) {
                    Segment s = region.segments[k];
                    center.x += (float)s.start.x;
                    center.y += (float)s.start.y;
                    center.x += (float)s.end.x;
                    center.y += (float)s.end.y;
                    count += 2;
                }
                center /= count;
                // expand segments
                for (int k = 0; k < segCount; k++) {
                    Segment s = region.segments[k];
                    float sx = ((float)s.start.x - center.x) * expand + center.x;
                    float sy = ((float)s.start.y - center.y) * expand + center.y;
                    float ex = ((float)s.end.x - center.x) * expand + center.x;
                    float ey = ((float)s.end.y - center.y) * expand + center.y;
                    Segment es = new Segment(new Point(sx, sy), new Point(ex, ey), s.border);
                    region.segments[k] = es;
                }
            }

            TerritoryMesh tm = new TerritoryMesh();
            if (!GenerateRegionMesh(tm, region, thickness)) return null;

            if (color == default) {
                color = Color.black;
            }

            Material material;
            if (thickness > 1f) {
                material = territoriesThickHackMat;
            }
            else {
                material = GetFrontierColorMaterial(color);
            }
            material = Instantiate(material);
            material.renderQueue--;
            material.color = color;
            material.DisableKeyword(ShaderParams.SKW_NEAR_CLIP_FADE);
            UpdateMaterialTerritoryThickness(material, thickness);
            SetStencil(material, STENCIL_MASK_BORDER);

            bool useVertexDisplacement = thickness > 1f & !canUseGeometryShaders;

            Transform root = CheckCellsCustomFrontiersRoot();
            GameObject go = DrawTerritoryFrontier(tm, material, root, CUSTOM_BORDER_NAME, useVertexDisplacement);

            return go;
        }

        /// <summary>
        /// Returns the rect enclosing the cell in local space coordinates
        /// </summary>
        public Rect CellGetRect (int cellIndex) {
            if (cells == null || cellIndex < 0 || cellIndex >= cells.Count)
                return new Rect(0, 0, 0, 0);
            Rect rect = cells[cellIndex].region.rect2D;
            return rect;
        }

        /// <summary>
        /// Cancels any ongoing visual effect on any cell
        /// </summary>
        public void CancelAnimations (float fadeOutDuration = 0) {
            CancelAnimationAll(fadeOutDuration);
        }

        /// <summary>
        /// Cancels any ongoing visual effect on a cell
        /// </summary>
        /// <param name="cellIndex">Cell index.</param>
        public void CellCancelAnimations (int cellIndex, float fadeOutDuration = 0) {
            CellCancelAnimation(cellIndex, fadeOutDuration);
        }

        /// <summary>
        /// Cancels any ongoing visual effect on a list of cells
        /// </summary>
        /// <param name="cellIndices">Cell indices.</param>
        public void CellCancelAnimations (List<int> cellIndices, float fadeOutDuration = 0) {
            int cellCount = cellIndices.Count;
            for (int k = 0; k < cellCount; k++) {
                CellCancelAnimation(cellIndices[k], fadeOutDuration);
            }
        }


        /// <summary>
        /// Returns the rect enclosing the cell in world space
        /// </summary>
        public Bounds CellGetRectWorldSpace (int cellIndex) {
            if (!ValidCellIndex(cellIndex)) return Misc.BoundsZero;
            Rect rect = cells[cellIndex].region.rect2D;
            Vector3 min = GetWorldSpacePosition(rect.min);
            Vector3 max = GetWorldSpacePosition(rect.max);
            Bounds bounds = new Bounds((min + max) * 0.5f, max - min);
            return bounds;
        }

        /// <summary>
        /// Returns the rect enclosing the cell in world space. Same than CellGetRectWorldSpace.
        /// </summary>
        public Bounds CellGetBounds (int cellIndex) {
            return CellGetRectWorldSpace(cellIndex);
        }

        /// <summary>
        /// Returns the size in normalized viewport coordinates (0..1) for the given cell if that cell was on the center of the screen
        /// </summary>
        /// <returns>The get rect screen space.</returns>
        /// <param name="cellIndex">Cell index.</param>
        public Vector2 CellGetViewportSize (int cellIndex) {
            Transform t = cameraMain.transform;
            Vector3 oldPos = t.position;
            Quaternion oldRot = t.rotation;

            Plane p = new Plane(transform.forward, transform.position);
            float dist = p.GetDistanceToPoint(oldPos);
            Vector3 cellPos = CellGetPosition(cellIndex);
            t.position = cellPos - transform.forward * dist;
            t.LookAt(cellPos);
            Vector3 cellRectMin = transform.TransformPoint(cells[cellIndex].region.rect2D.min);
            Vector3 cellRectMax = transform.TransformPoint(cells[cellIndex].region.rect2D.max);
            Vector3 screenMin = cameraMain.WorldToViewportPoint(cellRectMin);
            Vector3 screenMax = cameraMain.WorldToViewportPoint(cellRectMax);

            t.rotation = oldRot;
            t.position = oldPos;
            return new Vector2(Mathf.Abs(screenMax.x - screenMin.x), Mathf.Abs(screenMax.y - screenMin.y));
        }

        /// <summary>
        /// Gets the cell's center position in world space or local space.
        /// </summary>
        public Vector3 CellGetPosition (int cellIndex, bool worldSpace = true, float elevation = 0) {
            if (!ValidCellIndex(cellIndex)) return Misc.Vector3zero;
            return CellGetPosition(cells[cellIndex], worldSpace, elevation);
        }

        /// <summary>
        /// Gets the cell's center position in world space or local space.
        /// </summary>
        public Vector3 CellGetPosition (Cell cell, bool worldSpace = true, float elevation = 0) {
            if (cell == null)
                return Misc.Vector3zero;
            Vector2 cellGridCenter = cell.scaledCenter;
            if (worldSpace) {
                return GetWorldSpacePosition(cellGridCenter, elevation);
            }
            else {
                return cellGridCenter;
            }
        }


        /// <summary>
        /// Gets the cell's centroid in world space.
        /// </summary>
        public Vector3 CellGetCentroid (int cellIndex, bool worldSpace = true) {
            if (!ValidCellIndex(cellIndex)) return Misc.Vector3zero;
            Vector2 cellCentroid = cells[cellIndex].GetCentroid();
            if (worldSpace) {
                return GetWorldSpacePosition(cellCentroid);
            }
            return cellCentroid;
        }

        /// <summary>
        /// Returns the normal at the center of a cell
        /// </summary>
        /// <returns>The normal in world space coordinates.</returns>
        /// <param name="cellIndex">Cell index.</param>
        public Vector3 CellGetNormal (int cellIndex) {
            if (_terrainWrapper == null || !ValidCellIndex(cellIndex)) return Misc.Vector3zero;
            Vector2 cellCenter = cells[cellIndex].scaledCenter;
            return _terrainWrapper.GetInterpolatedNormal(cellCenter.x + 0.5f, cellCenter.y + 0.5f);
        }

        /// <summary>
        /// Returns the number of vertices of the cell
        /// </summary>
        public int CellGetVertexCount (int cellIndex) {
            if (!ValidCellIndex(cellIndex)) return 0;
            return cells[cellIndex].region.points.Count;
        }

        /// <summary>
        /// Returns the world space position of the vertex
        /// </summary>
        public Vector3 CellGetVertexPosition (int cellIndex, int vertexIndex) {
            if (cellIndex < 0 || cellIndex >= cells.Count || cells[cellIndex].region == null || cells[cellIndex].region.points == null || cells[cellIndex].region.points.Count <= vertexIndex)
                return Misc.Vector3zero;
            Vector2 localPosition = cells[cellIndex].region.points[vertexIndex];
            return GetWorldSpacePosition(localPosition);
        }

        /// <summary>
        /// Returns a list of neighbour cells for specificed cell.
        /// </summary>
        public List<Cell> CellGetNeighbours (Cell cell) {
            int cellIndex = CellGetIndex(cell);
            return CellGetNeighbours(cellIndex);
        }


        /// <summary>
        /// Returns the index of an ajacent cell by the side name
        /// </summary>
        public int CellGetNeighbour (int cellIndex, CELL_SIDE side) {
            if (!GetAdjacentCellCoordinates(cellIndex, side, out int r, out int c, out _)) {
                return -1;
            }
            return CellGetIndex(r, c);
        }

        /// <summary>
        /// Returns a list of neighbour cells for specificed cell index.
        /// </summary>
        public List<Cell> CellGetNeighbours (int cellIndex) {
            if (!ValidCellIndex(cellIndex)) return null;

            if (_gridTopology == GridTopology.Box && _pathFindingUseDiagonals) {
                Cell cell = cells[cellIndex];
                List<Cell> neighbours = new List<Cell>(8);
                for (int y = -1; y <= 1; y++) {
                    for (int x = -1; x <= 1; x++) {
                        Cell ncell = CellGetAtPosition(cell.column + x, cell.row + y);
                        if (ncell != null && ncell != cell) {
                            neighbours.Add(ncell);
                        }
                    }
                }
                return neighbours;
            }
            return cells[cellIndex].neighbours;
        }

        /// <summary>
        /// Returns a list of neighbour cells for specificed cell index.
        /// </summary>
        public int CellGetNeighbours (int cellIndex, List<Cell> neighbours) {
            if (!ValidCellIndex(cellIndex)) return 0;
            if (neighbours == null)
                return 0;

            if (_gridTopology == GridTopology.Box && _pathFindingUseDiagonals) {
                Cell cell = cells[cellIndex];
                neighbours.Clear();
                for (int y = -1; y <= 1; y++) {
                    for (int x = -1; x <= 1; x++) {
                        Cell ncell = CellGetAtPosition(cell.column + x, cell.row + y);
                        if (ncell != null && ncell != cell) {
                            neighbours.Add(ncell);
                        }
                    }
                }
                return neighbours.Count;
            }

            neighbours.Clear();
            neighbours.AddRange(cells[cellIndex].neighbours);
            return neighbours.Count;
        }

        /// <summary>
        /// Returns a list of neighbour cells for specificed cell index.
        /// </summary>
        public int CellGetNeighbours (int cellIndex, List<int> neighbours) {
            if (!ValidCellIndex(cellIndex)) return 0;
            if (neighbours == null)
                return 0;

            if (_gridTopology == GridTopology.Box && _pathFindingUseDiagonals) {
                // return CellGetNeighbours(cellIndex, maxSteps: 1, neighbours);
                Cell cell = cells[cellIndex];
                neighbours.Clear();
                for (int y = -1; y <= 1; y++) {
                    for (int x = -1; x <= 1; x++) {
                        Cell ncell = CellGetAtPosition(cell.column + x, cell.row + y);
                        if (ncell != null && ncell != cell) {
                            neighbours.Add(ncell.index);
                        }
                    }
                }
                return neighbours.Count;
            }

            neighbours.Clear();
            List<Cell> ncells = cells[cellIndex].neighbours;
            int count = ncells.Count;
            for (int k = 0; k < count; k++) {
                neighbours.Add(ncells[k].index);
            }
            return count;
        }

        /// <summary>
        /// Get a list of cells which are nearer than a given distance in cell count
        /// </summary>
        public List<int> CellGetNeighbours (Cell cell, int maxSteps = -1, int cellGroupMask = -1, float maxSearchCost = 0, CanCrossCheckType canCrossCheckType = CanCrossCheckType.Default, int maxResultsCount = int.MaxValue, bool cellGroupMaskExactComparison = false) {
            int cellIndex = CellGetIndex(cell);
            return CellGetNeighbours(cellIndex, maxSteps, cellGroupMask, maxSearchCost, canCrossCheckType, maxResultsCount, cellGroupMaskExactComparison);
        }

        /// <summary>
        /// Get a list of cells which are nearer than a given distance in cell count
        /// </summary>
        public List<int> CellGetNeighbours (int cellIndex, int maxSteps = -1, int cellGroupMask = -1, float maxSearchCost = 0, CanCrossCheckType canCrossCheckType = CanCrossCheckType.Default, int maxResultsCount = int.MaxValue, bool cellGroupMaskExactComparison = false) {
            List<int> results = new List<int>();
            CellGetNeighbours(cellIndex, maxSteps, results, cellGroupMask, maxSearchCost, canCrossCheckType, maxResultsCount, cellGroupMaskExactComparison);
            return results;
        }

        /// <summary>
        /// Get a list of cells which are nearer than a given distance in cell count
        /// </summary>
        public int CellGetNeighbours (int cellIndex, int maxSteps, List<int> cellIndices, int cellGroupMask = -1, float maxSearchCost = 0, CanCrossCheckType canCrossCheckType = CanCrossCheckType.Default, int maxResultsCount = int.MaxValue, bool ignoreCellCosts = false, bool includeInvisibleCells = true, bool cellGroupMaskExactComparison = false) {
            if (cellIndex < 0 || cellIndex >= cells.Count || cellIndices == null)
                return 0;
            Cell cell = cells[cellIndex];
            cellIndices.Clear();
            if (maxSteps < 0) maxSteps = _pathFindingMaxSteps;
            maxSteps = Mathf.Min(BIG_INT_NUMBER, maxSteps);
            int maxI = maxSteps * 2 + 1;
            maxI *= maxI;
            maxI--; // ignore starting cell
            int dx = -1;
            int dy = 0;
            int y = -maxSteps;
            int x = maxSteps;
            cellIteration++;
            GridDistanceFunction distanceFunction;
            if (_gridTopology == GridTopology.Hexagonal) {
                distanceFunction = CellGetHexagonDistance;
            }
            else {
                distanceFunction = CellGetBoxDistance;
            }
            int count = 0;
            for (int i = 0; i < maxI; i++) {
                int cx = x + cell.column;
                int cy = y + cell.row;
                if (cx >= 0 && cx < _cellColumnCount && cy >= 0 && cy < _cellRowCount) {
                    int ci = CellGetIndex(cy, cx, false);
                    if (cells[ci].iteration == cellIteration) {
                        cellIndices.Add(ci);
                        count++;
                    }
                    else {
                        if (distanceFunction(cellIndex, ci) <= maxSteps) {
                            int stepsCount = FindPath(cellIndex, ci, tempListCells, out _, maxSearchCost, maxSteps, cellGroupMask, canCrossCheckType, ignoreCellCosts, includeInvisibleCells, cellGroupMaskExactComparison: cellGroupMaskExactComparison);
                            if (stepsCount > 0) {
                                for (int k = 0; k < stepsCount - 1; k++) {
                                    cells[tempListCells[k]].iteration = cellIteration;
                                }
                                cellIndices.Add(ci);
                                count++;
                            }
                        }
                    }
                    if (count >= maxResultsCount) break;
                }
                if ((x == y) || ((x < 0) && (x == -y)) || ((x > 0) && (x == 1 - y))) {
                    int t = dx;
                    dx = dy;
                    dy = -t;
                }
                x += dx;
                y += dy;
            }
            return count;
        }


        /// <summary>
        /// Get a list of cells which are nearer than a given distance in cell count
        /// </summary>
        public List<int> CellGetNeighboursWithinRange (int cellIndex, int minSteps, int maxSteps, int cellGroupMask = -1, float maxCost = -1, bool includeInvisibleCells = true, CanCrossCheckType canCrossCheckType = CanCrossCheckType.Default, bool ignoreCellCosts = false, bool cellGroupMaskExactComparison = false) {
            if (cellIndex < 0 || cellIndex >= cells.Count)
                return null;
            minSteps = Mathf.Max(1, minSteps);
            Cell cell = cells[cellIndex];
            List<int> cc = new List<int>();
            GridDistanceFunction distanceFunction;
            if (_gridTopology == GridTopology.Hexagonal) {
                distanceFunction = CellGetHexagonDistance;
            }
            else {
                distanceFunction = CellGetBoxDistance;
            }
            for (int x = cell.column - maxSteps; x <= cell.column + maxSteps; x++) {
                if (x < 0 || x >= _cellColumnCount)
                    continue;
                for (int y = cell.row - maxSteps; y <= cell.row + maxSteps; y++) {
                    if (y < 0 || y >= _cellRowCount)
                        continue;
                    if (x == cell.column && y == cell.row)
                        continue;
                    int ci = CellGetIndex(y, x);
                    if (!includeInvisibleCells && !CellIsVisible(ci)) continue;
                    if (distanceFunction(cellIndex, ci) <= maxSteps) {
                        List<int> steps = FindPath(cellIndex, ci, maxCost, maxSteps, cellGroupMask, canCrossCheckType, ignoreCellCosts, includeInvisibleCells, cellGroupMaskExactComparison: cellGroupMaskExactComparison);
                        if (steps != null) {
                            int stepsCount = steps.Count;
                            if (stepsCount >= minSteps) {
                                cc.Add(ci);
                            }
                        }
                    }
                }
            }
            return cc;
        }

        /// <summary>
        /// Adds surrounding cells to a list of given cells
        /// </summary>
        public void CellExpandSelection (List<int> cellIndices) {
            if (_gridTopology == GridTopology.Irregular) return;
            int cellCount = cellIndices.Count;
            for (int k = 0; k < cellCount; k++) {
                int cellIndex = cellIndices[k];
                Cell cell = cells[cellIndex];
                if (cell == null) continue;
                if (_gridTopology == GridTopology.Box) {
                    for (int j = 0; j < 9; j++) {
                        int sr = (j / 3) - 1;
                        int sc = (j % 3) - 1;
                        int newRow = cell.row + sr;
                        int newColumn = cell.column + sc;
                        Cell newCell = CellGetAtPosition(newColumn, newRow);
                        if (newCell == null) continue;
                        if (!cellIndices.Contains(newCell.index)) {
                            cellIndices.Add(newCell.index);
                        }
                    }
                }
                else {
                    for (int j = 0; j < 8; j++) {
                        if (GetAdjacentCellCoordinates(cellIndex, (CELL_SIDE)j, out int newRow, out int newColumn, out CELL_SIDE otherSide)) {
                            Cell newCell = CellGetAtPosition(newColumn, newRow);
                            if (newCell == null) continue;
                            if (!cellIndices.Contains(newCell.index)) {
                                cellIndices.Add(newCell.index);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Returns cell's territory index to which it belongs to, or -1 if error.
        /// </summary>
        public int CellGetTerritoryIndex (int cellIndex) {
            if (!ValidCellIndex(cellIndex)) return -1;
            return cells[cellIndex].territoryIndex;
        }


        /// <summary>
        /// Returns cell's territory region index to which it belongs to, or -1 if error.
        /// </summary>
        public int CellGetTerritoryRegionIndex (int cellIndex) {
            if (!ValidCellIndex(cellIndex)) return -1;
            int terrIndex = cells[cellIndex].territoryIndex;
            Territory terr = territories[terrIndex];
            if (terr.regions.Count == 1) return 0;

            Cell cell = cells[cellIndex];
            List<Cell> regionCells = new List<Cell>();
            for (int k = 0; k < terr.regions.Count; k++) {
                TerritoryGetCells(terrIndex, k, regionCells);
                if (regionCells.Contains(cell)) {
                    return k;
                }
            }
            return -1;
        }

        /// <summary>
        /// Returns current cell's fill color
        /// </summary>
        public Color CellGetColor (int cellIndex) {
            if (cellIndex < 0 || cellIndex >= cells.Count || cells[cellIndex].region.customMaterial == null)
                return new Color(0, 0, 0, 0);
            return cells[cellIndex].region.customMaterial.color;
        }

        /// <summary>
        /// Returns current cell's fill texture
        /// </summary>
        public Texture2D CellGetTexture (int cellIndex) {
            if (cellIndex < 0 || cellIndex >= cells.Count || cells[cellIndex].region.customMaterial == null)
                return null;
            return (Texture2D)cells[cellIndex].region.customMaterial.mainTexture;
        }


        /// <summary>
        /// Sets current cell's fill color. Use CellToggleRegionSurface for more options
        /// </summary>
        public void CellSetColor (int cellIndex, Color color) {
            CellToggleRegionSurface(cellIndex, true, color, false, null, Misc.Vector2one, Misc.Vector2zero, 0, false, false, isCanvasTexture: false);
        }


        /// <summary>
        /// Sets current cell's fill color. Use CellToggleRegionSurface for more options
        /// </summary>
        public void CellSetColor (Cell cell, Color color) {
            int cellIndex = CellGetIndex(cell);
            CellSetColor(cellIndex, color);
        }

        /// <summary>
        /// Sets cells' fill color.
        /// </summary>
        public void CellSetColor (List<int> cellIndices, Color color) {
            if (cellIndices == null) return;
            int cellCount = cellIndices.Count;
            for (int k = 0; k < cellCount; k++) {
                CellToggleRegionSurface(cellIndices[k], color.a > 0, color, false, null, Misc.Vector2one, Misc.Vector2zero, 0, false, false, isCanvasTexture: false);
            }
        }

        /// <summary>
        /// Sets current cell's fill texture. Use CellToggleRegionSurface for more options
        /// </summary>
        /// <param name="isCanvasTexture">If true, the texture is assumed to fill the entire grid or canvas so only a portion of the texture would be visible in the cell</param>
        public void CellSetTexture (Cell cell, Texture2D texture, bool isCanvasTexture = false, float textureScale = 1f) {
            int cellIndex = CellGetIndex(cell);
            CellSetTexture(cellIndex, texture, isCanvasTexture, textureScale);
        }


        /// <summary>
        /// Sets current cell's fill texture. Use CellToggleRegionSurface for more options
        /// </summary>
        public void CellSetTexture (int cellIndex, Texture2D texture, bool isCanvasTexture = false, float textureScale = 1f) {
            if (texture != null) {
                CellToggleRegionSurface(cellIndex, true, Color.white, false, texture, isCanvasTexture, textureScale);
            }
            else {
                CellClear(cellIndex);
            }
        }

        /// <summary>
        /// Sets current cell's fill texture. Use CellToggleRegionSurface for more options
        /// </summary>
        /// <param name="isCanvasTexture">If true, the texture is assumed to fill the entire grid or canvas so only a portion of the texture would be visible in the cell</param>
        public void CellSetTexture (Cell cell, Texture2D texture, Color tintColor, bool isCanvasTexture = false) {
            int cellIndex = CellGetIndex(cell);
            CellSetTexture(cellIndex, texture, tintColor, isCanvasTexture);
        }


        /// <summary>
        /// Sets current cell's fill texture. Use CellToggleRegionSurface for more options
        /// </summary>
        /// <param name="isCanvasTexture">If true, the texture is assumed to fill the entire grid or canvas so only a portion of the texture would be visible in the cell</param>
        public void CellSetTexture (int cellIndex, Texture2D texture, Color tintColor, bool isCanvasTexture = false, float textureScale = 1f) {
            if (texture != null) {
                CellToggleRegionSurface(cellIndex, true, tintColor, false, texture, isCanvasTexture, textureScale);
            }
            else {
                CellClear(cellIndex);
            }
        }

        /// <summary>
        /// Sets current cell's fill texture. Use CellToggleRegionSurface for more options
        /// </summary>
        /// <param name="isCanvasTexture">If true, the texture is assumed to fill the entire grid or canvas so only a portion of the texture would be visible in the cell</param>
        public void CellSetTexture (Cell cell, Texture2D texture, Color tintColor, Vector2 textureScale, Vector2 textureOffset, bool isCanvasTexture = false) {
            int cellIndex = CellGetIndex(cell);
            CellSetTexture(cellIndex, texture, tintColor, textureScale, textureOffset, textureRotation: 0, isCanvasTexture);
        }


        /// <summary>
        /// Sets current cell's fill texture. Use CellToggleRegionSurface for more options
        /// </summary>
        /// <param name="isCanvasTexture">If true, the texture is assumed to fill the entire grid or canvas so only a portion of the texture would be visible in the cell</param>
        /// <param name="textureRotation">Rotation degrees</param>
        public void CellSetTexture (int cellIndex, Texture2D texture, Color tintColor, Vector2 textureScale, Vector2 textureOffset, float textureRotation = 0, bool isCanvasTexture = false) {
            if (texture != null) {
                CellToggleRegionSurface(cellIndex, true, tintColor, false, texture, textureScale, textureOffset, textureRotation, localSpace: true, isCanvasTexture);
            }
            else {
                CellClear(cellIndex);
            }
        }

        /// <summary>
        /// Assigns a custom material to a cell
        /// </summary>
        public GameObject CellSetMaterial (int cellIndex, Material material) {
            if (!ValidCellIndex(cellIndex)) return null;
            Color color = material.HasProperty(ShaderParams.Color) ? material.GetColor(ShaderParams.Color) : Color.white;
            Texture2D tex = null;
            if (material.HasProperty(ShaderParams.MainTex)) {
                tex = material.GetTexture(ShaderParams.MainTex) as Texture2D;
            }
            else if (material.HasProperty(ShaderParams.BaseMap)) {
                tex = material.GetTexture(ShaderParams.BaseMap) as Texture2D;
            }
            GameObject o = CellToggleRegionSurface(cellIndex, true, color, false, tex);
            Region region = cells[cellIndex].region;
            region.customMaterial = material;
            region.customRotateInLocalSpace = false;
            region.customTextureOffset = Vector2.zero;
            region.customTextureRotation = 0;
            region.customTextureScale = Vector2.one;
            region.customIsCanvasTexture = false;
            ApplyMaterialToSurface(region, material);
            return o;
        }

        /// <summary>
        /// Sets a cell overlay mode by setting the material 
        /// </summary>
        public void CellSetOverlayMode (int cellIndex, bool overlayMode) {
            if (!ValidCellIndex(cellIndex)) return;
            Region region = cells[cellIndex].region;
            Material mat = region.customMaterial;
            if (mat == null) return;
            CellToggleRegionSurface(cellIndex, true, mat.color, false, (Texture2D)mat.mainTexture, region.customTextureScale, region.customTextureOffset, region.customTextureRotation, overlayMode, region.customRotateInLocalSpace, region.customIsCanvasTexture);
        }

        /// <summary>
        /// Returns true if cell is in overlay mode
        /// </summary>
        public bool CellGetOverlayMode (int cellIndex) {
            if (!ValidCellIndex(cellIndex)) return false;
            Region region = cells[cellIndex].region;
            Material mat = region.customMaterial;
            if (mat == null) return false;
            if (mat.HasProperty(ShaderParams.ZTest)) {
                return mat.GetInt(ShaderParams.ZTest) == (int)UnityEngine.Rendering.CompareFunction.Always;
            }
            return false;
        }

        /// <summary>
        /// Returns current cell's fill texture index (if texture exists in textures list).
        /// Texture index is from 1..32. It will return 0 if texture does not exist or it does not match any texture in the list of textures.
        /// </summary>
        public int CellGetTextureIndex (int cellIndex) {
            if (!ValidCellIndex(cellIndex) || cells[cellIndex].region.customMaterial == null)
                return 0;
            Texture2D tex = (Texture2D)cells[cellIndex].region.customMaterial.mainTexture;
            if (textures != null) {
                for (int k = 1; k < textures.Length; k++) {
                    if (tex == textures[k])
                        return k;
                }
            }
            return 0;
        }

        /// <summary>
        /// Returns the scale of the texture of a given cell
        /// </summary>
        public Vector2 CellGetTextureScale (int cellIndex) {
            if (!ValidCellIndex(cellIndex)) return Vector2.one;
            Material mat = cells[cellIndex].region.customMaterial;
            if (mat == null) return Vector2.one;
            return mat.mainTextureScale;
        }

        /// <summary>
        /// Returns the offset of the texture of a given cell
        /// </summary>
        public Vector2 CellGetTextureOffset (int cellIndex) {
            if (!ValidCellIndex(cellIndex)) return Vector2.zero;
            Material mat = cells[cellIndex].region.customMaterial;
            if (mat == null) return Vector2.zero;
            return mat.mainTextureOffset;
        }

        /// <summary>
        /// Returns cell's row or -1 if cellIndex is not valid.
        /// </summary>
        public int CellGetRow (int cellIndex) {
            if (!ValidCellIndex(cellIndex))
                return -1;
            return cells[cellIndex].row;
        }

        /// <summary>
        /// Returns cell's column or -1 if cellIndex is not valid.
        /// </summary>
        public int CellGetColumn (int cellIndex) {
            if (!ValidCellIndex(cellIndex))
                return -1;
            return cells[cellIndex].column;
        }



        public bool CellIsBorder (int cellIndex) {

            if (!ValidCellIndex(cellIndex)) return false;

            Cell cell = cells[cellIndex];
            if (_gridTopology == GridTopology.Irregular) {
                int segmentCount = cell.region.segments.Count;
                for (int k = 0; k < segmentCount; k++) {
                    if (cell.region.segments[k].border) return true;
                }
                return false;
            }
            return (cell.column == 0 || cell.column == _cellColumnCount - 1 || cell.row == 0 || cell.row == _cellRowCount - 1);

        }


        /// <summary>
        /// Returns the index of a territory in the territory array by its reference.
        /// </summary>
        public int TerritoryGetIndex (Territory territory) {
            if (territory == null)
                return -1;
            if (territoryLookup.TryGetValue(territory, out int index))
                return index;
            else
                return -1;
        }

        /// <summary>
        /// Returns true if cell is visible
        /// </summary>
        public bool CellIsVisible (int cellIndex) {
            if (!ValidCellIndex(cellIndex)) return false;
            return cells[cellIndex].visible;
        }


        /// <summary>
        /// Merges cell2 into cell1. Cell2 is removed.
        /// Only cells which are neighbours can be merged.
        /// </summary>
        public bool CellMerge (Cell cell1, Cell cell2) {
            if (cell1 == null || cell2 == null)
                return false;
            if (!cell1.neighbours.Contains(cell2))
                return false;
            cell1.center = (cell2.center + cell1.center) / 2.0f;

            // Remove cell1 current surface
            CellDestroySurface(cell1.index);

            // Polygon UNION operation between both regions
            PolygonClipper pc = new PolygonClipper(cell1.region.polygon, cell2.region.polygon);
            pc.Compute(PolygonOp.UNION);

            // Remove cell2 from lists
            CellRemove(cell2);

            // Updates geometry data on cell1
            Polygon poly = pc.subject;
            cell1.region.polygon = poly;

            // Update segments list
            int pointsCount = poly.contours[0].points.Count;
            int oldCell1SegmentsCount = cell1.region.segments.Count;
            int oldCell2SegmentsCount = cell2.region.segments.Count;
            List<Segment> newSegments = new List<Segment>(pointsCount);
            Contour contour = poly.contours[0];
            for (int k = 0; k < pointsCount; k++) {
                Segment s = contour.GetSegment(k);
                bool found = false;
                // try to find the old segment from cell1 that corresponds to this new segment in poly
                for (int j = 0; j < oldCell1SegmentsCount; j++) {
                    Segment o = cell1.region.segments[j];
                    if ((Point.EqualsBoth(o.start, s.start) && Point.EqualsBoth(o.end, s.end)) || (Point.EqualsBoth(o.end, s.start) && Point.EqualsBoth(o.start, s.end))) {
                        newSegments.Add(o);
                        found = true;
                        break;
                    }
                }
                if (!found) {
                    // try to find the old segment in cell2 instead
                    for (int j = 0; j < oldCell2SegmentsCount; j++) {
                        Segment o = cell2.region.segments[j];
                        if ((Point.EqualsBoth(o.start, s.start) && Point.EqualsBoth(o.end, s.end)) || (Point.EqualsBoth(o.end, s.start) && Point.EqualsBoth(o.start, s.end))) {
                            newSegments.Add(o);
                            break;
                        }
                    }
                }
            }
            // Assign the new segment list
            cell1.region.segments = newSegments;

            // Refresh rect2D
            CellUpdateBounds(cell1);

            // Refresh neighbours
            CellsUpdateNeighbours();

            needResortCells = true;

            // Refresh territories
            if (territoriesAreUsed) {
                FindTerritoriesFrontiers();
                UpdateTerritoriesBoundary();
            }

            if (cell1 == _cellLastOver) {
                ClearLastOver();
            }

            return true;
        }


        /// <summary>
        /// Removes a cell from the cells and territories lists. Note that this operation only removes cell structure but does not affect polygons - mostly internally used
        /// </summary>
        /// <param name="cellIndex"></param>
        public void CellRemove (int cellIndex) {
            if (cells == null || cellIndex < 0 || cellIndex >= cells.Count) return;
            CellRemove(cells[cellIndex]);
        }


        /// <summary>
        /// Removes a cell from the cells and territories lists. Note that this operation only removes cell structure but does not affect polygons - mostly internally used
        /// </summary>
        public void CellRemove (Cell cell) {
            if (cell == _cellHighlighted)
                HideCellHighlight();
            if (cell == _cellLastOver) {
                ClearLastOver();
            }
            int territoryIndex = cell.territoryIndex;
            if (territoryIndex >= 0 && territoryIndex < territories.Count) {
                Territory territory = territories[territoryIndex];
                if (territory.cells != null && territory.cells.Contains(cell)) {
                    territory.cells.Remove(cell);
                }
            }

            // remove from sorted list
            if (sortedCells.Contains(cell)) {
                sortedCells.Remove(cell);
            }

            CellDestroySurface(cell.index);

            // remove cell from global list
            if (cell.index >= 0) {
                cells[cell.index] = null;
            }

            needRefreshRouteMatrix = true;
            needUpdateTerritories = true;
            issueRedraw = RedrawType.Full;
        }

        /// <summary>
        /// Tags a cell with a user-defined integer tag. Cell can be later retrieved very quickly using CellGetWithTag.
        /// </summary>
        public void CellSetTag (Cell cell, int tag) {
            // remove previous tag register
            cellTagged.Remove(cell.tag);
            // override existing tag
            cellTagged.Remove(tag);
            cellTagged.Add(tag, cell);
            cell.tag = tag;
        }

        /// <summary>
        /// Tags a cell with a user-defined integer tag. Cell can be later retrieved very quickly using CellGetWithTag.
        /// </summary>
        public void CellSetTag (int cellIndex, int tag) {
            if (cellIndex < 0 || cellIndex >= cells.Count)
                return;
            CellSetTag(cells[cellIndex], tag);
        }

        /// <summary>
        /// Returns the tag value of a given cell.
        /// </summary>
        public int CellGetTag (int cellIndex) {
            if (!ValidCellIndex(cellIndex)) return 0;
            return cells[cellIndex].tag;
        }

        /// <summary>
        /// Retrieves Cell object with associated tag.
        /// </summary>
        public Cell CellGetWithTag (int tag) {
            Cell cell;
            if (cellTagged.TryGetValue(tag, out cell))
                return cell;
            return null;
        }

        /// <summary>
        /// Returns the shape/surface gameobject of the cell.
        /// </summary>
        /// <returns>The get game object.</returns>
        /// <param name="cellIndex">Cell index.</param>
        public GameObject CellGetGameObject (int cellIndex) {
            if (!ValidCellIndex(cellIndex)) return null;
            Cell cell = cells[cellIndex];
            if (cell.region.surfaceGameObject != null)
                return cell.region.surfaceGameObject;
            GameObject go = CellToggleRegionSurface(cellIndex, true, Misc.ColorNull);
            CellToggleRegionSurface(cellIndex, false, Misc.ColorNull);
            return go;
        }


        /// <summary>
        /// Extrudes the surface of a cell
        /// </summary>
        /// <param name="cellIndex"></param>
        /// <param name="extrusionAmount"></param>
        public GameObject CellGetExtrudedGameObject (int cellIndex, float extrusionAmount = 1f) {
            if (!ValidCellIndex(cellIndex)) return null;

            Poly2Tri.Polygon poly = GetPolygon(cells[cellIndex].region, ref tempPolyPoints, out int pointCount, _cellFillPadding);
            if (poly == null)
                return null;

            GameObject cellSurface = CellGetGameObject(cellIndex);
            if (cellSurface == null) return null;

            return ExtrudeGameObject(cellSurface, extrusionAmount, tempPolyPoints, pointCount);
        }

        /// <summary>
        /// Returns true if a given cell can be crossed by using the pathfinding engine.
        /// </summary>
        public bool CellGetCanCross (int cellIndex) {
            if (!ValidCellIndex(cellIndex)) return false;
            return cells[cellIndex].canCross;
        }


        /// <summary>
        /// Specifies if a given cell can be crossed by using the pathfinding engine.
        /// </summary>
        public void CellSetCanCross (int cellIndex, bool canCross) {
            if (!ValidCellIndex(cellIndex)) return;
            cells[cellIndex].canCross = canCross;
            needRefreshRouteMatrix = true;
        }

        /// <summary>
        /// Specifies if a list of cells can be crossed by using the pathfinding engine.
        /// </summary>
        public void CellSetCanCross (List<int> cellIndices, bool canCross) {
            int count = cellIndices.Count;
            for (int k = 0; k < count; k++) {
                Cell cell = cells[cellIndices[k]];
                cell.canCross = canCross;
            }
            needRefreshRouteMatrix = true;
        }

        /// <summary>
        /// Sets the additional cost of crossing an hexagon side.
        /// </summary>
        /// <param name="cellIndex">Cell index.</param>
        /// <param name="side">Side of the hexagon.</param>
        /// <param name="cost">Crossing cost.</param>
        public void CellSetSideCrossCost (int cellIndex, CELL_SIDE side, float cost, CELL_DIRECTION direction = CELL_DIRECTION.Any) {
            if (cellIndex < 0 || cellIndex >= cells.Count)
                return;
            Cell cell = cells[cellIndex];
            if (direction != CELL_DIRECTION.Entering) {
                cell.SetSideCrossCost(side, cost);
            }
            if (direction != CELL_DIRECTION.Exiting) {
                int or, oc;
                CELL_SIDE os;
                if (GetAdjacentCellCoordinates(cellIndex, side, out or, out oc, out os)) {
                    int oindex = CellGetIndex(or, oc);
                    if (oindex >= 0) {
                        cells[oindex].SetSideCrossCost(os, cost);
                    }
                }
            }
#if UNITY_EDITOR
            ResetDebugInfo();
#endif
        }

        /// <summary>
        /// Gets the cost of crossing any hexagon side.
        /// </summary>
        /// <param name="cellIndex">Cell index.</param>
        /// <param name="side">Side of the cell.</param>/// 
        /// <param name="direction">The movementDirection for getting the cost. Entering or exiting values are acceptable. Both will return the entering cost.</param>
        public float CellGetSideCrossCost (int cellIndex, CELL_SIDE side, CELL_DIRECTION direction = CELL_DIRECTION.Any) {
            if (!ValidCellIndex(cellIndex)) return 0;
            Cell cell = cells[cellIndex];
            float cost = 0;
            if (direction != CELL_DIRECTION.Entering) {
                cost = cell.GetSideCrossCost(side);
                if (direction == CELL_DIRECTION.Exiting) return cost;
            }
            if (GetAdjacentCellCoordinates(cellIndex, side, out int or, out int oc, out CELL_SIDE os)) {
                int oindex = CellGetIndex(or, oc);
                float cost2 = cells[oindex].GetSideCrossCost(os);
                if (cost2 > cost) cost = cost2;
            }
            return cost;
        }

        /// <summary>
        /// Makes a side of a cell block the LOS.
        /// </summary>
        /// <param name="cellIndex">Cell index.</param>
        /// <param name="side">Side of the cell.</param>
        /// <param name="blocks">Status of the block.</param>
        public void CellSetSideBlocksLOS (int cellIndex, CELL_SIDE side, bool blocks, CELL_DIRECTION direction = CELL_DIRECTION.Any) {
            if (!ValidCellIndex(cellIndex)) return;
            if (direction == CELL_DIRECTION.Any || direction == CELL_DIRECTION.Entering) {
                Cell cell = cells[cellIndex];
                cell.SetSideBlocksLOS(side, blocks);
            }

            if (direction == CELL_DIRECTION.Any || direction == CELL_DIRECTION.Exiting) {
                GetAdjacentCellCoordinates(cellIndex, side, out int or, out int oc, out CELL_SIDE os);
                if (or >= 0 && or < _cellRowCount && oc >= 0 && oc < _cellColumnCount) {
                    int oindex = CellGetIndex(or, oc);
                    if (oindex >= 0) {
                        cells[oindex].SetSideBlocksLOS(os, blocks);
                    }
                }
            }
        }

        /// <summary>
        /// Returns true if the side of a cell blocks LOS.
        /// </summary>
        /// <param name="cellIndex">Cell index.</param>
        /// <param name="side">Side of the cell.</param>/// 
        public bool CellGetSideBlocksLOS (int cellIndex, CELL_SIDE side, CELL_DIRECTION direction = CELL_DIRECTION.Any) {
            if (!ValidCellIndex(cellIndex)) return false;
            Cell cell = cells[cellIndex];
            if (direction == CELL_DIRECTION.Any || direction == CELL_DIRECTION.Entering) {
                if (cell.GetSideBlocksLOS(side))
                    return true;
            }

            if (direction == CELL_DIRECTION.Any || direction == CELL_DIRECTION.Exiting) {
                GetAdjacentCellCoordinates(cellIndex, side, out int or, out int oc, out CELL_SIDE os);
                if (or >= 0 && or < _cellRowCount && oc >= 0 && oc < _cellColumnCount) {
                    int oindex = CellGetIndex(or, oc);
                    return cells[oindex].GetSideBlocksLOS(os);
                }
            }

            return false;
        }


        /// <summary>
        /// Sets cost of entering or exiting a given hexagonal cell across any edge.
        /// </summary>
        /// <param name="cellIndex">Cell index.</param>
        /// <param name="cost">Crossing cost.</param>
        public void CellSetCrossCost (int cellIndex, float cost, CELL_DIRECTION direction = CELL_DIRECTION.Entering) {
            if (!ValidCellIndex(cellIndex)) return;
            for (int side = 0; side < 8; side++) {
                CellSetSideCrossCost(cellIndex, (CELL_SIDE)side, cost, direction);
            }
        }


        /// <summary>
        /// Returns the cost of entering or exiting a given hexagonal cell without specifying a specific edge. This method is used along CellSetCrossCost which doesn't take into account per-edge costs.
        /// </summary>
        /// <param name="cellIndex">Cell index.</param>
        public float CellGetCrossCost (int cellIndex) {
            if (!ValidCellIndex(cellIndex)) return 0;
            return cells[cellIndex].GetSidesCost();
        }

        /// <summary>
        /// Specifies the cell group (by default 1) used by FindPath cellGroupMask optional argument
        /// </summary>
        public void CellSetGroup (int cellIndex, int group) {
            if (cellIndex < 0 || cellIndex >= cells.Count)
                return;
            if (cells[cellIndex] == null) return;
            cells[cellIndex].group = group;
            needRefreshRouteMatrix = true;
        }

        /// <summary>
        /// Returns cell group (default 1)
        /// </summary>
        public int CellGetGroup (int cellIndex) {
            if (cellIndex < 0 || cellIndex >= cells.Count)
                return -1;
            return cells[cellIndex].group;
        }



        /// <summary>
        /// Returns the indices of all cells belonging to a group in the indices array which must be fully allocated when passed. Also the length of this array determines the maximum number of indices returned.
        /// This method returns the actual number of indices returned, regardless of the length the array. This design helps reduce heap allocations.
        /// </summary>
        /// <param name="useBitMaskComparison">When enabled, the group will be compared using a bitwise comparison instead of an exact match</param>
        public int CellGetFromGroup (int group, int[] indices, bool useBitWiseComparison = false) {
            if (indices == null || cells == null)
                return 0;
            int cellCount = cells.Count;
            int count = 0;
            int indicesLength = indices.Length;
            if (useBitWiseComparison) {
                for (int k = 0; k < cellCount && k < indicesLength; k++) {
                    Cell cell = cells[k];
                    if (cell != null && (cell.group & group) != 0) {
                        indices[count++] = k;
                    }
                }
            }
            else {
                for (int k = 0; k < cellCount && k < indicesLength; k++) {
                    Cell cell = cells[k];
                    if (cell != null && cell.group == group) {
                        indices[count++] = k;
                    }
                }
            }
            return count;
        }


        /// <summary>
        /// Returns the indices of all cells belonging to a group in the indices array which must be fully allocated when passed. Also the length of this array determines the maximum number of indices returned.
        /// This method returns the actual number of indices returned, regardless of the length the array. This design helps reduce heap allocations.
        /// </summary>
        /// <param name="useBitMaskComparison">When enabled, the group will be compared using a bitwise comparison instead of an exact match</param>
        public int CellGetFromGroup (int group, List<int> indices, bool useBitWiseComparison = false) {
            if (indices == null || cells == null)
                return 0;
            int cellCount = cells.Count;
            if (useBitWiseComparison) {
                for (int k = 0; k < cellCount; k++) {
                    Cell cell = cells[k];
                    if (cell != null && (cell.group & group) != 0) {
                        indices.Add(k);
                    }
                }
            }
            else {
                for (int k = 0; k < cellCount; k++) {
                    Cell cell = cells[k];
                    if (cell != null && cell.group == group) {
                        indices.Add(k);
                    }
                }
            }
            return indices.Count;
        }


        /// <summary>
        /// Specifies if a given cell is visible.
        /// </summary>
        /// <param name="excludeFromAnyTerritory">If true, cell won't be part of any territory. Territory borders will be updated.</param>
        public void CellSetVisible (int cellIndex, bool visible, bool excludeFromAnyTerritory = false) {
            if (cellIndex < 0 || cellIndex >= cells.Count)
                return;
            Cell cell = cells[cellIndex];
            if (cell.visible == visible)
                return; // nothing to do

            cell.visible = visible;
            if (cellIndex == _cellLastOverIndex) {
                ClearLastOver();
            }
            needRefreshRouteMatrix = true;
            refreshCellMesh = true;
            if (excludeFromAnyTerritory) {
                if (cell.territoryIndex >= 0) {
                    cell.territoryIndex = -1;
                }
                needUpdateTerritories = true;
            }
            issueRedraw = RedrawType.Full;
        }


        CELL_SIDE GetSideByVector (Vector2 dir) {
            switch (_gridTopology) {
                case GridTopology.Box:
                    if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y)) {
                        return dir.x < 0 ? CELL_SIDE.Right : CELL_SIDE.Left;
                    }
                    else {
                        return dir.y < 0 ? CELL_SIDE.Top : CELL_SIDE.Bottom;
                    }
                default:
                    // hexagons
                    if (_pointyTopHexagons) {
                        if (dir.y == 0) {
                            return dir.x < 0 ? CELL_SIDE.Right : CELL_SIDE.Left;
                        }
                        else if (dir.y < 0) {
                            return dir.x < 0 ? CELL_SIDE.TopRight : CELL_SIDE.TopLeft;
                        }
                        else {
                            return dir.x < 0 ? CELL_SIDE.BottomRight : CELL_SIDE.BottomLeft;
                        }
                    }
                    else {
                        if (dir.x == 0) {
                            return dir.y < 0 ? CELL_SIDE.Top : CELL_SIDE.Bottom;
                        }
                        else if (dir.x < 0) {
                            return dir.y < 0 ? CELL_SIDE.TopRight : CELL_SIDE.BottomRight;
                        }
                        else {
                            return dir.y < 0 ? CELL_SIDE.TopLeft : CELL_SIDE.BottomLeft;
                        }
                    }
            }
        }


        /// <summary>
        /// Sets the cost for going from one cell to another (both cells must be adjacent).
        /// </summary>
        /// <param name="cellStartIndex">Cell start index.</param>
        /// <param name="cellEndIndex">Cell end index.</param>
        public void CellSetCrossCost (int cellStartIndex, int cellEndIndex, float cost) {
            if (cellStartIndex < 0 || cellStartIndex >= cells.Count || cellEndIndex < 0 || cellEndIndex >= cells.Count)
                return;
            CELL_SIDE side = GetSideByVector(cells[cellStartIndex].center - cells[cellEndIndex].center);
            CellSetSideCrossCost(cellEndIndex, side, cost, CELL_DIRECTION.Entering);
        }

        /// <summary>
        /// Returns the cost of going from one cell to another (both cells must be adjacent)
        /// </summary>
        /// <returns>The get cross cost.</returns>
        /// <param name="cellStartIndex">Cell start index.</param>
        /// <param name="cellEndIndex">Cell end index.</param>
        public float CellGetCrossCost (int cellStartIndex, int cellEndIndex) {
            if (cellStartIndex < 0 || cellStartIndex >= cells.Count || cellEndIndex < 0 || cellEndIndex >= cells.Count)
                return 0;
            CELL_SIDE side = GetSideByVector(cells[cellStartIndex].center - cells[cellEndIndex].center);
            return CellGetSideCrossCost(cellEndIndex, side, CELL_DIRECTION.Entering);
        }

        /// <summary>
        /// Specified visibility for a group of cells that lay within a given rectangle
        /// </summary>
        /// <param name="rect">Rect or boundary. If local space is used, coordinates must be in range (-0.5..0.5)</param>
        /// <param name="visible">If set to <c>true</c> visible.</param>
        public bool CellSetVisible (Rect rect, bool visible, bool worldSpace = false) {
            return ToggleCellsVisibility(rect, visible, worldSpace);
        }

        /// <summary>
        /// Specified visibility for a group of cells that lay within a given gameObject
        /// </summary>
        /// <param name="obj">GameObject whose mesh or collider will be used.</param>
        /// <param name="visible">If set to <c>true</c> visible.</param>
        public bool CellSetVisible (GameObject obj, bool visible) {
            Collider collider = obj.GetComponent<Collider>();
            if (collider != null)
                return CellSetVisible(collider.bounds, visible);
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
                return CellSetVisible(renderer.bounds, visible);
            return false;
        }


        /// <summary>
        /// Specified visibility for a group of cells that lay within a given gameObject
        /// </summary>
        /// <param name="renderer">Renderer whose bounds will be used.</param>
        /// <param name="visible">If set to <c>true</c> visible.</param>
        public bool CellSetVisible (Renderer renderer, bool visible) {
            return CellSetVisible(renderer.bounds, visible);
        }


        /// <summary>
        /// Specified visibility for a group of cells that lay within a given collider
        /// </summary>
        /// <param name="collider">Collider that provides boundary.</param>
        /// <param name="visible">If set to <c>true</c> visible.</param>
        public bool CellSetVisible (Collider collider, bool visible) {
            if (collider == null)
                return false;
            return CellSetVisible(collider.bounds, visible);
        }


        /// <summary>
        /// Specified visibility for a group of cells that lay within a given bounds
        /// </summary>
        /// <param name="bounds">Bounds in world space coordinates.</param>
        /// <param name="visible">If set to <c>true</c> visible.</param>
        public bool CellSetVisible (Bounds bounds, bool visible) {
            Rect rect = new Rect();
            Vector3 pos = bounds.min;
            if (!GetLocalHitFromWorldPosition(ref pos))
                return false;
            rect.min = pos;
            pos = bounds.max;
            if (!GetLocalHitFromWorldPosition(ref pos))
                return false;
            rect.max = pos;
            return ToggleCellsVisibility(rect, visible, false);
        }


        /// <summary>
        /// Specifies if a given cell's border is visible.
        /// </summary>
        public void CellSetBorderVisible (int cellIndex, bool visible) {
            if (cellIndex < 0 || cellIndex >= cells.Count)
                return;
            Cell cell = cells[cellIndex];
            cell.borderVisible = visible;
            issueRedraw = RedrawType.Full;
        }


        /// <summary>
        /// Returns the state of the border visibility after CellSetBorderVisible has been called
        /// </summary>
        /// <param name="cellIndex">Cell index.</param>
        public bool CellHasBorderVisible (int cellIndex) {
            if (cellIndex < 0 || cellIndex >= cells.Count)
                return false;
            return cells[cellIndex].borderVisible;
        }



        /// <summary>
        /// Returns the cell object under position in local or worldSpace coordinates
        /// </summary>
        /// <returns>The get at position.</returns>
        /// <param name="position">Position.</param>
        /// <param name="worldSpace">If set to <c>true</c>, position is given in world space coordinate units, otherwise position refer to local coordinates.</param>
        /// <param name="territoryIndex">Optional territory index to restrict the search and make it faster.</param>
        [Obsolete("Use CellGetAtWorldPosition or CellGetAtLocalPosition")]
        public Cell CellGetAtPosition (Vector3 position, bool worldSpace = false, int territoryIndex = -1) {
            return GetCellAtPoint(position, worldSpace, territoryIndex);
        }


        /// <summary>
        /// Returns the cell object under position in local or worldSpace coordinates
        /// </summary>
        /// <returns>The get at position.</returns>
        /// <param name="position">Position.</param>
        /// <param name="territoryIndex">Optional territory index to restrict the search and make it faster.</param>
        public Cell CellGetAtWorldPosition (Vector3 position, int territoryIndex = -1) {
            return GetCellAtPoint(position, true, territoryIndex);
        }


        /// <summary>
        /// Returns the cell object under position in local or worldSpace coordinates
        /// </summary>
        /// <returns>The get at position.</returns>
        /// <param name="position">Position.</param>
        /// <param name="territoryIndex">Optional territory index to restrict the search and make it faster.</param>
        public Cell CellGetAtLocalPosition (Vector3 position, int territoryIndex = -1) {
            return GetCellAtPoint(position, false, territoryIndex);
        }


        /// <summary>
        /// Returns the cell at the mouse position
        /// </summary>
        /// <returns></returns>
        public Cell CellGetAtMousePosition () {
            if (!GetLocalHitFromMousePos(out Vector3 localPoint, out _, out _)) return null;
            Cell cell = GetCellAtLocalPoint(localPoint.x, localPoint.y);
            return cell;
        }

        /// <summary>
        /// Returns the indices of the cells within or under a volume
        /// </summary>
        /// <param name="bounds">The bounds of volume or area in world space coordinates, for example the collider bounds.</param>
        /// <param name="cellIndices">An initialized list where results will be added</param>
        /// <param name="padding">Optional margin that's added or substracted to the resulting area.</param>
        /// <param name="checkAllVertices">If true, not only the center of the cell but all cell vertices will be checked</param>
        public int CellGetInArea (Bounds bounds, List<int> cellIndices, float padding = 0, bool checkAllVertices = false) {
            return GetCellInArea(bounds, cellIndices, padding, checkAllVertices);
        }

        /// <summary>
        /// Returns the indices of the cells within or under a volume
        /// </summary>
        /// <param name="gameObject">The gameobject that covers the desired cells</param>
        /// <param name="cellIndices">An initialized list where results will be added</param>
        /// <param name="padding">Optional margin that's added or substracted to the resulting area.</param>
        /// <param name="checkAllVertices">If true, not only the center of the cell but all cell vertices will be checked</param>
        public int CellGetInArea (GameObject gameObject, List<int> cellIndices, float padding = 0, bool checkAllVertices = false) {
            Collider collider = gameObject.GetComponentInChildren<Collider>();
            if (collider != null) {
                return GetCellInArea(collider.bounds, cellIndices, padding, checkAllVertices);
            }
            Renderer renderer = gameObject.GetComponent<Renderer>();
            if (renderer != null) {
                return GetCellInArea(renderer.bounds, cellIndices, padding, checkAllVertices);
            }
            return 0;
        }

        /// <summary>
        /// Returns the indices of the cells within or under a volume
        /// </summary>
        /// <param name="cellIndices">An initialized list where results will be added</param>
        /// <param name="paddingInLocalSpace">Optional margin that's added or substracted to the resulting area. In this case, padding is specified in local space.</param>
        /// <param name="checkAllVertices">If true, not only the center of the cell but all cell vertices will be checked</param>
        public int CellGetInArea (Vector2 localStartPos, Vector2 localEndPos, List<int> cellIndices, float paddingInLocalSpace = 0, bool checkAllVertices = false) {
            if (cellIndices == null) {
                Debug.LogError("CellGetInArea: cellIndices must be initialized.");
                return 0;
            }
            Vector2 size = new Vector2(Mathf.Abs(localEndPos.x - localStartPos.x), Mathf.Abs(localEndPos.y - localStartPos.y));
            size.x += paddingInLocalSpace * 2f;
            size.y += paddingInLocalSpace * 2f;
            Vector2 center = (localStartPos + localEndPos) * 0.5f;
            center -= size * 0.5f;
            Rect rect = new Rect(center, size);
            return GetCellInArea(rect, cellIndices, checkAllVertices);
        }


        /// <summary>
        /// Returns the indices of the cells under a box collider
        /// </summary>
        /// <param name="cellIndices">An initialized list where results will be added</param>
        /// <param name="resolution">Number of samples taken along one axis of the area. Total samples taken would be resolution * resolution. At each sample, the function checks if there's a cell under that position.</param>
        /// <param name="padding">Optional margin that's added or substracted to the resulting area.</param>
        /// <param name="offset">Optional position offset applied to each sample taken under the area</param>
        public int CellGetInArea (BoxCollider boxCollider, List<int> cellIndices, int resolution, float padding = 0, float offset = 0) {
            if (cellIndices == null) {
                Debug.LogError("CellGetInArea: cellIndices must be initialized.");
                return 0;
            }

            cellIteration++;
            int pointCount = ComputeProjectedBasePositions(boxCollider, resolution, padding, offset);

            cellIndices.Clear();
            for (int k = 0; k < pointCount; k++) {
                Vector3 point = projectedPositions[k];
                Cell cell = GetCellAtPoint(point, true);
                if (cell.iteration != cellIteration) {
                    cell.iteration = cellIteration;
                    cellIndices.Add(cell.index);
                }
            }

            return cellIndices.Count;
        }

        Vector3 FindBaseCorner (BoxCollider boxCollider, float padding) {

            Transform incTransform = boxCollider.transform;
            Vector3 scale = incTransform.localScale;
            Vector3 size = boxCollider.size;

            // Start from the collider center
            Vector3 corner = boxCollider.center;

            // Get to the corner by substracting half lengths and taking into account scaling
            corner -= incTransform.right * (size.x * 0.5f * scale.x + padding * 0.5f);
            corner -= incTransform.forward * (size.z * 0.5f * scale.z + padding * 0.5f);
            corner -= incTransform.up * (size.y * 0.5f * scale.y);
            corner += incTransform.position;
            return corner;
        }

        Vector3[] projectedPositions;
        int ComputeProjectedBasePositions (BoxCollider boxCollider, int resolution = 10, float padding = 0.1f, float offset = 0.0f) {

            Transform incTransform = boxCollider.transform;

            // Make an array of sampling positions
            int resPlusOne = resolution + 1;
            int positionsCount = resPlusOne * resPlusOne;
            if (projectedPositions == null || projectedPositions.Length < positionsCount) {
                projectedPositions = new Vector3[positionsCount];
            }

            int j = 0; // keep track of right axis
            int k = 0; // keep track of forward axis
            Vector3 scale = incTransform.localScale;
            Vector3 size = boxCollider.size;

            Vector3 baseCorner = FindBaseCorner(boxCollider, padding);
            for (int i = 0; i < positionsCount; i++) {
                Vector3 basePos = baseCorner
                         + incTransform.right * ((size.x * scale.x + padding) / resolution * j + offset) // move to the right / resolution
                         + incTransform.forward * ((size.z * scale.z + padding) / resolution * k + offset); // move to forward / resolution

                // Project base position on ground, you can change the plane of projection by changing Vector3.up
                Vector3 projectedPos = Vector3.ProjectOnPlane(basePos, Vector3.up);

                projectedPositions[i] = projectedPos;

                // Next point
                j++;

                // Next line
                if (j >= resPlusOne) {
                    j = 0;
                    k++;
                    if (k >= resPlusOne) k = 0;
                }

            }

            return positionsCount;
        }



        /// <summary>
        /// Returns a list of cells contained in a cone defined by a starting cell, a movementDirection, max distance and an angle for the cone
        /// </summary>
        /// <param name="cellIndices">List where results will be returned. Must be previously initialized.</param>
        /// <returns>The number of found cells (-1 if some index is out of range)</returns>
        public int CellGetWithinCone (int cellIndex, Vector2 direction, float maxDistance, float angle, List<int> cellIndices) {
            if (cellIndices == null) {
                Debug.LogError("CellGetWithinCone: cellIndices parameter must be initialized.");
                return 0;
            }
            cellIndices.Clear();
            if (!ValidCellIndex(cellIndex)) return -1;
            Cell startingCell = cells[cellIndex];
            Vector2 initialPos = startingCell.scaledCenter;
            Vector2 targetPos = initialPos + direction * maxDistance;

            int targetCellIndex = CellGetIndex(targetPos);
            if (targetCellIndex < 0) {
                Vector2 step = direction * startingCell.region.rect2D.width * 0.25f;
                targetPos = initialPos;
                for (int k = 0; k < 2048; k++) {
                    targetPos += step;
                    int stepCellIndex = CellGetIndex(targetPos);
                    if (stepCellIndex < 0) break;
                    targetCellIndex = stepCellIndex;
                }
                if (targetCellIndex < 0) return -1;
            }

            return CellGetWithinCone(cellIndex, targetCellIndex, angle, cellIndices);
        }

        /// <summary>
        /// Returns a list of cells contained in a cone defined by a starting cell, a target cellIndex, a max distance and an angle for the cone
        /// </summary>
        /// <param name="cellIndices">List where results will be returned. Must be previously initialized.</param>
        /// <returns>The number of found cells (-1 if some index is out of range)</returns>
        public int CellGetWithinCone (int cellIndex, int targetCellIndex, float angle, List<int> cellIndices) {
            if (cellIndices == null) {
                Debug.LogError("CellGetWithinCone: cellIndices parameter must be initialized.");
                return 0;
            }
            if (!ValidCellIndex(cellIndex)) return -1;
            if (!ValidCellIndex(targetCellIndex)) return -1;

            Vector2 startPos = cells[cellIndex].center;
            Vector2 endPos = cells[targetCellIndex].center;
            Vector2 v = endPos - startPos;
            float vdot = Vector2.Dot(v, v);
            Vector2 vnorm = v.normalized;
            float maxCos = Mathf.Cos(angle * 0.5f * Mathf.Deg2Rad);

            int distX = Mathf.Abs(cells[targetCellIndex].column - cells[cellIndex].column);
            int distY = Mathf.Abs(cells[targetCellIndex].row - cells[cellIndex].row);
            int dist = Mathf.CeilToInt(Mathf.Sqrt(distX * distX + distY * distY));
            int rowMin = Mathf.Max(0, cells[cellIndex].row - dist);
            int rowMax = Mathf.Min(_cellRowCount - 1, cells[cellIndex].row + dist);
            int columnMin = Mathf.Max(0, cells[cellIndex].column - dist);
            int columnMax = Mathf.Min(_cellColumnCount - 1, cells[cellIndex].column + dist);

            for (int r = rowMin; r <= rowMax; r++) {
                for (int c = columnMin; c <= columnMax; c++) {
                    int foundCellIndex = r * _cellColumnCount + c;
                    if (cells[foundCellIndex] == null) continue;
                    Vector2 pos = cells[foundCellIndex].center;
                    Vector2 w = pos - startPos;
                    float t = Vector2.Dot(w, v);
                    t /= vdot;
                    if (t > 1) continue;
                    float cos = Vector2.Dot(w.normalized, vnorm);
                    if (cos >= maxCos) {
                        cellIndices.Add(foundCellIndex);
                    }
                }
            }
            return cellIndices.Count;
        }

        /// <summary>
        /// Checks if the given cell is neighbour of the territory
        /// </summary>
        public bool CellIsAdjacentToTerritory (int cellIndex, int territoryIndex) {
            if (!ValidCellIndex(cellIndex) || !ValidTerritoryIndex(territoryIndex)) return false;

            Cell cell = cells[cellIndex];
            Territory terr = territories[territoryIndex];
            if (terr.cells.Contains(cell)) return false; // it's not adjacent but contained

            foreach (Cell terrCell in terr.cells) {
                if (terrCell.neighbours.Contains(cell)) return true;
            }

            return false;
        }


        /// <summary>
        /// Checks if the given cell is neighbour of another cell
        /// </summary>
        public bool CellIsAdjacentToCell (int cellIndex, int otherCellIndex) {
            if (!ValidCellIndex(cellIndex) || !ValidCellIndex(otherCellIndex)) return false;

            Cell cell = cells[cellIndex];
            Cell otherCell = cells[otherCellIndex];
            return otherCell.neighbours.Contains(cell);
        }

        /// <summary>
        /// Sets the territory of a cell triggering territory boundary recalculation. Note: changes are not applied immediately, but at end of frame. To apply changes immediately, call Redraw()
        /// </summary>
        /// <returns><c>true</c>, if cell was transferred., <c>false</c> otherwise.</returns>
        public bool CellSetTerritory (int cellIndex, int territoryIndex) {
            if (needGenerateMap) CheckGridChanges();
            if (!ValidCellIndex(cellIndex)) return false;
            int terrCount = territories != null ? territories.Count : 0;
            Cell cell = cells[cellIndex];
            if (cell.territoryIndex == territoryIndex) return true;
            if (cell.territoryIndex >= 0 && cell.territoryIndex < terrCount && territories[cell.territoryIndex].cells.Contains(cell)) {
                Territory territory = territories[cell.territoryIndex];
                territory.isDirty = true;
                territory.cells.Remove(cell);
            }
            cell.territoryIndex = (short)territoryIndex;
            if (territoryIndex >= 0 && territoryIndex < terrCount) {
                Territory territory = territories[territoryIndex];
                territory.isDirty = true;
                territory.cells.Add(cell);
            }
            needUpdateTerritories = true;
            issueRedraw = RedrawType.IncrementalTerritories;
            return true;
        }


        /// <summary>
        /// Sets the territory of a cell triggering territory boundary recalculation
        /// </summary>
        /// <returns><c>true</c>, if cell was transferred., <c>false</c> otherwise.</returns>
        public bool CellSetTerritory (List<int> cellIndices, int territoryIndex) {
            if (needGenerateMap) CheckGridChanges();
            if (!ValidTerritoryIndex(territoryIndex)) return false;
            int terrCount = territories != null ? territories.Count : 0;
            foreach (int cellIndex in cellIndices) {
                if (!ValidCellIndex(cellIndex)) continue;
                Cell cell = cells[cellIndex];
                if (cell.territoryIndex == territoryIndex) return true;
                if (cell.territoryIndex >= 0 && cell.territoryIndex < terrCount && territories[cell.territoryIndex].cells.Contains(cell)) {
                    territories[cell.territoryIndex].isDirty = true;
                    territories[cell.territoryIndex].cells.Remove(cell);
                }
                cell.territoryIndex = (short)territoryIndex;
                if (territoryIndex >= 0 && territoryIndex < terrCount) {
                    territories[territoryIndex].isDirty = true;
                    territories[territoryIndex].cells.Add(cell);
                }
            }
            needUpdateTerritories = true;
            issueRedraw = RedrawType.IncrementalTerritories;
            return true;
        }

        /// <summary>
        /// Returns a string-packed representation of current cells settings.
        /// Each cell separated by ;
        /// Individual settings mean:
        /// Position	Meaning
        /// 0			Visibility (0 = invisible, 1 = visible)
        /// 1			Territory Index
        /// 2			Color R (0..1)
        /// 3			Color G (0..1)
        /// 4			Color B (0..1)
        /// 5			Color A (0..1)
        /// 6			Texture Index
        /// 7		    Cell tag
        /// 8			Can cross? (pathfinding)
        /// </summary>
        /// <returns>The get configuration data.</returns>
        [Obsolete("Use CellGetSettings instead")]
        public string CellGetConfigurationData () {
            StringBuilder sb = new StringBuilder();
            int cellsCount = cells.Count;
            for (int k = 0; k < cellsCount; k++) {
                if (k > 0)
                    sb.Append(";");
                // 0
                Cell cell = cells[k];
                if (cell.visible) {
                    sb.Append("1");
                }
                else {
                    sb.Append("0");
                }
                // 1 territory index
                sb.Append(",");
                sb.Append(cell.territoryIndex);
                // 2 color.a
                sb.Append(",");
                Color color = CellGetColor(k);
                sb.Append(color.a.ToString("F3", CultureInfo.InvariantCulture));
                // 3 color.r
                sb.Append(",");
                sb.Append(color.r.ToString("F3", CultureInfo.InvariantCulture));
                // 4 color.g
                sb.Append(",");
                sb.Append(color.g.ToString("F3", CultureInfo.InvariantCulture));
                // 5 color.b
                sb.Append(",");
                sb.Append(color.b.ToString("F3", CultureInfo.InvariantCulture));
                // 6 texture index
                sb.Append(",");
                sb.Append(CellGetTextureIndex(k));
                // 7 tag
                sb.Append(",");
                sb.Append(cell.tag);
                // 8 can cross
                sb.Append(",");
                sb.Append(cell.canCross ? 1 : 0);
            }
            return sb.ToString();
        }


        /// <summary>
        /// Returns an array with the main settings of each cell
        /// </summary>
        /// <returns>The get settings.</returns>
        public TGSConfigEntry[] CellGetSettings () {
            if (cells == null)
                return null;
            int cellCount = cells.Count;
            TGSConfigEntry[] cellSettings = new TGSConfigEntry[cellCount];
            for (int k = 0; k < cellCount; k++) {
                Cell cell = cells[k];
                if (cell == null)
                    continue;
                cellSettings[k].territoryIndex = cell.territoryIndex;
                cellSettings[k].visible = cell.visibleSelf;
                cellSettings[k].color = CellGetColor(k);
                cellSettings[k].textureIndex = CellGetTextureIndex(k);
                cellSettings[k].tag = cell.tag;
                cellSettings[k].canCross = cell.canCross;
                cellSettings[k].crossCost = cell.GetSidesCost();
                cellSettings[k].crossSidesCost = cell.crossCost;
                cellSettings[k].textureScale = CellGetTextureScale(k);
                cellSettings[k].textureOffset = CellGetTextureOffset(k);
                if (cell.hasAttributes) {
                    List<string> keys = cell.attrib.keys;
                    int keysCount = keys.Count;
                    TGSConfigAttribPair[] attribs = new TGSConfigAttribPair[keysCount];
                    for (int j = 0; j < keysCount; j++) {
                        attribs[j].key = keys[j];
                        string stringValue = cell.attrib[j];
                        if (float.TryParse(cell.attrib, out float floatValue)) {
                            attribs[j].numericValue = floatValue;
                        }
                        else {
                            attribs[j].stringValue = stringValue;
                        }
                        cellSettings[k].attribData = attribs;
                    }
                }
            }
            return cellSettings;
        }

        /// <summary>
        /// Sets cell configuration data from a string
        /// </summary>
        /// <param name="cellData"></param>
        /// <param name="filterTerritories"></param>
        [Obsolete("Use CellSetSettings instead")]
        public void CellSetConfigurationData (string cellData, int[] filterTerritories) {
            if (cells == null)
                return;
            string[] cellsInfo = cellData.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            char[] separators = new char[] { ',' };
            if (cellsInfo.Length != cells.Count) {
                Debug.LogWarning("Grids 2D Config component has different cell count than grid itself.");
            }
            for (int k = 0; k < cellsInfo.Length && k < cells.Count; k++) {
                if (cells[k] == null)
                    continue;
                string[] cellInfo = cellsInfo[k].Split(separators, StringSplitOptions.RemoveEmptyEntries);
                int length = cellInfo.Length;
                if (length > 1) {
                    int territoryIndex = Misc.FastConvertToInt(cellInfo[1]);
                    if (filterTerritories != null && !filterTerritories.Contains(territoryIndex))
                        continue;
                    cells[k].territoryIndex = (short)territoryIndex;
                }
                if (length > 0) {
                    cells[k].visible = cellInfo[0] != "0";
                }
                Color color = new Color(0, 0, 0, 0);
                if (length > 5) {
                    Single.TryParse(cellInfo[2], out color.a);
                    if (color.a > 0) {
                        Single.TryParse(cellInfo[3], NumberStyles.Any, CultureInfo.InvariantCulture, out color.r);
                        Single.TryParse(cellInfo[4], NumberStyles.Any, CultureInfo.InvariantCulture, out color.g);
                        Single.TryParse(cellInfo[5], NumberStyles.Any, CultureInfo.InvariantCulture, out color.b);
                    }
                }
                int textureIndex = -1;
                if (length > 6) {
                    textureIndex = Misc.FastConvertToInt(cellInfo[6]);
                }
                if (color.a > 0 || textureIndex >= 1) {
                    CellToggleRegionSurface(k, true, color, false, textureIndex);
                }
                if (length > 7) {
                    CellSetTag(k, Misc.FastConvertToInt(cellInfo[7]));
                }
                if (length > 8) {
                    CellSetCanCross(k, cellInfo[8] != "0");
                }
            }
            needUpdateTerritories = true;
            needRefreshRouteMatrix = true;
            Redraw();
            isDirty = true;
        }

        public void CellSetSettings (TGSConfigEntry[] cellSettings, int[] filterTerritories) {
            if (cellSettings == null)
                return;

            if (cells == null) {
                OnEnable();
                if (cells == null) return;
            }

            if (cellSettings.Length != cells.Count) {
                Debug.LogWarning("Grids 2D Config component has different cell count than grid itself.");
            }
            // Get territory count
            int maxTerritoryIndex = 0;
            for (int k = 0; k < cellSettings.Length; k++) {
                if (cellSettings[k].territoryIndex > maxTerritoryIndex) {
                    maxTerritoryIndex = cellSettings[k].territoryIndex;
                }
            }
            _numTerritories = maxTerritoryIndex + 1;
            for (int k = 0; k < cellSettings.Length && k < cells.Count; k++) {
                int territoryIndex = cellSettings[k].territoryIndex;
                if (filterTerritories != null && !filterTerritories.Contains(territoryIndex))
                    continue;
                Cell cell = cells[k];
                cell.territoryIndex = (short)territoryIndex;
                cell.visible = cellSettings[k].visible;
                Color color = cellSettings[k].color;
                int textureIndex = cellSettings[k].textureIndex;
                if (color.a > 0 || textureIndex >= 1) {
                    Vector2 textureScale = cellSettings[k].textureScale;
                    if (textureScale == Vector2.zero) textureScale = Vector2.one;
                    Vector2 textureOffset = cellSettings[k].textureOffset;
                    CellToggleRegionSurface(k, true, color, false, textureIndex, textureScale, textureOffset);
                }
                cell.tag = cellSettings[k].tag;
                cell.canCross = cellSettings[k].canCross;
                if (cellSettings[k].crossSidesCost != null && cellSettings[k].crossSidesCost.Length > 0) {
                    cell.crossCost = cellSettings[k].crossSidesCost;
                }
                else {
                    cell.SetAllSidesCost(cellSettings[k].crossCost);
                }
                TGSConfigAttribPair[] attribs = cellSettings[k].attribData;
                if (attribs != null) {
                    int keysCount = attribs.Length;
                    for (int j = 0; j < keysCount; j++) {
                        string key = attribs[j].key;
                        if (string.IsNullOrEmpty(key)) continue;
                        if (!string.IsNullOrEmpty(attribs[j].stringValue)) {
                            cell.attrib[key] = attribs[j].stringValue;
                            continue;
                        }
                        cell.attrib[key] = attribs[j].numericValue;
                    }
                }
            }
            needUpdateTerritories = true;
            needRefreshRouteMatrix = true;
            Redraw();
            isDirty = true;
        }

        /// <summary>
        /// Returns the cell located at given row and column
        /// </summary>
        public Cell CellGetAtPosition (int column, int row) {
            if (_gridTopology == GridTopology.Irregular) return null;
            if (column >= _cellColumnCount || column < 0 || row >= _cellRowCount || row < 0) return null;
            int index = row * _cellColumnCount + column;
            if (index >= 0 && index < cells.Count)
                return cells[index];
            return null;
        }

        /// <summary>
        /// Returns two adjacent cells that share an edge defined by v1 and v2 in local coordinates
        /// </summary>
        public void CellGetAdjacents (Vector2 v1, Vector2 v2, out Cell cell1, out Cell cell2) {
            Vector2 midPoint = (v1 + v2) / 2;
            Vector2 direction = (v2 - v1).normalized;
            Vector2 normal = new Vector2(-direction.y, direction.x);

            const float separationAmount = 0.001f;
            Vector2 point1 = midPoint + normal * separationAmount;
            Vector2 point2 = midPoint - normal * separationAmount;

            cell1 = CellGetAtLocalPosition(point1);
            cell2 = CellGetAtLocalPosition(point2);
            if (cell2 == cell1) cell2 = null;
            if (cell1 == null && cell2 != null) {
                (cell1, cell2) = (cell2, cell1);
            }
        }

        /// <summary>
        /// Traces a line between two positions and check if there's no cell blocking the line
        /// </summary>
        /// <returns><c>true</c>, if there's a straight path of non-blocking cells between the two positions<c>false</c> otherwise.</returns>
        /// <param name="startPosition">Start position.</param>
        /// <param name="endPosition">End position.</param>
        /// <param name="cellIndices">Cell indices.</param>
        /// <param name="worldPositions">The world poitions of each sample taken between startPosition and endPosition</param>
        /// <param name="cellGroupMask">Optional cell layer mask</param>
        /// <param name="lineResolution">Resolution of the line. Increase to improve line accuracy.</param>
        /// <param name="exhaustiveCheck">If set to true, all vertices of destination cell will be considered instead of its center</param>
        /// <param name="canCrossCheckType">Different canCross check types</param>
        /// <param name="checkLastCell">If set to true, the last cell will also be evaluated against canCross field and group mask. By default the last or target cell is not checked as long as it results in a visible cell from the starting cell, but if you want to ensure the last cell also passes the canCross or group mask criteria then pass true to this parameter.</param>
        public bool CellGetLineOfSight (Vector3 startPosition, Vector3 endPosition, ref List<int> cellIndices, ref List<Vector3> worldPositions, int cellGroupMask = -1, int lineResolution = 2, bool exhaustiveCheck = false, CanCrossCheckType canCrossCheckType = CanCrossCheckType.Default, bool checkLastCell = false) {

            cellIndices = null;

            Cell startCell = GetCellAtPoint(startPosition, true);
            Cell endCell = GetCellAtPoint(endPosition, true);
            if (startCell == null || endCell == null) {
                return false;
            }

            int cell1 = CellGetIndex(startCell);
            int cell2 = CellGetIndex(endCell);
            if (cell1 < 0 || cell2 < 0)
                return false;

            return CellGetLineOfSight(cell1, cell2, ref cellIndices, ref worldPositions, cellGroupMask, lineResolution, exhaustiveCheck, canCrossCheckType, checkLastCell);
        }


        /// <summary>
        /// Traces a line between two positions and check if there's no cell blocking the line
        /// </summary>
        /// <returns><c>true</c>, if there's a straight path of non-blocking cells between the two positions<c>false</c> otherwise.</returns>
        /// <param name="cellIndices">Cell indices.</param>
        /// <param name="cellGroupMask">Optional cell layer mask</param>
        /// <param name="lineResolution">Resolution of the line. Increase to improve line accuracy.</param>
        /// <param name="exhaustiveCheck">If set to true, all vertices of destination cell will be considered instead of its center</param>
        /// <param name="canCrossCheckType">Different options for handling canCross check</param>
        /// <param name="checkLastCell">If set to true, the last cell will also be evaluated against canCross field and group mask. By default the last or target cell is not checked as long as it results in a visible cell from the starting cell, but if you want to ensure the last cell also passes the canCross or group mask criteria then pass true to this parameter.</param>
        /// <param name="bidirectionalCheck">If set to true, LOS check considers both directions when it crosses an cell side</param>
        public bool CellGetLineOfSight (int startCellIndex, int endCellIndex, ref List<int> cellIndices, ref List<Vector3> worldPositions, int cellGroupMask = -1, int lineResolution = 2, bool exhaustiveCheck = false, CanCrossCheckType canCrossCheckType = CanCrossCheckType.Default, bool checkLastCell = false, bool bidirectionalCheck = false) {

            if (cellIndices == null) {
                cellIndices = new List<int>();
            }
            else {
                cellIndices.Clear();
            }
            if (worldPositions == null) {
                worldPositions = new List<Vector3>();
            }
            else {
                worldPositions.Clear();
            }

            if (!ValidCellIndex(startCellIndex) || !ValidCellIndex(endCellIndex)) return false;

            Vector3 startPosition = CellGetPosition(startCellIndex);
            Vector3 endPosition = CellGetPosition(endCellIndex);
            int vertexCount = exhaustiveCheck ? cells[endCellIndex].region.points.Count : 0;
            bool success = true;

            int numSteps;
            switch (_gridTopology) {
                case GridTopology.Hexagonal:
                    // Hexagon distance
                    numSteps = CellGetHexagonDistance(startCellIndex, endCellIndex);
                    lineResolution = Mathf.Max(2, lineResolution);
                    numSteps *= lineResolution;
                    break;
                case GridTopology.Box:
                    numSteps = CellGetBoxDistance(startCellIndex, endCellIndex);
                    lineResolution = Mathf.Max(2, lineResolution);
                    numSteps *= lineResolution;
                    if (numSteps % 2 == 0)
                        numSteps++;
                    break;
                default:
                    float dist = Vector3.Distance(startPosition, endPosition);
                    numSteps = Mathf.CeilToInt(dist * lineResolution);
                    break;
            }

            Cell firstCell = cells[startCellIndex];
            Cell lastCell = cells[endCellIndex];
            Cell previousCell = firstCell;

            for (int p = 0; p <= vertexCount; p++) {
                if (p > 0) {
                    cellIndices.Clear();
                    worldPositions.Clear();
                    endPosition = CellGetVertexPosition(endCellIndex, p - 1);
                }

                success = true;
                for (int k = 1; k <= numSteps; k++) {
                    Vector3 position = Vector3.Lerp(startPosition, endPosition, (float)k / numSteps);
                    Cell cell = k == numSteps ? lastCell : GetCellAtPoint(position, true);
                    if (cell != null && cell != previousCell) {
                        if (checkLastCell || cell != lastCell) {
                            if ((cell.group & cellGroupMask) == 0) {
                                success = false;
                                break;
                            }
                            if (!cell.canCross) {
                                switch (canCrossCheckType) {
                                    case CanCrossCheckType.Default:
                                        success = false;
                                        break;
                                    case CanCrossCheckType.IgnoreCanCrossCheckOnStartAndEndCells:
                                        if (cell != firstCell && cell != lastCell) {
                                            success = false;
                                        }
                                        break;
                                    case CanCrossCheckType.IgnoreCanCrossCheckOnStartCell:
                                        if (cell != firstCell) {
                                            success = false;
                                        }
                                        break;
                                    case CanCrossCheckType.IgnoreCanCrossCheckOnEndCell:
                                        if (cell != lastCell) {
                                            success = false;
                                        }
                                        break;
                                    case CanCrossCheckType.IgnoreCanCrossCheckOnAllCellsExceptStartAndEndCells:
                                        if (cell == firstCell || cell == lastCell) {
                                            success = false;
                                        }
                                        break;
                                }
                                if (!success) break;
                            }
                        }

                        // Check LOD blocks
                        if (LOSIsBlocked(previousCell, cell) || (bidirectionalCheck && LOSIsBlocked(cell, previousCell))) {
                            success = false;
                            break;
                        }
                        cellIndices.Add(cell.index);
                        previousCell = cell;

                    }
                    worldPositions.Add(position);
                }
                if (success) {
                    if (p == 0 && _gridTopology != GridTopology.Irregular) {
                        return true;
                    }
                    break;
                }
            }
            if (success) {
                CellGetLine(startCellIndex, endCellIndex, ref cellIndices, ref worldPositions, lineResolution);
            }
            return success;
        }


        List<Vector3> dummyPositions;

        /// <summary>
        /// Checks if there's line of sight (LOS) between two cells
        /// </summary>
        /// <returns><c>true</c>, if there's a straight path of non-blocking cells between the two positions<c>false</c> otherwise.</returns>
        /// <param name="startCellIndex">Start cell index.</param>
        /// <param name="endCellIndex">End or target cell index.</param>
        /// <param name="cellGroupMask">Optional cell layer mask</param>
        /// <param name="lineResolution">Resolution of the line. Increase to improve line accuracy.</param>
        /// <param name="exhaustiveCheck">If set to true, all vertices of destination cell will be considered instead of its center</param>
        /// <param name="canCrossCheckType">Different canCross check types</param>
        /// <param name="checkLastCell">If set to true, the last cell will also be evaluated against canCross field and group mask. By default the last or target cell is not checked as long as it results in a visible cell from the starting cell, but if you want to ensure the last cell also passes the canCross or group mask criteria then pass true to this parameter.</param>
        public bool CellTestLineOfSight (int startCellIndex, int endCellIndex, int cellGroupMask = -1, int lineResolution = 2, bool exhaustiveCheck = false, CanCrossCheckType canCrossCheckType = CanCrossCheckType.Default, bool checkLastCell = false) {
            return CellGetLineOfSight(startCellIndex, endCellIndex, ref tempListCells, ref dummyPositions, cellGroupMask, lineResolution, exhaustiveCheck, canCrossCheckType, checkLastCell);
        }

        /// <summary>
        /// Removes any cell from a givel list of indices which are not in LOS
        /// </summary>
        /// <param name="startCellIndex">Start cell index.</param>
        /// <param name="targetCellIndices">Cell indices.</param>
        /// <param name="lineResolution">Line resolution.</param>
        /// <param name="exhaustiveCheck">If set to <c>true</c> exhaustive check.</param>
        /// <param name="canCrossCheckType">Different canCrossCheck options</param>
        /// <param name="checkLastCell">If set to true, the last cell will also be evaluated against canCross field and group mask. By default the last or target cell is not checked as long as it results in a visible cell from the starting cell, but if you want to ensure the last cell also passes the canCross or group mask criteria then pass true to this parameter.</param>
        public void CellTestLineOfSight (int startCellIndex, List<int> targetCellIndices, int cellGroupMask = -1, int lineResolution = 2, bool exhaustiveCheck = false, CanCrossCheckType canCrossCheckType = CanCrossCheckType.Default, bool checkLastCell = false) {
            int count = targetCellIndices.Count;
            cellIteration++;
            for (int k = 0; k < count; k++) {
                int targetCellIndex = targetCellIndices[k];
                if (cells[targetCellIndex].iteration != cellIteration) {
                    if (CellGetLineOfSight(startCellIndex, targetCellIndex, ref tempListCells, ref dummyPositions, cellGroupMask, lineResolution, exhaustiveCheck, canCrossCheckType, checkLastCell)) {
                        int lineCount = tempListCells.Count;
                        for (int j = 0; j < lineCount; j++) {
                            int index = tempListCells[j];
                            cells[index].iteration = cellIteration;
                        }
                    }
                    else {
                        targetCellIndices.RemoveAt(k);
                        k--;
                        count--;
                    }
                }
            }
        }


        bool LOSIsBlocked (Cell cell1, Cell cell2) {
            switch (_gridTopology) {
                case GridTopology.Box:
                    int row1 = cell1.row;
                    int column1 = cell1.column;
                    int row2 = cell2.row;
                    int column2 = cell2.column;
                    if (column1 == column2 && row1 == row2)
                        return false;

                    bool blocksVertically = row1 < row2 ? cell2.GetSideBlocksLOS(CELL_SIDE.Bottom) : cell2.GetSideBlocksLOS(CELL_SIDE.Top);
                    bool blocksHorizontally = column1 < column2 ? cell2.GetSideBlocksLOS(CELL_SIDE.Left) : cell2.GetSideBlocksLOS(CELL_SIDE.Right);

                    if (row1 == row2) {
                        return blocksHorizontally;
                    }
                    else if (column1 == column2) {
                        return blocksVertically;
                    }
                    else {
                        return blocksHorizontally || blocksVertically;
                    }
                default:
                    Vector2 dir = cell2.center - cell1.center;
                    CELL_SIDE side = GetSideByVector(dir);
                    return cell2.GetSideBlocksLOS(side);
            }
        }


        /// <summary>
        /// Returns a line composed of cells and world positions from starting cell to ending cell
        /// </summary>
        /// <returns><c>true</c>, if there's a straight path of non-blocking cells between the two positions<c>false</c> otherwise.</returns>
        /// <param name="cellIndices">Cell indices.</param>
        /// <param name="lineResolution">Resolution of the line. Increase to improve line accuracy.</param>
        public void CellGetLine (int startCellIndex, int endCellIndex, ref List<int> cellIndices, ref List<Vector3> worldPositions, int lineResolution = 2) {

            if (cellIndices == null)
                cellIndices = new List<int>();
            else
                cellIndices.Clear();
            if (worldPositions == null)
                worldPositions = new List<Vector3>();
            else
                worldPositions.Clear();
            if (startCellIndex < 0 || startCellIndex >= cells.Count || endCellIndex < 0 || endCellIndex >= cells.Count)
                return;

            Vector3 startPosition = CellGetPosition(startCellIndex);
            Vector3 endPosition = CellGetPosition(endCellIndex);

            int numSteps;
            switch (_gridTopology) {
                case GridTopology.Hexagonal:
                    // Hexagon distance
                    numSteps = CellGetHexagonDistance(startCellIndex, endCellIndex);
                    lineResolution = Mathf.Max(2, lineResolution);
                    numSteps *= lineResolution;
                    break;
                case GridTopology.Box:
                    numSteps = CellGetBoxDistance(startCellIndex, endCellIndex);
                    lineResolution = Mathf.Max(2, lineResolution);
                    numSteps *= lineResolution;
                    if (numSteps % 2 == 0)
                        numSteps++;
                    break;
                default:
                    float dist = Vector3.Distance(startPosition, endPosition);
                    dist *= 2f / (1f + Mathf.Sqrt(_numCells));
                    numSteps = Mathf.CeilToInt(dist * lineResolution);
                    break;
            }

            Cell lastCell = cells[startCellIndex];
            for (int k = 1; k <= numSteps; k++) {
                Vector3 position = Vector3.Lerp(startPosition, endPosition, (float)k / numSteps);
                Cell cell = GetCellAtPoint(position, true);
                if (cell != null && cell != lastCell) {
                    cellIndices.Add(cell.index);
                }
                worldPositions.Add(position);
                lastCell = cell;
            }
        }

        /// <summary>
        /// Returns the hexagon distance between two cells (number of steps to reach end cell from start cell).
        /// This method does not take into account cell masks or blocking cells. It just returns the distance.
        /// </summary>
        /// <returns>The get hexagon distance.</returns>
        /// <param name="startCellIndex">Start cell index.</param>
        /// <param name="endCellIndex">End cell index.</param>
        public int CellGetHexagonDistance (int startCellIndex, int endCellIndex) {
            if (cells == null)
                return -1;
            int cellCount = cells.Count;
            if (startCellIndex < 0 || startCellIndex >= cellCount || endCellIndex < 0 || endCellIndex >= cellCount)
                return -1;
            int r0 = cells[startCellIndex].row;
            int c0 = cells[startCellIndex].column;
            int r1 = cells[endCellIndex].row;
            int c1 = cells[endCellIndex].column;
            int offset = _evenLayout ? 0 : 1;
            int x0, y0, x1, y1;
            if (_pointyTopHexagons) {
                y0 = r0;
                x0 = c0 - Mathf.FloorToInt((r0 + offset) / 2);
                y1 = r1;
                x1 = c1 - Mathf.FloorToInt((r1 + offset) / 2);
            }
            else {
                y0 = r0 - Mathf.FloorToInt((c0 + offset) / 2);
                x0 = c0;
                y1 = r1 - Mathf.FloorToInt((c1 + offset) / 2);
                x1 = c1;
            }
            int dx = x1 - x0;
            int dy = y1 - y0;
            int numSteps = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
            numSteps = Mathf.Max(numSteps, Mathf.Abs(dx + dy));
            return numSteps;
        }


        /// <summary>
        /// Returns the number of steps between two cells in box topology.
        /// This method does not take into account cell masks or blocking cells. It just returns the distance.
        /// </summary>
        /// <param name="startCellIndex">Start cell index.</param>
        /// <param name="endCellIndex">End cell index.</param>
        public int CellGetBoxDistance (int startCellIndex, int endCellIndex) {
            if (cells == null)
                return -1;
            int cellCount = cells.Count;
            if (startCellIndex < 0 || startCellIndex >= cellCount || endCellIndex < 0 || endCellIndex >= cellCount)
                return -1;
            int r0 = cells[startCellIndex].row;
            int c0 = cells[startCellIndex].column;
            int r1 = cells[endCellIndex].row;
            int c1 = cells[endCellIndex].column;
            int dx = Mathf.Abs(c1 - c0);
            int dy = Mathf.Abs(r1 - r0);
            if (_pathFindingUseDiagonals) {
                return Mathf.Max(dx, dy);
            }
            return dx + dy;
        }

        /// <summary>
        /// Removes any color or texture from a cell and hides it
        /// </summary>
        /// <param name="cellIndex">Cell index.</param>
        public void CellClear (int cellIndex) {
            List<int> cellIndices = new List<int>();
            cellIndices.Add(cellIndex);
            CellClear(cellIndices);
        }

        /// <summary>
        /// Removes any color or texture from a list of cells and hides them
        /// </summary>
        /// <param name="cellIndices">Cell indices.</param>
        public void CellClear (List<int> cellIndices) {
            if (cellIndices == null)
                return;

            int count = cellIndices.Count;
            for (int k = 0; k < count; k++) {
                // Check if cell has a SurfaceFader animator
                int cellIndex = cellIndices[k];
                Cell cell = cells[cellIndex];
                if (cell.region.surfaceGameObject != null) {
                    SurfaceFader sf = cell.region.surfaceGameObject.GetComponent<SurfaceFader>();
                    if (sf != null) {
                        sf.Finish(0);
                    }
                    cell.region.surfaceGameObject.SetActive(false);
                }
                if (cell.region.customMaterial != null) {
                    cell.region.customMaterial = null;
                }
            }
        }


        /// <summary>
        /// Pregenerates and caches cell geometry for faster performance during gameplay
        /// </summary>
        public void WarmCells () {
            int cellCount = cells.Count;
            Material mat = GetColoredTexturedMaterialForCell(Color.white, null, false);
            for (int k = 0; k < cellCount; k++) {
                GenerateCellRegionSurface(k, mat, Misc.Vector2one, Misc.Vector2zero, 0, false, _cellTextureMode, isCanvasTexture: false);
            }
        }


        /// <summary>
        /// Draws a line over a cell side.
        /// </summary>
        /// <returns>The line.</returns>
        /// <param name="cellIndex">Cell index.</param>
        /// <param name="side">Side.</param>
        /// <param name="color">Color.</param>
        /// <param name="width">Width.</param>
        public GameObject DrawLine (int cellIndex, CELL_SIDE side, Color color, float width) {
            int v1 = 0, v2 = 0;
            switch (_gridTopology) {
                case GridTopology.Box:
                    switch (side) {
                        case CELL_SIDE.Right:
                            v1 = 1;
                            v2 = 2;
                            break;
                        case CELL_SIDE.Top:
                            v1 = 2;
                            v2 = 3;
                            break;
                        case CELL_SIDE.Bottom:
                            v1 = 0;
                            v2 = 1;
                            break;
                        case CELL_SIDE.Left:
                            v1 = 3;
                            v2 = 0;
                            break;
                    }
                    break;
                case GridTopology.Hexagonal:
                    if (_pointyTopHexagons) {
                        switch (side) {
                            case CELL_SIDE.BottomLeft:
                                v1 = 0;
                                v2 = 1;
                                break;
                            case CELL_SIDE.Left:
                                v1 = 1;
                                v2 = 2;
                                break;
                            case CELL_SIDE.TopLeft:
                                v1 = 2;
                                v2 = 3;
                                break;
                            case CELL_SIDE.TopRight:
                                v1 = 3;
                                v2 = 4;
                                break;
                            case CELL_SIDE.Right:
                                v1 = 4;
                                v2 = 5;
                                break;
                            case CELL_SIDE.BottomRight:
                                v1 = 5;
                                v2 = 0;
                                break;
                        }
                    }
                    else {
                        switch (side) {
                            case CELL_SIDE.BottomLeft:
                                v1 = 0;
                                v2 = 1;
                                break;
                            case CELL_SIDE.Bottom:
                                v1 = 1;
                                v2 = 2;
                                break;
                            case CELL_SIDE.BottomRight:
                                v1 = 2;
                                v2 = 3;
                                break;
                            case CELL_SIDE.TopRight:
                                v1 = 3;
                                v2 = 4;
                                break;
                            case CELL_SIDE.Top:
                                v1 = 4;
                                v2 = 5;
                                break;
                            case CELL_SIDE.TopLeft:
                                v1 = 5;
                                v2 = 0;
                                break;
                        }
                    }
                    break;
                default:
                    return null;
            }

            GameObject line = new GameObject("Line");
            line.layer = gameObject.layer;
            LineRenderer lr = line.AddComponent<LineRenderer>();
            lr.sortingOrder = _sortingOrder;
            if (cellLineMat == null)
                cellLineMat = Resources.Load<Material>("Materials/CellLine") as Material;
            Material mat = Instantiate(cellLineMat) as Material;
            disposalManager.MarkForDisposal(mat);
            mat.color = color;
            lr.sharedMaterial = mat;
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.startWidth = width;
            lr.endWidth = width;

            Vector3 offset = transform.forward * 0.05f;
            lr.SetPosition(0, CellGetVertexPosition(cellIndex, v1) - offset);
            lr.SetPosition(1, CellGetVertexPosition(cellIndex, v2) - offset);
            return line;
        }

        /// <summary>
        /// Draws a line connecting two cells centers
        /// </summary>
        /// <returns>The line.</returns>
        /// <param name="cellIndex1">Cell index1.</param>
        /// <param name="cellIndex2">Cell index2.</param>
        /// <param name="color">Color.</param>
        /// <param name="width">Width.</param>
        public GameObject DrawLine (int cellIndex1, int cellIndex2, Color color, float width) {

            GameObject line = new GameObject("Line");
            line.layer = gameObject.layer;
            LineRenderer lr = line.AddComponent<LineRenderer>();
            lr.sortingOrder = _sortingOrder;
            if (cellLineMat == null)
                cellLineMat = Resources.Load<Material>("Materials/CellLine") as Material;
            Material mat = Instantiate(cellLineMat) as Material;
            disposalManager.MarkForDisposal(mat);
            mat.color = color;
            lr.sharedMaterial = mat;
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.startWidth = width;
            lr.endWidth = width;
            Vector3 offset = transform.forward * 0.05f;
            lr.SetPosition(0, CellGetPosition(cellIndex1) - offset);
            lr.SetPosition(1, CellGetPosition(cellIndex2) - offset);
            return line;
        }


        /// <summary>
        /// Escales the gameobject of a colored/textured surface
        /// </summary>
        /// <param name="scale">Scale.</param>
        public void CellScaleSurface (int cellIndex, float scale) {
            if (cellIndex < 0)
                return;
            Cell cell = cells[cellIndex];
            GameObject surf = cell.region.surfaceGameObject;
            ScaleSurface(surf, cell.center, scale);
        }

        /// <summary>
        /// Updates grid mesh to reflect only specific cells
        /// </summary>
        public void RedrawCells (List<Cell> cells) {
            GenerateCellsMeshData(cells);
            DrawCellBorders();
        }

        /// <summary>
        /// Creates a gameobject with SpriteRenderer and places it on top of the cell
        /// </summary>
        public GameObject CellAddSprite (int cellIndex, Sprite sprite, bool adjustScale = true) {
            if (cellIndex < 0 || cellIndex >= cells.Count) return null;

            GameObject go = new GameObject("Tile", typeof(SpriteRenderer));
            go.layer = gameObject.layer;
            SpriteRenderer r = go.GetComponent<SpriteRenderer>();
            r.sprite = sprite;

            Cell cell = cells[cellIndex];
            go.transform.position = CellGetPosition(cell);
            go.transform.forward = transform.forward;

            if (adjustScale) {
                float tileWidth;
                if (_gridTopology == GridTopology.Hexagonal) {
                    tileWidth = Vector3.Distance(CellGetVertexPosition(0, 0), CellGetVertexPosition(0, 4));
                }
                else {
                    tileWidth = Vector3.Distance(CellGetVertexPosition(0, 0), CellGetVertexPosition(0, 3));
                }
                float spriteWidth = Mathf.Abs(sprite.bounds.max.x - sprite.bounds.min.x);
                float scale = tileWidth / spriteWidth;
                go.transform.localScale = new Vector3(scale, scale, 1f);
            }

            return go;

        }

        /// <summary>
        /// Sets the underline splatmap opacity of a cell. Optionally hide other terrain layers.
        /// </summary>
        public void CellSetSplatmapOpacity (int cellIndex, int splatmapIndex, float opacity, bool hideOtherLayers = false) {
            if (!ValidCellIndex(cellIndex) || terrain == null) return;
            Bounds bounds = CellGetRectWorldSpace(cellIndex);
            Vector2Int coordsMin = terrain.WorldToAlphamapCoordinates(bounds.min);
            Vector2Int coordsMax = terrain.WorldToAlphamapCoordinates(bounds.max);
            Vector2Int coords = default;
            TerrainData terrainData = terrain.terrainData;
            int alphamapWidth = terrainData.alphamapWidth;
            int alphamapHeight = terrainData.alphamapHeight;
            Vector3 anchor = terrain.GetComponent<Terrain>().GetPosition();
            Cell cell = cells[cellIndex];

            for (int y = coordsMin.y; y <= coordsMax.y; y++) {
                coords.y = y;
                for (int x = coordsMin.x; x <= coordsMax.x; x++) {
                    coords.x = x;
                    float fx = (coords.x + 0.5f) / alphamapWidth - 0.5f;
                    float fy = (coords.y + 0.5f) / alphamapHeight - 0.5f;
                    if (cell.region.Contains(fx, fy)) {
                        terrain.SetAlphamapOpacity(coords, splatmapIndex, opacity, hideOtherLayers);
                    }
                }
            }
        }

        #endregion



    }
}

