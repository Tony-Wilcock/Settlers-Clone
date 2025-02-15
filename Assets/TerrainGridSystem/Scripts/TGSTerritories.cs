using UnityEngine;
using System;
using System.Collections.Generic;
using TGS.Geom;

namespace TGS {

    public enum TerritoryInitialCellMethod {
        RandomBasedOnSeed,
        UserDefined
    }

    public enum TerritoryColorScheme {
        RandomBasedOnSeed,
        UserDefined
    }

    public partial class TerrainGridSystem : MonoBehaviour {

        [NonSerialized]
        public readonly List<Territory> territories = new List<Territory>();

        public Texture2D territoriesTexture;
        [ColorUsage(true, true)]
        public Color territoriesTextureNeutralColor;

        [SerializeField]
        bool _territoriesHideNeutralCells;

        /// <summary>
        /// Gets or sets if neutral cells are visible.
        /// </summary>
        public bool territoriesHideNeutralCells {
            get { return _territoriesHideNeutralCells; }
            set {
                if (_territoriesHideNeutralCells != value) {
                    _territoriesHideNeutralCells = value;
                    Redraw();
                    isDirty = true;
                }
            }
        }


        [SerializeField]
        TerritoryInitialCellMethod _territoriesInitialCellMethod = TerritoryInitialCellMethod.RandomBasedOnSeed;

        /// <summary>
        /// Gets or sets the number of initial territories.
        /// </summary>
        public TerritoryInitialCellMethod territoriesInitialCellMethod {
            get { return _territoriesInitialCellMethod; }
            set {
                if (_territoriesInitialCellMethod != value) {
                    _territoriesInitialCellMethod = value;
                    needGenerateMap = true;
                    isDirty = true;
                }
            }
        }

        [SerializeField]
        int[] _territoriesInitialCellIndices;

        /// <summary>
        /// Gets or sets the cell indices for the first cells of territories during generation.
        /// </summary>
        public int[] territoriesInitialCellIndices {
            get { return _territoriesInitialCellIndices; }
            set {
                if (_territoriesInitialCellIndices != value) {
                    _territoriesInitialCellIndices = value;
                    needGenerateMap = true;
                    isDirty = true;
                }
            }
        }


        [SerializeField]
        int _numTerritories = 3;

        /// <summary>
        /// Gets or sets the number of initial territories.
        /// </summary>
        public int numTerritories {
            get { return _numTerritories; }
            set {
                if (_numTerritories != value) {
                    _numTerritories = Mathf.Clamp(value, 0, MAX_TERRITORIES);
                    needGenerateMap = true;
                    isDirty = true;
                }
            }
        }

        /// <summary>
        /// Recreate territories and optionally keep current cells
        /// </summary>
        /// <param name="numTerritories"></param>
        /// <param name="preserveCells">If true, cells and their visibility state will be preserved</param>
        public void SetNumTerritories(int numTerritories, bool preserveCells) {
            if (_numTerritories == numTerritories) return;
            _numTerritories = numTerritories;
            isDirty = true;
            if (preserveCells) {
                GenerateMap(reuseTerrainData: true, keepCells: true);
            } else {
                needGenerateMap = true;
            }
        }

        [SerializeField]
        int _territoriesMaxIterations;

        /// <summary>
        /// Maximum number of iterations when generation territories
        /// </summary>
        public int territoriesMaxIterations {
            get { return _territoriesMaxIterations; }
            set {
                if (_territoriesMaxIterations != value) {
                    _territoriesMaxIterations = Mathf.Max(0, value);
                    needGenerateMap = true;
                    isDirty = true;
                }
            }
        }


        [SerializeField]
        float _territoriesAsymmetry;

        /// <summary>
        /// Determines the distribution of cells among territories
        /// </summary>
        public float territoriesAsymmetry {
            get { return _territoriesAsymmetry; }
            set {
                if (_territoriesAsymmetry != value) {
                    _territoriesAsymmetry = Mathf.Clamp01(value);
                    needGenerateMap = true;
                    isDirty = true;
                }
            }
        }



        [SerializeField]
        float _territoriesOrganic;

        /// <summary>
        /// Makes territory contour more organic
        /// </summary>
        public float territoriesOrganic {
            get { return _territoriesOrganic; }
            set {
                if (_territoriesOrganic != value) {
                    _territoriesOrganic = Mathf.Clamp01(value);
                    needGenerateMap = true;
                    isDirty = true;
                }
            }
        }

        [SerializeField]
        bool
            _showTerritories;

        /// <summary>
        /// Toggle frontiers visibility.
        /// </summary>
        public bool showTerritories {
            get {
                return _showTerritories;
            }
            set {
                if (value != _showTerritories) {
                    _showTerritories = value;
                    isDirty = true;
                    if (territoryLayer != null) {
                        territoryLayer.gameObject.SetActive(_showTerritories);
                        ClearLastOver();
                    } else {
                        Redraw();
                    }
                }
            }
        }


        [SerializeField]
        bool
            _showTerritoriesInteriorBorders;

        /// <summary>
        /// Toggle interior frontiers visibility.
        /// </summary>
        public bool showTerritoriesInteriorBorders {
            get {
                return _showTerritoriesInteriorBorders;
            }
            set {
                if (value != _showTerritoriesInteriorBorders) {
                    _showTerritoriesInteriorBorders = value;
                    isDirty = true;
                    if (territoryInteriorBorderLayer != null) {
                        territoryInteriorBorderLayer.gameObject.SetActive(_showTerritoriesInteriorBorders);
                    } else {
                        Redraw();
                    }
                }
            }
        }


        [SerializeField]
        bool
            _territoryInteriorBorderOnTop;

        /// <summary>
        /// Show interior borders on top of regular territory borders.
        /// </summary>
        public bool territoryInteriorBorderOnTop {
            get {
                return _territoryInteriorBorderOnTop;
            }
            set {
                if (value != _territoryInteriorBorderOnTop) {
                    _territoryInteriorBorderOnTop = value;
                    isDirty = true;
                    Redraw();
                }
            }
        }



        [SerializeField]
        bool
            _territoryInteriorBorderIncludeEnclaves = true;

        /// <summary>
        /// Show interior borders on top of regular territory borders.
        /// </summary>
        public bool territoryInteriorBorderIncludeEnclaves {
            get {
                return _territoryInteriorBorderIncludeEnclaves;
            }
            set {
                if (value != _territoryInteriorBorderIncludeEnclaves) {
                    _territoryInteriorBorderIncludeEnclaves = value;
                    isDirty = true;
                    Redraw();
                }
            }
        }

        [SerializeField]
        bool _colorizeTerritories;

        /// <summary>
        /// Toggle colorize countries.
        /// </summary>
        public bool colorizeTerritories {
            get {
                return _colorizeTerritories;
            }
            set {
                if (value != _colorizeTerritories) {
                    _colorizeTerritories = value;
                    isDirty = true;
                    if (!_colorizeTerritories && surfacesLayer != null) {
                        DestroyTerritorySurfaces();
                    } else {
                        Redraw();
                    }
                }
            }
        }


        [SerializeField]
        TerritoryColorScheme _territoriesColorScheme;

        /// <summary>
        /// How colors for territories are picked
        /// </summary>
        public TerritoryColorScheme territoriesColorScheme {
            get {
                return _territoriesColorScheme;
            }
            set {
                if (value != _territoriesColorScheme) {
                    _territoriesColorScheme = value;
                    isDirty = true;
                    Redraw();
                }
            }
        }


        [SerializeField]
        Color[] _territoriesFillColors;

        /// <summary>
        /// User defined territory colors
        /// </summary>
        public Color[] territoriesFillColors {
            get {
                return _territoriesFillColors;
            }
            set {
                if (value != _territoriesFillColors) {
                    _territoriesFillColors = value;
                    isDirty = true;
                    Redraw();
                }
            }
        }


        [SerializeField]
        float _colorizedTerritoriesAlpha = 0.7f;

        public float colorizedTerritoriesAlpha {
            get { return _colorizedTerritoriesAlpha; }
            set {
                if (_colorizedTerritoriesAlpha != value) {
                    _colorizedTerritoriesAlpha = value;
                    isDirty = true;
                    UpdateColorizedTerritoriesAlpha();
                }
            }
        }


