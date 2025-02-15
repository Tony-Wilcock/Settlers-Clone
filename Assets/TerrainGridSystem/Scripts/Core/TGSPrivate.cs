//#define SHOW_DEBUG_GIZMOS
using UnityEngine;
using System;
using System.Collections.Generic;
using TGS.Geom;
using TGS.Poly2Tri;
using TGS.ClipperLib;

namespace TGS {

    [ExecuteInEditMode]
    [Serializable]
    public partial class TerrainGridSystem : MonoBehaviour {

        enum RedrawType {
            None = 0,
            Full = 1,
            IncrementalTerritories = 2
        }

        // internal fields
        const double MIN_VERTEX_DISTANCE = 0.002;
        const double SQR_MIN_VERTEX_DIST = 0.0002 * 0.0002;
        public const int MAX_ROWS_OR_COLUMNS = 1000;

        const int STENCIL_MASK_HUD = 2;
        const int STENCIL_MASK_CELL = 4;
        const int STENCIL_MASK_TERRITORY = 8;
        const int STENCIL_MASK_BORDER = 16;
        const int STENCIL_MASK_INTERIOR_BORDER = 32;
        const int BIG_INT_NUMBER = 214748364;
        const string CANVAS_MESH_NAME = "CanvasQuad";

        readonly int[] hexIndices = new int[] { 0, 5, 1, 1, 5, 2, 5, 4, 2, 2, 4, 3 };
        readonly int[] quadIndices = new int[] { 0, 2, 1, 0, 3, 2 };
        readonly Vector3[] canvasVertices = new Vector3[] {
            new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1f, 0), new Vector3(1f, 1f, 0)
        };

        // Custom inspector stuff
        public const int MAX_TERRITORIES = 512;
        public int maxCellsSqrt = 100;

        [SerializeField]
        bool _isDirty;

        public bool isDirty {
            get { return _isDirty; }
            set {
                if (value) {
                    if (OnGridSettingsChanged != null) {
                        OnGridSettingsChanged(this);
                    }
                    _isDirty = value;
                }
            }
        }
        public const int MAX_CELLS_FOR_CURVATURE = 1000;
        public const int MAX_CELLS_FOR_RELAXATION = 1000;

        // Materials and resources
        Material territoriesThinMat, territoriesGeoMat, territoriesThickHackMat, territoriesGradientMat;
        Material cellsThinMat, cellsGeoMat;
        Material hudMatTerritoryOverlay, hudMatTerritoryGround, hudMatCellOverlay, hudMatCellGround;
        Material fadeMatOverlay, fadeMatGround;
        Material territoriesDisputedThinMat, territoriesDisputedGeoMat;
        Material coloredMatGroundCell, coloredMatOverlayCell;
        Material coloredMatGroundTerritory, coloredMatOverlayTerritory;
        Material texturizedMatGroundCell, texturizedMatOverlayCell;
        Material texturizedMatGroundTerritory, texturizedMatOverlayTerritory;
        Material cellLineMat;

        // Cell mesh data
        const string CELLS_LAYER_NAME = "Cells";
        const string CELLS_CUSTOM_BORDERS_ROOT = "Custom Cell Border";
        Vector3[][] cellMeshBorders;
        int[][] cellMeshIndices;
        float meshStep;
        bool recreateCells, recreateTerritories;
        Dictionary<int, Cell> cellTagged;
        bool needUpdateTerritories;

        // Territory mesh data
        const string TERRITORIES_LAYER_NAME = "Territories";
        const string TERRITORIES_INTERIOR_FRONTIERS_ROOT = "Interior Territory Frontier";
        const string TERRITORIES_CUSTOM_FRONTIERS_ROOT = "Custom Territory Frontier";
        const string TERRITORY_FRONTIER_NAME = "Territory Frontier";
        const string CUSTOM_BORDER_NAME = "Custom Border";
        const string TERRITORY_INTERIOR_BORDER_NAME = "Territory Interior Border";
        static readonly Vector4[] gradientUVs = new Vector4[] {
            new Vector4(0, 1, 0, 0),
            new Vector4(1, 1, 0, 0),
            new Vector4(1, 0, 0, 0),
            new Vector4(0, 0, 0, 0)
        };
        Dictionary<Segment, Frontier> territoryNeighbourHit;
        Frontier[] frontierPool;
        List<Segment> territoryFrontiers;
        List<Region> _sortedTerritoriesRegions;
        List<TerritoryMesh> territoryMeshes;
        Connector[] territoryConnectors;

        // Common territory & cell structures
        Vector3[] frontiersPoints;
        int frontiersPointsCount;
        Dictionary<Segment, bool> segmentHit;
        List<TriangulationPoint> steinerPoints;
        PolygonPoint[] tempPolyPoints;
        PolygonPoint[] tempPolyPoints2;
        Dictionary<TriangulationPoint, int> surfaceMeshHit;
        readonly Connector tempConnector = new Connector();
        List<Vector3> meshPoints;
        int[] triNew;
        int triNewIndex;
        int newPointsCount;
        List<Vector3> tempPoints;
        List<Vector4> tempUVs;
        List<int> tempIndices;
        bool canUseGeometryShaders;

        // Terrain data
        float[,] terrainHeights;
        float[] terrainRoughnessMap;
        float[] tempTerrainRoughnessMap;
        int terrainRoughnessMapWidth, terrainRoughnessMapHeight;
        float[] acumY;
        int heightMapWidth, heightMapHeight;
        const int TERRAIN_CHUNK_SIZE = 8;
        float maxRoughnessWorldSpace;

        // Placeholders and layers
        Transform territoryLayer;
        Transform _surfacesLayer;
        Transform territoryInteriorBorderLayer;

        Transform surfacesLayer {
            get {
                if (_surfacesLayer == null) {
                    CreateSurfacesLayer();
                }
                return _surfacesLayer;
            }
        }

        GameObject _highlightedObj, _highlightedRefSurface;
        GameObject cellLayer;

        // Caches
        readonly Dictionary<int, GameObject> surfaces = new Dictionary<int, GameObject>();
        Dictionary<Territory, int> _territoryLookup;
        int lastTerritoryLookupCount = -1;
        Dictionary<int, Material> coloredMatCacheGroundCell;
        Dictionary<int, Material> coloredMatCacheOverlayCell;
        Dictionary<Color, Material> frontierColorCache;
        Color[] factoryColors;
        bool refreshCellMesh, refreshTerritoriesMesh;
        List<Cell> sortedCells;
        bool needResortCells;
        bool needGenerateMap;
        RedrawType issueRedraw;
        DisposalManager disposalManager;
        List<int> tempListCells;
        int cellIteration;
        Dictionary<Point, Segment> dictSegments;
        bool gridNeedsUpdate;
        bool applyingChanges, redrawing;
        int cellUsedFlag;

        [NonSerialized]
        public readonly MaterialPool materialPool = new MaterialPool();

        // Z-Figther & LOD
        Vector3 lastCamPos, lastPos, lastScale, lastParentScale;
        Quaternion lastRot;
        float lastGridElevation, lastGridCameraOffset, lastGridMinElevationMultiplier;
        float terrainWidth;
        float terrainHeight;
        float terrainDepth;

        // Interaction
        static TerrainGridSystem _instance;
        public bool mouseIsOver;

        int buttonIndex;
        bool leftButtonDown;
        bool rightButtonDown;
        bool leftButtonUp;
        bool rightButtonUp;

        Region _territoryRegionHighlighted;
        Territory _territoryHighlighted {
            get {
                if (_territoryRegionHighlighted != null) {
                    return (Territory)_territoryRegionHighlighted.entity;
                }
                return null;
            }
        }
        int _territoryHighlightedIndex = -1;
        int _territoryHighlightedRegionIndex = -1;
        Cell _cellHighlighted;
        int _cellHighlightedIndex = -1;
        float highlightFadeStart;
        int _territoryLastClickedIndex = -1, _territoryRegionLastClickedIndex = -1;
        int _cellLastClickedIndex = -1;
        int _territoryLastOverIndex = -1, _territoryRegionLastOverIndex = -1;

        Region _territoryRegionLastOver {
            get {
                if (ValidTerritoryIndex(_territoryLastOverIndex)) {
                    return territories[_territoryLastOverIndex].regions[_territoryRegionLastOverIndex];
                }
                else {
                    return null;
                }
            }
        }
        Territory _territoryLastOver {
            get {
                if (ValidTerritoryIndex(_territoryLastOverIndex)) {
                    return territories[_territoryLastOverIndex];
                }
                else {
                    return null;
                }
            }
        }
        int _cellLastOverIndex = -1;
        Cell _cellLastOver;
        bool canInteract;
        List<string> highlightKeywords;
        RaycastHit[] hits;

        Vector3 localCursorLastClickedPos, localCursorPos;
        Vector3 screenCursorLastClickedPos, screenCursorPos;
        Vector3 worldCursorPos;

        enum DragStatus {
            Idle = 0,
            CanDrag = 1,
            Dragging = 2
        }
        DragStatus dragging = DragStatus.Idle;


        // Misc
        int _lastVertexCount = 0;
        Color32[] mask, flatMask;
        bool useEditorRay;
        Ray editorRay;
        List<SurfaceFader> tempFaders;

        [SerializeField, HideInInspector] bool newInHierarchy = true;
        bool shouldBeDestroyed;

        public int lastVertexCount { get { return _lastVertexCount; } }

        delegate int GridDistanceFunction (int cellStartIndex, int cellEndIndex);


        bool territoriesAreUsed {
            get { return _numTerritories > 0; }
        }

        int territoriesRegionsCount;
        List<Region> sortedTerritoriesRegions {
            get {
                if (_sortedTerritoriesRegions.Count != territoriesRegionsCount) {
                    PrepareSortedTerritoriesRegions();
                }
                return _sortedTerritoriesRegions;
            }
            set {
                _sortedTerritoriesRegions = value;
            }
        }

        Dictionary<Territory, int> territoryLookup {
            get {
                if (_territoryLookup != null && territories.Count == lastTerritoryLookupCount)
                    return _territoryLookup;
                if (_territoryLookup == null) {
                    _territoryLookup = new Dictionary<Territory, int>();
                }
                else {
                    _territoryLookup.Clear();
                }
                int terrCount = territories.Count;
                for (int k = 0; k < terrCount; k++) {
                    _territoryLookup.Add(territories[k], k);
                }
                lastTerritoryLookupCount = territories.Count;
                return _territoryLookup;
            }
        }

        Material cellsMat {
            get {
                if (_cellCustomBorderThickness)
                    return cellsGeoMat;
                else
                    return cellsThinMat;
            }
        }

        Material territoriesMat {
            get {
                if (_territoryCustomBorderThickness)
                    return territoriesGeoMat;
                else
                    return territoriesThinMat;
            }
        }

        Material territoriesDisputedMat {
            get {
                if (_territoryCustomBorderThickness)
                    return territoriesDisputedGeoMat;
                else
                    return territoriesDisputedThinMat;
            }
        }

        [SerializeField]
        bool territoryThicknessSettingMigrated; // used to migrate old settings to new ranges and options

        #region Gameloop events

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void InitDomain () {
            TerrainGridSystem[] grids = Misc.FindObjectsOfType<TerrainGridSystem>(true);
            foreach (var grid in grids) {
                grid.Dispose();
            }
        }

        public void OnEnable () {
            applyingChanges = redrawing = false;

            VRCheck.Init();
            if (VRCheck.isActive) _useGeometryShaders = false;

            if (input == null) {
#if ENABLE_INPUT_SYSTEM
                input = new NewInputSystem();
#else
                input = new DefaultInputSystem();
#endif
            }
            input.Init();

            // Use heuristic to determine if TGS should be reparented to this object automatically. Do it if we detect the object mesh is different to the basic quad TGS includes
            if (newInHierarchy) {
                newInHierarchy = false;
                bool isTerrain = GetComponent<MeshRenderer>() == null;
                if (!isTerrain) {
                    MeshFilter mf = GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null || mf.sharedMesh.vertexCount != 4) {
                        isTerrain = true;
                    }
                }
                if (isTerrain) {
                    GameObject terrain = TerrainWrapperProvider.GetSuitableTerrain(gameObject);
                    if (terrain != null) {
                        // Check if user attached TGS directly to the terrain
                        try {
                            GameObject obj = Instantiate(Resources.Load<GameObject>("Prefabs/TerrainGridSystem")) as GameObject;
                            obj.name = "TerrainGridSystem";
                            TerrainGridSystem tgs = obj.GetComponent<TerrainGridSystem>();
                            tgs.SetTerrain(gameObject);
#if UNITY_EDITOR
                            UnityEditor.Selection.activeGameObject = tgs.gameObject;
#endif
                        }
                        catch (Exception ex) {
                            Debug.LogError("Unexpected error while attaching Terrain Grid System: " + ex.ToString());
                        }
                        shouldBeDestroyed = true;
                    }
                    return;
                }
            }

            // Migration from hexSize
            if (_regularHexagonsWidth == 0) {
                _regularHexagonsWidth = _hexSize * transform.lossyScale.x;
            }

            // Migration from old thickness settings
            if (!territoryThicknessSettingMigrated) {
                territoryThicknessSettingMigrated = true;
                if (!_territoryCustomBorderThickness && _territoryFrontiersThickness > 1f) {
                    _territoryCustomBorderThickness = true;
                    _territoryFrontiersThickness -= 0.8f;
                    _territoryFrontiersThickness *= 10f;
                }
                if (_showTerritoriesInteriorBorders && _territoryInteriorBorderThickness > 1f) {
                    _territoryInteriorBorderThickness -= 0.8f;
                }
            }

            if (cameraMain == null) {
                cameraMain = Camera.main;
                int gameObjectLayerMask = 1 << gameObject.layer;
                if (cameraMain == null || (cameraMain.cullingMask & gameObjectLayerMask) == 0) {
#if UNITY_2023_1_OR_NEWER
                    Camera[] cams = FindObjectsByType<Camera>(FindObjectsSortMode.None);
#else
                    Camera[] cams = FindObjectsOfType<Camera>();
#endif
                    for (int k = 0; k < cams.Length; k++) {
                        Camera cam = cams[k];
                        if (cam.isActiveAndEnabled && (cam.cullingMask & gameObjectLayerMask) != 0) {
                            cameraMain = cam;
                            break;
                        }
                    }
                }
            }
            if (!initialized) {
                Init();
            }
            if (hudMatTerritoryOverlay != null && hudMatTerritoryOverlay.color != _territoryHighlightColor) {
                hudMatTerritoryOverlay.color = _territoryHighlightColor;
            }
            hudMatTerritoryOverlay.SetColor(ShaderParams.Color2, _territoryHighlightColor2);

            if (hudMatTerritoryGround != null && hudMatTerritoryGround.color != _territoryHighlightColor) {
                hudMatTerritoryGround.color = _territoryHighlightColor;
            }
            hudMatTerritoryGround.SetColor(ShaderParams.Color2, _territoryHighlightColor2);

            if (hudMatCellOverlay != null && hudMatCellOverlay.color != _cellHighlightColor) {
                hudMatCellOverlay.color = _cellHighlightColor;
                UpdateMaterialHighlightBorder(hudMatCellOverlay);
            }
            hudMatCellOverlay.SetColor(ShaderParams.Color2, _cellHighlightColor2);

            if (hudMatCellGround != null && hudMatCellGround.color != _cellHighlightColor) {
                hudMatCellGround.color = _cellHighlightColor;
                UpdateMaterialHighlightBorder(hudMatCellGround);
            }
            hudMatCellGround.SetColor(ShaderParams.Color2, _cellHighlightColor2);

            if (territoriesThinMat != null && territoriesThinMat.color != _territoryFrontierColor) {
                territoriesThinMat.color = _territoryFrontierColor;
            }
            if (_territoryDisputedFrontierColor == new Color(0, 0, 0, 0)) {
                _territoryDisputedFrontierColor = _territoryFrontierColor;
            }
            if (territoriesDisputedThinMat != null && territoriesDisputedThinMat.color != _territoryDisputedFrontierColor) {
                territoriesDisputedThinMat.color = _territoryDisputedFrontierColor;
            }
            if (territoriesDisputedGeoMat != null && territoriesDisputedGeoMat.color != _territoryDisputedFrontierColor) {
                territoriesDisputedGeoMat.color = _territoryDisputedFrontierColor;
            }
            if (cellsThinMat != null && cellsThinMat.color != _cellBorderColor) {
                cellsThinMat.color = _cellBorderColor;
            }
            if (!grids.Contains(this)) {
                grids.Add(this);
            }
        }

        public void Start () {
            if (shouldBeDestroyed) {
                DestroyImmediate(this);
            }
        }


        void OnDestroy () {
            Dispose();
        }

        /// <summary>
        /// Releases any grid memory or surface
        /// </summary>
        public void Dispose () {
            DestroySurfaces();
            if (grids != null && grids.Contains(this)) {
                grids.Remove(this);
            }
            if (_terrainWrapper != null) {
                _terrainWrapper.Dispose();
            }
            if (disposalManager != null) {
                disposalManager.DisposeAll();
            }
            if (rectangleSelection != null) {
                DestroyImmediate(rectangleSelection.gameObject);
            }
            if (materialPool != null) {
                materialPool.Release();
            }
            cells = null;
        }

        /// <summary>
        /// If grid needs any kind of update, including terrain or position changes, perform it now
        /// </summary>
        void CheckGridChanges () {
            if (applyingChanges) return;
            applyingChanges = true;

            if (needGenerateMap) {
                GenerateMap(reuseTerrainData: true);
            }

            if (needResortCells) {
                ResortCells();
            }

            FitToTerrain();         // Verify if there're changes in container and adjust the grid mesh accordingly

            if (issueRedraw != RedrawType.None) {
                Redraw(issueRedraw == RedrawType.IncrementalTerritories);
            }

            applyingChanges = false;
        }

        /// <summary>
        /// If cells or territories have changed, update them now
        /// </summary>
        void FlushCellChanges () {
            if (needGenerateMap || needResortCells || issueRedraw != RedrawType.None) {
                CheckGridChanges();
            }
        }


        void LateUpdate () {

            CheckGridChanges();

            if (_disableMeshGeneration) return;

#if ENABLE_INPUT_SYSTEM
            CheckMouseIsOver();
#endif

            if (_circularFadeEnabled && _circularFadeTarget != null) {
                Shader.SetGlobalVector(ShaderParams.CircularFadePosition, _circularFadeTarget.position);
            }

            bool mobileTouchStarting = input.touchCount > 0 && input.IsTouchStarting(0);

            // Check whether the points is on an UI element, then avoid user interaction
            if (respectOtherUI) {
                if (!canInteract && Application.isMobilePlatform && !mobileTouchStarting)
                    return;

                canInteract = true;
                if (UnityEngine.EventSystems.EventSystem.current != null) {
                    if (Application.isMobilePlatform && input.touchCount > 0 && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(input.GetFingerIdFromTouch(0))) {
                        canInteract = false;
                    }
                    else if (input.IsPointerOverUI()) {
                        canInteract = false;
                    }
                }
                if (!canInteract) {
                    if (_highlightMode == HighlightMode.Territories) {
                        HideTerritoryRegionHighlight();
                    }
                    else if (_highlightMode == HighlightMode.Cells) {
                        HideCellHighlight();
                    }
                    return;
                }
            }

            // check mouse pos and input events
            GatherInput();

            bool canCheckMousePos;
            if (Application.isMobilePlatform) {
                canCheckMousePos = mobileTouchStarting || (input.touchCount == 1 && (_allowHighlightWhileDragging || dragging != DragStatus.Idle));
            }
            else {
                canCheckMousePos = !(input.GetMouseButton(0) && !_allowHighlightWhileDragging && dragging == DragStatus.Idle);
            }
            if (canCheckMousePos) {
                CheckMousePos();
            }

            // dispatch events
            TriggerEvents();

            UpdateHighlightFade();
        }

        #endregion



        #region Initialization

        bool initialized => cells != null && hudMatTerritoryOverlay != null && textures != null;

        public void Init () {

#if UNITY_EDITOR
            var go = transform.root.gameObject;
            UnityEditor.PrefabInstanceStatus prefabInstanceStatus = UnityEditor.PrefabUtility.GetPrefabInstanceStatus(go);
            if (prefabInstanceStatus != UnityEditor.PrefabInstanceStatus.NotAPrefab) {
                UnityEditor.EditorApplication.delayCall += () => {
                    if (go != null) {
                        UnityEditor.PrefabUtility.UnpackPrefabInstance(go, UnityEditor.PrefabUnpackMode.Completely, UnityEditor.InteractionMode.AutomatedAction);
                    }
                };
            }
#endif

            disposalManager = new DisposalManager();
            tempFaders = new List<SurfaceFader>();
            tempListCells = new List<int>();
            tempPoints = new List<Vector3>();
            tempUVs = new List<Vector4>();
            tempIndices = new List<int>();

            LoadGeometryShaders();

            if (territoriesThinMat == null) {
                territoriesThinMat = Instantiate(Resources.Load<Material>("Materials/Territory")) as Material;
                disposalManager.MarkForDisposal(territoriesThinMat);
            }
            if (territoriesDisputedThinMat == null) {
                territoriesDisputedThinMat = Instantiate(territoriesThinMat) as Material;
                disposalManager.MarkForDisposal(territoriesDisputedThinMat);
                territoriesDisputedThinMat.color = _territoryDisputedFrontierColor;
            }
            if (cellsThinMat == null) {
                cellsThinMat = Instantiate(Resources.Load<Material>("Materials/Cell"));
                disposalManager.MarkForDisposal(cellsThinMat);
            }
            if (hudMatTerritoryOverlay == null) {
                hudMatTerritoryOverlay = new Material(Shader.Find("Terrain Grid System/Unlit Highlight Ground Texture"));
                hudMatTerritoryOverlay.SetInt(ShaderParams.Cull, (int)UnityEngine.Rendering.CullMode.Off);
                hudMatTerritoryOverlay.SetInt(ShaderParams.ZTest, (int)UnityEngine.Rendering.CompareFunction.Always);
                disposalManager.MarkForDisposal(hudMatTerritoryOverlay);
            }
            if (hudMatTerritoryGround == null) {
                hudMatTerritoryGround = Instantiate(hudMatTerritoryOverlay) as Material;
                SetOverlayMode(hudMatTerritoryGround, false);
                disposalManager.MarkForDisposal(hudMatTerritoryGround);
            }
            if (hudMatCellOverlay == null) {
                hudMatCellOverlay = Instantiate(hudMatTerritoryOverlay) as Material;
                SetOverlayMode(hudMatCellOverlay, true);
                disposalManager.MarkForDisposal(hudMatCellOverlay);
            }
            if (hudMatCellGround == null) {
                hudMatCellGround = Instantiate(hudMatTerritoryOverlay) as Material;
                hudMatCellGround.SetInt(ShaderParams.Cull, (int)UnityEngine.Rendering.CullMode.Back);
                hudMatCellGround.SetInt(ShaderParams.ZTest, (int)UnityEngine.Rendering.CompareFunction.LessEqual);
                disposalManager.MarkForDisposal(hudMatCellGround);
            }
            // Materials for cells
            const int cellsQueueOffset = 25;
            if (coloredMatGroundCell == null) {
                coloredMatGroundCell = Instantiate(Resources.Load<Material>("Materials/ColorizedRegionGround")) as Material;
                coloredMatGroundCell.renderQueue += cellsQueueOffset;
                disposalManager.MarkForDisposal(coloredMatGroundCell);
            }
            if (coloredMatOverlayCell == null) {
                coloredMatOverlayCell = Instantiate(Resources.Load<Material>("Materials/ColorizedRegionOverlay")) as Material;
                coloredMatOverlayCell.renderQueue += cellsQueueOffset;
                disposalManager.MarkForDisposal(coloredMatOverlayCell);
            }
            if (texturizedMatGroundCell == null) {
                texturizedMatGroundCell = Instantiate(Resources.Load<Material>("Materials/TexturizedRegionGround"));
                texturizedMatGroundCell.renderQueue += cellsQueueOffset;
                disposalManager.MarkForDisposal(texturizedMatGroundCell);
            }
            if (texturizedMatOverlayCell == null) {
                texturizedMatOverlayCell = Instantiate(Resources.Load<Material>("Materials/TexturizedRegionOverlay"));
                texturizedMatOverlayCell.renderQueue += cellsQueueOffset;
                disposalManager.MarkForDisposal(texturizedMatOverlayCell);
            }
            // Materials for territories
            if (coloredMatGroundTerritory == null) {
                coloredMatGroundTerritory = Instantiate(Resources.Load<Material>("Materials/ColorizedRegionGround")) as Material;
                disposalManager.MarkForDisposal(coloredMatGroundTerritory);
            }
            if (coloredMatOverlayTerritory == null) {
                coloredMatOverlayTerritory = Instantiate(Resources.Load<Material>("Materials/ColorizedRegionOverlay")) as Material;
                disposalManager.MarkForDisposal(coloredMatOverlayTerritory);
            }
            if (texturizedMatGroundTerritory == null) {
                texturizedMatGroundTerritory = Instantiate(Resources.Load<Material>("Materials/TexturizedRegionGround"));
                disposalManager.MarkForDisposal(texturizedMatGroundTerritory);
            }
            if (texturizedMatOverlayTerritory == null) {
                texturizedMatOverlayTerritory = Instantiate(Resources.Load<Material>("Materials/TexturizedRegionOverlay"));
                disposalManager.MarkForDisposal(texturizedMatOverlayTerritory);
            }
            if (fadeMatGround == null) {
                fadeMatGround = Instantiate(Resources.Load<Material>("Materials/Fade"));
                fadeMatGround.SetInt(ShaderParams.ZTest, (int)UnityEngine.Rendering.CompareFunction.LessEqual);
                disposalManager.MarkForDisposal(fadeMat);
            }
            if (fadeMatOverlay == null) {
                fadeMatOverlay = Instantiate(Resources.Load<Material>("Materials/Fade"));
                fadeMatOverlay.SetInt(ShaderParams.ZTest, (int)UnityEngine.Rendering.CompareFunction.Always);
                disposalManager.MarkForDisposal(fadeMat);
            }
            UpdatePreventOverdrawSettings();

            coloredMatCacheGroundCell = new Dictionary<int, Material>();
            coloredMatCacheOverlayCell = new Dictionary<int, Material>();
            frontierColorCache = new Dictionary<Color, Material>();
            if (hits == null || hits.Length == 0) {
                hits = new RaycastHit[100];
            }

            if (_sortedTerritoriesRegions == null)
                _sortedTerritoriesRegions = new List<Region>(MAX_TERRITORIES);

            if (textures == null || textures.Length < 32)
                textures = new Texture2D[32];

            UnityEngine.Random.State prevRandomState = UnityEngine.Random.state;
            UnityEngine.Random.InitState(seed);

            ReadMaskContents();
            Redraw();

            if (territoriesTexture != null) {
                CreateTerritories(territoriesTexture, territoriesTextureNeutralColor, territoriesHideNeutralCells);
            }
            UnityEngine.Random.state = prevRandomState;
        }

        void LoadGeometryShaders () {
            canUseGeometryShaders = _useGeometryShaders && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Metal;
            if (territoriesThickHackMat == null) {
                territoriesThickHackMat = new Material(Shader.Find("Terrain Grid System/Unlit Single Color Territory Thick Hack"));
            }
            if (territoriesGradientMat == null) {
                territoriesGradientMat = new Material(Shader.Find("Terrain Grid System/Unlit Single Color Territory Gradient"));
            }
            if (territoriesGeoMat == null) {
                if (canUseGeometryShaders) {
                    territoriesGeoMat = new Material(Shader.Find("Terrain Grid System/Unlit Single Color Territory Geo"));
                }
                else {
                    territoriesGeoMat = territoriesThickHackMat;
                }
                disposalManager.MarkForDisposal(territoriesGeoMat);
            }
            if (territoriesGeoMat != null && territoriesGeoMat.color != _territoryFrontierColor) {
                territoriesGeoMat.color = _territoryFrontierColor;
            }
            if (territoriesDisputedGeoMat == null) {
                territoriesDisputedGeoMat = Instantiate(territoriesGeoMat) as Material;
                disposalManager.MarkForDisposal(territoriesDisputedGeoMat);
            }
            if (territoriesDisputedGeoMat != null && territoriesDisputedGeoMat.color != _territoryDisputedFrontierColor) {
                territoriesDisputedGeoMat.color = _territoryDisputedFrontierColor;
            }
            if (cellsGeoMat == null) {
                if (canUseGeometryShaders) {
                    cellsGeoMat = new Material(Shader.Find("Terrain Grid System/Unlit Single Color Cell Geo"));
                }
                else {
                    cellsGeoMat = new Material(Shader.Find("Terrain Grid System/Unlit Single Color Cell Thick Hack"));
                }
                disposalManager.MarkForDisposal(cellsGeoMat);
            }
            if (cellsGeoMat != null && cellsGeoMat.color != _cellBorderColor) {
                cellsGeoMat.color = _cellBorderColor;
            }
            if (frontierColorCache != null) {
                frontierColorCache.Clear();
            }
        }

        public void ResetDebugInfo () {
#if UNITY_EDITOR
            currentCellDebugInfo = CellDebugInfo.Nothing;
            currentTerritoryDebugInfo = TerritoryDebugInfo.Nothing;
#endif
        }

#if UNITY_EDITOR
        string[] cellDebugInfoLabels;
        string[] territoryDebugInfoLabels;

        void OnDrawGizmosSelected () {
            if (_terrainWrapper != null) {
                Gizmos.DrawWireCube(_terrainWrapper.bounds.center, _terrainWrapper.bounds.size);
            }

            DisplayTerritoryInfo();
            DisplayCellInfo();
        }

        CellDebugInfo currentCellDebugInfo;
        void DisplayCellInfo () {

            if (cells == null) return;
            int cellsCount = cells.Count;
            const int MAX_DEBUG_CELLS = 2000;

            CellDebugInfo cellDebugInfo = _displayCellDebugInfo;
            if (cellDebugInfo == CellDebugInfo.Nothing && _numTerritories > 0 && _territoriesInitialCellMethod == TerritoryInitialCellMethod.UserDefined && cellsCount < MAX_DEBUG_CELLS) {
                cellDebugInfo = CellDebugInfo.CellIndex;
            }

            if (cellDebugInfo == CellDebugInfo.Nothing) return;

            if (cellDebugInfo == CellDebugInfo.CellCoordinates && _gridTopology == GridTopology.Irregular) return;

            if (cells == null) return;

            if (cells == null) return;

            cellsCount = Mathf.Min(cellsCount, MAX_DEBUG_CELLS);
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.alignment = TextAnchor.MiddleCenter;
            if (cellDebugInfoLabels == null || cellDebugInfoLabels.Length != cellsCount || currentCellDebugInfo != cellDebugInfo) {
                currentCellDebugInfo = cellDebugInfo;
                cellDebugInfoLabels = new string[cellsCount];
                switch (currentCellDebugInfo) {
                    case CellDebugInfo.CellIndex:
                        for (int k = 0; k < cellsCount; k++) {
                            cellDebugInfoLabels[k] = k.ToString();
                        }
                        break;
                    case CellDebugInfo.CellCoordinates:
                        for (int k = 0; k < cellsCount; k++) {
                            cellDebugInfoLabels[k] = cells[k].row + ", " + cells[k].column;
                        }
                        break;
                    case CellDebugInfo.PathFindingCrossCost:
                        for (int k = 0; k < cellsCount; k++) {
                            cellDebugInfoLabels[k] = cells[k].GetSideCrossCost(0).ToString();
                        }
                        break;
                }
            }

            for (int k = 0; k < cellsCount; k++) {
                Cell cell = cells[k];
                if (cell == null) continue;
                Vector3 wpos = CellGetPosition(cell);
                UnityEditor.Handles.Label(wpos, cellDebugInfoLabels[k], style);
            }
        }

        TerritoryDebugInfo currentTerritoryDebugInfo;
        void DisplayTerritoryInfo () {

            if (_displayTerritoryDebugInfo == TerritoryDebugInfo.Nothing) return;

            if (territories == null) return;

            int territoriesCount = territories.Count;
            if (territoryDebugInfoLabels == null || territoryDebugInfoLabels.Length != territoriesCount || currentTerritoryDebugInfo != _displayTerritoryDebugInfo) {
                currentTerritoryDebugInfo = _displayTerritoryDebugInfo;
                territoryDebugInfoLabels = new string[territoriesCount];
                switch (_displayTerritoryDebugInfo) {
                    case TerritoryDebugInfo.TerritoryIndex:
                        for (int k = 0; k < territoriesCount; k++) {
                            territoryDebugInfoLabels[k] = k.ToString();
                        }
                        break;
                }
            }

            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.alignment = TextAnchor.MiddleCenter;
            style.normal.textColor = Color.yellow;
            style.fontSize += 8;
            style.fontStyle = FontStyle.Bold;

            for (int k = 0; k < territoriesCount; k++) {
                Territory territory = territories[k];
                if (territory == null) continue;
                int regionsCount = territory.regions.Count;
                for (int r = 0; r < regionsCount; r++) {
                    Vector3 wpos = TerritoryGetPosition(territory, regionIndex: r);
                    if (regionsCount == 1) {
                        UnityEditor.Handles.Label(wpos, territoryDebugInfoLabels[k], style);
                    }
                    else {
                        UnityEditor.Handles.Label(wpos, k + "." + r, style);
                    }
                }
            }
        }