        [SerializeField]
        [ColorUsage(true, true)]
        Color
            _territoryHighlightColor = new Color(1, 0, 0, 0.8f);

        /// <summary>
        /// Fill color to use when the mouse hovers a territory's region.
        /// </summary>
        public Color territoryHighlightColor {
            get {
                return _territoryHighlightColor;
            }
            set {
                if (value != _territoryHighlightColor) {
                    _territoryHighlightColor = value;
                    isDirty = true;
                    if (hudMatTerritoryOverlay != null && _territoryHighlightColor != hudMatTerritoryOverlay.color) {
                        hudMatTerritoryOverlay.color = _territoryHighlightColor;
                    }
                    if (hudMatTerritoryGround != null && _territoryHighlightColor != hudMatTerritoryGround.color) {
                        hudMatTerritoryGround.color = _territoryHighlightColor;
                    }
                }
            }
        }

        [SerializeField]
        [ColorUsage(true, true)]
        Color
            _territoryHighlightColor2 = new Color(0, 1, 0, 0.8f);

        /// <summary>
        /// Alternate fill color to use when the mouse hovers a territory's region.
        /// </summary>
        public Color territoryHighlightColor2 {
            get {
                return _territoryHighlightColor2;
            }
            set {
                if (value != _territoryHighlightColor2) {
                    _territoryHighlightColor2 = value;
                    isDirty = true;
                    if (hudMatTerritoryOverlay != null) {
                        hudMatTerritoryOverlay.SetColor(ShaderParams.Color2, _territoryHighlightColor2);
                    }
                    if (hudMatTerritoryGround != null) {
                        hudMatTerritoryGround.SetColor(ShaderParams.Color2, _territoryHighlightColor2);
                    }
                }
            }
        }

        [SerializeField]
        [ColorUsage(true, true)]
        Color
            _territoryFrontierColor = new Color(0, 1, 0, 1.0f);

        /// <summary>
        /// Territories border color
        /// </summary>
        public Color territoryFrontiersColor {
            get {
                if (territoriesMat != null) {
                    return territoriesMat.color;
                } else {
                    return _territoryFrontierColor;
                }
            }
            set {
                if (value != _territoryFrontierColor) {
                    _territoryFrontierColor = value;
                    isDirty = true;
                    if (territoriesThinMat != null && _territoryFrontierColor != territoriesThinMat.color) {
                        territoriesThinMat.color = _territoryFrontierColor;
                    }
                    if (territoriesGeoMat != null && _territoryFrontierColor != territoriesGeoMat.color) {
                        territoriesGeoMat.color = _territoryFrontierColor;
                    }
                }
            }
        }

        public float territoryFrontiersAlpha {
            get {
                return _territoryFrontierColor.a;
            }
            set {
                if (_territoryFrontierColor.a != value) {
                    _territoryFrontierColor = new Color(_territoryFrontierColor.r, _territoryFrontierColor.g, _territoryFrontierColor.b, value);
                }
                if (_territoryDisputedFrontierColor.a != value) {
                    _territoryDisputedFrontierColor = new Color(_territoryDisputedFrontierColor.r, _territoryDisputedFrontierColor.g, _territoryDisputedFrontierColor.b, value);
                }
            }
        }

        [SerializeField]
        bool _territoryCustomBorderThickness;

        /// <summary>
        /// Enables territory custom border thickness
        /// </summary>
        public bool territoryCustomBorderThickness {
            get {
                return _territoryCustomBorderThickness;
            }
            set {
                if (value != _territoryCustomBorderThickness) {
                    _territoryCustomBorderThickness = value;
                    UpdateMaterialThickness();
                    if (_showTerritories) {
                        DrawTerritoryFrontiers();
                    }
                    isDirty = true;
                }
            }
        }

        [SerializeField]
        float
        _territoryFrontiersThickness = 0.2f;

        /// <summary>
        /// Territory frontier thickness
        /// </summary>
        public float territoryFrontiersThickness {
            get {
                return _territoryFrontiersThickness;
            }
            set {
                if (value != _territoryFrontiersThickness) {
                    _territoryFrontiersThickness = Mathf.Max(0f, value);
                    UpdateMaterialThickness();
                    if (_showTerritories) {
                        DrawTerritoryFrontiers();
                    }
                    isDirty = true;
                }
            }
        }


        [SerializeField]
        float
        _territoryInteriorBorderThickness = 2f;

        /// <summary>
        /// Territory interior border thickness
        /// </summary>
        public float territoryInteriorBorderThickness {
            get {
                return _territoryInteriorBorderThickness;
            }
            set {
                if (value != territoryInteriorBorderThickness) {
                    _territoryInteriorBorderThickness = Mathf.Max(0f, value);
                    if (_showTerritoriesInteriorBorders) {
                        DrawInteriorTerritoryFrontiers();
                    }
                    isDirty = true;
                }
            }
        }


        [SerializeField]
        float
        _territoryInteriorBorderPadding = -0.6f;

        /// <summary>
        /// Territory interior border padding
        /// </summary>
        public float territoryInteriorBorderPadding {
            get {
                return _territoryInteriorBorderPadding;
            }
            set {
                if (value != _territoryInteriorBorderPadding) {
                    _territoryInteriorBorderPadding = value;
                    if (_showTerritoriesInteriorBorders) {
                        DrawInteriorTerritoryFrontiers();
                    }
                    isDirty = true;
                }
            }
        }


        [SerializeField]
        [ColorUsage(true, true)]
        Color
        _territoryDisputedFrontierColor = new Color(0, 1, 0, 1.0f);

        /// <summary>
        /// Territories disputed borders color
        /// </summary>
        public Color territoryDisputedFrontierColor {
            get {
                if (territoriesDisputedMat != null) {
                    return territoriesDisputedMat.color;
                } else {
                    return _territoryDisputedFrontierColor;
                }
            }
            set {
                if (value != _territoryDisputedFrontierColor) {
                    _territoryDisputedFrontierColor = value;
                    isDirty = true;
                    if (territoriesDisputedThinMat != null && _territoryDisputedFrontierColor != territoriesDisputedThinMat.color) {
                        territoriesDisputedThinMat.color = _territoryDisputedFrontierColor;
                    }
                    if (territoriesDisputedGeoMat != null && _territoryDisputedFrontierColor != territoriesDisputedGeoMat.color) {
                        territoriesDisputedGeoMat.color = _territoryDisputedFrontierColor;
                    }
                }
            }
        }


        [SerializeField]
        bool _showTerritoriesOuterBorder = true;

        /// <summary>
        /// Shows perimetral/outer border of territories?
        /// </summary>
        /// <value><c>true</c> if show territories outer borders; otherwise, <c>false</c>.</value>
        public bool showTerritoriesOuterBorders {
            get { return _showTerritoriesOuterBorder; }
            set {
                if (_showTerritoriesOuterBorder != value) {
                    _showTerritoriesOuterBorder = value;
                    isDirty = true;
                    Redraw();
                }
            }
        }


        [SerializeField]
        bool _allowTerritoriesInsideTerritories;

        /// <summary>
        /// Set this property to true to allow territories to be surrounded by other territories.
        /// </summary>
        public bool allowTerritoriesInsideTerritories {
            get { return _allowTerritoriesInsideTerritories; }
            set {
                if (_allowTerritoriesInsideTerritories != value) {
                    _allowTerritoriesInsideTerritories = value;
                    isDirty = true;
                }
            }
        }



        [SerializeField]
        bool _territoriesIgnoreHiddenCells;

        /// <summary>
        /// Set this property to true to allow territories to be surrounded by other territories.
        /// </summary>
        public bool territoriesIgnoreHiddenCells {
            get { return _territoriesIgnoreHiddenCells; }
            set {
                if (_territoriesIgnoreHiddenCells != value) {
                    _territoriesIgnoreHiddenCells = value;
                    isDirty = true;
                }
            }
        }


        /// <summary>
        /// Returns Territory under mouse position or null if none.
        /// </summary>
        public Territory territoryHighlighted { get { return _territoryHighlighted; } }

        /// <summary>
        /// Returns currently highlighted territory index in the countries list.
        /// </summary>
        public int territoryHighlightedIndex { get { return _territoryHighlightedIndex; } }

        /// <summary>
        /// Returns currently highlighted territory region index.
        /// </summary>
        public int territoryHighlightedRegionIndex { get { return _territoryHighlightedRegionIndex; } }

        /// <summary>
        /// Returns Territory index which has been clicked
        /// </summary>
        public int territoryLastClickedIndex { get { return _territoryLastClickedIndex; } }

        /// <summary>
        /// Returns Territory region index which has been clicked
        /// </summary>
        public int territoryRegionLastClickedIndex { get { return _territoryRegionLastClickedIndex; } }


        #region Public Territories Functions

        /// <summary>
        /// Uncolorize/hide all territories.
        /// </summary>
        public void TerritoryHideRegionSurfaces() {
            int terrCount = territories.Count;
            for (int k = 0; k < terrCount; k++) {
                TerritoryHideRegionSurface(k);
            }
        }


        /// <summary>
        /// Uncolorize/hide specified territory by index in the territories collection.
        /// </summary>
        public void TerritoryHideRegionSurface(int territoryIndex, int regionIndex = 0) {
            if (!ValidTerritoryIndex(territoryIndex, regionIndex)) return;
            if ((_territoryHighlightedIndex != territoryIndex && _territoryHighlightedRegionIndex != regionIndex) || _highlightedObj == null) {
                int cacheIndex = GetCacheIndexForTerritoryRegion(territoryIndex, regionIndex);
                GameObject surf;
                if (surfaces.TryGetValue(cacheIndex, out surf)) {
                    if (surf == null) {
                        surfaces.Remove(cacheIndex);
                    } else {
                        surf.SetActive(false);
                    }
                }
            }
            territories[territoryIndex].regions[regionIndex].customMaterial = null;
        }