#endif

        void CreateSurfacesLayer () {
            Transform t = transform.Find("Surfaces");
            if (t != null) {
                DestroyImmediate(t.gameObject);
            }
            GameObject surfacesLayerGO = new GameObject("Surfaces");
            surfacesLayerGO.layer = gameObject.layer;
            _surfacesLayer = surfacesLayerGO.transform;
            _surfacesLayer.SetParent(transform, false);
            _surfacesLayer.localPosition = Misc.Vector3zero;
        }

        void DestroySurfaces () {
            HideTerritoryRegionHighlight();
            HideCellHighlight();
            if (highlightedObj != null) {
                DestroyImmediate(highlightedObj);
            }
            if (segmentHit != null) {
                segmentHit.Clear();
            }
            if (surfaces != null) {
                foreach (var obj in surfaces) {
                    GameObject go = obj.Value;
                    if (go != null) {
                        if (go.TryGetComponent<MeshFilter>(out MeshFilter mf) && mf.sharedMesh != null) {
                            DestroyImmediate(mf.sharedMesh);
                        }
                        DestroyImmediate(go);
                    }
                }
                surfaces.Clear();
            }
            if (_surfacesLayer != null) {
                DestroyImmediate(_surfacesLayer.gameObject);
            }
        }

        void DestroyTerritorySurfaces () {
            if (territories == null) return;
            int territoriesCount = territories.Count;
            for (int k = 0; k < territoriesCount; k++) {
                DestroyTerritorySurfaces(k);
            }
        }

        void DestroyTerritorySurfaces (int territoryIndex) {
            HideTerritoryRegionHighlight();
            Territory terr = territories[territoryIndex];
            for (int r = 0; r < terr.regions.Count; r++) {
                if (terr.regions[r] != null) {
                    terr.regions[r].DestroySurface();
                }
            }
        }

        void DestroySurfacesDirty () {
            if (cells != null) {
                int cellCount = cells.Count;
                for (int k = 0; k < cellCount; k++) {
                    Cell cell = cells[k];
                    if (cell != null && cell.isDirty) {
                        int cacheIndex = GetCacheIndexForCellRegion(k);
                        if (cell.region != null) {
                            cell.region.DestroySurface(true);
                        }
                        surfaces[cacheIndex] = null;
                    }
                }
                int territoryCount = territories.Count;
                for (int k = 0; k < territoryCount; k++) {
                    Territory territory = territories[k];
                    if (territory != null && territory.isDirty && territory.regions != null) {
                        int regionsCount = territory.regions.Count;
                        for (int r = 0; r < regionsCount; r++) {
                            int cacheIndex = GetCacheIndexForTerritoryRegion(k, r);
                            if (territory.regions[r] != null) {
                                territory.regions[r].DestroySurface(true);
                            }
                            surfaces[cacheIndex] = null;
                        }
                    }
                }
            }
        }



        void ReadMaskContents () {
            if (_gridMask == null)
                return;
            try {
                mask = _gridMask.GetPixels32();
            }
            catch {
                mask = null;
                Debug.Log("Mask texture is not readable. Check import settings.");
            }
        }


        void ReadFlatMaskContents () {
            if (_gridFlatMask == null)
                return;
            try {
                flatMask = _gridFlatMask.GetPixels32();
            }
            catch {
                flatMask = null;
                Debug.Log("Flat mask texture is not readable. Check import settings.");
            }
        }



        #endregion


        #region Map generation

        [NonSerialized]
        public VoronoiCell[] lastComputedVoronoiCells;

        void SetupIrregularGrid () {

            VoronoiFortune voronoi = new VoronoiFortune();

            if (hasBakedVoronoi) {
                VoronoiDeserializeData(ref voronoi.cells);
            }
            else {
                int userVoronoiSitesCount = _voronoiSites != null ? _voronoiSites.Count : 0;
                Point[] centers = new Point[_numCells];
                for (int k = 0; k < centers.Length; k++) {
                    if (k < userVoronoiSitesCount) {
                        centers[k] = new Point(_voronoiSites[k].x, _voronoiSites[k].y);
                    }
                    else {
                        centers[k] = new Point(UnityEngine.Random.Range(-0.49f, 0.49f), UnityEngine.Random.Range(-0.49f, 0.49f));
                    }
                }
                for (int k = 0; k < goodGridRelaxation; k++) {
                    voronoi.AssignData(centers);
                    voronoi.DoVoronoi();
                    if (k < goodGridRelaxation - 1) {
                        for (int j = 0; j < _numCells; j++) {
                            Point centroid = voronoi.cells[j].centroid;
                            centers[j] = (centers[j] + centroid) / 2;
                        }
                    }
                }
                if (_voronoiSerializationData != null) {
                    _voronoiSerializationData = null;
#if UNITY_EDITOR
                    if (!Application.isPlaying) {
                        UnityEditor.EditorUtility.SetDirty(this);
                        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
                    }
#endif
                }
            }
            lastComputedVoronoiCells = voronoi.cells;

            // Make cell regions: we assume cells have only 1 region but that can change in the future
            float curvature = goodGridCurvature;
            int cellsCount = 0;
            if (dictSegments == null) {
                dictSegments = new Dictionary<Point, Segment>(voronoi.cells.Length * 4);
            }
            else {
                dictSegments.Clear();
            }

            for (int k = 0; k < voronoi.cells.Length; k++) {
                VoronoiCell voronoiCell = voronoi.cells[k];
                Cell cell = new Cell(voronoiCell.center.vector3);
                Region cr = new Region(cell, false);
                if (curvature > 0) {
                    cr.polygon = voronoiCell.GetPolygon(3, curvature);
                }
                else {
                    cr.polygon = voronoiCell.GetPolygon(1, 0);
                }
                if (cr.polygon != null) {
                    // Add segments
                    int segCount = voronoiCell.segments.Count;
                    for (int i = 0; i < segCount; i++) {
                        Segment s = voronoiCell.segments[i];
                        if (!s.deleted) {
                            if (curvature > 0) {
                                cr.segments.AddRange(s.subdivisions);
                            }
                            else {
                                cr.segments.Add(s);
                            }
                        }
                    }

                    cell.region = cr;
                    cell.index = cellsCount++;
                    cells.Add(cell);
                }
            }
        }

        void SetupBoxGrid () {

            int qx = _cellColumnCount;
            int qy = _cellRowCount;

            double stepX = 1.0 / qx;
            double stepY = 1.0 / qy;

            double halfStepX = stepX * 0.5;
            double halfStepY = stepY * 0.5;

            Segment[,,] sides = new Segment[qx, qy, 4]; // 0 = left, 1 = top, 2 = right, 3 = bottom
            int subdivisions = goodGridCurvature > 0 ? 3 : 1;
            int cellsCount = 0;
            Connector connector = new Connector();
            Point[] quadPoints = new Point[4];

            for (int j = 0; j < qy; j++) {
                for (int k = 0; k < qx; k++) {
                    Point center = new Point((double)k / qx - 0.5 + halfStepX, (double)j / qy - 0.5 + halfStepY);
                    Cell cell = new Cell(new Vector2((float)center.x, (float)center.y));
                    cell.column = (ushort)k;
                    cell.row = (ushort)j;

                    Point p1 = center.Offset(-halfStepX, -halfStepY);
                    Point p2 = center.Offset(-halfStepX, halfStepY);
                    Point p3 = center.Offset(halfStepX, halfStepY);
                    Point p4 = center.Offset(halfStepX, -halfStepY);

                    Segment left = k > 0 ? sides[k - 1, j, 2] : new Segment(p1, p2, true);
                    sides[k, j, 0] = left;

                    Segment top = new Segment(p2, p3, j == qy - 1);
                    sides[k, j, 1] = top;

                    Segment right = new Segment(p3, p4, k == qx - 1);
                    sides[k, j, 2] = right;

                    Segment bottom = j > 0 ? sides[k, j - 1, 1] : new Segment(p4, p1, true);
                    sides[k, j, 3] = bottom;

                    Region cr = new Region(cell, true);
                    if (subdivisions > 1) {
                        cr.segments.AddRange(top.Subdivide(subdivisions, _gridCurvature));
                        cr.segments.AddRange(right.Subdivide(subdivisions, _gridCurvature));
                        cr.segments.AddRange(bottom.Subdivide(subdivisions, _gridCurvature));
                        cr.segments.AddRange(left.Subdivide(subdivisions, _gridCurvature));
                        connector.Clear();
                        connector.AddRange(cr.segments);
                        cr.polygon = connector.ToPolygon();
                    }
                    else {
                        cr.segments.Add(top);
                        cr.segments.Add(right);
                        cr.segments.Add(bottom);
                        cr.segments.Add(left);
                        quadPoints[0] = p1;
                        quadPoints[1] = p4;
                        quadPoints[2] = p3;
                        quadPoints[3] = p2;
                        cr.polygon = new Geom.Polygon(quadPoints);  // left.start, bottom.start, right.start, top.start);
                    }
                    if (cr.polygon != null) {
                        cell.region = cr;
                        cell.index = cellsCount++;
                        cells.Add(cell);
                    }
                }
            }
        }

        void SetupFlatToppedHexagonalGrid () {

            double qx = 1.0 + (_cellColumnCount - 1.0) * 3.0 / 4.0;
            double qy = _cellRowCount + 0.5;
            int qy2 = _cellRowCount;
            int qx2 = _cellColumnCount;

            double stepX = 1.0 / qx;
            double stepY = 1.0 / qy;

            double halfStepX = stepX * 0.5;
            double halfStepY = stepY * 0.5;
            int evenLayout = _evenLayout ? 1 : 0;
            Point[] hexPoints = new Point[6];

            Segment[,,] sides = new Segment[qx2, qy2, 6]; // 0 = left-up, 1 = top, 2 = right-up, 3 = right-down, 4 = down, 5 = left-down
            int subdivisions = goodGridCurvature > 0 ? 3 : 1;
            int cellsCount = 0;
            for (int j = 0; j < qy2; j++) {
                for (int k = 0; k < qx2; k++) {
                    Point center = new Point((double)k / qx - 0.5 + halfStepX, (double)j / qy - 0.5 + stepY);
                    center.x -= k * halfStepX / 2;
                    Cell cell = new Cell(new Vector2((float)center.x, (float)center.y));
                    cell.row = (ushort)j;
                    cell.column = (ushort)k;

                    double offsetY = (k % 2 == evenLayout) ? 0 : -halfStepY;

                    Point p1 = center.Offset(-halfStepX, offsetY);
                    Point p2 = center.Offset(-halfStepX / 2, halfStepY + offsetY);
                    Point p3 = center.Offset(halfStepX / 2, halfStepY + offsetY);
                    Point p4 = center.Offset(halfStepX, offsetY);
                    Point p5 = center.Offset(halfStepX / 2, -halfStepY + offsetY);
                    Point p6 = center.Offset(-halfStepX / 2, -halfStepY + offsetY);

                    Segment leftUp = (k > 0 && offsetY < 0) ? sides[k - 1, j, 3] : new Segment(p1, p2, k == 0 || (j == qy2 - 1 && offsetY == 0));
                    sides[k, j, 0] = leftUp;

                    Segment top = new Segment(p2, p3, j == qy2 - 1);
                    sides[k, j, 1] = top;

                    Segment rightUp = new Segment(p3, p4, k == qx2 - 1 || (j == qy2 - 1 && offsetY == 0));
                    sides[k, j, 2] = rightUp;

                    Segment rightDown = (j > 0 && k < qx2 - 1 && offsetY < 0) ? sides[k + 1, j - 1, 0] : new Segment(p4, p5, (j == 0 && offsetY < 0) || k == qx2 - 1);
                    sides[k, j, 3] = rightDown;

                    Segment bottom = j > 0 ? sides[k, j - 1, 1] : new Segment(p5, p6, true);
                    sides[k, j, 4] = bottom;

                    Segment leftDown;
                    if (offsetY < 0 && j > 0 && k > 0) {
                        leftDown = sides[k - 1, j - 1, 2];
                    }
                    else if (offsetY == 0 && k > 0) {
                        leftDown = sides[k - 1, j, 2];
                    }
                    else {
                        leftDown = new Segment(p6, p1, true);
                    }
                    sides[k, j, 5] = leftDown;

                    cell.center.y += (float)offsetY;

                    Region cr = new Region(cell, false);
                    if (subdivisions > 1) {
                        cr.segments.AddRange(top.Subdivide(subdivisions, _gridCurvature));
                        cr.segments.AddRange(rightUp.Subdivide(subdivisions, _gridCurvature));
                        cr.segments.AddRange(rightDown.Subdivide(subdivisions, _gridCurvature));
                        cr.segments.AddRange(bottom.Subdivide(subdivisions, _gridCurvature));
                        cr.segments.AddRange(leftDown.Subdivide(subdivisions, _gridCurvature));
                        cr.segments.AddRange(leftUp.Subdivide(subdivisions, _gridCurvature));
                        Connector connector = new Connector();
                        connector.AddRange(cr.segments);
                        cr.polygon = connector.ToPolygon();
                    }
                    else {
                        cr.segments.Add(top);
                        cr.segments.Add(rightUp);
                        cr.segments.Add(rightDown);
                        cr.segments.Add(bottom);
                        cr.segments.Add(leftDown);
                        cr.segments.Add(leftUp);
                        hexPoints[0] = p1;
                        hexPoints[1] = p6;
                        hexPoints[2] = p5;
                        hexPoints[3] = p4;
                        hexPoints[4] = p3;
                        hexPoints[5] = p2;
                        cr.polygon = new Geom.Polygon(hexPoints); // left, left/down, right/down, right, top/right, top/left
                    }
                    if (cr.polygon != null) {
                        cell.region = cr;
                        cell.index = cellsCount++;
                        cells.Add(cell);
                    }
                }
            }
        }

        void SetupPointyToppedHexagonalGrid () {
            double qx = _cellColumnCount + 0.5;
            double qy = 1.0 + (_cellRowCount - 1.0) * 3.0 / 4.0;
            int qy2 = _cellRowCount;
            int qx2 = _cellColumnCount;

            double stepX = 1.0 / qx;
            double stepY = 1.0 / qy;

            double halfStepX = stepX * 0.5;
            double halfStepY = stepY * 0.5;
            int evenLayout = _evenLayout ? 1 : 0;
            Point[] hexPoints = new Point[6];

            Segment[,,] sides = new Segment[qx2, qy2, 6]; // 0 = left-up, 1 = right-up, 2 = right, 3 = right-down, 4 = left-down, 5 = left
            int subdivisions = goodGridCurvature > 0 ? 3 : 1;
            int cellsCount = 0;
            for (int j = 0; j < qy2; j++) {
                double offsetX = (j % 2 == evenLayout) ? 0 : -halfStepX;
                for (int k = 0; k < qx2; k++) {
                    Point center = new Point((double)k / qx - 0.5 + stepX, (double)j / qy - 0.5 + halfStepY);
                    center.y -= j * halfStepY / 2;
                    Cell cell = new Cell(new Vector2((float)center.x, (float)center.y));
                    cell.row = (ushort)j;
                    cell.column = (ushort)k;

                    Point p1 = center.Offset(offsetX, -halfStepY);
                    Point p2 = center.Offset(halfStepX + offsetX, -halfStepY / 2);
                    Point p3 = center.Offset(halfStepX + offsetX, halfStepY / 2);
                    Point p4 = center.Offset(offsetX, halfStepY);
                    Point p5 = center.Offset(-halfStepX + offsetX, halfStepY / 2);
                    Point p6 = center.Offset(-halfStepX + offsetX, -halfStepY / 2);

                    Segment leftUp = new Segment(p4, p5, (k == 0 && offsetX < 0) || j == qy2 - 1);
                    sides[k, j, 0] = leftUp;

                    Segment rightUp = new Segment(p3, p4, (k == qx2 - 1 && offsetX == 0) || j == qy2 - 1);
                    sides[k, j, 1] = rightUp;

                    Segment right = new Segment(p2, p3, k == qx2 - 1);
                    sides[k, j, 2] = right;

                    Segment rightDown;
                    if (j > 0 && !(k == qx2 - 1 && offsetX == 0)) {
                        if (offsetX < 0) {
                            rightDown = sides[k, j - 1, 0];
                        }
                        else {
                            rightDown = sides[k + 1, j - 1, 0];
                        }
                    }
                    else {
                        rightDown = new Segment(p1, p2, j == 0 || (offsetX == 0 && k == qx2 - 1));
                    }
                    sides[k, j, 3] = rightDown;

                    Segment leftDown;
                    if (j > 0 && !(k == 0 && offsetX < 0)) {
                        if (offsetX < 0) {
                            leftDown = sides[k - 1, j - 1, 1];
                        }
                        else {
                            leftDown = sides[k, j - 1, 1];
                        }
                    }
                    else {
                        leftDown = new Segment(p6, p1, j == 0 || (offsetX < 0 && k == 0));
                    }
                    sides[k, j, 4] = leftDown;

                    Segment left = k > 0 ? sides[k - 1, j, 2] : new Segment(p5, p6, true);
                    sides[k, j, 5] = left;

                    cell.center.x += (float)offsetX;

                    Region cr = new Region(cell, false);
                    if (subdivisions > 1) {
                        cr.segments.AddRange(rightUp.Subdivide(subdivisions, _gridCurvature));
                        cr.segments.AddRange(right.Subdivide(subdivisions, _gridCurvature));
                        cr.segments.AddRange(rightDown.Subdivide(subdivisions, _gridCurvature));
                        cr.segments.AddRange(leftDown.Subdivide(subdivisions, _gridCurvature));
                        cr.segments.AddRange(left.Subdivide(subdivisions, _gridCurvature));
                        cr.segments.AddRange(leftUp.Subdivide(subdivisions, _gridCurvature));
                        Connector connector = new Connector();
                        connector.AddRange(cr.segments);
                        cr.polygon = connector.ToPolygon();
                    }
                    else {
                        cr.segments.Add(rightUp);
                        cr.segments.Add(right);
                        cr.segments.Add(rightDown);
                        cr.segments.Add(leftDown);
                        cr.segments.Add(left);
                        cr.segments.Add(leftUp);
                        hexPoints[0] = p1;
                        hexPoints[1] = p6;
                        hexPoints[2] = p5;
                        hexPoints[3] = p4;
                        hexPoints[4] = p3;
                        hexPoints[5] = p2;
                        cr.polygon = new Geom.Polygon(hexPoints); // right/up, right, right/down, left/down, left, left/up
                    }
                    if (cr.polygon != null) {
                        cell.region = cr;
                        cell.index = cellsCount++;
                        cells.Add(cell);
                    }
                }
            }
        }

        void CreateCells () {
            UnityEngine.Random.State prevRandomState = UnityEngine.Random.state;
            UnityEngine.Random.InitState(seed);

            _numCells = Mathf.Max(_numTerritories, 2, cellCount);
            if (cells == null) {
                cells = new List<Cell>(_numCells);
            }
            else {
                cells.Clear();
            }
            if (cellTagged == null)
                cellTagged = new Dictionary<int, Cell>();
            else
                cellTagged.Clear();

            switch (_gridTopology) {
                case GridTopology.Box:
                    SetupBoxGrid();
                    break;
                case GridTopology.Hexagonal:
                    if (_pointyTopHexagons) {
                        SetupPointyToppedHexagonalGrid();
                    }
                    else {
                        SetupFlatToppedHexagonalGrid();
                    }
                    break;
                default:
                    SetupIrregularGrid();
                    break;
            }

            CellsFindNeighbours();
            CellsUpdateBounds();

            // Update sorted cell list
            if (sortedCells == null) {
                sortedCells = new List<Cell>(cells);
            }
            else {
                sortedCells.Clear();
                sortedCells.AddRange(cells);
            }
            ResortCells();

            ClearLastOver();

            recreateCells = false;
            UnityEngine.Random.state = prevRandomState;

        }

        void ResortCells () {
            needResortCells = false;
            sortedCells.Sort((cell1, cell2) => {
                float area1 = cell1.region.rect2DArea;
                float area2 = cell2.region.rect2DArea;
                if (area1 < area2) return -1;
                if (area1 > area2) return 1;
                return 0;
            });
        }

        /// <summary>
        /// Takes the center of each cell or the border points and checks the alpha component of the mask to confirm visibility
        /// </summary>
        void CellsApplyVisibilityFilters () {
            int cellsCount = cells.Count;
            if (gridMask != null && mask != null) {
                int tw = gridMask.width;
                int th = gridMask.height;
                if (tw * th == mask.Length) {
                    for (int k = 0; k < cellsCount; k++) {
                        Cell cell = cells[k];
                        int pointCount = cell.region.points.Count;
                        bool visible = false;
                        int insideCount = 0;
                        for (int v = 0; v < pointCount; v++) {
                            Vector2 p = cell.region.points[v];
                            if (_gridMaskUseScale) {
                                GetUnscaledVector(ref p);
                            }
                            float y = p.y + 0.5f;
                            float x = p.x + 0.5f;
                            int ty = (int)(y * th);
                            int tx = (int)(x * tw);
                            if (ty >= 0 && ty < th && tx >= 0 && tx < tw && mask[ty * tw + tx].a > 0) {
                                insideCount++;
                                if (insideCount >= _gridMaskInsideCount) {
                                    visible = true;
                                    break;
                                }
                            }
                        }
                        cell.visibleByRules = visible;
                    }
                }
            }
            else {
                for (int k = 0; k < cellsCount; k++) {
                    Cell cell = cells[k];
                    if (cell != null) {
                        cell.visibleByRules = true;
                    }
                }
            }

            // update cell flat state
            if (_cellsFlat) {
                if (gridFlatMask != null && flatMask != null) {
                    int tw = gridFlatMask.width;
                    int th = gridFlatMask.height;
                    if (tw * th == flatMask.Length) {
                        for (int k = 0; k < cellsCount; k++) {
                            Cell cell = cells[k];
                            if (cell == null) continue;
                            Vector2 p = cell.scaledCenter;
                            float y = p.y + 0.5f;
                            float x = p.x + 0.5f;
                            int ty = (int)(y * th);
                            int tx = (int)(x * tw);
                            cell.region.isFlat = (ty >= 0 && ty < th && tx >= 0 && tx < tw && flatMask[ty * tw + tx].a > 0);
                        }
                    }
                }
                else {
                    for (int k = 0; k < cellsCount; k++) {
                        Cell cell = cells[k];
                        cell.region.isFlat = true;
                    }
                }
            }
            else {
                for (int k = 0; k < cellsCount; k++) {
                    Cell cell = cells[k];
                    if (cell != null) {
                        cell.region.isFlat = false;
                    }
                }
            }

            // potentially hide cells whose territories are invisible
            if (territoriesHideNeutralCells && territories != null) {
                int terrCount = territories.Count;
                for (int k = 0; k < cellsCount; k++) {
                    Cell cell = cells[k];
                    if (cell != null && cell.visible) {
                        int terr = cell.territoryIndex;
                        cell.visibleByRules = terr < 0 || (terr >= 0 && terr < terrCount && territories[terr].visible);
                    }
                }
            }


            if (_terrainWrapper != null) {
                if (_cellsMaxSlope < 1f) {
                    for (int k = 0; k < cellsCount; k++) {
                        Cell cell = cells[k];
                        if (cell == null || !cell.visible)
                            continue;
                        float x = cell.scaledCenter.x + 0.5f;
                        if (x < 0 || x > 1f)
                            continue;
                        float y = cell.scaledCenter.y + 0.5f;
                        if (y < 0 || y > 1f)
                            continue;
                        Vector3 norm = _terrainWrapper.GetInterpolatedNormal(x, y);
                        float slope = norm.y > 0 ? 1.00001f - norm.y : 1.00001f + norm.y;
                        if (slope > _cellsMaxSlope) {
                            cell.visibleByRules = false;
                        }
                    }
                }
                if (_cellsMinimumAltitude != 0f || _cellsMaximumAltitude != 0) {
                    float minAlt = _cellsMinimumAltitude != 0 ? _cellsMinimumAltitude : float.MinValue;
                    float maxAlt = _cellsMaximumAltitude != 0 ? _cellsMaximumAltitude : float.MaxValue;
                    for (int k = 0; k < cellsCount; k++) {
                        Cell cell = cells[k];
                        if (cell == null || !cell.visible)
                            continue;
                        Vector3 wpos = GetWorldSpacePosition(cell.scaledCenter);
                        if (wpos.y < minAlt || wpos.y > maxAlt) {
                            cell.visibleByRules = false;
                        }
                    }
                }
                if (_cellsMaxHeightDifference > 0) {
                    for (int k = 0; k < cellsCount; k++) {
                        Cell cell = cells[k];
                        if (cell == null || !cell.visible)
                            continue;
                        float minHeight = float.MaxValue;
                        float maxHeight = float.MinValue;
                        int vertexCount = cell.region.points.Count;
                        for (int v = 0; v < vertexCount; v++) {
                            Vector3 wpos = GetWorldSpacePosition(cell.region.points[v]);
                            if (wpos.y < minHeight) minHeight = wpos.y;
                            if (wpos.y > maxHeight) maxHeight = wpos.y;
                        }
                        float heightDiff = minHeight < maxHeight ? maxHeight - minHeight : minHeight - maxHeight;
                        if (heightDiff > _cellsMaxHeightDifference) {
                            cell.visibleByRules = false;
                        }
                    }
                }
            }

            ClearLastOver();
            needRefreshRouteMatrix = true;
        }

        void CellsUpdateBounds () {
            // Update cells polygon
            int count = cells.Count;
            for (int k = 0; k < count; k++) {
                CellUpdateBounds(cells[k]);
            }
        }

        void CellUpdateBounds (Cell cell) {
            if (cell == null) return;

            if (cell.region.polygon.contours.Count == 0)
                return;

            List<Vector2> points = cell.region.points;
            cell.region.polygon.contours[0].GetVector2Points(_gridCenter, _gridScale, ref points);
            cell.region.SetPoints(points);
            cell.scaledCenter = GetScaledVector(cell.center);
        }

        void CellsUpdateNeighbours () {
            int cellCount = cells.Count;
            for (int k = 0; k < cellCount; k++) {
                Cell cell = cells[k];
                if (cell == null) continue;
                Region region = cell.region;
                cell.neighbours.Clear();
                int segCount = region.segments.Count;
                for (int j = 0; j < segCount; j++) {
                    region.segments[j].cellIndex = k;
                }
            }
            CellsFindNeighbours();
        }

        readonly HashSet<int> cachedNeighboursControl = new HashSet<int>();

        void CellsFindNeighbours () {

            cachedNeighboursControl.Clear();
            int cellCount = cells.Count;
            for (int k = 0; k < cellCount; k++) {
                Cell cell = cells[k];
                if (cell == null) continue;
                Region region = cell.region;
                int numSegments = region.segments.Count;
                for (int i = 0; i < numSegments; i++) {
                    Segment seg = region.segments[i];
                    if (seg.cellIndex < 0) {
                        seg.cellIndex = cell.index;
                    }
                    else if (seg.cellIndex != cell.index) {
                        Cell neighbour = cells[seg.cellIndex];
                        int pairIndex;
                        if (cell.index < neighbour.index) {
                            pairIndex = cell.index * cellCount + neighbour.index;
                        }
                        else {
                            pairIndex = neighbour.index * cellCount + cell.index;

                        }
                        if (!cachedNeighboursControl.Contains(pairIndex)) { // avoid duplicates in merged cells which can have several segments adjancent to same neighbour
                            cachedNeighboursControl.Add(pairIndex);
                            cell.neighbours.Add(neighbour);
                            neighbour.neighbours.Add(cell);
                        }
                    }
                }
            }

        }

        readonly List<Region> newRegions = new List<Region>();

        void FindTerritoriesFrontiers () {

            if (territories.Count == 0)
                return;

            if (territoryFrontiers == null) {
                territoryFrontiers = new List<Segment>(50000);
            }
            else {
                territoryFrontiers.Clear();
            }
            if (territoryNeighbourHit == null) {
                territoryNeighbourHit = new Dictionary<Segment, Frontier>(50000);
            }
            else {
                territoryNeighbourHit.Clear();
            }
            int terrCount = territories.Count;
            if (territoryConnectors == null || territoryConnectors.Length != terrCount) {
                territoryConnectors = new Connector[terrCount];
            }
            int cellCount = cells.Count;
            if (frontierPool == null) {
                int frontierPoolLength = cellCount * 6;
                frontierPool = new Frontier[frontierPoolLength];
                for (int f = 0; f < frontierPoolLength; f++) {
                    frontierPool[f] = new Frontier();
                }
            }
            int frontierPoolUsed = 0;
            for (int k = 0; k < terrCount; k++) {
                if (territoryConnectors[k] == null) {
                    territoryConnectors[k] = new Connector();
                }
                else {
                    territoryConnectors[k].Clear();
                }
                Territory territory = territories[k];
                territory.cells.Clear();
                territory.neighbours.Clear();
            }

            for (int k = 0; k < cellCount; k++) {
                Cell cell = cells[k];
                if (cell == null || cell.territoryIndex >= terrCount)
                    continue;
                bool validCell = cell.visible && cell.territoryIndex >= 0;
                if (validCell) {
                    territories[cell.territoryIndex].cells.Add(cell);
                }
                Region cellRegion = cell.region;
                int numSegments = cellRegion.segments.Count;
                for (int i = 0; i < numSegments; i++) {
                    Segment seg = cellRegion.segments[i];
                    if (seg.border) {
                        if (validCell) {
                            territoryFrontiers.Add(seg);
                            int territory1 = cell.territoryIndex;
                            territoryConnectors[territory1].Add(seg);
                            seg.disputingTerritory1Index = seg.disputingTerritory2Index = seg.territoryIndex = territory1;
                        }
                        continue;
                    }
                    if (territoryNeighbourHit.TryGetValue(seg, out Frontier frontier)) {
                        Region neighbour = frontier.region1;
                        Cell neighbourCell = (Cell)neighbour.entity;
                        int territory1Index = cell.territoryIndex;
                        int territory2Index = neighbourCell.territoryIndex;
                        seg.disputingTerritory1Index = territory1Index;
                        seg.disputingTerritory2Index = territory2Index;
                        if (territory2Index != territory1Index) {
                            territoryFrontiers.Add(seg);
                            if (validCell) {
                                territoryConnectors[territory1Index].Add(seg);
                                Territory territory1 = territories[territory1Index];
                                // check segment ownership
                                if (territory2Index >= 0) {
                                    Territory territory2 = territories[territory2Index];
                                    if (!territory1.visible && !territory2.visible) {
                                        seg.territoryIndex = -9; // hide segment
                                        frontier.region2 = frontier.region1;
                                        frontier.region1 = cell.region;
                                    }
                                    else if (territory1.neutral && territory2.neutral) {
                                        seg.territoryIndex = territory1Index;
                                        frontier.region2 = frontier.region1;
                                        frontier.region1 = cell.region;
                                    }
                                    else if ((territory1.neutral || !territory1.visible) && !territory2.neutral) {
                                        seg.territoryIndex = territory2Index;
                                        frontier.region2 = cell.region;
                                    }
                                    else if (!territory1.neutral && (territory2.neutral || !territory2.visible)) {
                                        seg.territoryIndex = territory1Index;
                                        frontier.region2 = frontier.region1;
                                        frontier.region1 = cell.region;
                                    }
                                    else {
                                        seg.territoryIndex = -1; // if segment belongs to a visible cell and valid territory2, mark this segment as disputed. Otherwise make it part of territory1
                                        frontier.region2 = cell.region;
                                    }
                                }
                                else {
                                    seg.territoryIndex = territory1Index;
                                    frontier.region2 = cell.region;
                                }

                                if (seg.territoryIndex < 0) {
                                    // add territory neighbours
                                    Territory territory2 = territories[territory2Index];
                                    if (!territory1.neighbours.Contains(territory2)) {
                                        territory1.neighbours.Add(territory2);
                                    }
                                    if (!territory2.neighbours.Contains(territory1)) {
                                        territory2.neighbours.Add(territory1);
                                    }
                                }
                            }
                            if (territory2Index >= 0) {
                                territoryConnectors[territory2Index].Add(seg);
                            }
                        }
                    }
                    else {
                        if (frontierPoolUsed >= frontierPool.Length) {
                            // Resize frontier pool
                            int newLen = frontierPool.Length * 2;
                            Frontier[] newPool = new Frontier[newLen];
                            Array.Copy(frontierPool, newPool, frontierPool.Length);
                            for (int f = frontierPool.Length; f < newLen; f++) {
                                newPool[f] = new Frontier();
                            }
                            frontierPool = newPool;
                        }
                        // Get it from pool
                        frontier = frontierPool[frontierPoolUsed++];
                        frontier.region1 = cellRegion;
                        frontier.region2 = null;
                        territoryNeighbourHit[seg] = frontier;
                        seg.disputingTerritory1Index = seg.disputingTerritory2Index = seg.territoryIndex = cell.territoryIndex;
                        if (!validCell) {
                            seg.territoryIndex = -9; // hide segment
                        }
                    }
                }
            }

            newRegions.Clear();
            territoriesRegionsCount = 0;
            for (int k = 0; k < terrCount; k++) {
                Territory terr = territories[k];
                if (issueRedraw == RedrawType.IncrementalTerritories && !terr.isDirty) continue;
                newRegions.Clear();
                if (territories[k].cells.Count > 0) {
                    Geom.Polygon polygon = territoryConnectors[k].ToPolygon();
                    if (polygon == null) continue;

                    // remove invalid contours
                    int contoursCount = polygon.contours.Count;
                    for (int c = 0; c < contoursCount; c++) {
                        Geom.Contour contour = polygon.contours[c];
                        int pointsCount = contour.points.Count;
                        if (pointsCount < 3) {
                            polygon.contours.RemoveAt(c);
                            contoursCount--;
                            c--;
                            continue;
                        }
                    }

                    // detect self-crossing contours (rare case found in box topology with cell in diagonals)
                    if (_gridTopology == GridTopology.Box) {
                        contoursCount = polygon.contours.Count;
                        for (int c = 0; c < contoursCount; c++) {
                            Geom.Contour contour = polygon.contours[c];
                            int pointsCount = contour.points.Count;
                            if (pointsCount > 4) {
                                for (int j = 0; j < pointsCount; j++) {
                                    Point p0 = contour.points[j];
                                    for (int i = j + 1; i < pointsCount; i++) {
                                        Point p1 = contour.points[i];
                                        if (Point.EqualsBoth(p0, p1)) {
                                            Geom.Contour newContour = new Geom.Contour(contour.points, j, i - j);
                                            polygon.contours.Add(newContour);
                                            contour.points.RemoveRange(j, i - j);
                                            contoursCount++;
                                            j = pointsCount;
                                            c--;
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }


                    // sort contours by size
                    polygon.contours.Sort(delegate (Geom.Contour c1, Geom.Contour c2) {
                        return c1.rect2DArea.CompareTo(c2.rect2DArea);
                    });


                    // remove interior contours
                    for (int c = 0; c < contoursCount; c++) {
                        Geom.Contour contour1 = polygon.contours[c];
                        int pointsCount = contour1.points.Count;
                        int enclosing = 0;
                        // count how many contours are enclosing this
                        for (int d = c + 1; d < contoursCount; d++) {
                            Geom.Contour contour2 = polygon.contours[d];
                            if (contour2.ContainsPoint(contour1.points[0]) && contour2.ContainsPoint(contour1.points[pointsCount / 2])) {
                                enclosing++;
                            }
                        }
                        if (enclosing > 0 && (enclosing % 2) == 1) {
                            polygon.contours.RemoveAt(c);
                            c--;
                            contoursCount--;
                        }
                    }

                    contoursCount = polygon.contours.Count;
                    for (int c = 0; c < contoursCount; c++) {
                        Geom.Polygon regionPoly = new Geom.Polygon();
                        regionPoly.AddContour(polygon.contours[c]);
                        Region region = new Region(terr, false);
                        region.sortIndex = c;
                        region.polygon = regionPoly;
                        newRegions.Add(region);
                    }
                }

                // Update regions
                if (terr.regions.Count == 0) {
                    terr.regions.AddRange(newRegions);
                }
                else {
                    // In order to keep previous regions order, keep a copy of old regions and check the center of each new region with the old regions to find their old position
                    int newRegionsCount = newRegions.Count;
                    int terrRegionsCount = terr.regions.Count;
                    int lastIndex = -1;
                    for (int r = 0; r < newRegionsCount; r++) {
                        Region newRegion = newRegions[r];
                        newRegion.sortIndex = -1;
                        Vector2 newRegionCenter = newRegion.polygon.boundingBox.center;
                        for (int q = 0; q < terrRegionsCount; q++) {
                            Region oldRegion = terr.regions[q];
                            if (oldRegion.Contains(newRegionCenter.x, newRegionCenter.y)) {
                                newRegion.sortIndex = oldRegion.sortIndex;
                                if (newRegion.sortIndex > lastIndex) {
                                    lastIndex = newRegion.sortIndex;
                                }
                                break;
                            }
                        }
                    }

                    // ensure new regions indices follow the previous indices
                    for (int r = 0; r < newRegionsCount; r++) {
                        Region newRegion = newRegions[r];
                        if (newRegion.sortIndex < 0) {
                            newRegion.sortIndex = ++lastIndex;
                        }
                    }

                    // sort regions list
                    newRegions.Sort((Region r1, Region r2) => r1.sortIndex.CompareTo(r2.sortIndex));

                    // update existing territory regions
                    while (terr.regions.Count > newRegionsCount) {
                        terr.regions.RemoveAt(terr.regions.Count - 1);
                    }
                    for (int r = 0; r < newRegionsCount; r++) {
                        Region region;
                        if (terr.regions.Count <= r) {
                            region = terr.regions[0].CloneWithoutPointData();
                            terr.regions.Add(region);
                        }
                        else {
                            region = terr.regions[r];
                        }
                        region.polygon = newRegions[r].polygon;
                        region.ClearPointData();
                    }
                }
                territoriesRegionsCount += terr.regions.Count;
            }
        }


        /// <summary>
        /// Subdivides the segment in smaller segments
        /// </summary>
        void SurfaceSegmentForMesh (Transform transform, Segment s) {

            Vector3 p0 = GetScaledVector(s.start);
            Vector3 p1 = GetScaledVector(s.end);

            // trace the line until roughness is exceeded
            float dist = (float)Math.Sqrt((p1.x - p0.x) * (p1.x - p0.x) + (p1.y - p0.y) * (p1.y - p0.y));
            Vector3 direction = p1 - p0;

            int numSteps = (int)(meshStep * dist);
            EnsureCapacityFrontiersPoints(numSteps * 2);

            Vector3 t0 = p0;
            float h0 = _terrainWrapper.GetInterpolatedHeight(transform.TransformPoint(t0));
            if (_gridNormalOffset > 0) {
                Vector3 invNormal = transform.InverseTransformVector(_terrainWrapper.GetInterpolatedNormal(t0.x + 0.5f, t0.y + 0.5f));
                t0 += invNormal * _gridNormalOffset;
            }
            t0.z -= h0;

            float h1;
            Vector3 ta = t0;
            float ha = h0;
            for (int i = 1; i < numSteps; i++) {
                Vector3 t1 = p0 + direction * ((float)i / numSteps);
                h1 = _terrainWrapper.GetInterpolatedHeight(transform.TransformPoint(t1));
                if (h1 - h0 > maxRoughnessWorldSpace || h0 - h1 > maxRoughnessWorldSpace) {
                    frontiersPoints[frontiersPointsCount++] = t0;
                    if (t0 != ta) {
                        if (_gridNormalOffset > 0) {
                            Vector3 invNormal = transform.InverseTransformVector(_terrainWrapper.GetInterpolatedNormal(ta.x + 0.5f, ta.y + 0.5f));
                            ta += invNormal * _gridNormalOffset;
                        }
                        ta.z -= ha;
                        frontiersPoints[frontiersPointsCount++] = ta;
                        frontiersPoints[frontiersPointsCount++] = ta;
                    }
                    if (_gridNormalOffset > 0) {
                        Vector3 invNormal = transform.InverseTransformVector(_terrainWrapper.GetInterpolatedNormal(t1.x + 0.5f, t1.y + 0.5f));
                        t1 += invNormal * _gridNormalOffset;
                    }
                    t1.z -= h1;
                    frontiersPoints[frontiersPointsCount++] = t1;
                    t0 = t1;
                    h0 = h1;
                }
                ta = t1;
                ha = h1;
            }
            // Add last point
            h1 = _terrainWrapper.GetInterpolatedHeight(transform.TransformPoint(p1));
            if (_gridNormalOffset > 0) {
                Vector3 invNormal = transform.InverseTransformVector(_terrainWrapper.GetInterpolatedNormal(p1.x + 0.5f, p1.y + 0.5f));
                p1 += invNormal * _gridNormalOffset;
            }
            p1.z -= h1;
            frontiersPoints[frontiersPointsCount++] = t0;
            frontiersPoints[frontiersPointsCount++] = p1;
        }


        /// <summary>
        /// Subdivides the segment in smaller segments using the same height than the center of the cell
        /// </summary>
        void SurfaceSegmentForFlatMesh (Segment s, float height, Vector3 invNormal) {

            Vector3 p0 = GetScaledVector(s.start);
            Vector3 p1 = GetScaledVector(s.end);

            p0 += invNormal * _gridNormalOffset;
            p0.z -= height;

            p1 += invNormal * _gridNormalOffset;
            p1.z -= height;

            EnsureCapacityFrontiersPoints(2);
            frontiersPoints[frontiersPointsCount++] = p0;
            frontiersPoints[frontiersPointsCount++] = p1;
        }


        /// <summary>
        /// Subdivides the segment in smaller segments using the same height than the center of the cell
        /// </summary>
        void SurfaceSegmentForFlatMesh (Segment s, float height) {

            Vector3 p0 = GetScaledVector(s.start);
            Vector3 p1 = GetScaledVector(s.end);
            p0.z -= height;
            p1.z -= height;
            EnsureCapacityFrontiersPoints(2);
            frontiersPoints[frontiersPointsCount++] = p0;
            frontiersPoints[frontiersPointsCount++] = p1;
        }

        void GenerateCellsMeshData (List<Cell> cells) {

            if (_disableMeshGeneration) return;

            if (segmentHit == null) {
                segmentHit = new Dictionary<Segment, bool>(50000);
            }
            else {
                segmentHit.Clear();
            }

            if (frontiersPoints == null || frontiersPoints.Length == 0) {
                frontiersPoints = new Vector3[100000];
            }
            frontiersPointsCount = 0;

            int cellCount = cells.Count;
            if (_terrainWrapper == null) {
                for (int k = 0; k < cellCount; k++) {
                    Cell cell = cells[k];
                    if (cell != null && cell.visible && cell.borderVisible) {
                        Region region = cell.region;
                        int numSegments = region.segments.Count;
                        EnsureCapacityFrontiersPoints(numSegments * 2);
                        for (int i = 0; i < numSegments; i++) {
                            Segment s = region.segments[i];
                            if (segmentHit.TryAdd(s, true)) {
                                frontiersPoints[frontiersPointsCount++] = GetScaledVector(s.start);
                                frontiersPoints[frontiersPointsCount++] = GetScaledVector(s.end);
                            }
                        }
                    }
                }
            }
            else {
                meshStep = (2.0f - _gridRoughness) / (float)MIN_VERTEX_DISTANCE;
                Transform t = transform;
                for (int k = 0; k < cellCount; k++) {
                    Cell cell = cells[k];
                    if (cell != null && cell.visible && cell.borderVisible) {
                        Region region = cell.region;
                        int numSegments = region.segments.Count;
                        if (region.isFlat) {
                            float height = _terrainWrapper.GetInterpolatedHeight(transform.TransformPoint(cell.scaledCenter));
                            if (_gridNormalOffset > 0) {
                                Vector3 invNormal = transform.InverseTransformVector(_terrainWrapper.GetInterpolatedNormal(cell.scaledCenter.x, cell.scaledCenter.y));
                                for (int i = 0; i < numSegments; i++) {
                                    Segment s = region.segments[i];
                                    SurfaceSegmentForFlatMesh(s, height, invNormal);
                                }
                            }
                            else {
                                for (int i = 0; i < numSegments; i++) {
                                    Segment s = region.segments[i];
                                    SurfaceSegmentForFlatMesh(s, height);
                                }
                            }
                        }
                        else {
                            for (int i = 0; i < numSegments; i++) {
                                Segment s = region.segments[i];
                                if (segmentHit.TryAdd(s, true)) {
                                    SurfaceSegmentForMesh(t, s);
                                }
                            }
                        }
                    }
                }
            }

            int maxPointsPerMesh = 65504;
            if (isUsingVertexDispForCellThickness) {
                maxPointsPerMesh /= 4;
            }

            int meshGroups = (frontiersPointsCount / maxPointsPerMesh) + 1;
            int meshIndex = -1;
            if (cellMeshIndices == null || cellMeshIndices.GetUpperBound(0) != meshGroups - 1) {
                cellMeshIndices = new int[meshGroups][];
                cellMeshBorders = new Vector3[meshGroups][];
            }
            if (frontiersPointsCount == 0) {
                cellMeshBorders[0] = new Vector3[0];
                cellMeshIndices[0] = new int[0];
            }
            else {
                // Clamp points to minimum elevation
                if (_cellsMinimumAltitudeClampVertices && _cellsMinimumAltitude != 0) {
                    float localMinima = -(_cellsMinimumAltitude - _terrainWrapper.bounds.min.y) / _terrainWrapper.transform.lossyScale.y;
                    for (int k = 0; k < frontiersPointsCount; k++) {
                        if (frontiersPoints[k].z > localMinima) {
                            frontiersPoints[k].z = localMinima;
                        }
                    }
                }

                // Clamp points to maximum elevation
                if (_cellsMaximumAltitudeClampVertices && _cellsMaximumAltitude != 0) {
                    float localMaxima = -(_cellsMaximumAltitude - _terrainWrapper.bounds.min.y) / _terrainWrapper.transform.lossyScale.y;
                    for (int k = 0; k < frontiersPointsCount; k++) {
                        if (frontiersPoints[k].z < localMaxima) {
                            frontiersPoints[k].z = localMaxima;
                        }
                    }
                }

                for (int k = 0; k < frontiersPointsCount; k += maxPointsPerMesh) {
                    int max = Mathf.Min(frontiersPointsCount - k, maxPointsPerMesh);
                    ++meshIndex;
                    if (cellMeshBorders[meshIndex] == null || cellMeshBorders[0].GetUpperBound(0) != max - 1) {
                        cellMeshBorders[meshIndex] = new Vector3[max];
                        cellMeshIndices[meshIndex] = new int[max];
                    }
                    for (int j = 0; j < max; j++) {
                        cellMeshBorders[meshIndex][j] = frontiersPoints[j + k];
                        cellMeshIndices[meshIndex][j] = j;
                    }
                }
            }
        }

        void CreateTerritories () {

            recreateTerritories = false;

            _numTerritories = Mathf.Clamp(_numTerritories, 0, cellCount);
            territories.Clear();

            if (_numTerritories == 0) {
                if (territoryLayer != null) {
                    DestroyImmediate(territoryLayer.gameObject);
                }
                return;
            }

            CheckCells();
            if (cells == null) return;

            // Freedom for the cells!...
            int cellsCount = cells.Count;
            for (int k = 0; k < cellsCount; k++) {
                cells[k].territoryIndex = -1;
            }
            UnityEngine.Random.State prevRandomState = UnityEngine.Random.state;
            UnityEngine.Random.InitState(seed);

            for (int c = 0; c < _numTerritories; c++) {
                Territory territory = new Territory(c.ToString());
                int territoryIndex = territories.Count;
                int p = -1;
                if (_territoriesInitialCellMethod == TerritoryInitialCellMethod.UserDefined) {
                    if (_territoriesInitialCellIndices != null && c < _territoriesInitialCellIndices.Length && ValidCellIndex(_territoriesInitialCellIndices[c])) {
                        p = _territoriesInitialCellIndices[c];
                    }
                }
                if (_territoriesInitialCellMethod == TerritoryInitialCellMethod.RandomBasedOnSeed || p < 0) {
                    p = UnityEngine.Random.Range(0, cellsCount);
                    int z = 0;
                    while ((cells[p].territoryIndex != -1 || !cells[p].visible) && z++ <= cellsCount) {
                        p++;
                        if (p >= cellsCount)
                            p = 0;
                    }
                    if (z > cellsCount)
                        break; // no more territories can be found - this should not happen
                }
                Cell cell = cells[p];
                cell.territoryIndex = (short)territoryIndex;
                territory.center = cell.center;
                territory.cells.Add(cell);
                territories.Add(territory);
            }

            TerritoryUpdateFillColors();

            // Continue conquering cells
            int[] territoryCellIndex = new int[territories.Count];

            // Iterate one cell per country (this is not efficient but ensures balanced distribution)
            bool remainingCells = true;
            int iterations = 0;
            int terrCount = territories.Count;
            float asymmetry = Mathf.Max(0, terrCount * (1f - _territoriesAsymmetry));
            while (remainingCells) {
                remainingCells = false;
                if (++iterations >= _territoriesMaxIterations && _territoriesMaxIterations > 0) break;

                for (int k = 0; k < terrCount; k++) {
                    Territory territory = territories[k];
                    int territoryCellsCount = territory.cells.Count;
                    int startCellIndex = territoryCellIndex[k];
                    startCellIndex = (int)Mathf.Lerp(startCellIndex, territoryCellsCount, UnityEngine.Random.value * _territoriesOrganic);
                    for (int p = startCellIndex; p < territoryCellsCount; p++) {
                        Cell cell = territory.cells[p];
                        int nCount = cell.neighbours.Count;
                        for (int n = 0; n < nCount; n++) {
                            Cell otherCell = cell.neighbours[n];
                            if (otherCell.territoryIndex == -1 && otherCell.visible) {
                                otherCell.territoryIndex = (short)k;
                                territory.cells.Add(otherCell);
                                territoryCellsCount++;
                                remainingCells = true;
                                p = territoryCellsCount;
                                if (UnityEngine.Random.value > (k + asymmetry) / terrCount) k--;
                                break;
                            }
                        }
                        if (p < territoryCellsCount) {// no free neighbours left for this cell
                            territoryCellIndex[k]++;
                        }
                    }
                }
            }
            UnityEngine.Random.state = prevRandomState;

            FindTerritoriesFrontiers();
            UpdateTerritoriesBoundary();

        }

        void UpdateTerritoriesBoundary () {
            if (territories == null)
                return;

            // Update territory region points, also according to grid scale and offset
            int terrCount = territories.Count;
            for (int k = 0; k < terrCount; k++) {
                Territory territory = territories[k];
                if (issueRedraw == RedrawType.IncrementalTerritories && !territory.isDirty) continue;
                int regionsCount = territory.regions.Count;
                for (int r = 0; r < regionsCount; r++) {
                    Region territoryRegion = territory.regions[r];
                    if (territoryRegion.polygon == null) {
                        continue;
                    }

                    List<Vector2> regionPoints = territoryRegion.points;
                    territoryRegion.polygon.contours[0].GetVector2Points(_gridCenter, _gridScale, ref regionPoints);
                    territoryRegion.SetPoints(regionPoints);
                    territory.scaledCenter = GetScaledVector(territory.center);

                    regionPoints = null;

                    List<Point> points = territoryRegion.polygon.contours[0].points;
                    int pointCount = points.Count;

                    // Reuse segments memory
                    int segCount = territoryRegion.segments.Count;
                    if (segCount < pointCount) {
                        territoryRegion.segments.Clear();
                        for (int j = 0; j < pointCount; j++) {
                            Point p0 = points[j];
                            Point p1;
                            if (j == pointCount - 1) {
                                p1 = points[0];
                            }
                            else {
                                p1 = points[j + 1];
                            }
                            territoryRegion.segments.Add(new Segment(p0, p1));
                        }
                    }
                    else {
                        while (segCount > pointCount) {
                            territoryRegion.segments.RemoveAt(--segCount);
                        }
                        for (int j = 0; j < pointCount; j++) {
                            Point p0 = points[j];
                            Point p1;
                            if (j == pointCount - 1) {
                                p1 = points[0];
                            }
                            else {
                                p1 = points[j + 1];
                            }
                            territoryRegion.segments[j].Init(p0, p1);
                        }
                    }
                }
            }

            // Assign cells to territory regions
            PrepareSortedTerritoriesRegions();

            // For each region, compute if it's within another region and increase its sort index
            cellUsedFlag++;
            territoriesRegionsCount = _sortedTerritoriesRegions.Count;
            for (int k = territoriesRegionsCount - 1; k >= 0; k--) { // need to be >= 0 so cells are computed for all territory regions
                Region biggerRegion = _sortedTerritoriesRegions[k];

                // Compute region cells
                Territory territory = (Territory)biggerRegion.entity;
                if (issueRedraw != RedrawType.IncrementalTerritories || territory.isDirty) {
                    ComputeTerritoryRegionCells(biggerRegion);
                    if (biggerRegion.cells.Count == 0) {
                        // remove empty region
                        territory.regions.Remove(biggerRegion);
                        territoriesRegionsCount--;
                        continue;
                    }
                }
                biggerRegion.renderingOrder = 0;
                for (int j = k - 1; j >= 0; j--) {
                    Region smallerRegion = _sortedTerritoriesRegions[j];
                    Vector2 bottomLeft = smallerRegion.rect2D.min;
                    Vector2 topRight = smallerRegion.rect2D.max;
                    Vector2 bottomRight = new Vector2(topRight.x, bottomLeft.y);
                    Vector2 topLeft = new Vector2(bottomLeft.x, topRight.y);
                    if (biggerRegion.rect2D.Contains(bottomLeft) && biggerRegion.rect2D.Contains(bottomRight) && biggerRegion.rect2D.Contains(topLeft) && biggerRegion.rect2D.Contains(topRight)) {
                        biggerRegion.renderingOrder++;
                    }
                }
            }

        }

        void PrepareSortedTerritoriesRegions () {
            _sortedTerritoriesRegions.Clear();
            foreach (Territory terr in territories) {
                _sortedTerritoriesRegions.AddRange(terr.regions);
            }
            _sortedTerritoriesRegions.Sort(delegate (Region x, Region y) {
                return x.rect2DArea.CompareTo(y.rect2DArea);
            });
        }


        void ComputeTerritoryRegionCells (Region territoryRegion) {
            Territory territory = (Territory)territoryRegion.entity;
            // Optimization: if territory only contains one region, make region.cells point to territory.cells
            if (territory.regions.Count == 1) {
                territoryRegion.cells = territory.cells;
                return;
            }
            if (territoryRegion.cells == null || territoryRegion.cells == territory.cells) {
                territoryRegion.cells = new List<Cell>();
            }
            else {
                territoryRegion.cells.Clear();
            }
            int cellsCount = territory.cells.Count;
            for (int k = 0; k < cellsCount; k++) {
                Cell cell = territory.cells[k];
                if (cell.usedFlag == cellUsedFlag) continue;
                Vector2 scaledCenter = cell.scaledCenter;
                if (territoryRegion.Contains(scaledCenter.x, scaledCenter.y)) {
                    cell.usedFlag = cellUsedFlag;
                    territoryRegion.cells.Add(cell);
                    break;
                }
            }
            List<Cell> territoryRegionCells = territoryRegion.cells;
            int j = 0;
            int territoryIndex = TerritoryGetIndex(territory);

            while (j < territoryRegionCells.Count) {
                Cell cell = territoryRegionCells[j++];
                int neighbourCount = cell.neighbours.Count;
                for (int n = 0; n < neighbourCount; n++) {
                    Cell neighbour = cell.neighbours[n];
                    if (neighbour.territoryIndex != territoryIndex || neighbour.usedFlag == cellUsedFlag) continue;
                    territoryRegionCells.Add(neighbour);
                    neighbour.usedFlag = cellUsedFlag;
                }
            }
        }

        void GenerateTerritoriesMeshData () {

            int terrCount = territories.Count;
            if (terrCount == 0) return;

            if (frontiersPoints == null || frontiersPoints.Length == 0) {
                frontiersPoints = new Vector3[10000];
            }

            TerritoryMesh tm, tmDisputed = null;

            bool updateExistingTerritoryMeshes = issueRedraw == RedrawType.IncrementalTerritories && territoryMeshes != null;
            if (updateExistingTerritoryMeshes && territoryMeshes.Count < territories.Count) updateExistingTerritoryMeshes = false;

            if (updateExistingTerritoryMeshes) {
                if (territoryFrontiers == null)
                    return;
                int territoryMeshesCount = territoryMeshes.Count;
                for (int k = 0; k < territoryMeshesCount; k++) {
                    int terrIndex = territoryMeshes[k].territoryIndex;
                    if (ValidTerritoryIndex(terrIndex)) {
                        Territory territory = territories[terrIndex];
                        if (territory.isDirty) {
                            tm = territoryMeshes[k];
                            GenerateTerritoryMesh(tm, false);
                        }
                    }
                    else {
                        tmDisputed = territoryMeshes[k]; // Reuse it for disputes
                    }
                }
            }
            else {
                if (territoryMeshes == null) {
                    territoryMeshes = new List<TerritoryMesh>(terrCount + 1);
                }
                else {
                    territoryMeshes.Clear();
                }

                if (territoryFrontiers == null)
                    return;

                for (int k = 0; k < terrCount; k++) {
                    tm = new TerritoryMesh {
                        territoryIndex = k
                    };
                    if (GenerateTerritoryMesh(tm, false)) {
                        territoryMeshes.Add(tm);
                    }
                }
            }

            // Generate disputed frontiers
            if (tmDisputed == null) {
                tmDisputed = new TerritoryMesh {
                    territoryIndex = -1
                };
                if (GenerateTerritoryMesh(tmDisputed, false)) {
                    territoryMeshes.Add(tmDisputed);
                }
            }
            else if (!GenerateTerritoryMesh(tmDisputed, false)) {
                territoryMeshes.Remove(tmDisputed);
            }
        }

        /// <summary>
        /// Generates the territory mesh.
        /// </summary>
        /// <returns>True if something was produced.</returns>
        bool GenerateTerritoryMesh (TerritoryMesh tm, bool generateCustomFrontier, int adjacentTerritoryIndex = -1) {

            frontiersPointsCount = 0;

            int territoryFrontiersCount = territoryFrontiers.Count;
            int territoriesCount = territories.Count;
            if (_terrainWrapper == null) {
                EnsureCapacityFrontiersPoints(territoryFrontiersCount * 2);
                for (int k = 0; k < territoryFrontiersCount; k++) {
                    Segment s = territoryFrontiers[k];
                    if (generateCustomFrontier) {
                        if (s.disputingTerritory1Index != tm.territoryIndex && s.disputingTerritory2Index != tm.territoryIndex) continue;
                        if (adjacentTerritoryIndex != -1 && s.disputingTerritory1Index != adjacentTerritoryIndex && s.disputingTerritory2Index != adjacentTerritoryIndex) continue;
                    }
                    else if (s.territoryIndex != tm.territoryIndex) {
                        continue;
                    }
                    if (s.territoryIndex >= 0 && s.territoryIndex < territoriesCount && !territories[s.territoryIndex].borderVisible)
                        continue;
                    if (!s.border || _showTerritoriesOuterBorder) {
                        frontiersPoints[frontiersPointsCount++] = GetScaledVector(s.start);
                        frontiersPoints[frontiersPointsCount++] = GetScaledVector(s.end);
                    }
                }
            }
            else {
                meshStep = (2.0f - _gridRoughness) / (float)MIN_VERTEX_DISTANCE;
                Transform t = transform;
                for (int k = 0; k < territoryFrontiersCount; k++) {
                    Segment s = territoryFrontiers[k];
                    if (generateCustomFrontier) {
                        if (s.disputingTerritory1Index != tm.territoryIndex && s.disputingTerritory2Index != tm.territoryIndex) continue;
                        if (adjacentTerritoryIndex != -1 && s.disputingTerritory1Index != adjacentTerritoryIndex && s.disputingTerritory2Index != adjacentTerritoryIndex) continue;
                    }
                    else if (s.territoryIndex != tm.territoryIndex) {
                        continue;
                    }
                    if (s.territoryIndex >= 0 && s.territoryIndex < territoriesCount && !territories[s.territoryIndex].borderVisible)
                        continue;
                    if (!s.border || _showTerritoriesOuterBorder) {
                        SurfaceSegmentForMesh(t, s);
                    }
                }

            }

            int maxPointsPerMesh = 65504;
            if (isUsingVertexDispForTerritoryThickness) {
                maxPointsPerMesh /= 4;
            }

            int meshGroups = (frontiersPointsCount / maxPointsPerMesh) + 1;
            int meshIndex = -1;
            if (tm.territoryMeshIndices == null || tm.territoryMeshIndices.GetUpperBound(0) != meshGroups - 1) {
                tm.territoryMeshIndices = new int[meshGroups][];
                tm.territoryMeshBorders = new Vector3[meshGroups][];
            }

            // Clamp points to minimum elevation
            if (_cellsMinimumAltitudeClampVertices && _cellsMinimumAltitude != 0) {
                float localMinima = -(_cellsMinimumAltitude - _terrainWrapper.bounds.min.y) / _terrainWrapper.transform.lossyScale.y;
                for (int k = 0; k < frontiersPointsCount; k++) {
                    if (frontiersPoints[k].z > localMinima) {
                        frontiersPoints[k].z = localMinima;
                    }
                }
            }

            // Clamp points to maximum elevation
            if (_cellsMaximumAltitudeClampVertices && _cellsMaximumAltitude != 0) {
                float localMaxima = -(_cellsMaximumAltitude - _terrainWrapper.bounds.min.y) / _terrainWrapper.transform.lossyScale.y;
                for (int k = 0; k < frontiersPointsCount; k++) {
                    if (frontiersPoints[k].z < localMaxima) {
                        frontiersPoints[k].z = localMaxima;
                    }
                }
            }


            for (int k = 0; k < frontiersPointsCount; k += maxPointsPerMesh) {
                int max = Mathf.Min(frontiersPointsCount - k, maxPointsPerMesh);
                ++meshIndex;
                if (tm.territoryMeshBorders[meshIndex] == null || tm.territoryMeshBorders[meshIndex].GetUpperBound(0) != max - 1) {
                    tm.territoryMeshBorders[meshIndex] = new Vector3[max];
                    tm.territoryMeshIndices[meshIndex] = new int[max];
                }
                for (int j = 0; j < max; j++) {
                    tm.territoryMeshBorders[meshIndex][j] = frontiersPoints[j + k];
                    tm.territoryMeshIndices[meshIndex][j] = j;
                }
            }

            return frontiersPointsCount > 0;
        }

        /// <summary>
        /// Generates the territory mesh.
        /// </summary>
        /// <returns>True if something was produced.</returns>
        bool GenerateRegionMesh (TerritoryMesh tm, Region region, float thickness) {

            frontiersPointsCount = 0;

            int segmentsCount = region.segments.Count;
            if (_terrainWrapper == null) {
                EnsureCapacityFrontiersPoints(segmentsCount * 2);
                for (int k = 0; k < segmentsCount; k++) {
                    Segment s = region.segments[k];
                    frontiersPoints[frontiersPointsCount++] = GetScaledVector(s.start);
                    frontiersPoints[frontiersPointsCount++] = GetScaledVector(s.end);
                }
            }
            else {
                Transform t = transform;
                if (region.isFlat) {
                    for (int k = 0; k < segmentsCount; k++) {
                        Segment s = region.segments[k];
                        Cell cell = cells[s.cellIndex];
                        Vector2 scaledCenter = cell.scaledCenter;
                        float height = _terrainWrapper.GetInterpolatedHeight(transform.TransformPoint(scaledCenter));
                        if (_gridNormalOffset > 0) {
                            Vector3 invNormal = transform.InverseTransformVector(_terrainWrapper.GetInterpolatedNormal(scaledCenter.x, scaledCenter.y));
                            SurfaceSegmentForFlatMesh(s, height, invNormal);
                        }
                        else {
                            SurfaceSegmentForFlatMesh(s, height);
                        }
                    }
                    // ensure all segments are connected (avoid gaps if two adjacent segments are at different altitudes)
                    int pointCount = frontiersPointsCount;
                    for (int k = 0; k < pointCount; k += 2) {
                        Vector3 prevP0 = frontiersPoints[k];
                        Vector3 prevP1 = frontiersPoints[k + 1];
                        for (int j = k + 2; j < pointCount; j += 2) {
                            Vector3 p0 = frontiersPoints[j];
                            Vector3 p1 = frontiersPoints[j + 1];
                            if (prevP1.z != p0.z && prevP1.x == p0.x && prevP1.y == p0.y) {
                                EnsureCapacityFrontiersPoints(2);
                                frontiersPoints[frontiersPointsCount++] = prevP1;
                                frontiersPoints[frontiersPointsCount++] = p0;
                            }
                            if (prevP0.z != p1.z && prevP0.x == p1.x && prevP0.y == p1.y) {
                                EnsureCapacityFrontiersPoints(2);
                                frontiersPoints[frontiersPointsCount++] = prevP0;
                                frontiersPoints[frontiersPointsCount++] = p1;
                            }
                        }
                    }
                }
                else {
                    meshStep = (2.0f - _gridRoughness) / (float)MIN_VERTEX_DISTANCE;
                    for (int k = 0; k < segmentsCount; k++) {
                        Segment s = region.segments[k];
                        SurfaceSegmentForMesh(t, s);
                    }
                }

            }

            int maxPointsPerMesh = 65504;
            bool useVertexDisplacement = thickness > 1f & !canUseGeometryShaders;
            if (useVertexDisplacement) {
                maxPointsPerMesh /= 4;
            }

            int meshGroups = (frontiersPointsCount / maxPointsPerMesh) + 1;
            int meshIndex = -1;
            if (tm.territoryMeshIndices == null || tm.territoryMeshIndices.GetUpperBound(0) != meshGroups - 1) {
                tm.territoryMeshIndices = new int[meshGroups][];
                tm.territoryMeshBorders = new Vector3[meshGroups][];
            }

            // Clamp points to minimum elevation
            if (_cellsMinimumAltitudeClampVertices && _cellsMinimumAltitude != 0) {
                float localMinima = -(_cellsMinimumAltitude - _terrainWrapper.bounds.min.y) / _terrainWrapper.transform.lossyScale.y;
                for (int k = 0; k < frontiersPointsCount; k++) {
                    if (frontiersPoints[k].z > localMinima) {
                        frontiersPoints[k].z = localMinima;
                    }
                }
            }

            // Clamp points to maximum elevation
            if (_cellsMaximumAltitudeClampVertices && _cellsMaximumAltitude != 0) {
                float localMaxima = -(_cellsMaximumAltitude - _terrainWrapper.bounds.min.y) / _terrainWrapper.transform.lossyScale.y;
                for (int k = 0; k < frontiersPointsCount; k++) {
                    if (frontiersPoints[k].z < localMaxima) {
                        frontiersPoints[k].z = localMaxima;
                    }
                }
            }


            for (int k = 0; k < frontiersPointsCount; k += maxPointsPerMesh) {
                int max = Mathf.Min(frontiersPointsCount - k, maxPointsPerMesh);
                ++meshIndex;
                if (tm.territoryMeshBorders[meshIndex] == null || tm.territoryMeshBorders[meshIndex].GetUpperBound(0) != max - 1) {
                    tm.territoryMeshBorders[meshIndex] = new Vector3[max];
                    tm.territoryMeshIndices[meshIndex] = new int[max];
                }
                for (int j = 0; j < max; j++) {
                    tm.territoryMeshBorders[meshIndex][j] = frontiersPoints[j + k];
                    tm.territoryMeshIndices[meshIndex][j] = j;
                }
            }

            return frontiersPointsCount > 0;
        }


        void EnsureCapacityFrontiersPoints (int payload) {
            while (frontiersPointsCount + payload >= frontiersPoints.Length) {
                Vector3[] v = new Vector3[frontiersPoints.Length * 2];
                for (int k = 0; k < frontiersPointsCount; k++) {
                    v[k] = frontiersPoints[k];
                }
                frontiersPoints = v;
            }
        }

        void FitToTerrain () {
            if (_terrainWrapper == null)
                return;

            _cellTextureMode = CELL_TEXTURE_MODE.InTile;

            // Fit to terrain
            Vector3 terrainSize = _terrainWrapper.size;
            terrainWidth = terrainSize.x;
            if (terrainWidth == 0) return;
            terrainHeight = terrainSize.y;
            terrainDepth = terrainSize.z;
            Vector3 localEulerAngles = transform.localEulerAngles;
            localEulerAngles.x = 90;
            transform.eulerAngles = Misc.Vector3zero;
            Vector3 lossyScale = _terrainObject.transform.lossyScale;
            transform.localScale = new Vector3(terrainWidth / lossyScale.x, terrainDepth / lossyScale.z, 1);
            transform.localRotation = Quaternion.Euler(localEulerAngles);

            maxRoughnessWorldSpace = _gridRoughness * terrainHeight;

            if (_terrainWrapper is MeshTerrainWrapper) {
                ((MeshTerrainWrapper)_terrainWrapper).pivot = _terrainMeshPivot;
            }

            if (transform.parent != null && transform.parent.lossyScale != lastParentScale) {
                gridNeedsUpdate = true;
            }
            else if (transform.rotation != lastRot) {
                gridNeedsUpdate = true;
                issueRedraw = RedrawType.Full;
            }
            else if (transform.position != lastPos || transform.lossyScale != lastScale) {
                gridNeedsUpdate = true;
            }
            Vector3 camPos = cameraMain != null ? cameraMain.transform.position : Misc.Vector3zero;
            bool refresh = gridNeedsUpdate || gridElevationCurrent != lastGridElevation || _gridCameraOffset != lastGridCameraOffset || _gridMinElevationMultiplier != lastGridMinElevationMultiplier || camPos != lastCamPos;
            if (refresh) {
                if (gridNeedsUpdate) {
                    gridNeedsUpdate = false;
                    _terrainWrapper.Refresh();
                }
                Vector3 localPosition = _terrainWrapper.localCenter;
                localPosition.y += 0.01f * _gridMinElevationMultiplier + gridElevationCurrent;
                if (cameraMain != null) {
                    if (_gridCameraOffset > 0) {
                        localPosition += (camPos - transform.position).normalized * (camPos - _terrainWrapper.bounds.center).sqrMagnitude * _gridCameraOffset * 0.001f;
                    }
                }
                transform.localPosition = transform.parent != null ? localPosition : _terrainWrapper.transform.TransformPoint(localPosition);
                lastCamPos = camPos;
                lastPos = transform.position;
                lastRot = transform.rotation;
                lastScale = transform.lossyScale;
                if (transform.parent != null) {
                    lastParentScale = transform.parent.lossyScale;
                }
                lastGridElevation = gridElevationCurrent;
                lastGridCameraOffset = _gridCameraOffset;
                lastGridMinElevationMultiplier = _gridMinElevationMultiplier;
            }
        }


        void BuildTerrainWrapper () {
            // migration
            if (_terrain != null && _terrainObject == null) {
                _terrainObject = _terrain.gameObject;
                _terrain = null;
            }
            if (_terrainObject == null) {
                _terrainWrapper = null;
            }
            else if ((_terrainWrapper == null && _terrainObject != null) || (_terrainWrapper != null && (_terrainWrapper.gameObject != _terrainObject || _terrainWrapper.heightmapWidth != _heightmapWidth || _terrainWrapper.heightmapHeight != _heightmapHeight))) {
                _terrainWrapper = TerrainWrapperProvider.GetTerrainWrapper(_terrainObject, _terrainObjectsPrefix, _terrainObjectsLayerMask, _terrainObjectsSearchGlobal, _heightmapWidth, _heightmapHeight, _multiTerrain);
            }
        }

        void UpdateTerrainReference (bool reuseTerrainData) {

            if (!TryGetComponent<MeshRenderer>(out MeshRenderer renderer)) return;
            renderer.sortingOrder = _sortingOrder;
            if (_terrainWrapper == null) {
                if (renderer.enabled && (_transparentBackground || _disableMeshGeneration)) {
                    renderer.enabled = false;
                }
                else if (!renderer.enabled && !_transparentBackground) {
                    renderer.enabled = true;
                }

                if (transform.parent != null && transform.GetComponentInParent<Terrain>() != null) {
                    transform.SetParent(null);
                    transform.localScale = new Vector3(100, 100, 1);
                    transform.localRotation = Quaternion.Euler(0, 0, 0);
                }
                // Check if box collider exists
                if (_highlightMode != HighlightMode.None) {
                    if (!TryGetComponent<BoxCollider2D>(out _)) {
                        if (!TryGetComponent<MeshCollider>(out _)) {
                            gameObject.AddComponent<MeshCollider>();
                        }
                    }
                }
            }
            else {
                if (!_multiTerrain) {
                    transform.SetParent(_terrainWrapper.transform, false);
                }
                if (renderer.enabled) {
                    renderer.enabled = false;
                }
                if (Application.isPlaying) {
                    _terrainWrapper.SetupTriggers(this);
                }
                if (TryGetComponent<MeshCollider>(out MeshCollider mc)) {
                    DestroyImmediate(mc);
                }
                Camera cam = cameraMain;
                if (cam != null) {
                    lastCamPos = cameraMain.transform.position - Vector3.up; // just to force update on first frame
                    FitToTerrain();
                    lastCamPos = cameraMain.transform.position - Vector3.up; // just to force update on first update as well
                }
                if (CalculateTerrainRoughness(reuseTerrainData)) {
                    refreshCellMesh = true;
                    refreshTerritoriesMesh = true;
                    // Clear geometry
                    DestroyCellLayer();
                    DestroyTerritoryLayer();
                }
            }
        }

        /// <summary>
        /// Calculates the terrain roughness.
        /// </summary>
        /// <returns><c>true</c>, if terrain roughness has changed, <c>false</c> otherwise.</returns>
        bool CalculateTerrainRoughness (bool reuseTerrainData) {
            if (reuseTerrainData && _terrainWrapper.heightmapWidth == heightMapWidth && _terrainWrapper.heightmapHeight == heightMapHeight && terrainHeights != null && terrainRoughnessMap != null) {
                return false;
            }
            _terrainWrapper.Refresh();
            heightMapWidth = _terrainWrapper.heightmapWidth;
            heightMapHeight = _terrainWrapper.heightmapHeight;
            terrainHeights = _terrainWrapper.GetHeights(0, 0, heightMapWidth, heightMapHeight);
            terrainRoughnessMapWidth = heightMapWidth / TERRAIN_CHUNK_SIZE;
            terrainRoughnessMapHeight = heightMapHeight / TERRAIN_CHUNK_SIZE;
            int length = terrainRoughnessMapHeight * terrainRoughnessMapWidth;
            if (terrainRoughnessMap == null || terrainRoughnessMap.Length != length) {
                terrainRoughnessMap = new float[length];
                tempTerrainRoughnessMap = new float[length];
            }
            else {
                for (int k = 0; k < length; k++) {
                    terrainRoughnessMap[k] = 0;
                    tempTerrainRoughnessMap[k] = 0;
                }
            }

#if SHOW_DEBUG_GIZMOS
			if (GameObject.Find ("ParentDot")!=null) DestroyImmediate(GameObject.Find ("ParentDot"));
			GameObject parentDot = new GameObject("ParentDot");
            disposalManager.MarkForDisposal(parentDot);
			parentDot.transform.position = Misc.Vector3zero;
#endif

            float maxStep = (float)TERRAIN_CHUNK_SIZE / heightMapWidth;
            float minStep = 1.0f / heightMapWidth;

            float roughnessFactor = _gridRoughness * 5.0f + 0.00001f;
            for (int y = 0, l = 0; l < terrainRoughnessMapHeight; y += TERRAIN_CHUNK_SIZE, l++) {
                int linePos = l * terrainRoughnessMapWidth;
                for (int x = 0, c = 0; c < terrainRoughnessMapWidth; x += TERRAIN_CHUNK_SIZE, c++) {
                    int j0 = y == 0 ? 1 : y;
                    int j1 = y + TERRAIN_CHUNK_SIZE;
                    int k0 = x == 0 ? 1 : x;
                    int k1 = x + TERRAIN_CHUNK_SIZE;
                    float maxDiff = 0;
                    for (int j = j0; j < j1; j++) {
                        for (int k = k0; k < k1; k++) {
                            float mh = terrainHeights[j, k];
                            float diff = mh - terrainHeights[j, k - 1];
                            if (diff > maxDiff) {
                                maxDiff = diff;
                            }
                            else if (-diff > maxDiff) {
                                maxDiff = -diff;
                            }
                            diff = mh - terrainHeights[j + 1, k - 1];
                            if (diff > maxDiff) {
                                maxDiff = diff;
                            }
                            else if (-diff > maxDiff) {
                                maxDiff = -diff;
                            }
                            diff = mh - terrainHeights[j + 1, k];
                            if (diff > maxDiff) {
                                maxDiff = diff;
                            }
                            else if (-diff > maxDiff) {
                                maxDiff = -diff;
                            }
                            diff = mh - terrainHeights[j + 1, k + 1];
                            if (diff > maxDiff) {
                                maxDiff = diff;
                            }
                            else if (-diff > maxDiff) {
                                maxDiff = -diff;
                            }
                            diff = mh - terrainHeights[j, k + 1];
                            if (diff > maxDiff) {
                                maxDiff = diff;
                            }
                            else if (-diff > maxDiff) {
                                maxDiff = -diff;
                            }
                            diff = mh - terrainHeights[j - 1, k + 1];
                            if (diff > maxDiff) {
                                maxDiff = diff;
                            }
                            else if (-diff > maxDiff) {
                                maxDiff = -diff;
                            }
                            diff = mh - terrainHeights[j - 1, k];
                            if (diff > maxDiff) {
                                maxDiff = diff;
                            }
                            else if (-diff > maxDiff) {
                                maxDiff = -diff;
                            }
                            diff = mh - terrainHeights[j - 1, k - 1];
                            if (diff > maxDiff) {
                                maxDiff = diff;
                            }
                            else if (-diff > maxDiff) {
                                maxDiff = -diff;
                            }
                        }
                    }
                    maxDiff /= roughnessFactor;
                    float t = (1.0f - maxDiff) / (1.0f + maxDiff);
                    if (t <= 0)
                        maxDiff = minStep;
                    else if (t >= 1f)
                        maxDiff = maxStep;
                    else
                        maxDiff = minStep * (1f - t) + maxStep * t;

                    tempTerrainRoughnessMap[linePos + c] = maxDiff;
                }
            }

            // collapse chunks with low gradient
            float flatThreshold = maxStep * (1.0f - _gridRoughness * 0.1f);
            for (int j = 0; j < terrainRoughnessMapHeight; j++) {
                int jPos = j * terrainRoughnessMapWidth;
                for (int k = 0; k < terrainRoughnessMapWidth - 1; k++) {
                    if (tempTerrainRoughnessMap[jPos + k] >= flatThreshold) {
                        int i = k + 1;
                        while (i < terrainRoughnessMapWidth && tempTerrainRoughnessMap[jPos + i] >= flatThreshold)
                            i++;
                        while (k < i && k < terrainRoughnessMapWidth)
                            tempTerrainRoughnessMap[jPos + k] = maxStep * (i - k++);
                    }
                }
            }

            // spread min step
            for (int l = 0; l < terrainRoughnessMapHeight; l++) {
                int linePos = l * terrainRoughnessMapWidth;
                int prevLinePos = linePos - terrainRoughnessMapWidth;
                int postLinePos = linePos + terrainRoughnessMapWidth;
                for (int c = 0; c < terrainRoughnessMapWidth; c++) {
                    minStep = tempTerrainRoughnessMap[linePos + c];
                    if (l > 0) {
                        if (tempTerrainRoughnessMap[prevLinePos + c] < minStep)
                            minStep = tempTerrainRoughnessMap[prevLinePos + c];
                        if (c > 0)
                            if (tempTerrainRoughnessMap[prevLinePos + c - 1] < minStep)
                                minStep = tempTerrainRoughnessMap[prevLinePos + c - 1];
                        if (c < terrainRoughnessMapWidth - 1)
                            if (tempTerrainRoughnessMap[prevLinePos + c + 1] < minStep)
                                minStep = tempTerrainRoughnessMap[prevLinePos + c + 1];
                    }
                    if (c > 0 && tempTerrainRoughnessMap[linePos + c - 1] < minStep)
                        minStep = tempTerrainRoughnessMap[linePos + c - 1];
                    if (c < terrainRoughnessMapWidth - 1 && tempTerrainRoughnessMap[linePos + c + 1] < minStep)
                        minStep = tempTerrainRoughnessMap[linePos + c + 1];
                    if (l < terrainRoughnessMapHeight - 1) {
                        if (tempTerrainRoughnessMap[postLinePos + c] < minStep)
                            minStep = tempTerrainRoughnessMap[postLinePos + c];
                        if (c > 0)
                            if (tempTerrainRoughnessMap[postLinePos + c - 1] < minStep)
                                minStep = tempTerrainRoughnessMap[postLinePos + c - 1];
                        if (c < terrainRoughnessMapWidth - 1)
                            if (tempTerrainRoughnessMap[postLinePos + c + 1] < minStep)
                                minStep = tempTerrainRoughnessMap[postLinePos + c + 1];
                    }
                    terrainRoughnessMap[linePos + c] = minStep;
                }
            }

#if SHOW_DEBUG_GIZMOS
            for (int l = 0; l < terrainRoughnessMapHeight - 1; l++) {
                for (int c = 0; c < terrainRoughnessMapWidth - 1; c++) {
                    float r = terrainRoughnessMap[l * terrainRoughnessMapWidth + c];
                    GameObject marker = Instantiate(Resources.Load<GameObject>("Prefabs/Dot"));
                    marker.transform.SetParent(parentDot.transform, false);
                    disposalManager.MarkForDisposal(marker);
                    marker.transform.localPosition = new Vector3(-terrainCenter.x + terrainWidth * ((float)c / 64 + 0.5f / 64), r * terrainHeight, -terrainCenter.z + terrainDepth * ((float)l / 64 + 0.5f / 64));
                    marker.transform.localScale = Misc.Vector3one * 350 / 512.0f;

                }
            }
#endif

            return true;
        }

        void UpdateMaterialHighlightBorder (Material mat) {
            if (mat == null) return;
            if (_gridTopology != GridTopology.Box) {
                mat.SetFloat(ShaderParams.HighlightBorderSize, 100f);
                return;
            }
            mat.SetFloat(ShaderParams.HighlightBorderSize, 0.5f - _cellHighlightBorderSize);
            mat.SetColor(ShaderParams.HighlightBorderColor, _cellHighlightBorderColor);
        }


        void UpdateMaterialDepthOffset () {

            float gridDepthOffset = _gridMeshDepthOffset / 10000.0f;

            if (territories != null) {
                int territoriesCount = territories.Count;
                for (int c = 0; c < territoriesCount; c++) {
                    Territory terr = territories[c];
                    int regionsCount = terr.regions.Count;
                    for (int r = 0; r < regionsCount; r++) {
                        Region region = terr.regions[r];
                        if (region.renderer != null) {
                            Material mat = region.renderer.sharedMaterial;
                            mat.SetInt(ShaderParams.Offset, _gridSurfaceDepthOffsetTerritory);
                            SetBlend(mat);
                        }
                        if (region.interiorBorderGameObject != null) {
                            SetDepthOffset(region.interiorBorderGameObject, gridDepthOffset);
                        }
                    }
                    if (terr.customFrontiersGameObject != null) {
                        SetDepthOffset(terr.customFrontiersGameObject, gridDepthOffset);
                    }
                }
            }

            if (cells != null) {
                int cellsCount = cells.Count;
                for (int c = 0; c < cellsCount; c++) {
                    Cell cell = cells[c];
                    if (cell != null && cell.region != null) {
                        if (cell.region.customBorderGameObject != null) {
                            SetDepthOffset(cell.region.customBorderGameObject, gridDepthOffset);
                        }
                        if (cell.region.renderer != null) {
                            Material mat = cell.region.renderer.sharedMaterial;
                            mat.SetInt(ShaderParams.Offset, _gridSurfaceDepthOffset);
                            SetBlend(mat);
                            if (_cellTextureMode == CELL_TEXTURE_MODE.Floating) {
                                mat.SetInt(ShaderParams.ZWrite, 1); // need to force depth testing to ensure lower tiles are drawn on top of upper tiles
                            }
                        }
                    }
                }
            }

            cellsThinMat.SetFloat(ShaderParams.Offset, gridDepthOffset);
            SetBlend(cellsThinMat);

            cellsGeoMat.SetFloat(ShaderParams.Offset, gridDepthOffset);
            SetBlend(cellsGeoMat);

            territoriesThinMat.SetFloat(ShaderParams.Offset, gridDepthOffset);
            SetBlend(territoriesThinMat);

            territoriesGeoMat.SetFloat(ShaderParams.Offset, gridDepthOffset);
            SetBlend(territoriesGeoMat);

            territoriesThickHackMat.SetFloat(ShaderParams.Offset, gridDepthOffset);
            SetBlend(territoriesThickHackMat);

            territoriesDisputedThinMat.SetFloat(ShaderParams.Offset, gridDepthOffset);
            SetBlend(territoriesDisputedThinMat);

            territoriesDisputedGeoMat.SetFloat(ShaderParams.Offset, gridDepthOffset);
            SetBlend(territoriesDisputedGeoMat);

            territoriesGradientMat.SetFloat(ShaderParams.Offset, gridDepthOffset);
            SetBlend(territoriesGradientMat);

            foreach (Material mat in frontierColorCache.Values) {
                mat.SetFloat(ShaderParams.Offset, gridDepthOffset);
                SetBlend(mat);
            }
            hudMatCellOverlay.SetInt(ShaderParams.Offset, _gridSurfaceDepthOffset);
            hudMatCellGround.SetInt(ShaderParams.Offset, _gridSurfaceDepthOffset - 1);
            hudMatTerritoryOverlay.SetInt(ShaderParams.Offset, _gridSurfaceDepthOffsetTerritory);
            hudMatTerritoryGround.SetInt(ShaderParams.Offset, _gridSurfaceDepthOffsetTerritory - 1);
            fadeMat.SetInt(ShaderParams.Offset, Mathf.Min(_gridSurfaceDepthOffset, _gridSurfaceDepthOffsetTerritory) - 1);
        }

        void SetDepthOffset (GameObject go, float depthOffset) {
            Renderer r = go.GetComponentInChildren<Renderer>();
            if (r == null) return;
            Material mat = r.sharedMaterial;
            if (mat == null) return;
            mat.SetFloat(ShaderParams.Offset, depthOffset);
        }

        void SetBlend (Material mat) {
            SetBlend(mat, _transparentBackground);
        }

        void SetBlend (Material mat, bool transparent) {
            if (mat == null) return;
            if (transparent) {
                mat.SetInt(ShaderParams.ZWrite, 0);
                mat.SetInt(ShaderParams.SrcBlend, (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt(ShaderParams.DstBlend, (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                if (mat.renderQueue < 3000) {
                    mat.renderQueue += 1000;
                }
            }
            else {
                mat.SetInt(ShaderParams.ZWrite, 0);
                mat.SetInt(ShaderParams.SrcBlend, (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt(ShaderParams.DstBlend, (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                if (mat.renderQueue >= 3000) {
                    mat.renderQueue -= 1000;
                }
            }
        }

        void SetBlendHighlight (Material mat) {
            if (mat == null) return;
            if (_transparentBackground) {
                mat.SetInt(ShaderParams.ZWrite, 0);
                mat.SetInt(ShaderParams.SrcBlend, (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt(ShaderParams.DstBlend, (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                if (mat.renderQueue < 3000) {
                    mat.renderQueue += 1000;
                }
            }
            else {
                mat.SetInt(ShaderParams.ZWrite, 0);
                mat.SetInt(ShaderParams.SrcBlend, (int)UnityEngine.Rendering.BlendMode.One);
                mat.SetInt(ShaderParams.DstBlend, (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                if (mat.renderQueue >= 3000) {
                    mat.renderQueue -= 1000;
                }
            }
        }

        void SetMaskHighlight (Material mat) {
            if (mat == null) return;
            mat.SetTexture(ShaderParams.MaskTex, _highlightMask);
        }

        void SetOverlayMode (Material mat, bool overlayMode) {
            if (overlayMode) {
                mat.SetInt(ShaderParams.Cull, (int)UnityEngine.Rendering.CullMode.Off);
                mat.SetInt(ShaderParams.ZTest, (int)UnityEngine.Rendering.CompareFunction.Always);
                return;
            }
            mat.SetInt(ShaderParams.Cull, (int)UnityEngine.Rendering.CullMode.Back);
            mat.SetInt(ShaderParams.ZTest, (int)UnityEngine.Rendering.CompareFunction.LessEqual);
        }

        void UpdatePreventOverdrawSettings () {
            // Update surfaces material
            SetStencil(hudMatTerritoryGround, STENCIL_MASK_HUD);
            SetStencil(hudMatCellGround, STENCIL_MASK_HUD);
            SetStencil(coloredMatGroundCell, STENCIL_MASK_CELL);
            SetStencil(coloredMatOverlayCell, STENCIL_MASK_CELL);
            SetStencil(texturizedMatGroundCell, STENCIL_MASK_CELL);
            SetStencil(texturizedMatOverlayCell, STENCIL_MASK_CELL);
            SetStencil(coloredMatGroundTerritory, STENCIL_MASK_TERRITORY);
            SetStencil(texturizedMatGroundTerritory, STENCIL_MASK_TERRITORY);
            SetStencil(coloredMatOverlayTerritory, STENCIL_MASK_TERRITORY);
            SetStencil(texturizedMatOverlayTerritory, STENCIL_MASK_TERRITORY);

            if (territories != null) {
                int territoriesCount = territories.Count;
                for (int c = 0; c < territoriesCount; c++) {
                    Territory terr = territories[c];
                    for (int r = 0; r < terr.regions.Count; r++) {
                        int cacheIndex = GetCacheIndexForTerritoryRegion(c, r);
                        if (surfaces.TryGetValue(cacheIndex, out GameObject surf)) {
                            if (surf != null && surf.TryGetComponent<Renderer>(out Renderer renderer)) {
                                Material mat = renderer.sharedMaterial;
                                SetStencil(mat, STENCIL_MASK_TERRITORY);
                            }
                        }
                    }
                }
            }
            if (cells != null) {
                int cellsCount = cells.Count;
                for (int c = 0; c < cellsCount; c++) {
                    int cacheIndex = GetCacheIndexForCellRegion(c);
                    GameObject surf;
                    if (surfaces.TryGetValue(cacheIndex, out surf)) {
                        if (surf != null && surf.TryGetComponent<Renderer>(out Renderer renderer)) {
                            Material mat = renderer.sharedMaterial;
                            SetStencil(mat, STENCIL_MASK_CELL);
                            SetAlphaClipping(mat);
                        }
                    }
                }
            }

            // Update mesh materials
            SetStencil(cellsThinMat);
            SetStencil(cellsGeoMat);
            SetStencil(territoriesThinMat);
            SetStencil(territoriesGeoMat);
            SetStencil(territoriesDisputedThinMat);
            SetStencil(territoriesThickHackMat);
        }

        void SetStencil (Material mat) {
            SetStencil(mat, _useStencilBuffer);
        }

        void SetStencil (Material mat, bool useStencil) {
            if (mat == null) return;
            if (useStencil) {
                mat.SetInt(ShaderParams.StencilComp, (int)UnityEngine.Rendering.CompareFunction.NotEqual);
                mat.SetInt(ShaderParams.StencilOp, (int)UnityEngine.Rendering.StencilOp.Replace);
            }
            else {
                mat.SetInt(ShaderParams.StencilComp, (int)UnityEngine.Rendering.CompareFunction.Always);
                mat.SetInt(ShaderParams.StencilOp, (int)UnityEngine.Rendering.StencilOp.Keep);
            }
        }

        void SetStencil (Material mat, int stencilMask) {
            if (mat == null) return;
            if (_useStencilBuffer) {
                mat.SetInt(ShaderParams.StencilRef, stencilMask);
                mat.SetInt(ShaderParams.StencilComp, (int)UnityEngine.Rendering.CompareFunction.NotEqual);
                mat.SetInt(ShaderParams.StencilOp, (int)UnityEngine.Rendering.StencilOp.Replace);
            }
            else {
                mat.SetInt(ShaderParams.StencilRef, 0);
                mat.SetInt(ShaderParams.StencilComp, (int)UnityEngine.Rendering.CompareFunction.Always);
                mat.SetInt(ShaderParams.StencilOp, (int)UnityEngine.Rendering.StencilOp.Keep);
            }
        }

        void SetAlphaClipping (Material mat) {
            if (_cellAlphaClipping && _cellAlphaTestThreshold > 0) {
                mat.EnableKeyword(ShaderParams.SKW_ALPHA_CLIPPING);
                mat.SetFloat(ShaderParams.AlphaTestThreshold, _cellAlphaTestThreshold);
            }
            else {
                mat.DisableKeyword(ShaderParams.SKW_ALPHA_CLIPPING);
            }
        }

        void UpdateMaterialNearClipFade () {
            if (_nearClipFadeEnabled && _terrainWrapper != null) {
                cellsThinMat.EnableKeyword(ShaderParams.SKW_NEAR_CLIP_FADE);
                cellsThinMat.SetFloat(ShaderParams.NearClip, _nearClipFade);
                cellsThinMat.SetFloat(ShaderParams.FallOff, _nearClipFadeFallOff);

                cellsGeoMat.EnableKeyword(ShaderParams.SKW_NEAR_CLIP_FADE);
                cellsGeoMat.SetFloat(ShaderParams.NearClip, _nearClipFade);
                cellsGeoMat.SetFloat(ShaderParams.FallOff, _nearClipFadeFallOff);

                territoriesThinMat.EnableKeyword(ShaderParams.SKW_NEAR_CLIP_FADE);
                territoriesThinMat.SetFloat(ShaderParams.NearClip, _nearClipFade);
                territoriesThinMat.SetFloat(ShaderParams.FallOff, _nearClipFadeFallOff);

                territoriesGeoMat.EnableKeyword(ShaderParams.SKW_NEAR_CLIP_FADE);
                territoriesGeoMat.SetFloat(ShaderParams.NearClip, _nearClipFade);
                territoriesGeoMat.SetFloat(ShaderParams.FallOff, _nearClipFadeFallOff);

                territoriesThickHackMat.EnableKeyword(ShaderParams.SKW_NEAR_CLIP_FADE);
                territoriesThickHackMat.SetFloat(ShaderParams.NearClip, _nearClipFade);
                territoriesThickHackMat.SetFloat(ShaderParams.FallOff, _nearClipFadeFallOff);
                territoriesDisputedThinMat.EnableKeyword(ShaderParams.SKW_NEAR_CLIP_FADE);
                territoriesDisputedThinMat.SetFloat(ShaderParams.NearClip, _nearClipFade);
                territoriesDisputedThinMat.SetFloat(ShaderParams.FallOff, _nearClipFadeFallOff);

                territoriesDisputedGeoMat.EnableKeyword(ShaderParams.SKW_NEAR_CLIP_FADE);
                territoriesDisputedGeoMat.SetFloat(ShaderParams.NearClip, _nearClipFade);
                territoriesDisputedGeoMat.SetFloat(ShaderParams.FallOff, _nearClipFadeFallOff);

                fadeMat.EnableKeyword(ShaderParams.SKW_NEAR_CLIP_FADE);
                fadeMat.SetFloat(ShaderParams.NearClip, _nearClipFade);
                fadeMat.SetFloat(ShaderParams.FallOff, _nearClipFadeFallOff);

                foreach (Material mat in frontierColorCache.Values) {
                    mat.EnableKeyword(ShaderParams.SKW_NEAR_CLIP_FADE);
                    mat.SetFloat(ShaderParams.NearClip, _nearClipFade);
                    mat.SetFloat(ShaderParams.FallOff, _nearClipFadeFallOff);
                }
            }
            else {
                cellsThinMat.DisableKeyword(ShaderParams.SKW_NEAR_CLIP_FADE);
                cellsGeoMat.DisableKeyword(ShaderParams.SKW_NEAR_CLIP_FADE);
                territoriesThinMat.DisableKeyword(ShaderParams.SKW_NEAR_CLIP_FADE);
                territoriesGeoMat.DisableKeyword(ShaderParams.SKW_NEAR_CLIP_FADE);
                territoriesThickHackMat.DisableKeyword(ShaderParams.SKW_NEAR_CLIP_FADE);
                territoriesDisputedThinMat.DisableKeyword(ShaderParams.SKW_NEAR_CLIP_FADE);
                territoriesDisputedGeoMat.DisableKeyword(ShaderParams.SKW_NEAR_CLIP_FADE);
                fadeMat.DisableKeyword(ShaderParams.SKW_NEAR_CLIP_FADE);
                foreach (Material mat in frontierColorCache.Values) {
                    mat.DisableKeyword(ShaderParams.SKW_NEAR_CLIP_FADE);
                }
            }
        }

        void UpdateMaterialFarFade () {
            if (_farFadeEnabled && _terrainWrapper != null) {
                cellsThinMat.EnableKeyword(ShaderParams.SKW_FAR_FADE);
                cellsThinMat.SetFloat(ShaderParams.FarFadeDistance, _farFadeDistance);
                cellsThinMat.SetFloat(ShaderParams.FarFadeFallOff, _farFadeFallOff);

                cellsGeoMat.EnableKeyword(ShaderParams.SKW_FAR_FADE);
                cellsGeoMat.SetFloat(ShaderParams.FarFadeDistance, _farFadeDistance);
                cellsGeoMat.SetFloat(ShaderParams.FarFadeFallOff, _farFadeFallOff);

                territoriesThinMat.EnableKeyword(ShaderParams.SKW_FAR_FADE);
                territoriesThinMat.SetFloat(ShaderParams.FarFadeDistance, _farFadeDistance);
                territoriesThinMat.SetFloat(ShaderParams.FarFadeFallOff, _farFadeFallOff);

                territoriesGeoMat.EnableKeyword(ShaderParams.SKW_FAR_FADE);
                territoriesGeoMat.SetFloat(ShaderParams.FarFadeDistance, _farFadeDistance);
                territoriesGeoMat.SetFloat(ShaderParams.FarFadeFallOff, _farFadeFallOff);

                territoriesThickHackMat.EnableKeyword(ShaderParams.SKW_FAR_FADE);
                territoriesThickHackMat.SetFloat(ShaderParams.FarFadeDistance, _farFadeDistance);
                territoriesThickHackMat.SetFloat(ShaderParams.FarFadeFallOff, _farFadeFallOff);

                territoriesDisputedThinMat.EnableKeyword(ShaderParams.SKW_FAR_FADE);
                territoriesDisputedThinMat.SetFloat(ShaderParams.FarFadeDistance, _farFadeDistance);
                territoriesDisputedThinMat.SetFloat(ShaderParams.FarFadeFallOff, _farFadeFallOff);

                territoriesDisputedGeoMat.EnableKeyword(ShaderParams.SKW_FAR_FADE);
                territoriesDisputedGeoMat.SetFloat(ShaderParams.FarFadeDistance, _farFadeDistance);
                territoriesDisputedGeoMat.SetFloat(ShaderParams.FarFadeFallOff, _farFadeFallOff);

                fadeMat.EnableKeyword(ShaderParams.SKW_FAR_FADE);
                fadeMat.SetFloat(ShaderParams.FarFadeDistance, _farFadeDistance);
                fadeMat.SetFloat(ShaderParams.FarFadeFallOff, _farFadeFallOff);

                foreach (Material mat in frontierColorCache.Values) {
                    mat.EnableKeyword(ShaderParams.SKW_FAR_FADE);
                    mat.SetFloat(ShaderParams.FarFadeDistance, _farFadeDistance);
                    mat.SetFloat(ShaderParams.FarFadeFallOff, _farFadeFallOff);
                }
            }
            else {
                cellsThinMat.DisableKeyword(ShaderParams.SKW_FAR_FADE);
                cellsGeoMat.DisableKeyword(ShaderParams.SKW_FAR_FADE);
                territoriesThinMat.DisableKeyword(ShaderParams.SKW_FAR_FADE);
                territoriesGeoMat.DisableKeyword(ShaderParams.SKW_FAR_FADE);
                territoriesThickHackMat.DisableKeyword(ShaderParams.SKW_FAR_FADE);
                territoriesDisputedThinMat.DisableKeyword(ShaderParams.SKW_FAR_FADE);
                territoriesDisputedGeoMat.DisableKeyword(ShaderParams.SKW_FAR_FADE);
                fadeMat.DisableKeyword(ShaderParams.SKW_FAR_FADE);
                foreach (Material mat in frontierColorCache.Values) {
                    mat.DisableKeyword(ShaderParams.SKW_FAR_FADE);
                }
            }
        }


        void UpdateMaterialCircularFade () {
            if (_circularFadeEnabled && _circularFadeTarget != null) {
                float circularFadeDistanceSqr = _circularFadeDistance * _circularFadeDistance;
                float circularFadeFallOff = _circularFadeFallOff * _circularFadeFallOff;

                cellsThinMat.EnableKeyword(ShaderParams.SKW_CIRCULAR_FADE);
                cellsThinMat.SetFloat(ShaderParams.CircularFadeDistanceSqr, circularFadeDistanceSqr);
                cellsThinMat.SetFloat(ShaderParams.CircularFadeFallOff, circularFadeFallOff);

                cellsGeoMat.EnableKeyword(ShaderParams.SKW_CIRCULAR_FADE);
                cellsGeoMat.SetFloat(ShaderParams.CircularFadeDistanceSqr, circularFadeDistanceSqr);
                cellsGeoMat.SetFloat(ShaderParams.CircularFadeFallOff, circularFadeFallOff);

                territoriesThinMat.EnableKeyword(ShaderParams.SKW_CIRCULAR_FADE);
                territoriesThinMat.SetFloat(ShaderParams.CircularFadeDistanceSqr, circularFadeDistanceSqr);
                territoriesThinMat.SetFloat(ShaderParams.CircularFadeFallOff, circularFadeFallOff);

                territoriesGeoMat.EnableKeyword(ShaderParams.SKW_CIRCULAR_FADE);
                territoriesGeoMat.SetFloat(ShaderParams.CircularFadeDistanceSqr, circularFadeDistanceSqr);
                territoriesGeoMat.SetFloat(ShaderParams.CircularFadeFallOff, circularFadeFallOff);

                territoriesThickHackMat.EnableKeyword(ShaderParams.SKW_CIRCULAR_FADE);
                territoriesThickHackMat.SetFloat(ShaderParams.CircularFadeDistanceSqr, circularFadeDistanceSqr);
                territoriesThickHackMat.SetFloat(ShaderParams.CircularFadeFallOff, circularFadeFallOff);

                territoriesDisputedThinMat.EnableKeyword(ShaderParams.SKW_CIRCULAR_FADE);
                territoriesDisputedThinMat.SetFloat(ShaderParams.CircularFadeDistanceSqr, circularFadeDistanceSqr);
                territoriesDisputedThinMat.SetFloat(ShaderParams.CircularFadeFallOff, circularFadeFallOff);

                territoriesDisputedGeoMat.EnableKeyword(ShaderParams.SKW_CIRCULAR_FADE);
                territoriesDisputedGeoMat.SetFloat(ShaderParams.CircularFadeDistanceSqr, circularFadeDistanceSqr);
                territoriesDisputedGeoMat.SetFloat(ShaderParams.CircularFadeFallOff, circularFadeFallOff);

                fadeMat.EnableKeyword(ShaderParams.SKW_CIRCULAR_FADE);
                fadeMat.SetFloat(ShaderParams.CircularFadeDistanceSqr, circularFadeDistanceSqr);
                fadeMat.SetFloat(ShaderParams.CircularFadeFallOff, circularFadeFallOff);

                foreach (Material mat in frontierColorCache.Values) {
                    mat.EnableKeyword(ShaderParams.SKW_CIRCULAR_FADE);
                    mat.SetFloat(ShaderParams.CircularFadeDistanceSqr, circularFadeDistanceSqr);
                    mat.SetFloat(ShaderParams.CircularFadeFallOff, circularFadeFallOff);
                }
            }
            else {
                cellsThinMat.DisableKeyword(ShaderParams.SKW_CIRCULAR_FADE);
                cellsGeoMat.DisableKeyword(ShaderParams.SKW_CIRCULAR_FADE);
                territoriesThinMat.DisableKeyword(ShaderParams.SKW_CIRCULAR_FADE);
                territoriesGeoMat.DisableKeyword(ShaderParams.SKW_CIRCULAR_FADE);
                territoriesThickHackMat.DisableKeyword(ShaderParams.SKW_CIRCULAR_FADE);
                territoriesDisputedThinMat.DisableKeyword(ShaderParams.SKW_CIRCULAR_FADE);
                territoriesDisputedGeoMat.DisableKeyword(ShaderParams.SKW_CIRCULAR_FADE);
                fadeMat.DisableKeyword(ShaderParams.SKW_CIRCULAR_FADE);
                foreach (Material mat in frontierColorCache.Values) {
                    mat.DisableKeyword(ShaderParams.SKW_CIRCULAR_FADE);
                }
            }
        }

        void UpdateMaterialThickness () {
            UpdateMaterialTerritoryThickness(territoriesGeoMat, _territoryFrontiersThickness);
            UpdateMaterialTerritoryThickness(territoriesDisputedGeoMat, _territoryFrontiersThickness);

            if (cellsGeoMat != null) {
                float thick = _cellBorderThickness * transform.lossyScale.x / 5000f;
                cellsGeoMat.SetFloat(ShaderParams.Thickness, thick);
            }
        }

        void UpdateMaterialTerritoryThickness (Material material, float thickness) {
            if (material == null) return;
            float thick = thickness * transform.lossyScale.x / 5000f;
            material.SetFloat(ShaderParams.Thickness, thick);
        }


        void UpdateHighlightEffect () {
            if (highlightKeywords == null) {
                highlightKeywords = new List<string>();
            }
            else {
                highlightKeywords.Clear();
            }
            switch (_highlightEffect) {
                case HighlightEffect.TextureAdditive:
                    highlightKeywords.Add(ShaderParams.SKW_TEX_HIGHLIGHT_ADDITIVE);
                    break;
                case HighlightEffect.TextureMultiply:
                    highlightKeywords.Add(ShaderParams.SKW_TEX_HIGHLIGHT_MULTIPLY);
                    break;
                case HighlightEffect.TextureColor:
                    highlightKeywords.Add(ShaderParams.SKW_TEX_HIGHLIGHT_COLOR);
                    break;
                case HighlightEffect.TextureScale:
                    highlightKeywords.Add(ShaderParams.SKW_TEX_HIGHLIGHT_SCALE);
                    break;
                case HighlightEffect.DualColors:
                    highlightKeywords.Add(ShaderParams.SKW_TEX_DUAL_COLORS);
                    break;
                case HighlightEffect.None:
                    HideCellHighlightObject();
                    break;
            }
            if (_transparentBackground) {
                highlightKeywords.Add(ShaderParams.SKW_TRANSPARENT);
            }
            string[] keywords = highlightKeywords.ToArray();
            hudMatCellGround.shaderKeywords = keywords;
            hudMatCellOverlay.shaderKeywords = keywords;
            hudMatTerritoryGround.shaderKeywords = keywords;
            hudMatTerritoryOverlay.shaderKeywords = keywords;
            SetBlendHighlight(hudMatCellGround);
            SetBlendHighlight(hudMatCellOverlay);
            SetBlendHighlight(hudMatTerritoryGround);
            SetBlendHighlight(hudMatTerritoryOverlay);
            SetMaskHighlight(hudMatCellGround);
            SetMaskHighlight(hudMatCellOverlay);
            SetMaskHighlight(hudMatTerritoryGround);
            SetMaskHighlight(hudMatTerritoryOverlay);
        }

        void AdjustCanvasSize () {
            if (_terrainObject != null) return;

            if (!TryGetComponent<MeshFilter>(out MeshFilter mf) || mf.sharedMesh == null) return;

            Rect size = _regularHexagons && _adjustCanvasSize ? _canvasRect : new Rect(-0.5f, -0.5f, 1, 1);
            Mesh mesh = mf.sharedMesh;
            if (!CANVAS_MESH_NAME.Equals(mesh.name)) {
                mesh = Instantiate(mesh);
                mesh.name = CANVAS_MESH_NAME;
                mf.sharedMesh = mesh;
            }

            Vector3[] vertices = new Vector3[4];
            for (int k = 0; k < 4; k++) {
                vertices[k].x = canvasVertices[k].x * size.width + size.xMin;
                vertices[k].y = canvasVertices[k].y * size.height + size.yMin;
            }
            mesh.SetVertices(vertices);
        }


        #endregion

        #region Drawing stuff

        int GetCacheIndexForTerritoryRegion (int territoryIndex, int regionIndex) {
            return territoryIndex * 10000 + regionIndex;
        }

        Material hudMatTerritory { get { return _overlayMode == OverlayMode.Overlay ? hudMatTerritoryOverlay : hudMatTerritoryGround; } }

        Material hudMatCell { get { return _overlayMode == OverlayMode.Overlay ? hudMatCellOverlay : hudMatCellGround; } }

        Material fadeMat { get { return _overlayMode == OverlayMode.Overlay ? fadeMatOverlay : fadeMatGround; } }

        int GetMaterialHash (Color color, Texture2D texture) {
            int hash = 17;
            hash = hash * 23 + color.GetHashCode();
            if (texture != null) {
                hash = hash * 23 + texture.GetHashCode();
            }
            return hash;
        }


        Material GetColoredTexturedMaterialForCell (Color color, Texture2D texture, bool overlay) {
            Material mat = GetColoredTexturedMaterialForCell_internal(color, texture, overlay);
            if (_cellTextureMode == CELL_TEXTURE_MODE.Floating) {
                SetStencil(mat, _cellTileBackgroundMode == CELL_TILE_BACKGROUND_MODE.Opaque);
                mat.SetInt(ShaderParams.ZWrite, 1); // need to force depth testing to ensure lower tiles are drawn on top of upper tiles
            }
            return mat;
        }

        Material GetColoredTexturedMaterialForCell_internal (Color color, Texture2D texture, bool overlay) {
            Dictionary<int, Material> matCache = overlay ? coloredMatCacheOverlayCell : coloredMatCacheGroundCell;

            int materialHash = GetMaterialHash(color, texture);

            if (matCache.TryGetValue(materialHash, out Material mat)) {
                mat.color = color;
                return mat;
            }
            else {
                Material customMat;
                if (texture != null) {
                    mat = overlay ? texturizedMatOverlayCell : texturizedMatGroundCell;
                    customMat = Instantiate(mat);
                    customMat.mainTexture = texture;
                }
                else {
                    mat = overlay ? coloredMatOverlayCell : coloredMatGroundCell;
                    customMat = Instantiate(mat);
                }
                customMat.name = mat.name;
                customMat.color = color;
                customMat.SetFloat(ShaderParams.Offset, _gridSurfaceDepthOffset);
                matCache[materialHash] = customMat;
                SetBlend(customMat);
                SetStencil(customMat, STENCIL_MASK_CELL);
                SetAlphaClipping(customMat);
                disposalManager.MarkForDisposal(customMat);
                return customMat;
            }
        }


        Material GetColoredTexturedMaterialForTerritory (Region region, Color color, Texture2D texture, bool overlay) {

            Material mat;
            if (texture != null) {
                mat = overlay ? texturizedMatOverlayTerritory : texturizedMatGroundTerritory;
            }
            else {
                mat = overlay ? coloredMatOverlayTerritory : coloredMatGroundTerritory;
            }
            Material customMat = region.cachedMat;
            if (customMat == null || customMat.name != mat.name) {
                customMat = Instantiate(mat);
                customMat.name = mat.name;
                region.cachedMat = customMat;
            }
            customMat.color = color;
            customMat.mainTexture = texture;
            customMat.SetFloat(ShaderParams.Offset, _gridSurfaceDepthOffsetTerritory);
            customMat.renderQueue = mat.renderQueue + region.renderingOrder;
            SetBlend(customMat);
            SetStencil(customMat, STENCIL_MASK_TERRITORY);
            disposalManager.MarkForDisposal(customMat);
            return customMat;

        }


        Material GetFrontierColorMaterial (Color color) {
            if (color == territoriesMat.color)
                return territoriesMat;

            Material mat;
            if (frontierColorCache.TryGetValue(color, out mat)) {
                return mat;
            }
            else {
                Material customMat = Instantiate(territoriesMat);
                customMat.name = territoriesMat.name;
                customMat.color = color;
                disposalManager.MarkForDisposal(customMat);
                frontierColorCache[color] = customMat;
                return customMat;
            }
        }

        void ApplyMaterialToSurface (Region region, Material sharedMaterial) {
            if (region.renderer != null) {
                region.renderer.sharedMaterial = sharedMaterial;
                if (region.childrenSurfaces != null) {
                    int count = region.childrenSurfaces.Count;
                    for (int k = 0; k < count; k++) {
                        Renderer r = region.childrenSurfaces[k];
                        if (r != null) {
                            r.sharedMaterial = sharedMaterial;
                        }
                    }
                }
            }
        }

        void DrawColorizedTerritories () {
            if (_disableMeshGeneration) return;
            int territoriesCount = territories.Count;
            for (int k = 0; k < territoriesCount; k++) {
                Territory territory = territories[k];
                if (issueRedraw == RedrawType.IncrementalTerritories && !territory.isDirty) continue;
                for (int r = 0; r < territory.regions.Count; r++) {
                    Region region = territory.regions[r];
                    if (region.customMaterial != null) {
                        TerritoryToggleRegionSurface(k, true, region.customMaterial.color, false, (Texture2D)region.customMaterial.mainTexture, region.customTextureScale, region.customTextureOffset, region.customTextureRotation, region.customRotateInLocalSpace, regionIndex: r, isCanvasTexture: region.customIsCanvasTexture);
                    }
                    else {
                        Color fillColor = _territoriesColorScheme == TerritoryColorScheme.UserDefined && _territoriesFillColors != null && k < _territoriesFillColors.Length ? _territoriesFillColors[k] : territories[k].fillColor;
                        fillColor.a *= colorizedTerritoriesAlpha;
                        TerritoryToggleRegionSurface(k, true, fillColor, regionIndex: r);
                    }
                }
            }
        }

        public void GenerateMap (bool reuseTerrainData = false, bool keepCells = false) {
            needGenerateMap = false;
            if (!keepCells) {
                recreateCells = true;
                if (cells != null) {
                    cells.Clear();
                }
                finder = null;
                needRefreshRouteMatrix = true;
            }
            if (territories != null) {
                territories.Clear();
                if (territories.Count > 0) {
                    recreateTerritories = true;
                }
            }
            Redraw(reuseTerrainData);
            if (territoriesTexture != null) {
                CreateTerritories(territoriesTexture, territoriesTextureNeutralColor, territoriesHideNeutralCells);
            }
            // Reload configuration if component exists
            TGSConfig[] configs = GetComponents<TGSConfig>();
            for (int k = 0; k < configs.Length; k++) {
                if (configs[k].enabled)
                    configs[k].LoadConfiguration();
            }
        }

        void SetScaleByCellSize () {
            if (_gridTopology == GridTopology.Hexagonal) {
                _gridScale.x = _cellSize.x * (1f + (_cellColumnCount - 1f) * 0.75f) / transform.lossyScale.x;
                _gridScale.y = _cellSize.y * (_cellRowCount + 0.5f) / transform.lossyScale.y;
            }
            else if (_gridTopology == GridTopology.Box) {
                _gridScale.x = _cellSize.x * _cellColumnCount / transform.lossyScale.x;
                _gridScale.y = _cellSize.y * _cellRowCount / transform.lossyScale.y;
            }
        }

        void ComputeGridScale () {
            if (_regularHexagons && _gridTopology == GridTopology.Hexagonal) {
                if (_pointyTopHexagons) {
                    _gridScale = new Vector2((_cellColumnCount + 0.5f) * 0.8660254f, 1f + (_cellRowCount - 1f) * 0.75f); // cos(60), sqrt(3)/2
                }
                else {
                    _gridScale = new Vector2(1f + (_cellColumnCount - 1f) * 0.75f, (_cellRowCount + 0.5f) * 0.8660254f); // cos(60), sqrt(3)/2
                }
                float hexScale = Mathf.Max(0.00001f, _regularHexagonsWidth / transform.lossyScale.x);
                _gridScale.x *= hexScale;
                _gridScale.y *= hexScale;
                float aspectRatio = Mathf.Clamp(transform.lossyScale.x / transform.lossyScale.y, 0.01f, 10f);
                _gridScale.x = Mathf.Max(_gridScale.x, 0.0001f);
                _gridScale.y *= aspectRatio;
            }
            _gridScale.x = Mathf.Max(_gridScale.x, 0.0001f);
            _gridScale.y = Mathf.Max(_gridScale.y, 0.0001f);
            _cellSize.x = transform.lossyScale.x * _gridScale.x / _cellColumnCount;
            _cellSize.y = transform.lossyScale.y * _gridScale.y / _cellRowCount;
        }

        /// <summary>
        /// Refresh grid.
        /// </summary>
        public void Redraw () {
            BuildTerrainWrapper();

            if (issueRedraw == RedrawType.None) {
                issueRedraw = RedrawType.Full;
            }
            CheckGridChanges();
        }

        /// <summary>
        /// Hides any color/texture from all cells and territories
        /// </summary>
        public void HideAll () {
            foreach (GameObject go in surfaces.Values) {
                if (go != null) {
                    go.SetActive(false);
                }
            }
        }


        /// <summary>
        /// Shows any color/texture from all cells and territories
        /// </summary>
        public void ShowAll () {
            foreach (GameObject go in surfaces.Values) {
                if (go != null) {
                    go.SetActive(true);
                }
            }
        }


        /// <summary>
        /// Removes any color/texture from all cells and territories
        /// </summary>
        public void ClearAll () {
            if (cells != null) {
                int cellsCount = cells.Count;
                for (int k = 0; k < cellsCount; k++) {
                    if (cells[k] != null && cells[k].region != null) {
                        cells[k].region.customMaterial = null;
                    }
                }
            }
            if (territories != null) {
                int territoriesCount = territories.Count;
                for (int k = 0; k < territoriesCount; k++) {
                    Territory territory = territories[k];
                    for (int r = 0; r < territory.regions.Count; r++) {
                        Region region = territory.regions[r];
                        if (region != null) {
                            region.customMaterial = null;
                        }
                    }
                }
            }

            DestroySurfaces();
            issueRedraw = RedrawType.Full;
        }

        /// <summary>
        /// Refresh grid. Set reuseTerrainData to true to avoid computation of terrain heights and slope (useful if terrain is not changed).
        /// </summary>
        public void Redraw (bool reuseTerrainData) {

            if (!gameObject.activeInHierarchy || redrawing)
                return;

            redrawing = true;

            if (issueRedraw == RedrawType.IncrementalTerritories) {
                DestroySurfacesDirty();
            }
            else {
                DestroySurfaces();
            }

            ClearLastOver();

            UpdateTerrainReference(reuseTerrainData);

            if (issueRedraw != RedrawType.IncrementalTerritories) {
                refreshCellMesh = true;
                _lastVertexCount = 0;
                ComputeGridScale();
                CheckCells();
                if (_showCells) {
                    DrawCellBorders();
                }
                DrawColorizedCells();
            }

            refreshTerritoriesMesh = true;
            CheckTerritories();
            if (_showTerritories) {
                DrawTerritoryFrontiers();
            }
            if (_showTerritoriesInteriorBorders) {
                DrawInteriorTerritoryFrontiers();
            }
            if (_colorizeTerritories) {
                DrawColorizedTerritories();
            }
            UpdateMaterialDepthOffset();
            UpdateMaterialNearClipFade();
            UpdateMaterialFarFade();
            UpdateMaterialThickness();
            UpdateHighlightEffect();
            AdjustCanvasSize();

            if (issueRedraw == RedrawType.IncrementalTerritories) {
                int territoryCount = territories.Count;
                for (int k = 0; k < territoryCount; k++) {
                    Territory territory = territories[k];
                    territory.isDirty = false;
                }
            }
            issueRedraw = RedrawType.None;
            redrawing = false;
        }

        void CheckCells () {
            if (cells == null || recreateCells) {
                CreateCells();
                refreshCellMesh = true;
            }
            if (refreshCellMesh) {
                DestroyCellLayer();
                CellsApplyVisibilityFilters();
                if (_showCells) {
                    GenerateCellsMeshData(cells);
                }
                refreshCellMesh = false;
                refreshTerritoriesMesh = true;
            }
        }


        void DestroyMultiMeshObject (GameObject go) {
            if (go == null) return;
            MeshFilter[] mfs = go.GetComponentsInChildren<MeshFilter>(true);
            foreach (var mf in mfs) {
                if (mf.sharedMesh != null) {
                    DestroyImmediate(mf.sharedMesh);
                }
            }
            DestroyImmediate(go);
        }

        void DestroyCellLayer () {
            if (cellLayer == null) {
                Transform t = transform.Find(CELLS_LAYER_NAME);
                if (t != null) {
                    cellLayer = t.gameObject;
                }
            }
            DestroyMultiMeshObject(cellLayer);
        }

        bool isUsingVertexDispForCellThickness { get { return _cellCustomBorderThickness && !canUseGeometryShaders; } }
        bool isUsingVertexDispForTerritoryThickness { get { return _territoryCustomBorderThickness && !canUseGeometryShaders; } }

        void DrawCellBorders () {

            DestroyCellLayer();
            if (cells == null || cells.Count == 0 || cellMeshBorders == null || _disableMeshGeneration)
                return;

            cellLayer = new GameObject(CELLS_LAYER_NAME);
            disposalManager.MarkForDisposal(cellLayer);
            cellLayer.transform.SetParent(transform, false);
            cellLayer.transform.localPosition = new Vector3(0, 0, -0.001f * _gridMinElevationMultiplier);
            cellLayer.layer = gameObject.layer;

            for (int k = 0; k < cellMeshBorders.Length; k++) {
                GameObject flayer = new GameObject("flayer");
                flayer.layer = gameObject.layer;
                disposalManager.MarkForDisposal(flayer);
                flayer.hideFlags |= HideFlags.HideInHierarchy;
                flayer.transform.SetParent(cellLayer.transform, false);
                flayer.transform.localPosition = Misc.Vector3zero;
                flayer.transform.localRotation = Quaternion.Euler(Misc.Vector3zero);

                Mesh mesh = new Mesh();
                mesh.name = "Cells Mesh";
                if (isUsingVertexDispForCellThickness) {
                    ComputeExplodedVertices(mesh, cellMeshBorders[k]);
                }
                else {
                    mesh.vertices = cellMeshBorders[k];
                    mesh.SetIndices(cellMeshIndices[k], MeshTopology.Lines, 0);
                }
                disposalManager.MarkForDisposal(mesh);

                MeshFilter mf = flayer.AddComponent<MeshFilter>();
                mf.sharedMesh = mesh;
                _lastVertexCount += mesh.vertexCount;

                MeshRenderer mr = flayer.AddComponent<MeshRenderer>();
                mr.sortingOrder = _sortingOrder;
                mr.receiveShadows = false;
                mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.sharedMaterial = cellsMat;
            }
            cellLayer.SetActive(_showCells);
            UpdateMaterialThickness();
        }

        bool ValidCellIndex (int cellIndex) {
            return (cellIndex >= 0 && cells != null && cellIndex < cells.Count && cells[cellIndex] != null);
        }

        void ComputeExplodedVertices (Mesh mesh, Vector3[] vertices) {
            tempPoints.Clear();
            tempUVs.Clear();
            tempIndices.Clear();
            Vector4 uv;
            int verticesLength = vertices.Length;
            for (int k = 0; k < verticesLength; k += 2) {
                // Triangle points
                tempPoints.Add(vertices[k]);
                tempPoints.Add(vertices[k]);
                tempPoints.Add(vertices[k + 1]);
                tempPoints.Add(vertices[k + 1]);
                uv = vertices[k + 1]; uv.w = -1; tempUVs.Add(uv);
                uv = vertices[k + 1]; uv.w = 1; tempUVs.Add(uv);
                uv = vertices[k]; uv.w = 1; tempUVs.Add(uv);
                uv = vertices[k]; uv.w = -1; tempUVs.Add(uv);
                // First triangle
                tempIndices.Add(k * 2);
                tempIndices.Add(k * 2 + 1);
                tempIndices.Add(k * 2 + 2);
                // Second triangle
                tempIndices.Add(k * 2 + 1);
                tempIndices.Add(k * 2 + 3);
                tempIndices.Add(k * 2 + 2);
            }
            mesh.SetVertices(tempPoints);
            mesh.SetUVs(0, tempUVs);
            mesh.SetTriangles(tempIndices, 0);
        }

        void ComputeExplodedMesh (Mesh mesh, Vector3[] vertices, float lineWidth, float padding) {

            int numVertices = vertices.Length;
            tempPoints.Clear();
            tempUVs.Clear();
            tempIndices.Clear();

            for (int k = 0; k < numVertices; k += 2) {

                // previous normal
                Vector2 leftSegment;
                if (k > 0) {
                    leftSegment.x = vertices[k - 1].x - vertices[k - 2].x;
                    leftSegment.y = vertices[k - 1].y - vertices[k - 2].y;
                }
                else {
                    leftSegment.x = vertices[numVertices - 1].x - vertices[numVertices - 2].x;
                    leftSegment.y = vertices[numVertices - 1].y - vertices[numVertices - 2].y;

                }
                Vector2 leftNormal = new Vector2(-leftSegment.y, leftSegment.x).normalized;

                Vector2 thisSegmentNormalized = vertices[k + 1] - vertices[k];
                thisSegmentNormalized.Normalize();
                Vector2 thisNormal = new Vector2(-thisSegmentNormalized.y, thisSegmentNormalized.x);

                Vector2 rightSegment;
                if (k < numVertices - 3) {
                    rightSegment.x = vertices[k + 3].x - vertices[k + 2].x;
                    rightSegment.y = vertices[k + 3].y - vertices[k + 2].y;
                }
                else {
                    rightSegment.x = vertices[1].x - vertices[0].x;
                    rightSegment.y = vertices[1].y - vertices[0].y;
                }
                Vector2 rightNormal = new Vector2(-rightSegment.y, rightSegment.x).normalized;

                Vector2 leftBisector = (leftNormal + thisNormal).normalized;
                Vector2 rightBisector = (rightNormal + thisNormal).normalized;

                float dx1 = Vector2.Dot(leftBisector, thisSegmentNormalized);
                float leftDisplacementAmount = lineWidth / Mathf.Sqrt(1f - dx1 * dx1);
                Vector3 leftDisplacement = leftBisector * leftDisplacementAmount;

                float dx2 = Vector2.Dot(rightBisector, -thisSegmentNormalized);
                float rightDisplacementAmount = lineWidth / Mathf.Sqrt(1f - dx2 * dx2);
                Vector3 rightDisplacement = rightBisector * rightDisplacementAmount;

                Vector3 p0 = vertices[k];
                Vector3 p1 = (k < numVertices - 1) ? vertices[k + 1] : vertices[0];

                tempPoints.Add(p0 + leftDisplacement * (1f + padding));
                tempPoints.Add(p1 + rightDisplacement * (1f + padding));
                tempPoints.Add(p1 - rightDisplacement + rightDisplacement * padding);
                tempPoints.Add(p0 - leftDisplacement + leftDisplacement * padding);

                tempUVs.AddRange(gradientUVs);

                // first triangle
                tempIndices.Add(k * 2);
                tempIndices.Add(k * 2 + 1);
                tempIndices.Add(k * 2 + 2);

                // second triangle
                tempIndices.Add(k * 2);
                tempIndices.Add(k * 2 + 2);
                tempIndices.Add(k * 2 + 3);
            }

            mesh.SetVertices(tempPoints);
            mesh.SetUVs(0, tempUVs);
            mesh.SetTriangles(tempIndices, 0);
        }

        void DrawColorizedCells () {
            if (cells == null || _disableMeshGeneration)
                return;
            int cellsCount = cells.Count;
            for (int k = 0; k < cellsCount; k++) {
                Cell cell = cells[k];
                if (cell == null) continue;
                Region region = cell.region;
                if (region.customMaterial != null && cell.visible) {
                    CellToggleRegionSurface(k, true, region.customMaterial.color, false, (Texture2D)region.customMaterial.mainTexture, region.customTextureScale, region.customTextureOffset, region.customTextureRotation, region.customRotateInLocalSpace);
                }
            }
        }

        void CheckTerritories () {
            if (!territoriesAreUsed)
                return;
            if (territories.Count == 0 || recreateTerritories) {
                CreateTerritories();
                refreshTerritoriesMesh = true;
            }
            else if (needUpdateTerritories) {
                FindTerritoriesFrontiers();
                UpdateTerritoriesBoundary();
                needUpdateTerritories = false;
                refreshTerritoriesMesh = true;
            }

            if (refreshTerritoriesMesh) {
                DestroyTerritoryLayer();
                GenerateTerritoriesMeshData();
                refreshTerritoriesMesh = false;
            }

        }

        void DestroyTerritoryLayer () {
            if (territoryLayer == null) {
                Transform t = transform.Find(TERRITORIES_LAYER_NAME);
                if (t != null) {
                    territoryLayer = t;
                }
            }
            if (territoryLayer != null) {
                DestroyMultiMeshObject(territoryLayer.gameObject);
            }
        }

        void DrawInteriorTerritoryFrontiers () {
            if (territoryInteriorBorderLayer != null) {
                DestroyImmediate(territoryInteriorBorderLayer.gameObject);
            }
            if (_disableMeshGeneration) return;
            int territoryCount = territories.Count;
            for (int k = 0; k < territoryCount; k++) {
                int regionsCount = territories[k].regions.Count;
                for (int r = 0; r < regionsCount; r++) {
                    TerritoryDrawInteriorBorder(k, _territoryInteriorBorderPadding, _territoryInteriorBorderThickness, regionIndex: r, includeEnclaves: _territoryInteriorBorderIncludeEnclaves);
                }
            }
        }

        void DrawTerritoryFrontiers () {
            DestroyTerritoryLayer();
            if (territories.Count == 0 || territoryMeshes == null || _disableMeshGeneration)
                return;

            GameObject territoryLayerGO = new GameObject(TERRITORIES_LAYER_NAME);
            territoryLayerGO.layer = gameObject.layer;
            disposalManager.MarkForDisposal(territoryLayerGO);
            territoryLayer = territoryLayerGO.transform;
            territoryLayer.SetParent(transform, false);
            territoryLayer.localPosition = Vector3.back * 0.001f * _gridMinElevationMultiplier;

            for (int t = 0; t < territoryMeshes.Count; t++) {
                TerritoryMesh tm = territoryMeshes[t];

                Material mat;
                if (tm.territoryIndex < 0) {
                    mat = territoriesDisputedMat;
                }
                else {
                    int territoryIndex = tm.territoryIndex;
                    if (territoryIndex >= territories.Count) continue;
                    Color frontierColor = territories[territoryIndex].frontierColor;
                    if (frontierColor.a == 0 && frontierColor.r == 0 && frontierColor.g == 0 && frontierColor.b == 0) {
                        mat = territoriesMat;
                    }
                    else {
                        mat = GetFrontierColorMaterial(frontierColor);
                    }
                }
                DrawTerritoryFrontier(tm, mat, territoryLayer, TERRITORY_FRONTIER_NAME, isUsingVertexDispForTerritoryThickness);
            }

            territoryLayer.gameObject.SetActive(_showTerritories);

            UpdateMaterialThickness();
        }

        GameObject DrawTerritoryFrontier (TerritoryMesh tm, Material mat, Transform parent, string name, bool useVertexDisplacementForTerritoryThickness, bool usesGradient = false, float thickness = 1f, float padding = 0) {
            GameObject root = new GameObject(name);
            root.layer = gameObject.layer;
            disposalManager.MarkForDisposal(root);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0, 0, -0.001f * _gridMinElevationMultiplier);
            root.transform.localRotation = Quaternion.Euler(Misc.Vector3zero);

            for (int k = 0; k < tm.territoryMeshBorders.Length; k++) {
                GameObject flayer = new GameObject("flayer");
                flayer.layer = gameObject.layer;
                disposalManager.MarkForDisposal(flayer);
                flayer.hideFlags |= HideFlags.HideInHierarchy;
                flayer.transform.SetParent(root.transform, false);
                flayer.transform.localPosition = Misc.Vector3zero;
                flayer.transform.localRotation = Quaternion.Euler(Misc.Vector3zero);

                Mesh mesh = new Mesh();
                mesh.name = "Territory Mesh";
                if (usesGradient) {
                    float adjustedThickness = (thickness - 0.8f) / transform.lossyScale.x;
                    ComputeExplodedMesh(mesh, tm.territoryMeshBorders[k], adjustedThickness, padding);
                }
                else if (useVertexDisplacementForTerritoryThickness) {
                    ComputeExplodedVertices(mesh, tm.territoryMeshBorders[k]);
                }
                else {
                    mesh.vertices = tm.territoryMeshBorders[k];
                    mesh.SetIndices(tm.territoryMeshIndices[k], MeshTopology.Lines, 0);
                }

                disposalManager.MarkForDisposal(mesh);

                MeshFilter mf = flayer.AddComponent<MeshFilter>();
                mf.sharedMesh = mesh;
                _lastVertexCount += mesh.vertexCount;

                MeshRenderer mr = flayer.AddComponent<MeshRenderer>();
                mr.sortingOrder = _sortingOrder;
                mr.receiveShadows = false;
                mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.sharedMaterial = mat;
            }
            return root;
        }

        Transform CheckCellsCustomFrontiersRoot () {
            Transform root = transform.Find(CELLS_CUSTOM_BORDERS_ROOT);
            if (root == null) {
                GameObject rootGO = new GameObject(CELLS_CUSTOM_BORDERS_ROOT);
                disposalManager.MarkForDisposal(rootGO);
                rootGO.layer = gameObject.layer;
                root = rootGO.transform;
                root.SetParent(transform, false);
                root.localRotation = Quaternion.Euler(Misc.Vector3zero);
            }
            root.localPosition = new Vector3(0, 0, -0.001f * _gridMinElevationMultiplier);
            return root;
        }

        Transform CheckTerritoriesCustomFrontiersRoot () {
            Transform root = transform.Find(TERRITORIES_CUSTOM_FRONTIERS_ROOT);
            if (root == null) {
                GameObject rootGO = new GameObject(TERRITORIES_CUSTOM_FRONTIERS_ROOT);
                disposalManager.MarkForDisposal(rootGO);
                rootGO.layer = gameObject.layer;
                root = rootGO.transform;
                root.SetParent(transform, false);
                root.localRotation = Quaternion.Euler(Misc.Vector3zero);
            }
            root.localPosition = new Vector3(0, 0, -0.001f * _gridMinElevationMultiplier);
            return root;
        }

        Transform CheckTerritoriesInteriorFrontiersRoot () {
            Transform root = transform.Find(TERRITORIES_INTERIOR_FRONTIERS_ROOT);
            if (root == null) {
                GameObject rootGO = new GameObject(TERRITORIES_INTERIOR_FRONTIERS_ROOT);
                disposalManager.MarkForDisposal(rootGO);
                rootGO.layer = gameObject.layer;
                root = rootGO.transform;
                root.SetParent(transform, false);
                root.localRotation = Quaternion.Euler(Misc.Vector3zero);
            }
            root.localPosition = new Vector3(0, 0, -0.001f * _gridMinElevationMultiplier);
            return root;
        }

        void PrepareNewSurfaceMesh (int pointCount) {
            if (meshPoints == null) {
                meshPoints = new List<Vector3>(pointCount);
            }
            else {
                meshPoints.Clear();
            }
            if (triNew == null || triNew.Length != pointCount) {
                triNew = new int[pointCount];
            }
            if (surfaceMeshHit == null)
                surfaceMeshHit = new Dictionary<TriangulationPoint, int>(20000);
            else {
                surfaceMeshHit.Clear();
            }

            triNewIndex = -1;
            newPointsCount = -1;
        }

        void AddPointToSurfaceMeshWithNormalOffset (TriangulationPoint p) {
            int tri;
            if (surfaceMeshHit.TryGetValue(p, out tri)) {
                triNew[++triNewIndex] = tri;
            }
            else {
                Vector3 np;
                np.x = p.Xf - 2;
                np.y = p.Yf - 2;
                np.z = -p.Zf;
                np += transform.InverseTransformVector(_terrainWrapper.GetInterpolatedNormal(np.x + 0.5f, np.y + 0.5f)) * _gridNormalOffset;
                meshPoints.Add(np);
                surfaceMeshHit[p] = ++newPointsCount;
                triNew[++triNewIndex] = newPointsCount;
            }
        }

        void AddPointToSurfaceMeshWithoutNormalOffset (TriangulationPoint p) {
            if (surfaceMeshHit.TryGetValue(p, out int tri)) {
                triNew[++triNewIndex] = tri;
            }
            else {
                Vector3 np;
                np.x = p.Xf - 2;
                np.y = p.Yf - 2;
                np.z = -p.Zf;
                meshPoints.Add(np);
                surfaceMeshHit[p] = ++newPointsCount;
                triNew[++triNewIndex] = newPointsCount;
            }
        }


        void AddPointToSurfaceMeshWithNormalOffsetPrimitive (TriangulationPoint p) {
            Vector3 np;
            np.x = p.Xf - 2;
            np.y = p.Yf - 2;
            np.z = -p.Zf;
            np += transform.InverseTransformVector(_terrainWrapper.GetInterpolatedNormal(np.x + 0.5f, np.y + 0.5f)) * _gridNormalOffset;
            meshPoints.Add(np);
        }

        void AddPointToSurfaceMeshWithoutNormalOffsetPrimitive (TriangulationPoint p) {
            Vector3 np;
            np.x = p.Xf - 2;
            np.y = p.Yf - 2;
            np.z = -p.Zf;
            meshPoints.Add(np);
        }

        double polyShift;
        Poly2Tri.Polygon GetPolygon (Region region, ref PolygonPoint[] polyPoints, out int pointCount, float padding) {
            // Calculate region's surface points
            pointCount = 0;
            int numSegments = region.segments.Count;
            if (numSegments == 0)
                return null;

            polyShift = 0;
            tempConnector.Clear();
            if (_gridScale.x == 1f && _gridScale.y == 1f && _gridCenter.x == 0 && _gridCenter.y == 0) {
                // non scaling segments
                if (region.isFlat || _terrainWrapper == null) {
                    for (int i = 0; i < numSegments; i++) {
                        Segment s = region.segments[i];
                        tempConnector.Add(s);
                    }
                }
                else {
                    for (int i = 0; i < numSegments; i++) {
                        Segment s = region.segments[i];
                        SurfaceSegmentForSurface(s, tempConnector);
                    }
                }
            }
            else {
                // scaling segments
                if (region.isFlat || _terrainWrapper == null) {
                    for (int i = 0; i < numSegments; i++) {
                        Segment s = region.segments[i];
                        tempConnector.Add(GetScaledSegment(s));
                    }
                }
                else {
                    for (int i = 0; i < numSegments; i++) {
                        Segment s = region.segments[i];
                        SurfaceSegmentForSurface(GetScaledSegment(s), tempConnector);
                    }
                }
            }
            Geom.Polygon surfacedPolygon = tempConnector.ToPolygonFromLargestLineStrip();
            if (surfacedPolygon == null)
                return null;

            List<Point> surfacedPoints = surfacedPolygon.contours[0].points;

            int spCount = surfacedPoints.Count;
            if (polyPoints == null || polyPoints.Length < spCount) {
                polyPoints = new PolygonPoint[spCount];
            }

            if (region.isFlat) {
                Vector3 centerWS = transform.TransformPoint(region.entity.GetScaledCenter());
                float cellCenterHeight = _terrainWrapper.GetInterpolatedHeight(centerWS);
                bool usesTerrain = _terrainWrapper != null;
                for (int k = 0; k < spCount; k++) {
                    double x = surfacedPoints[k].x;
                    double y = surfacedPoints[k].y;
                    if (!IsTooNearPolygon(x, y, polyPoints, pointCount)) {
                        float h = usesTerrain ? cellCenterHeight : 0;
                        polyPoints[pointCount++] = new PolygonPoint(x, y, h);
                    }
                }
            }
            else {
                bool usesTerrain = _terrainWrapper != null;
                for (int k = 0; k < spCount; k++) {
                    double x = surfacedPoints[k].x;
                    double y = surfacedPoints[k].y;
                    if (!IsTooNearPolygon(x, y, polyPoints, pointCount)) {
                        float h = usesTerrain ? _terrainWrapper.GetInterpolatedHeight(transform.TransformPoint((float)x, (float)y, 0)) : 0;
                        polyPoints[pointCount++] = new PolygonPoint(x, y, h);
                    }
                }
            }

            if (pointCount < 3)
                return null;

            double enlarge = padding / 100f;
            if (enlarge != 0) {
                if (_gridTopology == GridTopology.Hexagonal && region.entity is Territory) {
                    Vector2[] originalPoints = new Vector2[pointCount];
                    for (int k = 0; k < pointCount; k++) {
                        originalPoints[k].x = polyPoints[k].Xf;
                        originalPoints[k].y = polyPoints[k].Yf;
                    }
                    const double EPSILON = 0.000001;
                    for (int k = 0; k < pointCount; k++, polyShift++) {
                        PolygonPoint p = polyPoints[k];
                        Vector2 prevp, nextp;
                        if (k > 0) {
                            prevp = originalPoints[k - 1];
                        }
                        else {
                            prevp = originalPoints[pointCount - 1];
                        }
                        if (k < pointCount - 1) {
                            nextp = originalPoints[k + 1];
                        }
                        else {
                            nextp = originalPoints[0];
                        }
                        double x = p.X;
                        double y = p.Y;
                        double dx = (prevp.x + nextp.x) * 0.5 - x;
                        double dy = (prevp.y + nextp.y) * 0.5 - y;

                        if (prevp.x > nextp.x + EPSILON && dy > -EPSILON) {
                            dx = -dx;
                            dy = -dy;
                        }
                        else if (prevp.y > nextp.y + EPSILON && dx < EPSILON) {
                            dx = -dx;
                            dy = -dy;
                        }
                        else if (prevp.x < nextp.x - EPSILON && dy < EPSILON) {
                            dx = -dx;
                            dy = -dy;
                        }
                        else if (prevp.y < nextp.y - EPSILON && dx > -EPSILON) {
                            dx = -dx;
                            dy = -dy;
                        }
                        double len = Math.Sqrt(dx * dx + dy * dy);
                        // these additions are required to prevent issues with polytri library
                        x += 2 + polyShift / 1e8 - (dx / len) * enlarge;
                        y += 2 + polyShift / 1e8 - (dy / len) * enlarge;
                        polyPoints[k] = new PolygonPoint(x, y, p.Zf);
                    }
                }
                else if (_gridTopology == GridTopology.Box && region.entity is Territory) {
                    Vector2[] originalPoints = new Vector2[pointCount];
                    for (int k = 0; k < pointCount; k++) {
                        originalPoints[k].x = polyPoints[k].Xf;
                        originalPoints[k].y = polyPoints[k].Yf;
                    }
                    const double EPSILON = 0.000001;
                    for (int k = 0; k < pointCount; k++, polyShift++) {
                        PolygonPoint p = polyPoints[k];
                        Vector2 prevp, nextp;
                        if (k > 0) {
                            prevp = originalPoints[k - 1];
                        }
                        else {
                            prevp = originalPoints[pointCount - 1];
                        }
                        if (k < pointCount - 1) {
                            nextp = originalPoints[k + 1];
                        }
                        else {
                            nextp = originalPoints[0];
                        }
                        double x = p.X;
                        double y = p.Y;
                        double dx = 1, dy = 1;
                        if (prevp.x > x - EPSILON && nextp.x < x + EPSILON) {
                            dy = -1;
                        }
                        if (prevp.y < y + EPSILON && nextp.y > y - EPSILON) {
                            dx = -1;
                        }
                        double len = Math.Sqrt(dx * dx + dy * dy);
                        // these additions are required to prevent issues with polytri library
                        x += 2 + polyShift / 1e8 - (dx / len) * enlarge;
                        y += 2 + polyShift / 1e8 - (dy / len) * enlarge;
                        polyPoints[k] = new PolygonPoint(x, y, p.Zf);
                    }
                }
                else {
                    double rx = region.rect2D.center.x;
                    double ry = region.rect2D.center.y;
                    for (int k = 0; k < pointCount; k++, polyShift++) {
                        PolygonPoint p = polyPoints[k];
                        double x = p.X;
                        double y = p.Y;
                        double dx = x - rx;
                        double dy = y - ry;
                        double len = Math.Sqrt(dx * dx + dy * dy);
                        // these additions are required to prevent issues with polytri library
                        x += 2 + polyShift / 1e8 + (dx / len) * enlarge;
                        y += 2 + polyShift / 1e8 + (dy / len) * enlarge;
                        polyPoints[k] = new PolygonPoint(x, y, p.Zf);
                    }
                }
            }
            else {
                for (int k = 0; k < pointCount; k++, polyShift++) {
                    PolygonPoint p = polyPoints[k];
                    double x = p.X;
                    double y = p.Y;
                    // these additions are required to prevent issues with polytri library
                    x += 2 + polyShift / 1e8;
                    y += 2 + polyShift / 1e8;
                    polyPoints[k] = new PolygonPoint(x, y, p.Zf);
                }
            }
            return new Poly2Tri.Polygon(polyPoints, pointCount);
        }

        bool SubstractInsideTerritories (Region region, ref PolygonPoint[] polyPoints, Poly2Tri.Polygon poly) {
            List<Region> negativeRegions = new List<Region>();
            int terrRegionsCount = sortedTerritoriesRegions.Count;
            for (int r = 0; r < terrRegionsCount; r++) {
                Region otherRegion = _sortedTerritoriesRegions[r];
                if (otherRegion != region && otherRegion.points.Count >= 3 && region.ContainsRegion(otherRegion)) {
                    Region clonedRegion = otherRegion.Clone();
                    clonedRegion.Enlarge(_gridTopology == GridTopology.Box ? -0.01f : 0.01f); // try to avoid floating point issues when merging cells or regions adjacents with shared vertices
                    negativeRegions.Add(clonedRegion);
                }
            }
            // Collapse negative regions in big holes
            for (int nr = 0; nr < negativeRegions.Count - 1; nr++) {
                for (int nr2 = nr + 1; nr2 < negativeRegions.Count; nr2++) {
                    if (negativeRegions[nr].Intersects(negativeRegions[nr2])) {
                        Clipper clipper = new Clipper();
                        clipper.AddPath(negativeRegions[nr], PolyType.ptSubject);
                        clipper.AddPath(negativeRegions[nr2], PolyType.ptClip);
                        clipper.Execute(ClipType.ctUnion);
                        negativeRegions.RemoveAt(nr2);
                        nr = -1;
                        break;
                    }
                }
            }

            // Substract holes
            bool hasHoles = false;
            for (int nr = 0; nr < negativeRegions.Count; nr++) {
                List<Vector2> surfacedPoints = negativeRegions[nr].points;
                int spCount = surfacedPoints.Count;
                double midx = 0, midy = 0;
                if (polyPoints == null || polyPoints.Length < spCount) {
                    polyPoints = new PolygonPoint[spCount];
                }

                bool usesTerrain = _terrainWrapper != null;
                int pointCount = 0;
                for (int k = 0; k < spCount; k++) {
                    double x = surfacedPoints[k].x;
                    double y = surfacedPoints[k].y;
                    if (!IsTooNearPolygon(x, y, polyPoints, pointCount)) {
                        float h = usesTerrain ? _terrainWrapper.GetInterpolatedHeight(transform.TransformPoint((float)x, (float)y, 0)) : 0;
                        polyPoints[pointCount++] = new PolygonPoint(x, y, h);
                        midx += x;
                        midy += y;
                    }
                }

                if (pointCount >= 3) {
                    // reduce
                    midx /= pointCount;
                    midy /= pointCount;
                    for (int k = 0; k < pointCount; k++, polyShift++) {
                        PolygonPoint p = polyPoints[k];
                        double DX = midx - p.X;
                        double DY = midy - p.Y;
                        // these additions are required to prevent issues with polytri library
                        double x = p.X + 2 + polyShift / 1000000.0;
                        double y = p.Y + 2 + polyShift / 1000000.0;
                        polyPoints[k] = new PolygonPoint(x + DX * 0.001, y + DY * 0.001, p.Zf);
                    }
                    // add poly hole
                    Poly2Tri.Polygon polyHole = new Poly2Tri.Polygon(polyPoints, pointCount);
                    poly.AddHole(polyHole);
                    hasHoles = true;
                }
            }

            return hasHoles;
        }

        GameObject GenerateRegionSurface (Region region, int cacheIndex, Material material, Vector2 textureScale, Vector2 textureOffset, float textureRotation, bool rotateInLocalSpace, CELL_TEXTURE_MODE textureMode, bool isCanvasTexture) {

            try {
                // Deletes potential residual surface
                if (region.surfaceGameObject != null) {
                    DestroyImmediate(region.surfaceGameObject);
                }

                Poly2Tri.Polygon poly = GetPolygon(region, ref tempPolyPoints, out int pointCount, _cellFillPadding);
                if (poly == null)
                    return null;

                // Support for internal territories
                bool hasHole = false;
                if (_allowTerritoriesInsideTerritories && region.entity is Territory) {
                    hasHole = SubstractInsideTerritories(region, ref tempPolyPoints2, poly);
                }

                if (!hasHole) {
                    if (_gridTopology == GridTopology.Hexagonal && pointCount == 6) {
                        if (textureMode == CELL_TEXTURE_MODE.InTile) {
                            return GenerateRegionSurfaceSimpleHex(poly, region, cacheIndex, material, textureScale, textureOffset, textureRotation, rotateInLocalSpace, isCanvasTexture);
                        }
                        else {
                            return GenerateRegionSurfaceFloatingQuad(region, cacheIndex, material, textureScale, textureOffset, textureRotation, rotateInLocalSpace, isCanvasTexture);
                        }
                    }
                    else if (_gridTopology == GridTopology.Box && pointCount == 4) {
                        return GenerateRegionSurfaceSimpleQuad(poly, region, cacheIndex, material, textureScale, textureOffset, textureRotation, rotateInLocalSpace, isCanvasTexture);
                    }
                }

                if (_terrainWrapper != null && !_cellsFlat) {
                    if (steinerPoints == null) {
                        steinerPoints = new List<TriangulationPoint>(6000);
                    }
                    else {
                        steinerPoints.Clear();
                    }

                    float stepX = 1.0f / heightMapWidth;
                    float smallStep = 1.0f / heightMapWidth;
                    float y = region.rect2D.yMin + smallStep;
                    float ymax = region.rect2D.yMax - smallStep;
                    if (acumY == null || acumY.Length != terrainRoughnessMapWidth) {
                        acumY = new float[terrainRoughnessMapWidth];
                    }
                    else {
                        Array.Clear(acumY, 0, terrainRoughnessMapWidth);
                    }
                    int steinerPointsCount = 0;
                    while (y < ymax) {
                        int j = (int)((y + 0.5f) * terrainRoughnessMapHeight);
                        if (j >= terrainRoughnessMapHeight)
                            j = terrainRoughnessMapHeight - 1;
                        else if (j < 0)
                            j = 0;
                        int jPos = j * terrainRoughnessMapWidth;
                        float sy = y + 2;
                        GetFirstAndLastPointInRow(sy, pointCount, out float xin, out float xout);
                        xin += smallStep;
                        xout -= smallStep;
                        int k0 = -1;
                        for (float x = xin; x < xout; x += stepX) {
                            int k = (int)((x + 0.5f) * terrainRoughnessMapWidth);
                            if (k >= terrainRoughnessMapWidth)
                                k = terrainRoughnessMapWidth - 1;
                            else if (k < 0)
                                k = 0;
                            if (k0 != k) {
                                k0 = k;
                                stepX = terrainRoughnessMap[jPos + k];
                                if (acumY[k] >= stepX)
                                    acumY[k] = 0;
                                acumY[k] += smallStep;
                            }
                            if (acumY[k] >= stepX) {
                                // Gather precision height
                                float h = _terrainWrapper.GetInterpolatedHeight(transform.TransformPoint(x, y, 0));
                                float htl = _terrainWrapper.GetInterpolatedHeight(transform.TransformPoint(x - smallStep, y + smallStep, 0));
                                if (htl > h)
                                    h = htl;
                                float htr = _terrainWrapper.GetInterpolatedHeight(transform.TransformPoint(x + smallStep, y + smallStep, 0));
                                if (htr > h)
                                    h = htr;
                                float hbr = _terrainWrapper.GetInterpolatedHeight(transform.TransformPoint(x + smallStep, y - smallStep, 0));
                                if (hbr > h)
                                    h = hbr;
                                float hbl = _terrainWrapper.GetInterpolatedHeight(transform.TransformPoint(x - smallStep, y - smallStep, 0));
                                if (hbl > h)
                                    h = hbl;
                                steinerPoints.Add(new PolygonPoint(x + 2, sy, h));
                                steinerPointsCount++;
                            }
                        }
                        y += smallStep;
                        if (steinerPointsCount > 80000) {
                            Debug.LogWarning("Terrain Grid System: number of adaptation points exceeded. Try increasing the Roughness or the number of points in polygon.");
                            break;
                        }
                    }
                    poly.AddSteinerPoints(steinerPoints);
                }

                P2T.Triangulate(poly);


                Rect rect = GetRectForTexturedRegion(region, material, isCanvasTexture);

                // Calculate & optimize mesh data
                int triCount = poly.Triangles.Count;
                GameObject parentSurf = null;

                for (int triBase = 0; triBase < triCount; triBase += 65500) {
                    int meshTriCount = 65500;
                    if (triBase + meshTriCount > triCount) {
                        meshTriCount = triCount - triBase;
                    }

                    PrepareNewSurfaceMesh(meshTriCount * 3);

                    if (_gridNormalOffset > 0 && _terrainWrapper != null) {
                        for (int k = 0; k < meshTriCount; k++) {
                            DelaunayTriangle dt = poly.Triangles[k + triBase];
                            AddPointToSurfaceMeshWithNormalOffset(dt.Points[0]);
                            AddPointToSurfaceMeshWithNormalOffset(dt.Points[2]);
                            AddPointToSurfaceMeshWithNormalOffset(dt.Points[1]);
                        }
                    }
                    else {
                        for (int k = 0; k < meshTriCount; k++) {
                            DelaunayTriangle dt = poly.Triangles[k + triBase];
                            AddPointToSurfaceMeshWithoutNormalOffset(dt.Points[0]);
                            AddPointToSurfaceMeshWithoutNormalOffset(dt.Points[2]);
                            AddPointToSurfaceMeshWithoutNormalOffset(dt.Points[1]);
                        }
                    }
                    if (_terrainWrapper != null) {
                        if (_cellsMinimumAltitudeClampVertices && _cellsMinimumAltitude != 0) {
                            float localMinima = -(_cellsMinimumAltitude - _terrainWrapper.bounds.min.y) / _terrainWrapper.transform.lossyScale.y;
                            int meshPointsCount = meshPoints.Count;
                            for (int k = 0; k < meshPointsCount; k++) {
                                Vector3 p = meshPoints[k];
                                if (p.z > localMinima) {
                                    p.z = localMinima;
                                    meshPoints[k] = p;
                                }
                            }
                        }
                        if (_cellsMaximumAltitudeClampVertices && _cellsMaximumAltitude != 0) {
                            float localMaxima = -(_cellsMaximumAltitude - _terrainWrapper.bounds.min.y) / _terrainWrapper.transform.lossyScale.y;
                            int meshPointsCount = meshPoints.Count;
                            for (int k = 0; k < meshPointsCount; k++) {
                                Vector3 p = meshPoints[k];
                                if (p.z < localMaxima) {
                                    p.z = localMaxima;
                                    meshPoints[k] = p;
                                }
                            }
                        }
                    }

                    string surfName;
                    if (triBase == 0) {
                        surfName = cacheIndex.ToString();
                    }
                    else {
                        surfName = "splitMesh";
                    }
                    Renderer surfRenderer = Drawing.CreateSurface(surfName, meshPoints, triNew, material, rect, textureScale, textureOffset, textureRotation, rotateInLocalSpace, _sortingOrder, disposalManager);
                    GameObject surf = surfRenderer.gameObject;
                    _lastVertexCount += meshPoints.Count;
                    if (triBase == 0) {
                        surf.transform.SetParent(surfacesLayer.transform, false);
                        surfaces[cacheIndex] = surf;
                        if (region.childrenSurfaces != null) {
                            region.childrenSurfaces.Clear();
                        }
                        region.renderer = surfRenderer;
                    }
                    else {
                        surf.transform.SetParent(parentSurf.transform, false);
                        if (region.childrenSurfaces == null) {
                            region.childrenSurfaces = new List<Renderer>();
                        }
                        region.childrenSurfaces.Add(surfRenderer);
                    }
                    surf.transform.localPosition = Misc.Vector3zero;
                    surf.layer = gameObject.layer;
                    parentSurf = surf;
                }
                return parentSurf;
            }
            catch {
                return null;
            }
        }


        GameObject GenerateRegionSurfaceHighlightSimpleShape (Material material, bool isCanvasTexture) {

            Cell cell = null;
            int cellsCount = cells.Count;
            int expectedPointCount;
            int[] triangles;
            if (_gridTopology == GridTopology.Hexagonal) {
                expectedPointCount = 6;
                triangles = hexIndices;
            }
            else {
                expectedPointCount = 4;
                triangles = quadIndices;
            }

            for (int k = 0; k < cellsCount; k++) {
                Cell thisCell = cells[k];
                if (thisCell != null && thisCell.region.points.Count == expectedPointCount) {
                    cell = thisCell;
                    break;
                }
            }
            if (cell == null) return null;

            Region region = cell.region;

            Poly2Tri.Polygon poly = GetPolygon(region, ref tempPolyPoints, out int pointCount, _cellFillPadding);
            if (poly == null)
                return null;

            // Calculate & optimize mesh data
            PrepareNewSurfaceMesh(pointCount);
            IList<TriangulationPoint> points = poly.Points;
            for (int k = 0; k < pointCount; k++) {
                AddPointToSurfaceMeshWithoutNormalOffsetPrimitive(points[k]);
            }
            Rect rect = GetRectForTexturedRegion(region, material, isCanvasTexture);
            Renderer surfRenderer = Drawing.CreateSurface("Highlight Shape", meshPoints, triangles, material, rect, Misc.Vector2one, Misc.Vector2zero, 0, false, _sortingOrder, disposalManager);
            GameObject surf = surfRenderer.gameObject;
            surf.transform.SetParent(surfacesLayer.transform, false);
            surf.transform.localPosition = Misc.Vector3zero;
            surf.layer = gameObject.layer;
            return surf;
        }


        GameObject GenerateRegionSurfaceSimpleHex (Poly2Tri.Polygon poly, Region region, int cacheIndex, Material material, Vector2 textureScale, Vector2 textureOffset, float textureRotation, bool rotateInLocalSpace, bool isCanvasTexture) {

            // Calculate & optimize mesh data
            PrepareNewSurfaceMesh(6);
            IList<TriangulationPoint> points = poly.Points;
            if (_gridNormalOffset > 0 && _terrainWrapper != null) {
                for (int k = 0; k < 6; k++) {
                    AddPointToSurfaceMeshWithNormalOffsetPrimitive(points[k]);
                }
            }
            else {
                for (int k = 0; k < 6; k++) {
                    AddPointToSurfaceMeshWithoutNormalOffsetPrimitive(points[k]);
                }
            }
            Rect rect = GetRectForTexturedRegion(region, material, isCanvasTexture);
            Renderer surfRenderer = Drawing.CreateSurface(cacheIndex.ToString(), meshPoints, hexIndices, material, rect, textureScale, textureOffset, textureRotation, rotateInLocalSpace, _sortingOrder, disposalManager);
            GameObject surf = surfRenderer.gameObject;
            _lastVertexCount += 6;
            surf.transform.SetParent(surfacesLayer.transform, false);
            surfaces[cacheIndex] = surf;
            surf.transform.localPosition = Misc.Vector3zero;
            surf.layer = gameObject.layer;
            region.renderer = surfRenderer;
            return surf;
        }


        GameObject GenerateRegionSurfaceSimpleQuad (Poly2Tri.Polygon poly, Region region, int cacheIndex, Material material, Vector2 textureScale, Vector2 textureOffset, float textureRotation, bool rotateInLocalSpace, bool isCanvasTexture) {

            // Calculate & optimize mesh data
            PrepareNewSurfaceMesh(4);
            IList<TriangulationPoint> points = poly.Points;
            if (_gridNormalOffset > 0 && _terrainWrapper != null) {
                for (int k = 0; k < 4; k++) {
                    AddPointToSurfaceMeshWithNormalOffsetPrimitive(points[k]);
                }
            }
            else {
                for (int k = 0; k < 4; k++) {
                    AddPointToSurfaceMeshWithoutNormalOffsetPrimitive(points[k]);
                }
            }
            Rect rect = GetRectForTexturedRegion(region, material, isCanvasTexture);
            Renderer surfRenderer = Drawing.CreateSurface(cacheIndex.ToString(), meshPoints, quadIndices, material, rect, textureScale, textureOffset, textureRotation, rotateInLocalSpace, _sortingOrder, disposalManager);
            GameObject surf = surfRenderer.gameObject;
            _lastVertexCount += 4;
            surf.transform.SetParent(surfacesLayer.transform, false);
            surfaces[cacheIndex] = surf;
            surf.transform.localPosition = Misc.Vector3zero;
            surf.layer = gameObject.layer;
            region.renderer = surfRenderer;
            return surf;
        }

        Rect GetRectForTexturedRegion (Region region, Material material, bool isCanvasTexture) {
            Texture2D canvasTexture = this.canvasTexture;
            if (isCanvasTexture) {
                canvasTexture = material.mainTexture as Texture2D;
            }
            return (canvasTexture != null && material != null && material.mainTexture == canvasTexture) ? canvasRect : region.rect2D;
        }

        GameObject GenerateRegionSurfaceFloatingQuad (Region region, int cacheIndex, Material material, Vector2 textureScale, Vector2 textureOffset, float textureRotation, bool rotateInLocalSpace, bool isCanvasTexture) {

            // Calculate & optimize mesh data
            PrepareNewSurfaceMesh(4);
            Vector2 min = region.rect2D.min;
            Vector2 max = region.rect2D.max;
            if (_gridNormalOffset > 0 && _terrainWrapper != null) {
                IList<TriangulationPoint> points = new TriangulationPoint[4];
                min.x += 2f;
                min.y += 2f;
                max.x += 2f;
                max.y += 2f;
                points[0] = new TriangulationPoint(min.x, max.y);
                points[1] = new TriangulationPoint(min.x, min.y);
                points[2] = new TriangulationPoint(max.x, min.y);
                points[3] = new TriangulationPoint(max.x, max.y);
                for (int k = 0; k < 4; k++) {
                    AddPointToSurfaceMeshWithNormalOffsetPrimitive(points[k]);
                }
            }
            else {
                meshPoints.Add(new Vector3(min.x, max.y, 0));
                meshPoints.Add(new Vector3(min.x, min.y, 0));
                meshPoints.Add(new Vector3(max.x, min.y, 0));
                meshPoints.Add(new Vector3(max.x, max.y, 0));
            }
            Rect rect = GetRectForTexturedRegion(region, material, isCanvasTexture);
            Renderer surfRenderer = Drawing.CreateSurface(cacheIndex.ToString(), meshPoints, quadIndices, material, rect, textureScale, textureOffset, textureRotation, rotateInLocalSpace, _sortingOrder, disposalManager, cellTileScale, cellTileOffset);
            GameObject surf = surfRenderer.gameObject;
            _lastVertexCount += 4;
            surf.transform.SetParent(surfacesLayer.transform, false);
            surfaces[cacheIndex] = surf;
            surf.transform.localPosition = Misc.Vector3zero;
            surf.layer = gameObject.layer;
            region.renderer = surfRenderer;
            return surf;
        }

        GameObject ExtrudeGameObject (GameObject go, float extrusionAmount, PolygonPoint[] polygonPoints, int polygonPointsCount) {

            if (!go.TryGetComponent<MeshFilter>(out MeshFilter mf)) return null;
            Mesh mesh = mf.sharedMesh;
            mesh.GetVertices(tempPoints);
            mesh.GetUVs(0, tempUVs);
            mesh.GetIndices(tempIndices, 0);

            int vertexCount = tempPoints.Count;
            // add top face vertices
            for (int k = 0; k < vertexCount; k++) {
                Vector3 t = tempPoints[k];
                t.z -= extrusionAmount;
                tempPoints.Add(t);
                tempUVs.Add(tempUVs[k]);
            }
            // add top face and swap indices of bottom face
            int indicesCount = tempIndices.Count;
            for (int k = 0; k < indicesCount; k += 3) {
                int i = tempIndices[k];
                int j = tempIndices[k + 1];
                int l = tempIndices[k + 2];
                // add top face
                tempIndices.Add(i + vertexCount);
                tempIndices.Add(j + vertexCount);
                tempIndices.Add(l + vertexCount);
                // swap indices of bottom face
                tempIndices[k] = j;
                tempIndices[k + 1] = i;
            }
            // add side triangles
            int sideBaseIndex = tempPoints.Count;
            for (int k = 0; k < polygonPointsCount; k++) {
                PolygonPoint pp = polygonPoints[k];
                Vector3 p = new Vector3(pp.Xf, pp.Yf, pp.Zf);
                FitPointToSurface(ref p);
                tempPoints.Add(p);
                tempUVs.Add(Misc.Vector3zero);
                p.z -= extrusionAmount;
                tempPoints.Add(p);
                tempUVs.Add(Misc.Vector3zero);
            }
            int sidePolyCount = polygonPointsCount * 2;
            for (int k = 0; k < polygonPointsCount; k++) {
                int i = k * 2;
                tempIndices.Add(sideBaseIndex + i);
                tempIndices.Add(sideBaseIndex + ((i + 1) % sidePolyCount));
                tempIndices.Add(sideBaseIndex + ((i + 2) % sidePolyCount));

                tempIndices.Add(sideBaseIndex + ((i + 3) % sidePolyCount));
                tempIndices.Add(sideBaseIndex + ((i + 2) % sidePolyCount));
                tempIndices.Add(sideBaseIndex + ((i + 1) % sidePolyCount));
            }

            // create extruded object
            Mesh extrudedMesh = Instantiate(mesh);
            extrudedMesh.name = "Extruded mesh";
            extrudedMesh.SetVertices(tempPoints);
            extrudedMesh.SetUVs(0, tempUVs);
            extrudedMesh.SetIndices(tempIndices, MeshTopology.Triangles, 0);
            GameObject extruded = Instantiate(go);
            extruded.name = go.name + " Extruded";
            extruded.transform.position = go.transform.position;
            extruded.transform.rotation = go.transform.rotation;
            extruded.transform.localScale = go.transform.lossyScale;
            extruded.GetComponent<MeshFilter>().mesh = extrudedMesh;

            return extruded;
        }


        void FitPointToSurface (ref Vector3 p) {
            p.x = p.x - 2f;
            p.y = p.y - 2f;
            if (_terrainWrapper == null) return;
            p.z = -p.z;
            if (_gridNormalOffset > 0) {
                p += transform.InverseTransformVector(_terrainWrapper.GetInterpolatedNormal(p.x + 0.5f, p.y + 0.5f)) * _gridNormalOffset;
            }
            if (_cellsMinimumAltitudeClampVertices && _cellsMinimumAltitude != 0) {
                float localMinima = -(_cellsMinimumAltitude - _terrainWrapper.bounds.min.y) / _terrainWrapper.transform.lossyScale.y;
                if (p.z > localMinima) {
                    p.z = localMinima;
                }
            }
            if (_cellsMaximumAltitudeClampVertices && _cellsMaximumAltitude != 0) {
                float localMaxima = -(_cellsMaximumAltitude - _terrainWrapper.bounds.min.y) / _terrainWrapper.transform.lossyScale.y;
                if (p.z < localMaxima) {
                    p.z = localMaxima;
                }
            }
        }

        #endregion


        #region Internal API

        int GetFittedSizeForHeightmap (int value) {
            value = (int)Mathf.Log(value, 2);
            value = (int)Mathf.Pow(2, value);
            return value + 1;
        }


        public string GetMapData () {
            return "";
        }

        float goodGridRelaxation {
            get {
                if (_numCells > MAX_CELLS_FOR_RELAXATION) {
                    return 1;
                }
                else {
                    return _gridRelaxation;
                }
            }
        }

        float goodGridCurvature {
            get {
                if (_numCells > MAX_CELLS_FOR_CURVATURE) {
                    return 0;
                }
                else {
                    return _gridCurvature;
                }
            }
        }

        /// <summary>
        /// Issues a selection check based on a given ray. Used by editor to manipulate cells from Scene window.
        /// Returns true if ray hits the grid.
        /// </summary>
        public bool CheckRay (Ray ray) {
            useEditorRay = true;
            editorRay = ray;
            return CheckMousePos();
        }


        #endregion


        #region Highlighting

        // The new input system does not call OnMouseEnter/Exit so we manually check this here
        void CheckMouseIsOver () {
            // Make sure it's outside of grid
            Vector3 mousePos = input.mousePosition;
            if (cameraMain != null) {
                Ray ray = cameraMain.ScreenPointToRay(mousePos);
                int hitCount = Physics.RaycastNonAlloc(ray, hits);
                if (hitCount > 0) {
                    for (int k = 0; k < hitCount; k++) {
                        GameObject hitGO = hits[k].collider.gameObject;
                        if (hitGO == gameObject || (terrain != null && terrain.includesGameObject(hitGO))) {
                            if (!mouseIsOver) {
                                PointerEnters();
                            }
                            return;
                        }
                    }
                }
            }
            if (mouseIsOver) {
                PointerExits();
            }
        }

        // Old input system support (OnMouseEnter/OnMouseExit)
        void OnMouseEnter () {
            PointerEnters();
        }

        void OnMouseExit () {
            // Make sure it's outside of grid
            Vector3 mousePos = input.mousePosition;
            Ray ray = cameraMain.ScreenPointToRay(mousePos);
            int hitCount = Physics.RaycastNonAlloc(ray, hits);
            if (hitCount > 0) {
                for (int k = 0; k < hitCount; k++) {
                    if (hits[k].collider.gameObject == gameObject)
                        return;
                }
            }
            PointerExits();
        }

        void PointerEnters () {
            mouseIsOver = true;
            ClearLastOver();
        }

        void PointerExits () {
            mouseIsOver = false;
            ClearLastOver();
        }

        public void ClearHighlights () {
            HideTerritoryRegionHighlight();
            ClearLastOver();
        }

        void ClearLastOver () {
            NotifyExitingEntities();
            _cellLastOver = null;
            _cellLastOverIndex = -1;
            _territoryLastOverIndex = -1;
            _territoryRegionLastOverIndex = -1;
        }

        void NotifyExitingEntities () {
            if (_territoryLastOverIndex >= 0 && OnTerritoryExit != null)
                OnTerritoryExit(this, _territoryLastOverIndex);
            if (_cellLastOverIndex >= 0 && OnCellExit != null)
                OnCellExit(this, _cellLastOverIndex);
        }


        /// <summary>
        /// When using grid on a group of gameobjects, if the parent is not a mesh, it won't hold a collider so this method check collision with the grid plane
        /// </summary>
        /// <param name="localPoint"></param>
        /// <returns></returns>
        bool GetBaseLocalHitFromMousePos (Ray ray, out Vector3 localPoint, out Vector3 worldPosition) {
            localPoint = Misc.Vector3zero;
            worldPosition = Misc.Vector3zero;

            if (_terrainWrapper == null) return false; // returns if grid is not over custom gameobjects

            // intersection with grid plane
            Plane gridPlane = new Plane(transform.forward, transform.position);
            if (gridPlane.Raycast(ray, out float enter)) {
                worldPosition = ray.origin + ray.direction * enter;
                localPoint = transform.InverseTransformPoint(worldPosition);
                return (localPoint.x >= -0.5f && localPoint.x <= 0.5f && localPoint.y >= -0.5f && localPoint.y <= 0.5f);
            }
            return false;
        }

        bool GetLocalHitFromMousePos (out Vector3 localPoint, out Vector3 worldPosition, out Vector3 mousePos) {

            localPoint = Misc.Vector3zero;
            mousePos = Misc.Vector3zero;
            worldPosition = Misc.Vector3zero;
            if (cameraMain == null) return false;

            Ray ray;
            if (useEditorRay && !Application.isPlaying) {
                ray = editorRay;
            }
            else {
                mousePos = input.mousePosition;
                if (mousePos.x < 0 || mousePos.x > Screen.width || mousePos.y < 0 || mousePos.y > Screen.height) {
                    return false;
                }
                ray = cameraMain.ScreenPointToRay(mousePos);
            }

            int hitCount = Physics.RaycastNonAlloc(ray, hits, cameraMain.farClipPlane);
            if (hitCount > 0) {
                if (_terrainWrapper != null) {
                    float minDistance = float.MaxValue;
                    Vector3 bestPoint = Vector3.zero;
                    for (int k = 0; k < hitCount; k++) {
                        GameObject o = hits[k].collider.gameObject;
                        if (_terrainWrapper.Contains(o)) {
                            float distance = Vector3.Distance(ray.origin, hits[k].point);
                            if (distance >= _highlightMinimumTerrainDistance && distance < minDistance) {
                                minDistance = distance;
                                bestPoint = hits[k].point;
                            }
                        }
                    }
                    if (minDistance < float.MaxValue) {
                        if (_blockingMask != 0 && IsRayBlocked(ray, minDistance)) return false;
                        worldPosition = bestPoint;
                        localPoint = transform.InverseTransformPoint(bestPoint); // this method supports grid rotation on a terrain // _terrainWrapper.GetLocalPoint(o, hits[k].point);
                        return true;
                    }
                }
                else {
                    for (int k = 0; k < hitCount; k++) {
                        if (hits[k].collider.gameObject == gameObject) {
                            if (_blockingMask != 0 && IsRayBlocked(ray, Vector3.Distance(ray.origin, hits[k].point))) return false;
                            worldPosition = hits[k].point;
                            localPoint = transform.InverseTransformPoint(worldPosition);
                            return true;
                        }
                    }
                }
            }
            if (IsRayBlocked(ray, float.MaxValue)) return false;
            return GetBaseLocalHitFromMousePos(ray, out localPoint, out worldPosition);
        }

        bool IsRayBlocked (Ray ray, float maxDistance) {
            int mask = _blockingMask & ~(1 << gameObject.layer);
            return Physics.Raycast(ray, maxDistance, mask);
        }

        bool GetLocalHitFromWorldPosition (ref Vector3 position) {
            if (_terrainWrapper != null) {
                Ray ray = new Ray(position - transform.forward * 100, transform.forward);
                int hitCount = Physics.RaycastNonAlloc(ray, hits);
                bool goodHit = false;
                if (hitCount > 0) {
                    for (int k = 0; k < hitCount; k++) {
                        if (hits[k].collider.gameObject == _terrainWrapper.gameObject) {
                            position = transform.InverseTransformPoint(hits[k].point);  // this method supports grid rotation on a terrain // _terrainWrapper.GetLocalPoint(hits[k].collider.gameObject, hits[k].point);
                            goodHit = true;
                            break;
                        }
                    }
                }
                if (!goodHit) {
                    // defaults to base grid
                    position = transform.InverseTransformPoint(position);
                    return (position.x >= -0.5f && position.x <= 0.5f && position.y >= -0.5f && position.y <= 0.5f);
                }
            }
            else {
                position = transform.InverseTransformPoint(position);
            }
            return true;
        }

        bool CheckMousePos () {
            HighlightMode currentHighlightMode = _highlightMode;
#if UNITY_EDITOR
            if (!Application.isPlaying && useEditorRay) {
                currentHighlightMode = HighlightMode.Cells;
            }
#endif

            bool goodHit = GetLocalHitFromMousePos(out localCursorPos, out worldCursorPos, out screenCursorPos);
#if UNITY_EDITOR
            if (!Application.isPlaying && !_enableGridEditor) {
                goodHit = false;
            }
#endif
            if (!goodHit) {
                NotifyExitingEntities();
                ClearHighlights();
                return false;
            }

            // verify if last highlighted territory remains active
            bool sameTerritoryRegionHighlight = false;
            float sameTerritoryRegionArea = float.MaxValue;
            if (_territoryRegionLastOver != null) {
                if (_territoryLastOver.visible && _territoryRegionLastOver.Contains(localCursorPos.x, localCursorPos.y)) {
                    sameTerritoryRegionHighlight = true;
                    sameTerritoryRegionArea = _territoryRegionLastOver.rect2DArea;
                }
            }
            int newTerritoryHighlightedIndex = -1;
            int newTerritoryRegionHighlightedIndex = -1;

            // mouse if over the grid - verify if hitPos is inside any territory polygon
            if (territories != null) {
                int sortedTerrRegionsCount = sortedTerritoriesRegions.Count;
                for (int r = 0; r < sortedTerrRegionsCount; r++) {
                    Region sreg = _sortedTerritoriesRegions[r];
                    Territory sterr = (Territory)sreg.entity;
                    if (sreg != null && sterr.visible && sterr.cells != null && sterr.cells.Count > 0) {
                        if (sreg.Contains(localCursorPos.x, localCursorPos.y)) {
                            newTerritoryHighlightedIndex = TerritoryGetIndex(sterr);
                            newTerritoryRegionHighlightedIndex = sterr.regions.IndexOf(sreg);
                            sameTerritoryRegionHighlight = newTerritoryHighlightedIndex == _territoryLastOverIndex && newTerritoryRegionHighlightedIndex == _territoryRegionLastOverIndex;
                            break;
                        }
                        if (sreg.rect2DArea > sameTerritoryRegionArea) {
                            break;
                        }
                    }
                }
            }

            if (!sameTerritoryRegionHighlight) {
                if (_territoryLastOverIndex >= 0 && OnTerritoryExit != null) {
                    OnTerritoryExit(this, _territoryLastOverIndex);
                }
                if (_territoryLastOverIndex >= 0 && _territoryRegionLastOverIndex >= 0 && OnTerritoryRegionExit != null) {
                    OnTerritoryRegionExit(this, _territoryLastOverIndex, _territoryRegionLastOverIndex);
                }
                if (newTerritoryHighlightedIndex >= 0 && OnTerritoryEnter != null) {
                    OnTerritoryEnter(this, newTerritoryHighlightedIndex);
                }
                if (newTerritoryHighlightedIndex >= 0 && newTerritoryRegionHighlightedIndex >= 0 && OnTerritoryRegionEnter != null) {
                    OnTerritoryRegionEnter(this, newTerritoryHighlightedIndex, newTerritoryRegionHighlightedIndex);
                }
                _territoryLastOverIndex = newTerritoryHighlightedIndex;
                _territoryRegionLastOverIndex = newTerritoryRegionHighlightedIndex;
            }

            // verify if last highlited cell remains active
            bool sameCellHighlight = false;
            if (_cellLastOver != null) {
                if (_cellLastOver.region.Contains(localCursorPos.x, localCursorPos.y)) {
                    sameCellHighlight = true;
                }
            }
            int newCellHighlightedIndex = -1;

            if (!sameCellHighlight) {
                Cell newCellHighlighted = GetCellAtPoint(localCursorPos, false, _territoryLastOverIndex);
                if (newCellHighlighted != null) {
                    newCellHighlightedIndex = CellGetIndex(newCellHighlighted);
                }
            }

            if (!sameCellHighlight) {
                if (_cellLastOverIndex >= 0 && OnCellExit != null)
                    OnCellExit(this, _cellLastOverIndex);
                if (newCellHighlightedIndex >= 0 && OnCellEnter != null)
                    OnCellEnter(this, newCellHighlightedIndex);
                _cellLastOverIndex = newCellHighlightedIndex;
                if (newCellHighlightedIndex >= 0)
                    _cellLastOver = cells[newCellHighlightedIndex];
                else
                    _cellLastOver = null;
            }

            if (currentHighlightMode == HighlightMode.Cells) {
                if (!sameCellHighlight) {
                    if (newCellHighlightedIndex >= 0 && (cells[newCellHighlightedIndex].visible || _cellHighlightNonVisible)) {
                        HighlightCell(newCellHighlightedIndex, false);
                    }
                    else {
                        HideCellHighlight();
                    }
                }
            }
            else if (currentHighlightMode == HighlightMode.Territories) {
                if (!sameTerritoryRegionHighlight) {
                    if (newTerritoryHighlightedIndex >= 0 && territories[newTerritoryHighlightedIndex].visible && newTerritoryRegionHighlightedIndex >= 0) {
                        HighlightTerritoryRegion(newTerritoryHighlightedIndex, false, newTerritoryRegionHighlightedIndex);
                    }
                    else {
                        HideTerritoryRegionHighlight();
                    }
                }
            }

            return true;
        }

        void UpdateHighlightFade () {
            if (_highlightFadeAmount == 0)
                return;

            if (_highlightedObj != null) {
                float newAlpha = _highlightFadeMin + Mathf.PingPong(Time.time * _highlightFadeSpeed - highlightFadeStart, _highlightFadeAmount - _highlightFadeMin);
                Material mat = highlightMode == HighlightMode.Territories ? hudMatTerritory : hudMatCell;
                mat.SetFloat(ShaderParams.FadeAmount, newAlpha);
                float newScale = _highlightScaleMin + Mathf.PingPong(Time.time * highlightFadeSpeed, _highlightScaleMax - _highlightScaleMin);
                mat.SetFloat(ShaderParams.Scale, 1f / (newScale + 0.0001f));
                if (_highlightedObj.TryGetComponent<Renderer>(out Renderer renderer)) {
                    renderer.sharedMaterial = mat;
                }
            }
        }

        void GatherInput () {
            leftButtonDown = input.GetMouseButtonDown(0);
            rightButtonDown = input.GetMouseButtonDown(1);
            leftButtonUp = input.GetMouseButtonUp(0);
            rightButtonUp = input.GetMouseButtonUp(1);

            if (leftButtonUp || leftButtonDown) {
                buttonIndex = 0;
            }
            else if (rightButtonUp || rightButtonDown) {
                buttonIndex = 1;
            }
            else {
                buttonIndex = -1;
            }
        }
        void TriggerEvents () {

            if (leftButtonDown || rightButtonDown) {
                // record last clicked cell/territory
                _cellLastClickedIndex = _cellLastOverIndex;
                _territoryLastClickedIndex = _territoryLastOverIndex;
                _territoryRegionLastClickedIndex = _territoryRegionLastOverIndex;
                localCursorLastClickedPos = localCursorPos;
                screenCursorLastClickedPos = screenCursorPos;

                if (_territoryLastOverIndex >= 0 && OnTerritoryMouseDown != null) {
                    OnTerritoryMouseDown(this, _territoryLastOverIndex, _territoryRegionLastOverIndex, buttonIndex);
                }
                if (_territoryLastOverIndex >= 0 && _territoryRegionLastOverIndex >= 0 && OnTerritoryRegionMouseDown != null) {
                    OnTerritoryRegionMouseDown(this, _territoryLastOverIndex, _territoryRegionLastOverIndex, buttonIndex);
                }
                if (_cellLastOverIndex >= 0 && OnCellMouseDown != null) {
                    OnCellMouseDown(this, _cellLastOverIndex, buttonIndex);
                }
                if (_cellLastClickedIndex >= 0) {
                    dragging = DragStatus.CanDrag;
                }
                if (OnMouseDown != null) {
                    OnMouseDown(this, worldCursorPos);
                }
            }
            else if (leftButtonUp || rightButtonUp) {
                if (_territoryLastOverIndex >= 0) {
                    if (_territoryLastOverIndex == _territoryLastClickedIndex && OnTerritoryClick != null) {
                        OnTerritoryClick(this, _territoryLastOverIndex, _territoryRegionLastOverIndex, buttonIndex);
                    }
                    if (_territoryLastOverIndex == _territoryLastClickedIndex && _territoryRegionLastOverIndex == _territoryRegionLastClickedIndex && OnTerritoryRegionClick != null) {
                        OnTerritoryRegionClick(this, _territoryLastOverIndex, _territoryRegionLastOverIndex, buttonIndex);
                    }
                    if (OnTerritoryMouseUp != null) {
                        OnTerritoryMouseUp(this, _territoryLastOverIndex, _territoryRegionLastOverIndex, buttonIndex);
                    }
                    if (OnTerritoryRegionMouseUp != null) {
                        OnTerritoryRegionMouseUp(this, _territoryLastOverIndex, _territoryRegionLastOverIndex, buttonIndex);
                    }
                }
                if (_cellLastOverIndex >= 0) {
                    if (_cellLastOverIndex == _cellLastClickedIndex && OnCellClick != null) {
                        OnCellClick(this, _cellLastOverIndex, buttonIndex);
                    }
                    if (OnCellMouseUp != null) {
                        OnCellMouseUp(this, _cellLastOverIndex, buttonIndex);
                    }
                }
                if (dragging == DragStatus.Dragging) {
                    if (OnCellDragEnd != null) OnCellDragEnd(this, _cellLastClickedIndex, _cellLastOverIndex);
                    dragging = DragStatus.Idle;
                }
                if (_cellLastOverIndex == _cellLastClickedIndex && OnMouseClick != null) {
                    OnMouseClick(this, worldCursorPos);
                }
                if (OnMouseUp != null) {
                    OnMouseUp(this, worldCursorPos);
                }
            }
            else {
                bool leftButtonDown = input.GetMouseButton(0);
                if (leftButtonDown && screenCursorLastClickedPos != screenCursorPos) {
                    if (dragging == DragStatus.Dragging) {
                        if (OnCellDrag != null) {
                            OnCellDrag(this, _cellLastClickedIndex, _cellLastOverIndex);
                        }
                    }
                    else if (dragging == DragStatus.CanDrag) {
                        dragging = DragStatus.Dragging;
                        if (OnCellDragStart != null) {
                            OnCellDragStart(this, _cellLastClickedIndex);
                        }
                    }

                }
            }
        }

        #endregion


        #region Geometric functions

        Vector3 GetWorldSpacePosition (Vector2 localPosition, float elevation = 0) {
            if (_terrainWrapper != null) {
                Vector3 wPos = transform.TransformPoint(localPosition);
                wPos.y += _terrainWrapper.GetInterpolatedHeight(wPos) * _terrainWrapper.transform.lossyScale.y;
                wPos.y += elevation;
                return wPos;
            }
            Vector3 pos = localPosition;
            pos.z = -elevation;
            return transform.TransformPoint(pos);
        }

        Vector3 GetLocalSpacePosition (Vector3 worldPosition) {
            return transform.InverseTransformPoint(worldPosition);
        }

        Vector3 GetScaledVector (Vector3 p) {
            p.x *= _gridScale.x;
            p.x += _gridCenter.x;
            p.y *= _gridScale.y;
            p.y += _gridCenter.y;
            return p;
        }

        Vector2 GetScaledVector (Vector2 p) {
            p.x *= _gridScale.x;
            p.x += _gridCenter.x;
            p.y *= _gridScale.y;
            p.y += _gridCenter.y;
            return p;
        }

        Vector3 GetScaledVector (Point q) {
            Vector3 p;
            p.x = (float)q.x * _gridScale.x;
            p.x += _gridCenter.x;
            p.y = (float)q.y * _gridScale.y;
            p.y += _gridCenter.y;
            p.z = 0;
            return p;
        }

        Vector3 GetScaledVector (double x, double y) {
            Vector3 p;
            p.x = (float)x * _gridScale.x;
            p.x += _gridCenter.x;
            p.y = (float)y * _gridScale.y;
            p.y += _gridCenter.y;
            p.z = 0;
            return p;
        }


        void GetUnscaledVector (ref Vector2 p) {
            p.x -= _gridCenter.x;
            p.x /= _gridScale.x;
            p.y -= _gridCenter.y;
            p.y /= _gridScale.y;
        }

        Point GetScaledPoint (Point p) {
            p.x *= _gridScale.x;
            p.x += _gridCenter.x;
            p.y *= _gridScale.y;
            p.y += _gridCenter.y;
            return p;
        }

        Segment GetScaledSegment (Segment s) {
            Segment ss = new Segment(s.start, s.end, s.border);
            ss.start = GetScaledPoint(ss.start);
            ss.end = GetScaledPoint(ss.end);
            return ss;
        }


        #endregion



        #region Territory stuff

        public void HideTerritoryRegionHighlight () {
            if (_territoryRegionHighlighted == null || _highlightMode != HighlightMode.Territories)
                return;
            if (_highlightedObj != null) {
                if (_territoryRegionHighlighted.customMaterial != null) {
                    ApplyMaterialToSurface(_territoryRegionHighlighted, _territoryRegionHighlighted.customMaterial);
                }
                else if (!_highlightedObj.TryGetComponent<SurfaceFader>(out _)) {
                    _highlightedObj.SetActive(false);
                }
                if (!_territoryHighlighted.visible) {
                    _highlightedObj.SetActive(false);
                }
                _highlightedObj = null;
            }
            _territoryRegionHighlighted = null;
            _territoryHighlightedIndex = -1;
            _territoryHighlightedRegionIndex = -1;
        }

        /// <summary>
        /// Highlights the territory region specified. Returns the generated highlight surface gameObject.
        /// Internally used by the Map UI and the Editor component, but you can use it as well to temporarily mark a territory region.
        /// </summary>
        /// <param name="refreshGeometry">Pass true only if you're sure you want to force refresh the geometry of the highlight (for instance, if the frontiers data has changed). If you're unsure, pass false.</param>
        public GameObject HighlightTerritoryRegion (int territoryIndex, bool refreshGeometry, int regionIndex = 0) {
            if (_territoryHighlighted != null)
                HideTerritoryRegionHighlight();
            if (!ValidTerritoryIndex(territoryIndex, regionIndex))
                return null;

            if (_highlightEffect != HighlightEffect.None) {
                if (OnTerritoryHighlight != null) {
                    bool cancelHighlight = false;
                    OnTerritoryHighlight(this, territoryIndex, ref cancelHighlight);
                    if (cancelHighlight)
                        return null;
                }

                int cacheIndex = GetCacheIndexForTerritoryRegion(territoryIndex, regionIndex);
                GameObject obj;
                bool existsInCache = surfaces.TryGetValue(cacheIndex, out obj) && obj != null;
                if (refreshGeometry && existsInCache) {
                    surfaces.Remove(cacheIndex);
                    DestroyImmediate(obj);
                    existsInCache = false;
                }
                if (existsInCache) {
                    _highlightedObj = surfaces[cacheIndex];
                    if (_highlightedObj == null) {
                        surfaces.Remove(cacheIndex);
                    }
                    else {
                        if (!_highlightedObj.activeSelf)
                            _highlightedObj.SetActive(true);
                        Renderer rr = _highlightedObj.GetComponent<Renderer>();
                        if (rr.sharedMaterial != hudMatTerritory)
                            rr.sharedMaterial = hudMatTerritory;
                    }
                }
                else {
                    _highlightedObj = GenerateTerritoryRegionSurface(territoryIndex, hudMatTerritory, Misc.Vector2one, Misc.Vector2zero, 0, false, regionIndex);
                }
                // Reuse territory texture
                Territory territory = territories[territoryIndex];
                if (territory.regions[regionIndex].customMaterial != null) {
                    if (hudMatTerritory.HasProperty(ShaderParams.MainTex)) {
                        hudMatTerritory.mainTexture = territory.regions[regionIndex].customMaterial.mainTexture;
                    }
                    else {
                        hudMatTerritory.mainTexture = null;
                    }
                }
                highlightFadeStart = Time.time;
            }

            _territoryHighlightedIndex = territoryIndex;
            _territoryHighlightedRegionIndex = regionIndex;
            _territoryRegionHighlighted = territories[territoryIndex].regions[regionIndex];

            return _highlightedObj;
        }

        GameObject GenerateTerritoryRegionSurface (int territoryIndex, Material material, Vector2 textureScale, Vector2 textureOffset, float textureRotation, bool rotateInLocalSpace, int regionIndex = 0, bool isCanvasTexture = false) {
            if (!ValidTerritoryIndex(territoryIndex, regionIndex))
                return null;

            Region region = territories[territoryIndex].regions[regionIndex];
            int cacheIndex = GetCacheIndexForTerritoryRegion(territoryIndex, regionIndex);
            return GenerateRegionSurface(region, cacheIndex, material, textureScale, textureOffset, textureRotation, rotateInLocalSpace, CELL_TEXTURE_MODE.InTile, isCanvasTexture);
        }

        void UpdateColorizedTerritoriesAlpha () {
            int territoriesCount = territories.Count;
            for (int c = 0; c < territoriesCount; c++) {
                Territory territory = territories[c];
                for (int r = 0; r < territory.regions.Count; r++) {
                    int cacheIndex = GetCacheIndexForTerritoryRegion(c, r);
                    GameObject surf;
                    if (surfaces.TryGetValue(cacheIndex, out surf)) {
                        if (surf != null) {
                            Material mat = surf.GetComponent<Renderer>().sharedMaterial;
                            Color newColor = mat.color;
                            newColor.a = territory.fillColor.a * _colorizedTerritoriesAlpha;
                            mat.color = newColor;
                        }
                    }
                }
            }
        }

        Territory GetTerritoryAtPoint (Vector3 position, bool worldSpace) {
            if (worldSpace) {
                if (!GetLocalHitFromWorldPosition(ref position))
                    return null;
            }
            int territoriesCount = territories.Count;
            for (int p = 0; p < territoriesCount; p++) {
                Territory territory = territories[p];
                for (int r = 0; r < territory.regions.Count; r++) {
                    if (territory.regions[r].Contains(position.x, position.y)) {
                        return territory;
                    }
                }
            }
            return null;
        }

        void TerritoryAnimate (FaderStyle style, int territoryIndex, Color color, float duration, int repetitions, int regionIndex = 0) {
            if (!ValidTerritoryIndex(territoryIndex, regionIndex)) return;

            GameObject territorySurface = TerritoryGetGameObject(territoryIndex, regionIndex);

            GameObject animatedSurface = new GameObject("SurfaceFader", typeof(MeshFilter), typeof(MeshRenderer));
            animatedSurface.transform.SetParent(territorySurface.transform.parent, false);
            MeshFilter mf = animatedSurface.GetComponent<MeshFilter>();
            mf.sharedMesh = territorySurface.GetComponent<MeshFilter>().sharedMesh;
            Material fadeMaterial = materialPool.Get(fadeMat);
            fadeMaterial.color = Misc.ColorNull;
            animatedSurface.GetComponent<MeshRenderer>().sharedMaterial = fadeMaterial;

            int cacheIndex = GetCacheIndexForTerritoryRegion(territoryIndex, regionIndex);
            SurfaceFader.Animate(cacheIndex, style, this, animatedSurface, fadeMaterial, color, duration, repetitions);
        }


        void TerritoryCancelAnimation (int territoryIndex, float fadeOutDuration, int regionIndex = 0) {
            if (territoryIndex < 0 || territoryIndex >= territories.Count)
                return;
            int cacheIndex = GetCacheIndexForTerritoryRegion(territoryIndex, regionIndex);
            transform.GetComponentsInChildren(tempFaders);
            int count = tempFaders.Count;
            for (int k = 0; k < count; k++) {
                SurfaceFader fader = tempFaders[k];
                if (fader.cacheIndex == cacheIndex) {
                    fader.Finish(fadeOutDuration);
                }
            }
        }

        bool ValidTerritoryIndex (int territoryIndex) {
            return territoryIndex >= 0 && territoryIndex < territories.Count;
        }

        bool ValidTerritoryIndex (int territoryIndex, int regionIndex) {
            return territoryIndex >= 0 && territoryIndex < territories.Count && regionIndex >= 0 && regionIndex < territories[territoryIndex].regions.Count;
        }

        #endregion


        #region Cell stuff

        int GetCacheIndexForCellRegion (int cellIndex) {
            return 10000000 + cellIndex;
        }


        /// <summary>
        /// Highlights the specified cell. Returns the generated highlight surface gameObject.
        /// Internally used by the Map UI and the Editor component, but you can use it as well to temporarily mark a territory region.
        /// </summary>
        /// <param name="refreshGeometry">Pass true only if you're sure you want to force refresh the geometry of the highlight (for instance, if the frontiers data has changed). If you're unsure, pass false.</param>
        public GameObject HighlightCell (int cellIndex, bool refreshGeometry) {
            if (_cellHighlighted != null) {
                HideCellHighlight();
            }
            if (!ValidCellIndex(cellIndex)) return null;

            Cell cell = cells[cellIndex];

            if (_highlightEffect != HighlightEffect.None) {

                if (OnCellHighlight != null) {
                    bool cancelHighlight = false;
                    OnCellHighlight(this, cellIndex, ref cancelHighlight);
                    if (cancelHighlight)
                        return null;
                }

                _highlightedObj = null;
                if (_gridTopology != GridTopology.Irregular && _terrainWrapper == null) {
                    // optimization: try reusing a primitive cell shape
                    int expectedPointCount = _gridTopology == GridTopology.Hexagonal ? 6 : 4;
                    if (cell.region.points.Count == expectedPointCount) { // merged cells can't use this optimization
                        if (_highlightedRefSurface == null) {
                            _highlightedRefSurface = GenerateRegionSurfaceHighlightSimpleShape(hudMatCell, false);
                        }
                        if (_highlightedRefSurface != null) {
                            Vector2 cellCenter = cell.scaledCenter;
                            Cell refCell = cells[0];
                            if (refCell == null) {
                                int cellsCount = cells.Count;
                                for (int k = 1; k < cellsCount; k++) {
                                    refCell = cells[k];
                                    if (refCell != null) break;
                                }
                            }
                            _highlightedObj = _highlightedRefSurface;
                            if (refCell != null) {
                                Vector2 cellCenterRef = refCell.scaledCenter;
                                _highlightedObj.transform.localPosition = cellCenter - cellCenterRef;
                            }
                            _highlightedObj.SetActive(true);
                        }
                    }
                }

                if (_highlightedObj == null) {
                    // try generating a surface with actual cell points
                    int cacheIndex = GetCacheIndexForCellRegion(cellIndex);
                    bool existsInCache = surfaces.TryGetValue(cacheIndex, out GameObject obj);
                    if (refreshGeometry && existsInCache) {
                        surfaces.Remove(cacheIndex);
                        DestroyImmediate(obj);
                        existsInCache = false;
                    }
                    if (existsInCache) {
                        _highlightedObj = surfaces[cacheIndex];
                        if (_highlightedObj != null) {
                            _highlightedObj.SetActive(true);
                            _highlightedObj.GetComponent<Renderer>().sharedMaterial = hudMatCell;
                        }
                        else {
                            surfaces.Remove(cacheIndex);
                        }
                    }
                    else {
                        _highlightedObj = GenerateCellRegionSurface(cellIndex, hudMatCell, Misc.Vector2one, Misc.Vector2zero, textureRotation: 0, rotateInLocalSpace: false, CELL_TEXTURE_MODE.InTile, isCanvasTexture: false);
                    }
                }

                hudMatCell.mainTexture = null;
                if (_cellTextureMode != CELL_TEXTURE_MODE.Floating || (_cellTileScale == Misc.Vector2one && _cellTileOffset == Misc.Vector2zero)) {
                    // Reuse cell texture
                    if (_cellHighlightUseCellColor) {
                        hudMatCell.color = _cellHighlightColor;
                    }
                    if (cell.region.customMaterial != null) {
                        if (cell.region.customMaterial.HasProperty(ShaderParams.MainTex)) {
                            hudMatCell.mainTexture = cell.region.customMaterial.mainTexture;
                        }
                        if (_cellHighlightUseCellColor) {
                            if (cell.region.customMaterial.HasProperty(ShaderParams.Color)) {
                                hudMatCell.color = cell.region.customMaterial.color;
                            }
                        }
                    }
                }

                if (_cellTextureMode == CELL_TEXTURE_MODE.Floating) {
                    ApplyCellFloatingOffset(cellIndex, _highlightedObj.transform);
                }

                highlightFadeStart = Time.time;
            }

            _cellHighlighted = cell;
            _cellHighlightedIndex = cellIndex;
            return _highlightedObj;
        }

        void ApplyCellFloatingOffset (int cellIndex, Transform transform) {
            Vector3 pos = transform.localPosition;
            pos.z = (cells.Count - cellIndex) * -0.00001f;
            transform.localPosition = pos;
        }


        /// <summary>
        /// Just hides the highlighted object. It doesn't release the highlighted cell.
        /// </summary>
        void HideCellHighlightObject () {
            if (_cellHighlighted != null) {
                if (_highlightedObj != null) {
                    if (cellHighlighted.region.customMaterial != null) {
                        ApplyMaterialToSurface(cellHighlighted.region, _cellHighlighted.region.customMaterial);
                    }
                    else if (_highlightedObj.GetComponent<SurfaceFader>() == null) {
                        _highlightedObj.SetActive(false);
                    }
                    if (!cellHighlighted.visible) {
                        _highlightedObj.SetActive(false);
                    }
                }
            }
        }

        public void HideCellHighlight () {
            if (_cellHighlighted == null)
                return;
            if (_highlightedObj != null) {
                if (cellHighlighted.region.customMaterial != null) {
                    ApplyMaterialToSurface(cellHighlighted.region, _cellHighlighted.region.customMaterial);
                }
                else if (_highlightedObj.GetComponent<SurfaceFader>() == null) {
                    _highlightedObj.SetActive(false);
                }
                if (!cellHighlighted.visible) {
                    _highlightedObj.SetActive(false);
                }
                if (_highlightedObj == _highlightedRefSurface) {
                    _highlightedObj.SetActive(false);
                }
                _highlightedObj = null;
            }
            _cellHighlighted = null;
            _cellHighlightedIndex = -1;
        }

        void SurfaceSegmentForSurface (Segment s, Connector connector) {

            // trace the line until roughness is exceeded
            double dist = s.magnitude;
            Point direction = s.end - s.start;
            int numSteps = (int)(dist / MIN_VERTEX_DISTANCE);
            if (numSteps <= 1) {
                connector.Add(s);
                return;
            }

            Point t0 = s.start;
            Vector3 wp_start = transform.TransformPoint(t0.vector3);
            float h0 = _terrainWrapper.GetInterpolatedHeight(wp_start);
            Point ta = t0;
            float h1;
            Vector3 wp_end = transform.TransformPoint(s.end.vector3);
            Vector3 wp_direction = wp_end - wp_start;
            for (int i = 1; i < numSteps; i++) {
                Point t1 = s.start + direction * ((double)i / numSteps);
                Vector3 v1 = wp_start + wp_direction * ((float)i / numSteps);
                h1 = _terrainWrapper.GetInterpolatedHeight(v1);
                if (h1 - h0 > maxRoughnessWorldSpace || h0 - h1 > maxRoughnessWorldSpace) {
                    if (t0 != ta) {
                        Segment s0 = new Segment(t0, ta, s.border);
                        connector.Add(s0);
                        Segment s1 = new Segment(ta, t1, s.border);
                        connector.Add(s1);
                    }
                    else {
                        Segment s0 = new Segment(t0, t1, s.border);
                        connector.Add(s0);
                    }
                    t0 = t1;
                    h0 = h1;
                }
                ta = t1;
            }
            // Add last point
            Segment finalSeg = new Segment(t0, s.end, s.border);
            connector.Add(finalSeg);
        }

        void GetFirstAndLastPointInRow (float y, int pointCount, out float first, out float last) {
            float minx = float.MaxValue;
            float maxx = float.MinValue;
            PolygonPoint p0 = tempPolyPoints[0], p1;
            for (int k = 1; k <= pointCount; k++) {
                if (k == pointCount) {
                    p1 = tempPolyPoints[0];
                }
                else {
                    p1 = tempPolyPoints[k];
                }
                // if line crosses the horizontal line
                if (p0.Yf >= y && p1.Yf <= y || p0.Yf <= y && p1.Yf >= y) {
                    float x;
                    if (p1.Xf == p0.Xf) {
                        x = p0.Xf;
                    }
                    else {
                        float a = (p1.Xf - p0.Xf) / (p1.Yf - p0.Yf);
                        x = p0.Xf + a * (y - p0.Yf);
                    }
                    if (x > maxx)
                        maxx = x;
                    if (x < minx)
                        minx = x;
                }
                p0 = p1;
            }
            first = minx - 2f;
            last = maxx - 2f;
        }

        bool IsTooNearPolygon (double x, double y, PolygonPoint[] tempPolyPoints, int pointsCount) {
            for (int j = pointsCount - 1; j >= 0; j--) {
                PolygonPoint p1 = tempPolyPoints[j];
                double dx = x - p1.mX;
                double dy = y - p1.mY;
                if (dx * dx + dy * dy < SQR_MIN_VERTEX_DIST) {
                    return true;
                }
            }
            return false;
        }

        GameObject GenerateCellRegionSurface (int cellIndex, Material material, Vector2 textureScale, Vector2 textureOffset, float textureRotation, bool rotateInLocalSpace, CELL_TEXTURE_MODE textureMode, bool isCanvasTexture) {
            if (!ValidCellIndex(cellIndex)) return null;
            Region region = cells[cellIndex].region;
            int cacheIndex = GetCacheIndexForCellRegion(cellIndex);
            return GenerateRegionSurface(region, cacheIndex, material, textureScale, textureOffset, textureRotation, rotateInLocalSpace, textureMode, isCanvasTexture);
        }

        Cell GetCellAtPoint (Vector3 position, bool worldSpace, int territoryIndex = -1) {
            // Compute local point
            if (worldSpace) {
                if (!GetLocalHitFromWorldPosition(ref position))
                    return null;
            }

            return GetCellAtLocalPoint(position.x, position.y, territoryIndex);
        }

        Cell GetCellAtLocalPoint (float x, float y, int territoryIndex = -1) {

            if (cells == null) return null;
            int cellsCount = cells.Count;
            if ((_gridTopology == GridTopology.Hexagonal || _gridTopology == GridTopology.Box) && cellsCount == _cellRowCount * _cellColumnCount) {
                float px = (x - _gridCenter.x) / _gridScale.x + 0.5f;
                float py = (y - _gridCenter.y) / _gridScale.y + 0.5f;
                int xc = (int)(_cellColumnCount * px);
                int yc = (int)(_cellRowCount * py);
                for (int j = yc - 1; j < yc + 2; j++) {
                    for (int k = xc - 1; k < xc + 2; k++) {
                        int index = j * _cellColumnCount + k;
                        if (index < 0 || index >= cellCount)
                            continue;
                        Cell cell = cells[index];
                        if (cell == null || cell.region == null || cell.region.points == null)
                            continue;
                        if (cell.region.Contains(x, y)) {
                            return cell;
                        }
                    }
                }
            }
            else {
                if (territoryIndex >= 0 && !ValidTerritoryIndex(territoryIndex))
                    return null;
                List<Cell> cells = territoryIndex >= 0 ? territories[territoryIndex].cells : sortedCells;
                cellsCount = cells.Count;
                for (int p = 0; p < cellsCount; p++) {
                    Cell cell = cells[p];
                    if (cell == null || cell.region == null || cell.region.points == null)
                        continue;
                    if (cell.region.Contains(x, y)) {
                        return cell;
                    }
                }
            }
            return null;
        }


        void Encapsulate (ref Rect r, Vector3 position) {
            if (position.x < r.xMin)
                r.xMin = position.x;
            if (position.x > r.xMax)
                r.xMax = position.x;
            if (position.y < r.yMin)
                r.yMin = position.y;
            if (position.y > r.yMax)
                r.yMax = position.y;
        }



        int GetCellInArea (Bounds bounds, List<int> cellIndices, float padding = 0, bool checkAllVertices = false) {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            min.x -= padding;
            min.y -= padding;
            min.z -= padding;
            max.x += padding;
            max.y += padding;
            max.z += padding;
            Rect rect = new Rect(transform.InverseTransformPoint(min), Misc.Vector2zero);
            Encapsulate(ref rect, transform.InverseTransformPoint(new Vector3(min.x, min.y, max.z)));
            Encapsulate(ref rect, transform.InverseTransformPoint(new Vector3(max.x, min.y, min.z)));
            Encapsulate(ref rect, transform.InverseTransformPoint(new Vector3(max.x, min.y, max.z)));
            Encapsulate(ref rect, transform.InverseTransformPoint(new Vector3(min.x, max.y, min.z)));
            Encapsulate(ref rect, transform.InverseTransformPoint(new Vector3(min.x, max.y, max.z)));
            Encapsulate(ref rect, transform.InverseTransformPoint(new Vector3(max.x, max.y, min.z)));
            Encapsulate(ref rect, transform.InverseTransformPoint(new Vector3(max.x, max.y, max.z)));
            return GetCellInArea(rect, cellIndices, checkAllVertices);
        }



        int GetCellInArea (Rect rect, List<int> cellIndices, bool checkAllVertices = false) {
            cellIndices.Clear();
            int cellCount = cells.Count;
            if (checkAllVertices) {
                for (int k = 0; k < cellCount; k++) {
                    Cell cell = cells[k];
                    if (cell != null) {
                        if (cell.scaledCenter.x >= rect.xMin && cell.scaledCenter.x <= rect.xMax && cell.scaledCenter.y >= rect.yMin && cell.scaledCenter.y <= rect.yMax) {
                            cellIndices.Add(k);
                            continue;
                        }
                        bool included = false;
                        int vertexCount = cell.region.points.Count;
                        for (int v = 0; v < vertexCount; v++) {
                            Vector3 vertexPos = cell.region.points[v];
                            if (vertexPos.x >= rect.xMin && vertexPos.x <= rect.xMax && vertexPos.y >= rect.yMin && vertexPos.y <= rect.yMax) {
                                cellIndices.Add(k);
                                included = true;
                                break;
                            }
                        }
                        if (included) continue;
                        // check if center of rect is inside the cell (cover case where the rect is smaller than the cell size)
                        Vector2 rectCenter = rect.center;
                        if (cell.region.Contains(rectCenter.x, rectCenter.y)) {
                            cellIndices.Add(k);
                        }
                    }
                }
            }
            else {
                for (int k = 0; k < cellCount; k++) {
                    Cell cell = cells[k];
                    if (cell != null) {
                        if (cell.scaledCenter.x >= rect.xMin && cell.scaledCenter.x <= rect.xMax && cell.scaledCenter.y >= rect.yMin && cell.scaledCenter.y <= rect.yMax) {
                            cellIndices.Add(k);
                        }
                    }
                }
            }
            return cellIndices.Count;
        }


        void CellAnimate (FaderStyle style, int cellIndex, Color initialColor, Color color, float duration, int repetitions) {
            if (!ValidCellIndex(cellIndex)) return;

            GameObject cellSurface = CellGetGameObject(cellIndex);
            if (cellSurface == null)
                return;

            GameObject animatedSurface = new GameObject("SurfaceFader", typeof(MeshFilter), typeof(MeshRenderer));
            animatedSurface.transform.SetParent(cellSurface.transform.parent, false);
            MeshFilter mf = animatedSurface.GetComponent<MeshFilter>();
            mf.sharedMesh = cellSurface.GetComponent<MeshFilter>().sharedMesh;
            Material fadeMaterial = materialPool.Get(fadeMat);
            fadeMaterial.color = initialColor;
            animatedSurface.GetComponent<MeshRenderer>().sharedMaterial = fadeMaterial;

            int cacheIndex = GetCacheIndexForCellRegion(cellIndex);
            SurfaceFader.Animate(cacheIndex, style, this, animatedSurface, fadeMaterial, color, duration, repetitions);
        }


        void CellCancelAnimation (int cellIndex, float fadeOutDuration) {
            if (cellIndex < 0 || cellIndex >= cells.Count)
                return;
            int cacheIndex = GetCacheIndexForCellRegion(cellIndex);
            transform.GetComponentsInChildren(tempFaders);
            int count = tempFaders.Count;
            for (int k = 0; k < count; k++) {
                SurfaceFader fader = tempFaders[k];
                if (fader.cacheIndex == cacheIndex) {
                    fader.Finish(fadeOutDuration);
                }
            }
        }

        void CancelAnimationAll (float fadeOutDuration) {
            transform.GetComponentsInChildren<SurfaceFader>(tempFaders);
            int count = tempFaders.Count;
            for (int k = 0; k < count; k++) {
                tempFaders[k].Finish(fadeOutDuration);
            }
        }

        bool ToggleCellsVisibility (Rect rect, bool visible, bool worldSpace = false) {
            if (cells == null)
                return false;
            int count = cells.Count;
            if (worldSpace) {
                Vector3 pos = rect.min;
                if (!GetLocalHitFromWorldPosition(ref pos))
                    return false;
                rect.min = pos;
                pos = rect.max;
                if (!GetLocalHitFromWorldPosition(ref pos))
                    return false;
                rect.max = pos;
            }
            if (rect.xMin < -0.4999f)
                rect.xMin = -0.4999f;
            if (rect.yMin < -0.4999f)
                rect.yMin = -0.4999f;
            if (rect.xMax > 0.4999f)
                rect.xMax = 0.4999f;
            if (rect.yMax > 0.4999f)
                rect.yMax = 0.4999f;
            if (_gridTopology == GridTopology.Irregular) {
                float xmin = rect.xMin;
                float xmax = rect.xMax;
                float ymin = rect.yMin;
                float ymax = rect.yMax;
                for (int k = 0; k < count; k++) {
                    Cell cell = cells[k];
                    if (cell.center.x >= xmin && cell.center.x <= xmax && cell.center.y >= ymin && cell.center.y <= ymax) {
                        cell.visible = visible;
                    }
                }
            }
            else {
                Cell topLeft = GetCellAtPoint(rect.min, worldSpace);
                Cell bottomRight = GetCellAtPoint(rect.max, worldSpace);
                if (topLeft == null || bottomRight == null)
                    return false;
                int row0 = topLeft.row;
                int col0 = topLeft.column;
                int row1 = bottomRight.row;
                int col1 = bottomRight.column;
                if (row1 < row0) {
                    int tmp = row0;
                    row0 = row1;
                    row1 = tmp;
                }
                if (col1 < col0) {
                    int tmp = col0;
                    col0 = row1;
                    col1 = tmp;
                }
                for (int k = 0; k < count; k++) {
                    Cell cell = cells[k];
                    if (cell.row >= row0 && cell.row <= row1 && cell.column >= col0 && cell.column <= col1) {
                        cell.visible = visible;
                    }
                }
            }
            ClearLastOver();
            needRefreshRouteMatrix = true;
            refreshCellMesh = true;
            issueRedraw = RedrawType.Full;
            return true;
        }


        bool GetAdjacentCellCoordinates (int cellIndex, CELL_SIDE side, out int or, out int oc, out CELL_SIDE os) {
            Cell cell = cells[cellIndex];
            int r = cell.row;
            int c = cell.column;
            or = r;
            oc = c;
            os = side;
            if (_gridTopology == GridTopology.Hexagonal) {
                int evenValue = _evenLayout ? 1 : 0;
                if (_pointyTopHexagons) {
                    switch (side) {
                        case CELL_SIDE.BottomLeft:
                            if (or % 2 != evenValue) {
                                oc--;
                            }
                            or--;
                            os = CELL_SIDE.TopRight;
                            break;
                        case CELL_SIDE.Left:
                            oc--;
                            os = CELL_SIDE.Right;
                            break;
                        case CELL_SIDE.TopLeft:
                            if (or % 2 != evenValue) {
                                oc--;
                            }
                            or++;
                            os = CELL_SIDE.BottomRight;
                            break;
                        case CELL_SIDE.TopRight:
                            if (or % 2 == evenValue) {
                                oc++;
                            }
                            or++;
                            os = CELL_SIDE.BottomLeft;
                            break;
                        case CELL_SIDE.Right:
                            oc++;
                            os = CELL_SIDE.Left;
                            break;
                        case CELL_SIDE.BottomRight:
                            if (or % 2 == evenValue) {
                                oc++;
                            }
                            or--;
                            os = CELL_SIDE.TopLeft;
                            break;
                        default:
                            return false;
                    }

                }
                else {
                    switch (side) {
                        case CELL_SIDE.Bottom:
                            or--;
                            os = CELL_SIDE.Top;
                            break;
                        case CELL_SIDE.Top:
                            or++;
                            os = CELL_SIDE.Bottom;
                            break;
                        case CELL_SIDE.BottomRight:
                            if (oc % 2 != evenValue) {
                                or--;
                            }
                            oc++;
                            os = CELL_SIDE.TopLeft;
                            break;
                        case CELL_SIDE.TopRight:
                            if (oc % 2 == evenValue) {
                                or++;
                            }
                            oc++;
                            os = CELL_SIDE.BottomLeft;
                            break;
                        case CELL_SIDE.TopLeft:
                            if (oc % 2 == evenValue) {
                                or++;
                            }
                            oc--;
                            os = CELL_SIDE.BottomRight;
                            break;
                        case CELL_SIDE.BottomLeft:
                            if (oc % 2 != evenValue) {
                                or--;
                            }
                            oc--;
                            os = CELL_SIDE.TopRight;
                            break;
                        default:
                            return false;
                    }
                }
            }
            else if (_gridTopology == GridTopology.Box) {
                switch (side) {
                    case CELL_SIDE.Bottom:
                        or--;
                        os = CELL_SIDE.Top;
                        break;
                    case CELL_SIDE.Top:
                        or++;
                        os = CELL_SIDE.Bottom;
                        break;
                    case CELL_SIDE.BottomRight:
                        or--;
                        oc++;
                        os = CELL_SIDE.TopLeft;
                        break;
                    case CELL_SIDE.TopRight:
                        or++;
                        oc++;
                        os = CELL_SIDE.BottomLeft;
                        break;
                    case CELL_SIDE.TopLeft:
                        or++;
                        oc--;
                        os = CELL_SIDE.BottomRight;
                        break;
                    case CELL_SIDE.BottomLeft:
                        or--;
                        oc--;
                        os = CELL_SIDE.TopRight;
                        break;
                    case CELL_SIDE.Left:
                        oc--;
                        os = CELL_SIDE.Right;
                        break;
                    case CELL_SIDE.Right:
                        oc++;
                        os = CELL_SIDE.Left;
                        break;
                }
            }
            else {
                return false;
            }
            return or >= 0 && or < _cellRowCount && oc >= 0 && oc < _cellColumnCount;
        }

        void GetCellIndices (List<Cell> cells, List<int> cellIndices) {
            if (cells == null || cellIndices == null) return;
            cellIndices.Clear();
            int cellsCount = cells.Count;
            for (int k = 0; k < cellsCount; k++) {
                cellIndices.Add(cells[k].index);
            }
        }

        #endregion

        #region Rectangle selection

        RectangleSelection _rectangleSelection;

        public RectangleSelection rectangleSelection {
            get {
                CheckRectangleSelectionObject();
                return _rectangleSelection;
            }
        }

        void CheckRectangleSelectionObject () {
            if (_rectangleSelection == null) {
#if UNITY_2023_1_OR_NEWER
                _rectangleSelection = FindFirstObjectByType<RectangleSelection>();
#else
                _rectangleSelection = FindObjectOfType<RectangleSelection>();
#endif
            }
            if (_rectangleSelection == null) {
                GameObject rectGO = Resources.Load<GameObject>("Prefabs/CanvasSelectionRectangle");
                if (rectGO != null) {
                    GameObject rectangleSelectionObj = Instantiate(rectGO);
                    rectangleSelectionObj.name = "TGS Rectangle Selector";
                    _rectangleSelection = rectangleSelectionObj.GetComponentInChildren<RectangleSelection>();
                }
            }
        }

        #endregion

    }

    public static class TGSHelper {

        public static TerrainGridSystem AddTerrainGridSystem (this GameObject o) {
            if (o == null)
                return null;
            TerrainGridSystem tgs = o.AddComponent<TerrainGridSystem>();
            if (tgs != null) tgs.Start();
            return o.GetComponentInChildren<TerrainGridSystem>();
        }
    }
}