        /// <summary>
        /// Assigns a custom material to a territory
        /// </summary>
        public GameObject TerritorySetMaterial(int territoryIndex, Material material, int regionIndex = 0) {
            if (!ValidTerritoryIndex(territoryIndex, regionIndex)) return null;
            GameObject o = TerritoryToggleRegionSurface(territoryIndex, true, Color.white, regionIndex: regionIndex);
            Region region = territories[territoryIndex].regions[regionIndex];
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
        /// Sets the color of a territory
        /// </summary>
        public GameObject TerritorySetColor(Territory territory, Color color, int regionIndex = 0) {
            int territoryIndex = TerritoryGetIndex(territory);
            return TerritorySetColor(territoryIndex, color, regionIndex);

        }

        /// <summary>
        /// Sets the color of a territory
        /// </summary>
        public GameObject TerritorySetColor(int territoryIndex, Color color, int regionIndex = 0) {
            return TerritoryToggleRegionSurface(territoryIndex, true, color, regionIndex: regionIndex);
        }

        /// <summary>
        /// Sets the texture of a territory
        /// </summary>
        public GameObject TerritorySetTexture(Territory territory, Texture2D texture, int regionIndex = 0) {
            int territoryIndex = TerritoryGetIndex(territory);
            return TerritorySetTexture(territoryIndex, texture, regionIndex);

        }

        /// <summary>
        /// Sets the texture of a territory
        /// </summary>
        public GameObject TerritorySetTexture(int territoryIndex, Texture2D texture, int regionIndex = 0) {
            return TerritoryToggleRegionSurface(territoryIndex, true, Color.white, false, texture, regionIndex);
        }

        /// <summary>
        /// Toggles visibility of a territory surface.
        /// </summary>
        /// <param name="territory">Territory.</param>
        /// <param name="visible">If the colored surface will be visible or not.</param>
        public void TerritoryToggleRegionSurface(int territoryIndex, bool visible, int regionIndex = 0) {
            int cacheIndex = GetCacheIndexForTerritoryRegion(territoryIndex, regionIndex);
            surfaces.TryGetValue(cacheIndex, out GameObject surf);
            if (surf != null) {
                surf.SetActive(visible);
            }
        }

        /// <summary>
        /// Colorize specified territory by index.
        /// </summary>
        /// <param name="territory">Territory.</param>
        /// <param name="visible">If the colored surface will be visible or not.</param>
        /// <param name="color">Color.</param>
        public GameObject TerritoryToggleRegionSurface(Territory territory, bool visible, Color color, bool refreshGeometry = false, int regionIndex = 0) {
            int territoryIndex = TerritoryGetIndex(territory);
            return TerritoryToggleRegionSurface(territoryIndex, visible, color, refreshGeometry, null, Misc.Vector2one, Misc.Vector2zero, 0, false, regionIndex);
        }

        /// <summary>
        /// Colorize specified territory by index.
        /// </summary>
        /// <param name="territoryIndex">Territory index.</param>
        /// <param name="visible">If the colored surface will be visible or not.</param>
        /// <param name="color">Color.</param>
        public GameObject TerritoryToggleRegionSurface(int territoryIndex, bool visible, Color color, bool refreshGeometry = false, int regionIndex = 0) {
            return TerritoryToggleRegionSurface(territoryIndex, visible, color, refreshGeometry, null, Misc.Vector2one, Misc.Vector2zero, 0, false, regionIndex);
        }

        /// <summary>
        /// Colorize specified territory by index.
        /// </summary>
        /// <param name="territoryIndex">Territory index.</param>
        /// <param name="visible">If the colored surface will be visible or not.</param>
        /// <param name="color">Color.</param>
        /// <param name="refreshGeometry">If set to <c>true</c> any cached surface will be destroyed and regenerated. Usually you pass false to improve performance.</param>
        /// <param name="texture">Texture, which will be tinted according to the color. Use Color.white to preserve original texture colors.</param>
        public GameObject TerritoryToggleRegionSurface(int territoryIndex, bool visible, Color color, bool refreshGeometry, Texture2D texture, int regionIndex = 0) {
            return TerritoryToggleRegionSurface(territoryIndex, visible, color, refreshGeometry, texture, Misc.Vector2one, Misc.Vector2zero, 0, rotateInLocalSpace: false, regionIndex);
        }

        /// <summary>
        /// Colorize specified territory by index.
        /// </summary>
        /// <param name="territoryIndex">Territory index.</param>
        /// <param name="visible">If the colored surface will be visible or not.</param>
        /// <param name="color">Color.</param>
        /// <param name="refreshGeometry">If set to <c>true</c> any cached surface will be destroyed and regenerated. Usually you pass false to improve performance.</param>
        /// <param name="texture">Texture, which will be tinted according to the color. Use Color.white to preserve original texture colors.</param>
        /// <param name="textureScale">Texture scale.</param>
        /// <param name="textureOffset">Texture offset.</param>
        /// <param name="textureRotation">Texture rotation.</param>
        /// <param name="isCanvasTexture">If true, the texture is assumed to fill the entire grid or canvas so only a portion of the texture would be visible in the cell</param>
        public GameObject TerritoryToggleRegionSurface(int territoryIndex, bool visible, Color color, bool refreshGeometry, Texture2D texture, Vector2 textureScale, Vector2 textureOffset, float textureRotation, bool rotateInLocalSpace, int regionIndex = 0, bool isCanvasTexture = false) {
            return TerritoryToggleRegionSurface(territoryIndex, visible, color, refreshGeometry, texture, textureScale, textureOffset, textureRotation, false, rotateInLocalSpace, regionIndex, isCanvasTexture: isCanvasTexture);
        }


        /// <summary>
        /// Colorize specified territory by index.
        /// </summary>
        /// <param name="territory">Territory.</param>
        /// <param name="visible">If the colored surface will be visible or not.</param>
        /// <param name="color">Color.</param>
        /// <param name="refreshGeometry">If set to <c>true</c> any cached surface will be destroyed and regenerated. Usually you pass false to improve performance.</param>
        /// <param name="texture">Texture, which will be tinted according to the color. Use Color.white to preserve original texture colors.</param>
        /// <param name="textureScale">Texture scale.</param>
        /// <param name="textureOffset">Texture offset.</param>
        /// <param name="textureRotation">Texture rotation.</param>
        /// <param name="overlay">If set to <c>true</c> the colored surface will be shown over any object.</param>
        /// <param name="isCanvasTexture">If true, the texture is assumed to fill the entire grid or canvas so only a portion of the texture would be visible in the cell</param>
        public GameObject TerritoryToggleRegionSurface(Territory territory, bool visible, Color color, bool refreshGeometry, Texture2D texture, Vector2 textureScale, Vector2 textureOffset, float textureRotation, bool overlay, bool rotateInLocalSpace, int regionIndex = 0, bool isCanvasTexture = false) {
            int territoryIndex = TerritoryGetIndex(territory);
            return TerritoryToggleRegionSurface(territoryIndex, visible, color, refreshGeometry, texture, textureScale, textureOffset, textureRotation, overlay, rotateInLocalSpace, regionIndex, isCanvasTexture: isCanvasTexture);
        }

        /// <summary>
        /// Colorize specified territory by index.
        /// </summary>
        /// <param name="territoryIndex">Territory index.</param>
        /// <param name="visible">If the colored surface will be visible or not.</param>
        /// <param name="color">Color.</param>
        /// <param name="refreshGeometry">If set to <c>true</c> any cached surface will be destroyed and regenerated. Usually you pass false to improve performance.</param>
        /// <param name="texture">Texture, which will be tinted according to the color. Use Color.white to preserve original texture colors.</param>
        /// <param name="textureScale">Texture scale.</param>
        /// <param name="textureOffset">Texture offset.</param>
        /// <param name="textureRotation">Texture rotation.</param>
        /// <param name="overlay">If set to <c>true</c> the colored surface will be shown over any object.</param>
        /// <param name="isCanvasTexture">If true, the texture is assumed to fill the entire grid or canvas so only a portion of the texture would be visible in the cell</param>
        public GameObject TerritoryToggleRegionSurface(int territoryIndex, bool visible, Color color, bool refreshGeometry, Texture2D texture, Vector2 textureScale, Vector2 textureOffset, float textureRotation, bool overlay, bool rotateInLocalSpace, int regionIndex = 0, bool isCanvasTexture = false) {

            FlushCellChanges();

            if (!ValidTerritoryIndex(territoryIndex, regionIndex))
                return null;

            if (!visible) {
                TerritoryHideRegionSurface(territoryIndex);
                return null;
            }

            Region region = territories[territoryIndex].regions[regionIndex];
            int cacheIndex = GetCacheIndexForTerritoryRegion(territoryIndex, regionIndex);

            // Checks if current cached surface contains a material with a texture, if it exists but it has not texture, destroy it to recreate with uv mappings
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

            // Should the surface be recreated?
            Material surfMaterial;
            if (surf != null) {
                Material texturizedMat = overlay ? texturizedMatOverlayTerritory : texturizedMatGroundTerritory;
                if (texture != null && (region.customMaterial == null || textureScale != region.customTextureScale || textureOffset != region.customTextureOffset ||
                    textureRotation != region.customTextureRotation || !region.customMaterial.name.Equals(texturizedMat.name))) {
                    surfaces.Remove(cacheIndex);
                    DestroyImmediate(surf);
                    surf = null;
                }
            }
            // If it exists, activate and check proper material, if not create surface
            bool isHighlighted = territoryHighlightedIndex == territoryIndex && _highlightEffect != HighlightEffect.None;
            if (surf != null) {
                if (!surf.activeSelf) {
                    surf.SetActive(true);
                }
                Material goodMaterial = GetColoredTexturedMaterialForTerritory(region, color, texture, overlay);
                region.customMaterial = goodMaterial;
                if (!isHighlighted) {
                    ApplyMaterialToSurface(region, goodMaterial);
                }
            } else {
                surfMaterial = GetColoredTexturedMaterialForTerritory(region, color, texture, overlay);
                surf = GenerateTerritoryRegionSurface(territoryIndex, surfMaterial, textureScale, textureOffset, textureRotation, rotateInLocalSpace, regionIndex, isCanvasTexture);
                region.customMaterial = surfMaterial;
                region.customTextureOffset = textureOffset;
                region.customTextureRotation = textureRotation;
                region.customTextureScale = textureScale;
                region.customRotateInLocalSpace = rotateInLocalSpace;
                region.customIsCanvasTexture = isCanvasTexture;
            }
            // If it was highlighted, highlight it again
            if (isHighlighted && region.customMaterial != null && _highlightedObj != null) {
                if (hudMatTerritory.HasProperty(ShaderParams.MainTex)) {
                    if (region.customMaterial != null) {
                        hudMatTerritory.mainTexture = region.customMaterial.mainTexture;
                    } else {
                        hudMatTerritory.mainTexture = null;
                    }
                }
                surf.GetComponent<Renderer>().sharedMaterial = hudMatTerritory;
                _highlightedObj = surf;
            }
            return surf;
        }

        /// <summary>
        /// Specifies if a given territory border is visible.
        /// </summary>
        public void TerritorySetBorderVisible(int territoryIndex, bool visible) {
            if (territoryIndex < 0 || territoryIndex >= territories.Count)
                return;
            territories[territoryIndex].borderVisible = visible;
        }


        /// <summary>
        /// Returns a list of neighbour territories for specificed cell index.
        /// </summary>
        public List<Territory> TerritoryGetNeighbours(int territoryIndex) {
            if (!ValidTerritoryIndex(territoryIndex)) return null;
            return territories[territoryIndex].neighbours;
        }

        /// <summary>
        /// Returns a list of neighbour territories for specificed territory index in the territories parameter.
        /// </summary>
        public int TerritoryGetNeighbours(int territoryIndex, List<Territory> territories) {
            if (!ValidTerritoryIndex(territoryIndex)) return 0;
            territories.Clear();
            territories.AddRange(this.territories[territoryIndex].neighbours);
            return territories.Count;
        }

        /// <summary>
        /// Returns a list of cells that form the frontiers of a given territory
        /// </summary>
        /// <returns>The number of cells found.</returns>
        /// <param name="territoryIndex">Territory index.</param>
        /// <param name="cellIndices">Cells that form the frontier. You need to pass an already initialized list, which will be cleared and filled with the cells.</param>
        /// <param name="regionIndex">If the territory has several regions, the index of the region</param>
        /// <param name="includeGridEdges">If a cell is located in the edge of the grid, include it in the results</param>
        public int TerritoryGetFrontierCells(int territoryIndex, List<int> cellIndices, int regionIndex = -1, bool includeGridEdges = false) {
            return TerritoryGetFrontierCells(territoryIndex, -1, cellIndices, regionIndex, includeGridEdges);
        }


        /// <summary>
        /// Returns a copy of all cells belonging to a territory. Use Territory.cells to access the list without making a copy
        /// </summary>
        public List<Cell> TerritoryGetCells(int territoryIndex) {
            if (!ValidTerritoryIndex(territoryIndex)) return null;
            List<Cell> cells = new List<Cell>();
            Territory terr = territories[territoryIndex];
            if (terr.cells != null) {
                cells.AddRange(terr.cells);
            }
            return cells;
        }

        /// <summary>
        /// Returns all cells belonging to a territory into an user given list. See alto territory.cells list.
        /// </summary>
        public void TerritoryGetCells(int territoryIndex, List<Cell> cells) {
            if (!ValidTerritoryIndex(territoryIndex)) return;
            cells.Clear();
            Territory terr = territories[territoryIndex];
            if (terr.cells != null) {
                cells.AddRange(terr.cells);
            }
        }

        /// <summary>
        /// Returns all cells belonging to a territory region
        /// </summary>
        public List<Cell> TerritoryGetCells(int territoryIndex, int regionIndex) {
            List<Cell> cells = new List<Cell>();
            TerritoryGetCells(territoryIndex, regionIndex, cells);
            return cells;
        }

        /// <summary>
        /// Returns all cells belonging to a territory region into an user given cell list
        /// </summary>
        public void TerritoryGetCells(int territoryIndex, int regionIndex, List<Cell> cells) {
            if (!ValidTerritoryIndex(territoryIndex, regionIndex)) return;
            FlushCellChanges();
            Territory territory = territories[territoryIndex];
            Region region = territory.regions[regionIndex];
            cells.Clear();
            cells.AddRange(region.cells);
        }

        /// <summary>
        /// Returns a list of cells that forms the frontiers between a given territory and another one.
        /// </summary>
        /// <returns>The get frontier cells.</returns>
        /// <param name="territoryIndex">Territory index.</param>
        /// <param name="otherTerritoryIndex">Other territory index.</param>
        /// <param name="cellIndices">Cells that form the frontier. You need to pass an already initialized list, which will be cleared and filled with the cells.</param>
        /// <param name="regionIndex">Limit search to a given region. -1 means include all regions.</param>
        /// <param name="includeGridEdges">If a cell is located in the edge of the grid, include it in the results</param>
        public int TerritoryGetFrontierCells(int territoryIndex, int otherTerritoryIndex, List<int> cellIndices, int regionIndex = -1, bool includeGridEdges = false) {

            if (territoryIndex < 0 || territoryIndex >= territories.Count || territories[territoryIndex].cells == null || cells == null)
                return 0;

            cellUsedFlag++;

            if (regionIndex >= 0) {
                List<Cell> regionCells = TerritoryGetCells(territoryIndex, regionIndex);
                int regionCellsCount = regionCells.Count;
                for (int k = 0; k < regionCellsCount; k++) {
                    regionCells[k].usedFlag2 = cellUsedFlag;
                }
            }

            cellIndices.Clear();
            foreach (KeyValuePair<Segment, Frontier> kv in territoryNeighbourHit) {
                Frontier frontier = kv.Value;
                if (frontier.region1 == null || frontier.region2 == null)
                    continue;
                Cell cell1 = (Cell)frontier.region1.entity;
                Cell cell2 = (Cell)frontier.region2.entity;
                if (cell1.territoryIndex == territoryIndex && (otherTerritoryIndex < 0 || cell2.territoryIndex == otherTerritoryIndex)) {
                    if (cell1.usedFlag != cellUsedFlag && (regionIndex < 0 || cell1.usedFlag2 == cellUsedFlag)) {
                        cell1.usedFlag = cellUsedFlag;
                        cellIndices.Add(cell1.index);
                    }
                } else if (cell2.territoryIndex == territoryIndex && (otherTerritoryIndex < 0 || cell1.territoryIndex == otherTerritoryIndex)) {
                    if (cell2.usedFlag != cellUsedFlag && (regionIndex < 0 || cell2.usedFlag2 == cellUsedFlag)) {
                        cell2.usedFlag = cellUsedFlag;
                        cellIndices.Add(cell2.index);
                    }
                }
            }

            if (includeGridEdges) {
                Territory territory = territories[territoryIndex];
                foreach (Cell cell in territory.cells) {
                    if (cell.usedFlag == cellUsedFlag) continue;
                    if (CellIsBorder(cell.index)) {
                        cellIndices.Add(cell.index);
                    }
                }
            }

            return cellIndices.Count;
        }

        /// <summary>
        /// Similar to TerritoryGetFrontierCells but returns the cells of the adjacent territory
        /// </summary>
        public int TerritoryGetAdjacentCells(int territoryIndex, List<int> cellIndices, int regionIndex = -1, int otherTerritoryIndex = -1) {

            if (territoryIndex < 0 || territoryIndex >= territories.Count || territories[territoryIndex].cells == null || cells == null)
                return 0;

            cellUsedFlag++;

            if (regionIndex >= 0) {
                List<Cell> regionCells = TerritoryGetCells(territoryIndex, regionIndex);
                int regionCellsCount = regionCells.Count;
                for (int k=0;k<regionCellsCount;k++) {
                    regionCells[k].usedFlag2 = cellUsedFlag;
                }
            }

            cellIndices.Clear();
            foreach (KeyValuePair<Segment, Frontier> kv in territoryNeighbourHit) {
                Frontier frontier = kv.Value;
                if (frontier.region1 == null || frontier.region2 == null)
                    continue;
                Cell cell1 = (Cell)frontier.region1.entity;
                Cell cell2 = (Cell)frontier.region2.entity;
                if (cell1.territoryIndex == territoryIndex && (otherTerritoryIndex < 0 || cell2.territoryIndex == otherTerritoryIndex)) {
                    if (cell2.usedFlag != cellUsedFlag && (regionIndex < 0 || cell1.usedFlag2 == cellUsedFlag)) {
                        cell2.usedFlag = cellUsedFlag;
                        cellIndices.Add(cell2.index);
                    }
                } else if (cell2.territoryIndex == territoryIndex && (otherTerritoryIndex < 0 || cell1.territoryIndex == otherTerritoryIndex)) {
                    if (cell1.usedFlag != cellUsedFlag && (regionIndex < 0 || cell2.usedFlag2 == cellUsedFlag)) {
                        cell1.usedFlag = cellUsedFlag;
                        cellIndices.Add(cell1.index);
                    }
                }
            }

            return cellIndices.Count;
        }


        /// <summary>
        /// Colors a territory and fades it out during "duration" in seconds.
        /// </summary>
        public void TerritoryFadeOut(int territoryIndex, Color color, float duration, int repetitions = 1) {
            TerritoryAnimate(FaderStyle.FadeOut, territoryIndex, color, duration, repetitions);
        }

        /// <summary>
        /// Flashes a territory with "color" and "duration" in seconds.
        /// </summary>
        public void TerritoryFlash(int territoryIndex, Color color, float duration, int repetitions = 1) {
            TerritoryAnimate(FaderStyle.Flash, territoryIndex, color, duration, repetitions);
        }

        /// <summary>
        /// Blinks a territory with "color" and "duration" in seconds.
        /// </summary>
        public void TerritoryBlink(int territoryIndex, Color color, float duration, int repetitions = 1) {
            TerritoryAnimate(FaderStyle.Blink, territoryIndex, color, duration, repetitions);
        }

        /// <summary>
        /// Temporarily colors a territory for "duration" in seconds.
        /// </summary>
        public void TerritoryColorTemp(int territoryIndex, Color color, float duration) {
            TerritoryAnimate(FaderStyle.ColorTemp, territoryIndex, color, duration, 1);
        }

        /// <summary>
        /// Cancels any ongoing visual effect on a territory
        /// </summary>
        /// <param name="cellIndex">Cell index.</param>
        public void TerritoryCancelAnimations(int territoryIndex, float fadeOutDuration = 0) {
            TerritoryCancelAnimation(territoryIndex, fadeOutDuration);
        }


        /// <summary>
        /// Specifies if a given territory is visible.
        /// </summary>
        public void TerritorySetVisible(int territoryIndex, bool visible) {
            if (!ValidTerritoryIndex(territoryIndex)) return;
            territories[territoryIndex].visible = visible;
            if (territoryIndex == _territoryLastOverIndex) {
                ClearLastOver();
            }
            needUpdateTerritories = true;
            issueRedraw = RedrawType.Full;
        }

        /// <summary>
        /// Returns true if territory is visible
        /// </summary>
        public bool TerritoryIsVisible(int territoryIndex) {
            if (territoryIndex < 0 || territoryIndex >= territories.Count)
                return false;
            return territories[territoryIndex].visible;
        }

        /// <summary>
        /// Specifies if a given territory is neutral.
        /// </summary>
        public void TerritorySetNeutral(int territoryIndex, bool neutral) {
            if (territoryIndex < 0 || territoryIndex >= territories.Count)
                return;
            territories[territoryIndex].neutral = neutral;
            needUpdateTerritories = true;
            issueRedraw = RedrawType.Full;
        }

        /// <summary>
        /// Returns true if territory is neutral
        /// </summary>
        public bool TerritoryIsNeutral(int territoryIndex) {
            if (territoryIndex < 0 || territoryIndex >= territories.Count)
                return false;
            return territories[territoryIndex].neutral;
        }


        /// <summary>
        /// Specifies the color of the territory borders.
        /// </summary>
        public void TerritorySetFrontierColor(int territoryIndex, Color color) {
            if (territoryIndex < 0 || territoryIndex >= territories.Count)
                return;
            Territory terr = territories[territoryIndex];
            if (terr.frontierColor != color) {
                terr.frontierColor = color;
                DrawTerritoryFrontiers();
            }
        }

        /// <summary>
        /// Creates a gameobject with the frontier for the given territory. Optionally, the frontier could be limited to those segments adjacent to another territory.
        /// </summary>
        /// <returns></returns>
        public GameObject TerritoryDrawFrontier(Territory territory, int adjacentTerritoryIndex = -1, Material material = null, Color color = default(Color), float thickness = 0) {
            int territoryIndex = TerritoryGetIndex(territory);
            return TerritoryDrawFrontier(territoryIndex, adjacentTerritoryIndex, material, color, thickness);
        }

        /// <summary>
        /// Creates a gameobject with the frontier for the given territory. Optionally, the frontier could be limited to those segments adjacent to another territory.
        /// </summary>
        /// <returns></returns>
        public GameObject TerritoryDrawFrontier(int territoryIndex, int adjacentTerritoryIndex = -1, Material material = null, Color color = default, float thickness = 0, bool removeExistingCustomFrontiers = true) {

            FlushCellChanges();

            TerritoryMesh tm = new TerritoryMesh { territoryIndex = territoryIndex };

            if (!GenerateTerritoryMesh(tm, true, adjacentTerritoryIndex)) return null;

            if (material == null) {
                if (tm.territoryIndex < 0) {
                    material = territoriesDisputedMat;
                } else {
                    if (territoryIndex >= territories.Count) return null;
                    Color frontierColor = territories[tm.territoryIndex].frontierColor;
                    if (frontierColor.a == 0 && frontierColor.r == 0 && frontierColor.g == 0 && frontierColor.b == 0) {
                        material = territoriesMat;
                    } else {
                        material = GetFrontierColorMaterial(frontierColor);
                    }
                }
            }

            material = Instantiate(material);
            material.renderQueue--; // ensure it writes to stencil before normal territory material
            if (color != default) {
                material.color = color;
            }
            if (thickness > 0) {
                UpdateMaterialTerritoryThickness(material, thickness);
            } else {
                thickness = _territoryFrontiersThickness;
            }
            bool useVertexDisplacement = thickness > 1f & !canUseGeometryShaders;

            Transform root = CheckTerritoriesCustomFrontiersRoot();

            GameObject go = DrawTerritoryFrontier(tm, material, root, TERRITORY_FRONTIER_NAME, useVertexDisplacement);
            Territory territory = territories[territoryIndex];
            if (removeExistingCustomFrontiers && territory.customFrontiersGameObject != null) {
                DestroyImmediate(territory.customFrontiersGameObject);
            }
            territory.customFrontiersGameObject = go;
            return go;
        }

        /// <summary>
        /// Hides all territories interior boders
        /// </summary>
        public void TerritoryHideInteriorBorders() {
            if (territoryInteriorBorderLayer != null) {
                DestroyImmediate(territoryInteriorBorderLayer.gameObject);
            }
        }

        /// <summary>
        /// Hides territory border of a territory
        /// </summary>
        /// <param name="regionIndex">-1 will destroy all interior borders for the territory</param>
        public void TerritoryHideInteriorBorder(int territoryIndex, int regionIndex = -1) {
            if (!ValidTerritoryIndex(territoryIndex)) return;
            Territory territory = territories[territoryIndex];
            if (regionIndex >= 0) {
                if (!ValidTerritoryIndex(territoryIndex, regionIndex)) return;
                foreach (Region region in territory.regions) {
                    if (region.interiorBorderGameObject != null) {
                        DestroyImmediate(region.interiorBorderGameObject);
                    }
                }
                return;
            }

            {
                Region region = territory.regions[regionIndex];
                if (region.interiorBorderGameObject != null) {
                    DestroyImmediate(region.interiorBorderGameObject);
                }
            }
        }


        /// <summary>
        /// Creates a gameobject with the interior border for the given territory with optional padding and thickness.
        /// </summary>
        /// <returns></returns>
        public GameObject TerritoryDrawInteriorBorder(int territoryIndex, float padding, float thickness, Color color = default, Color secondColor = default, int regionIndex = 0, float animationSpeed = 0, bool includeEnclaves = false) {

            if (!ValidTerritoryIndex(territoryIndex, regionIndex)) return null;
            Territory territory = territories[territoryIndex];
            return TerritoryDrawInteriorBorder(territory, padding, thickness, color, secondColor, regionIndex, animationSpeed, includeEnclaves);
        }

        /// <summary>
        /// Creates a gameobject with the interior border for the given territory with optional padding and thickness.
        /// </summary>
        /// <param name="includeEnclaves">Draw additional interior borders for other regions contained inside this territory region</param>
        /// <returns></returns>
        public GameObject TerritoryDrawInteriorBorder(Territory territory, float padding = -0.7f, float thickness = 3f, Color color = default, Color secondColor = default, int regionIndex = 0, float animationSpeed = 0, bool includeEnclaves = false) {

            GetInteriorBorderColors(territory, ref color, ref secondColor);
            GameObject border = TerritoryDrawInteriorBorderSingle(territory, padding, thickness, color, secondColor, regionIndex, animationSpeed);
            if (border == null) return null;

            // Check other regions inside this region
            if (includeEnclaves) {
                Region thisRegion = territory.regions[regionIndex];
                int territoryCount = territories.Count;
                for (int k = 0; k < territoryCount; k++) {
                    Territory otherTerritory = territories[k];
                    if (otherTerritory == territory) continue;
                    int otherRegionCount = otherTerritory.regions.Count;
                    for (int r = 0; r < otherRegionCount; r++) {
                        Region otherRegion = otherTerritory.regions[r];
                        if (thisRegion.ContainsRegion(otherRegion)) {
                            GameObject enclaveBorder = TerritoryDrawInteriorBorderSingle(otherTerritory, -padding, thickness, secondColor, color, r, animationSpeed);
                            enclaveBorder.transform.SetParent(border.transform);
                        }
                    }
                }
            }

            return border;
        }


        void GetInteriorBorderColors(Territory territory, ref Color color, ref Color secondColor) {
            if (color == default) {
                color = territory.fillColor;
            }
            if (secondColor == default) {
                secondColor = territory.fillColor;
                secondColor.r *= 0.5f;
                secondColor.g *= 0.5f;
                secondColor.b *= 0.5f;
                secondColor.a = 0;
            }
        }


        /// <summary>
        /// Creates a gameobject with the interior border for the given territory with optional padding and thickness.
        /// </summary>
        /// <returns></returns>
        GameObject TerritoryDrawInteriorBorderSingle(Territory territory, float padding = -0.7f, float thickness = 3f, Color color = default, Color secondColor = default, int regionIndex = 0, float animationSpeed = 0, bool removeExistingBorders = true) {

            if (territory == null) return null;

            FlushCellChanges();

            if (regionIndex < 0 || territory.regions == null || regionIndex >= territory.regions.Count) return null;

            TerritoryMesh tm = new TerritoryMesh();
            if (!GenerateRegionMesh(tm, territory.regions[regionIndex], thickness)) return null;

            Material material = Instantiate(territoriesGradientMat);
            GetInteriorBorderColors(territory, ref color, ref secondColor);
            material.color = color;
            material.SetColor(ShaderParams.TerritorySecondColor, secondColor);
            material.renderQueue += _territoryInteriorBorderOnTop ? 3 : 1; // ensure it draws behind regular territory frontiers
            material.SetFloat(ShaderParams.TerritoryAnimationSpeed, animationSpeed);
            material.EnableKeyword(ShaderParams.SKW_GRADIENT);
            material.DisableKeyword(ShaderParams.SKW_NEAR_CLIP_FADE);
            SetBlend(material);
            SetStencil(material, STENCIL_MASK_INTERIOR_BORDER);
            float depthOffset = _gridMeshDepthOffset / 10000.0f;
            material.SetFloat(ShaderParams.Offset, depthOffset);
            UpdateMaterialTerritoryThickness(material, thickness);

            territoryInteriorBorderLayer = CheckTerritoriesInteriorFrontiersRoot();
            territoryInteriorBorderLayer.gameObject.SetActive(true);

            GameObject go = DrawTerritoryFrontier(tm, material, territoryInteriorBorderLayer, TERRITORY_INTERIOR_BORDER_NAME, useVertexDisplacementForTerritoryThickness: true, usesGradient: true, thickness, padding);
            Region region = territory.regions[regionIndex];
            if (region.interiorBorderGameObject != null) DestroyImmediate(region.interiorBorderGameObject);
            region.interiorBorderGameObject = go;
            return go;
        }


        /// <summary>
        /// Returns the territory object under position in local coordinates
        /// </summary>
        [Obsolete("Use TerritoryGetAtWorldPosition or TerritoryGetAtLocalPosition instead.")]
        public Territory TerritoryGetAtPosition(Vector2 localPosition) {
            return GetTerritoryAtPoint(localPosition, false);
        }

        /// <summary>
        /// Returns the territory object under position in local or worldSpace coordinates
        /// </summary>
        [Obsolete("Use TerritoryGetAtWorldPosition or TerritoryGetAtLocalPosition instead.")]
        public Territory TerritoryGetAtPosition(Vector3 position, bool worldSpace) {
            return GetTerritoryAtPoint(position, worldSpace);
        }

        /// <summary>
        /// Returns the territory object under position in worldSpace coordinates
        /// </summary>
        public Territory TerritoryGetAtWorldPosition(Vector3 position) {
            return GetTerritoryAtPoint(position, worldSpace: true);
        }

        /// <summary>
        /// Returns the territory object under position in local coordinates
        /// </summary>
        public Territory TerritoryGetAtLocalPosition(Vector3 position) {
            return GetTerritoryAtPoint(position, worldSpace: false);
        }

        /// <summary>
        /// Gets the territory's center position in world space.
        /// </summary>
        /// <param name="centroidType">The accuracy of the algorithm. Defaults to betterCentroId which is slower but more accurate.</param>
        public Vector3 TerritoryGetPosition(int territoryIndex, CentroidType centroidType = CentroidType.BetterCentroid, bool worldSpace = true, int regionIndex = 0) {
            if (!ValidTerritoryIndex(territoryIndex)) return Misc.Vector3zero;
            Vector3 territoryCenter;
            if (centroidType == CentroidType.BetterCentroid) {
                territoryCenter = territories[territoryIndex].GetBetterCentroid(regionIndex);
            } else if (centroidType == CentroidType.Centroid) {
                territoryCenter = territories[territoryIndex].GetCentroid(regionIndex);
            } else {
                territoryCenter = territories[territoryIndex].scaledCenter;
            }
            if (worldSpace) {
                territoryCenter = GetWorldSpacePosition(territoryCenter);
            }
            return territoryCenter;
        }

        /// <summary>
        /// Gets the territory's center position in world space.
        /// </summary>
        /// <param name="centroidType">The accuracy of the algorithm. Defaults to betterCentroId which is slower but more accurate.</param>
        public Vector3 TerritoryGetPosition(Territory territory, CentroidType centroidType = CentroidType.BetterCentroid, bool worldSpace = true, int regionIndex = 0) {
            int territoryIndex = TerritoryGetIndex(territory);
            return TerritoryGetPosition(territoryIndex, centroidType, worldSpace, regionIndex);
        }

        /// <summary>
        /// Returns the rect enclosing the territory in world space
        /// </summary>
        public Bounds TerritoryGetRectWorldSpace(int territoryIndex, int regionIndex = 0) {
            if (!ValidTerritoryIndex(territoryIndex, regionIndex))
                return new Bounds(Misc.Vector3zero, Misc.Vector3zero);
            FlushCellChanges();
            Rect rect = territories[territoryIndex].regions[regionIndex].rect2D;
            Vector3 min = GetWorldSpacePosition(rect.min);
            Vector3 max = GetWorldSpacePosition(rect.max);
            Bounds bounds = new Bounds((min + max) * 0.5f, max - min);
            return bounds;
        }


        /// <summary>
        /// Returns the number of vertices of the territory
        /// </summary>
        public int TerritoryGetVertexCount(int territoryIndex, int regionIndex = 0) {
            if (!ValidTerritoryIndex(territoryIndex, regionIndex))
                return 0;
            return territories[territoryIndex].regions[regionIndex].points.Count;
        }


        /// <summary>
        /// Returns the world space position of the vertex of a territory
        /// </summary>
        public Vector3 TerritoryGetVertexPosition(int territoryIndex, int vertexIndex, int regionIndex = 0) {
            if (!ValidTerritoryIndex(territoryIndex, regionIndex))
                return Misc.Vector3zero;
            Vector2 localPosition = territories[territoryIndex].regions[regionIndex].points[vertexIndex];
            return GetWorldSpacePosition(localPosition);
        }


        /// <summary>
        /// Returns the shape/surface gameobject of the territory.
        /// </summary>
        /// <returns>The get game object.</returns>
        /// <param name="cellIndex">Cell index.</param>
        public GameObject TerritoryGetGameObject(int territoryIndex, int regionIndex = 0) {
            if (!ValidTerritoryIndex(territoryIndex, regionIndex))
                return null;
            Territory territory = territories[territoryIndex];
            if (territory == null) return null;
            if (territory.regions[regionIndex].surfaceGameObject != null) {
                return territory.regions[regionIndex].surfaceGameObject;
            }
            GameObject go = TerritoryToggleRegionSurface(territoryIndex, true, Misc.ColorNull, false, regionIndex);
            TerritoryToggleRegionSurface(territoryIndex, false, Misc.ColorNull, false, regionIndex);
            return go;
        }



        /// <summary>
        /// Automatically generates territories based on the different colors included in the texture.
        /// </summary>
        /// <param name="neutral">This color won't generate any texture.</param>
        public void CreateTerritories(Texture2D texture, Color neutral, bool hideNeutralCells = false) {

            if (texture == null || cells == null)
                return;

            List<Color> dsColors = new List<Color>();
            Dictionary<Color, int> dsColorDict = new Dictionary<Color, int>();

            int cellCount = cells.Count;
            Color[] colors;
            try {
                colors = texture.GetPixels();
            } catch {
                Debug.Log("Texture used to create territories is not readable. Check import settings.");
                return;
            }
            for (int k = 0; k < cellCount; k++) {
                Cell cell = cells[k];
                if (cell == null) continue;
                cell.territoryIndex = -1;
                if (!cell.visible)
                    continue;
                Vector2 uv = cell.center;
                uv.x += 0.5f;
                uv.y += 0.5f;

                int x = (int)(uv.x * texture.width);
                int y = (int)(uv.y * texture.height);
                int pos = y * texture.width + x;
                if (pos < 0 || pos >= colors.Length)
                    continue;
                Color pixelColor = colors[pos];
                int territoryIndex;
                if (!dsColorDict.TryGetValue(pixelColor, out territoryIndex)) {
                    dsColors.Add(pixelColor);
                    territoryIndex = dsColors.Count - 1;
                    dsColorDict[pixelColor] = territoryIndex;
                }
                cell.territoryIndex = (short)territoryIndex;
                if (territoryIndex >= MAX_TERRITORIES - 1)
                    break;
            }
            needUpdateTerritories = true;

            if (dsColors.Count > 0) {
                _numTerritories = dsColors.Count;
                _showTerritories = true;

                territories.Clear();
                for (int c = 0; c < _numTerritories; c++) {
                    Territory territory = new Territory(c.ToString());
                    Color territoryColor = dsColors[c];
                    if (territoryColor.r != neutral.r || territoryColor.g != neutral.g || territoryColor.b != neutral.b) {
                        territory.fillColor = territoryColor;
                    } else {
                        territory.fillColor = new Color(0, 0, 0, 0);
                        territory.visible = false;
                    }
                    // Add cells to territories
                    for (int k = 0; k < cellCount; k++) {
                        Cell cell = cells[k];
                        if (cell.territoryIndex == c) {
                            territory.cells.Add(cell);
                            territory.center += cell.center;
                        }
                    }
                    if (territory.cells.Count > 0) {
                        territory.center /= territory.cells.Count;
                        // Ensure center belongs to territory
                        Cell cellAtCenter = GetCellAtPoint(territory.center, false);
                        if (cellAtCenter != null && cellAtCenter.territoryIndex != c) {
                            territory.center = territory.cells[0].center;
                        }
                    }
                    territories.Add(territory);
                }

                isDirty = true;
                issueRedraw = RedrawType.Full;
                Redraw();
            }
        }


        /// <summary>
        /// Scales the gameobject of a colored/textured surface
        /// </summary>
        public void TerritoryScaleSurface(int territoryIndex, float scale, int regionIndex = 0) {
            if (!ValidTerritoryIndex(territoryIndex, regionIndex)) return;
            Territory territory = territories[territoryIndex];
            GameObject surf = territory.regions[regionIndex].surfaceGameObject;
            ScaleSurface(surf, territory.center, scale);
        }


        /// <summary>
        /// Exports all territories as independent meshes
        /// </summary>
        public void ExportTerritoriesMesh() {
            int territoryCount = territories.Count;
            for (int t = 0; t < territoryCount; t++) {
                Territory terr = territories[t];
                if (terr.regions == null) continue;
                int regionCount = terr.regions.Count;
                for (int tr = 0; tr < regionCount; tr++) {
                    ExportTerritoryMesh(t, tr);
                }
            }
        }

        /// <summary>
        /// Exports the specified territory mesh
        /// </summary>
        /// <param name="territoryIndex"></param>
        /// <param name="regionIndex"></param>
        public void ExportTerritoryMesh(int territoryIndex, int regionIndex = 0) {
            if (territoryIndex < 0 || territoryIndex >= territories.Count || regionIndex < 0 || regionIndex >= territories[territoryIndex].regions.Count) return;

            GameObject surf = TerritoryGetGameObject(territoryIndex, regionIndex);
            if (surf == null)
                return;
            MeshFilter mf = surf.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null)
                return;

            Mesh mesh = mf.sharedMesh;
            if (mesh != null && (mesh.uv == null || mesh.uv.Length == 0)) {
                // forces mesh to have UV mappings
                Material oldMat = territories[territoryIndex].regions[regionIndex].customMaterial;
                Color color = oldMat != null ? oldMat.color : Misc.ColorNull;
                surf = TerritoryToggleRegionSurface(territoryIndex, true, color, true, Texture2D.whiteTexture, regionIndex);
                TerritoryToggleRegionSurface(territoryIndex, false, Misc.ColorNull, false, regionIndex);

                mf = surf.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null)
                    return;
            }

            mesh = Instantiate<Mesh>(mf.sharedMesh);
            mesh.name = "Territory " + territoryIndex;
            mesh.hideFlags = 0;

            GameObject newSurf = new GameObject("Copy of territory " + territoryIndex);
            newSurf.layer = gameObject.layer;
            newSurf.transform.position = transform.position;
            newSurf.transform.rotation = transform.rotation;
            newSurf.transform.localScale = transform.localScale;
            mf = newSurf.AddComponent<MeshFilter>();
            mf.mesh = mesh;
            MeshRenderer mr = newSurf.AddComponent<MeshRenderer>();
            mr.material = new Material(Shader.Find("Diffuse"));
        }


        /// <summary>
        /// Returns the points that form the frontier of a territory/region (a territory usually has a single region but could include more than one if it gets split)
        /// Points returned already have the offset and scale of the grid applied
        /// </summary>
        public List<Vector2> TerritoryGetFrontier(int territoryIndex, int regionIndex) {
            if (territoryIndex < 0 || territoryIndex >= territories.Count || regionIndex < 0 || regionIndex >= territories[territoryIndex].regions.Count) return null;
            return territories[territoryIndex].regions[regionIndex].points;
        }


        /// <summary>
        /// Create a new territory with a single cell
        /// </summary>
        /// <returns>The newly created territory</returns>
        public Territory TerritoryCreate(Cell cell) {
            if (cell == null) return null;
            Territory territory = new Territory();
            territories.Add(territory);
            _numTerritories = territories.Count;
            int terrIndex = _numTerritories - 1;
            lastTerritoryLookupCount = -1;
            CellSetTerritory(cell.index, terrIndex);
            return territory;
        }

        /// <summary>
        /// Create a new territory with a single cell
        /// </summary>
        /// <returns>The newly created territory</returns>
        public Territory TerritoryCreate(int cellIndex) {
            if (!ValidCellIndex(cellIndex)) return null;
            return TerritoryCreate(cells[cellIndex]);
        }

        /// <summary>
        /// Create a new territory from a list of cells
        /// </summary>
        /// <returns>The newly created territory</returns>
        public Territory TerritoryCreate(List<Cell> cells) {
            tempListCells.Clear();
            foreach (Cell cell in cells) {
                if (cell != null) tempListCells.Add(cell.index);
            }
            if (tempListCells.Count == 0) return null;
            return TerritoryCreate(tempListCells);
        }


        /// <summary>
        /// Create a new territory from a list of cells
        /// </summary>
        /// <returns>The newly created territory</returns>
        public Territory TerritoryCreate(List<int> cellIndices) {
            Territory territory = new Territory();
            territories.Add(territory);
            _numTerritories = territories.Count;
            int terrIndex = _numTerritories - 1;
            lastTerritoryLookupCount = -1;
            CellSetTerritory(cellIndices, terrIndex);
            return territory;
        }

        /// <summary>
        /// Removes an existing territory. Cells belonging to the territory will be freed.
        /// </summary>
        /// <returns>True if successful</returns>
        public bool TerritoryDestroy(int territoryIndex) {
            if (!ValidTerritoryIndex(territoryIndex)) return false;
            Territory territory = territories[territoryIndex];
            foreach (Cell cell in territory.cells) {
                cell.territoryIndex = -1;
            }
            DestroyTerritorySurfaces(territoryIndex);
            TerritoryHideInteriorBorder(territoryIndex);
            territories.RemoveAt(territoryIndex);
            // update territory indices for other cells
            foreach (Cell cell in cells) {
                if (cell.territoryIndex >= territoryIndex) {
                    cell.territoryIndex--;
                }
            }
            _numTerritories = territories.Count;
            lastTerritoryLookupCount = -1;
            needUpdateTerritories = true;
            issueRedraw = RedrawType.Full;
            return true;
        }


        /// <summary>
        /// Destroys all territories
        /// </summary>
        public void TerritoryDestroyAll() {
            foreach (Cell cell in cells) {
                cell.territoryIndex = -1;
            }
            DestroyTerritorySurfaces();
            TerritoryHideInteriorBorders();
            territories.Clear();
            _numTerritories = 0;
            lastTerritoryLookupCount = -1;
            needUpdateTerritories = true;
            issueRedraw = RedrawType.IncrementalTerritories;
        }

        /// <summary>
        /// If territory color scheme is set to user-defined, copy those settings into current territories and redraw
        /// </summary>
        public void TerritoryUpdateFillColors() {
            if (territories == null || territoriesTexture != null) return;
            int terrCount = territories.Count;

            if (factoryColors == null || factoryColors.Length < MAX_TERRITORIES) {
                factoryColors = new Color[MAX_TERRITORIES];
            }

            UnityEngine.Random.State prevRandomState = UnityEngine.Random.state;
            UnityEngine.Random.InitState(_territoriesColorSeed);
            int factoryColorsLength = factoryColors.Length;
            for (int k = 0; k < factoryColorsLength; k++) {
                factoryColors[k] = new Color(UnityEngine.Random.Range(0.0f, 0.5f), UnityEngine.Random.Range(0.0f, 0.5f), UnityEngine.Random.Range(0.0f, 0.5f));
            }
            UnityEngine.Random.state = prevRandomState;

            if (_territoriesColorScheme == TerritoryColorScheme.UserDefined) {
                if (_territoriesFillColors == null || _territoriesFillColors.Length == 0) {
                    _territoriesFillColors = new Color[terrCount];
                    for (int k = 0; k < _territoriesFillColors.Length; k++) {
                        _territoriesFillColors[k] = factoryColors[k];
                    }
                }
                for (int k = 0; k < _territoriesFillColors.Length; k++) {
                    if (k < terrCount) {
                        if (_territoriesFillColors[k].a == 0) {
                            _territoriesFillColors[k] = factoryColors[k];
                        }
                        territories[k].fillColor = _territoriesFillColors[k];
                    }
                }
            } else {
                for (int k = 0; k < factoryColorsLength; k++) {
                    if (k < terrCount) {
                        territories[k].fillColor = factoryColors[k];
                    }
                }

            }
            TerritoryHideRegionSurfaces();
            Redraw(true);
        }


        [SerializeField]
        int _territoriesColorSeed = 1;

        /// <summary>
        /// Randomize seed used to generate cells. Use this to control randomization.
        /// </summary>
        public int territoriesColorSeed {
            get { return _territoriesColorSeed; }
            set {
                if (_territoriesColorSeed != value) {
                    _territoriesColorSeed = value;
                    TerritoryUpdateFillColors();
                    isDirty = true;
                }
            }
        }


        #endregion



    }
}

