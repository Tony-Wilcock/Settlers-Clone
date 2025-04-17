using Codice.Client.BaseCommands;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Windows;

namespace PunkyFruitBat
{
    [Serializable] // Makes this visible and editable in the Inspector
    public struct InitialHexColor
    {
        [Tooltip("The global index of the CENTER node of the hex to colour.")]
        public int centerNodeIndex;

        [Tooltip("The colour to apply to this hex.")]
        public Color color;
    }

    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public class HexGridManager : Singleton<HexGridManager>
    {
        public event Action<int> OnCreateFlagButtonPressed;
        public event Action OnRemoveFlagButtonPressed;
        public event Action<int, BuildingType> OnCreateBuildingButtonPressed;
        public event Action OnGridComplete;
        public event Action<float> OnGenerationProgress; // Event to report progress (0.0 to 1.0)
        public float GenerationProgress { get; private set; } // Public property to check progress

        [field: SerializeField] public Flag FlagPrefab { get; private set; }
        [field: SerializeField] public GameObject TempPathVisualPrefab { get; private set; }
        [field: SerializeField] public GameObject PathVisualPrefab { get; private set; }
        [field: SerializeField] public GameObject NodePrefab { get; private set; }
        [field: SerializeField] public IconPrefabs_SO IconPrefabs { get; private set; }
        [field: SerializeField] public BuildingPrefabs_SO BuildingPrefabs { get; private set; }
        [field: SerializeField] public CharacterPrefabs_SO CharacterPrefabs { get; private set; }
        [field: SerializeField] public ResourcePrefabs_SO ResourcePrefabs { get; private set; }

        public int LiveNode => nodeManager.LiveNodeIndex;
        public int SelectedNode => nodeSelector.selectedNode;

        [field: SerializeField] public bool IsDebugModeActive { get; set; } = false;
        [field: SerializeField] public Transform ChunksTransform { get; private set; }
        [field: SerializeField] public Transform NodesTransform { get; private set; }
        [field: SerializeField] public Transform NodeIconsTransform { get; private set; }
        [field: SerializeField] public Transform FlagsTransform { get; private set; }
        [field: SerializeField] public Transform PathVisualsTransform { get; private set; }
        [field: SerializeField] public Transform TempPathTransform { get; private set; }
        [field: SerializeField] public Transform BuildingTransform { get; private set; }
        [field: SerializeField] public Transform CharactersTransform { get; private set; }

        #region Components

        [SerializeField] private Input_SO input;
        [SerializeField] private UIManager uiManager;
        [SerializeField] private CameraManager cameraManager;
        [SerializeField] private EventHandlingManager eventHandler = new();

        [SerializeField] private HexGridSettings settings = new();
        [SerializeField] private PathManager pathManager = new();
        [SerializeField] private HexGridBuilder gridBuilder = new();
        [SerializeField] private NodeIconPicker iconPicker = new();
        [SerializeField] private HexGridAdjacencyBuilder adjacencyBuilder = new();
        [SerializeField] private HexGridEdgeIdentifier edgeIdentifier = new();
        [SerializeField] private NodeSelector nodeSelector = new();
        [SerializeField] private NodeManager nodeManager = new();
        [SerializeField] private FlagManager flagManager = new();
        [SerializeField] private BuildingManager buildingManager = new();
        [SerializeField] private CharacterManager characterManager = new();
        [SerializeField] private ResourceManager resourceManager = new();

        [SerializeField] private VertexManipulator vertexManipulator = new();

        public EventHandlingManager EventHandler => eventHandler;
        public HexGridSettings Settings => settings;
        public Input_SO Input_SO => input;
        public UIManager UIManager => uiManager;
        public NodeIconPicker IconPicker => iconPicker;
        public HexGridEdgeIdentifier EdgeIdentifier => edgeIdentifier;
        public CameraManager CameraManager => cameraManager;
        public NodeSelector NodeSelector => nodeSelector;
        public NodeManager NodeManager => nodeManager;
        public FlagManager FlagManager => flagManager;
        public PathManager PathManager => pathManager;
        public BuildingManager BuildingManager => buildingManager;
        public CharacterManager CharacterManager => characterManager;
        public ResourceManager ResourceManager => resourceManager;
        public VertexManipulator VertexManipulator => vertexManipulator;

        #endregion Components

        [Tooltip("Define initial colours for specific hexes using their grid coordinates.")]
        public List<InitialHexColor> initialHexColors = new();
        public List<Chunk> chunks = new();
        public Node[] EditableVerticesIndices { get; private set; }

        public Dictionary<(int x, int y), List<int>> cellVertexMap;

        public Camera MainCamera { get; private set; }

        public Vector3[] globalVertices;
        public HashSet<int> EdgeVertices { get => edgeIdentifier.edgeVertices; set => edgeIdentifier.edgeVertices = value; }
        public Dictionary<int, List<int>> AdjacencyList { get => adjacencyBuilder.adjacencyList; set => adjacencyBuilder.adjacencyList = value; }

        public bool IsGridGenerated { get; private set; } = false;
        private bool isGenerating = false;

        protected override void Awake()
        {
            base.Awake();
            if (transform.position != Vector3.zero) transform.position = Vector3.zero; // Ensure grid is at origin
            input.playerInput.Player.Disable(); // Disable input actions
            MainCamera = Camera.main;
            adjacencyBuilder.Initialise(this);
            edgeIdentifier.Initialise(this);
            gridBuilder.Initialise(this);
        }

        private void OnDisable()
        {
            iconPicker?.Unsubscribe();
            nodeSelector?.Unsubscribe();
            flagManager?.Unsubscribe();
            pathManager?.Unsubscribe();
            buildingManager?.Unsubscribe();
            characterManager?.Unsubscribe();
        }

        private void Update()
        {
            if (!IsGridGenerated)
            {
                return;
            }
            nodeManager.UpdateLiveNodeIndex(UnityEngine.Input.mousePosition);
        }

        /// <summary>
        /// Public method to be called by the UI button to start grid generation.
        /// </summary>
        public void StartGridGeneration()
        {
            if (isGenerating || IsGridGenerated)
            {
                Debug.LogWarning("Grid generation already started or completed.");
                return;
            }

            isGenerating = true; // Set flag
            GenerationProgress = 0f; // Reset progress
            OnGenerationProgress?.Invoke(GenerationProgress); // Notify initial progress

            // Allocate the globalVertices array just before starting generation
            globalVertices = new Vector3[Settings.width * Settings.height * 7]; // 7 vertices per hexagon

            StartCoroutine(gridBuilder.CreateHexGridAsync(chunks, globalVertices, OnGridGenerationComplete, UpdateProgress));
        }

        /// <summary>
        /// Callback method passed to the builder to update generation progress.
        /// </summary>
        /// <param name="progress">Current progress value (0.0 to 1.0).</param>
        private void UpdateProgress(float progress)
        {
            GenerationProgress = Mathf.Clamp01(progress);
            OnGenerationProgress?.Invoke(GenerationProgress);
            // Debug.Log($"Grid Generation Progress: {GenerationProgress:P0}"); // Optional debug log
        }

        /// <summary>
        /// Iterates through the initialHexColors list (set in Inspector)
        /// and applies the colours to the grid using VertexManipulator.
        /// Must be called AFTER the grid and VertexManipulator are initialised.
        /// </summary>
        void ApplyInitialHexColors()
        {
            // Ensure dependencies are ready
            if (vertexManipulator == null) { Debug.LogError("ApplyInitialHexColors: VertexManipulator is null!"); return; }
            if (initialHexColors == null) { Debug.LogWarning("ApplyInitialHexColors: initialHexColors list is null or empty."); return; }
            if (cellVertexMap == null) { Debug.LogError("ApplyInitialHexColors: cellVertexMap is null! Cannot map coordinates."); return; }
            if (EditableVerticesIndices == null) { Debug.LogError("ApplyInitialHexColors: EditableVerticesIndices is null! Cannot verify center nodes."); return; }


            int appliedCount = 0;
            foreach (InitialHexColor initialColor in initialHexColors)
            {
                if (initialColor.centerNodeIndex >= 0) // Example validation
                {
                    vertexManipulator.SetHexColor(initialColor.centerNodeIndex, initialColor.color);
                    appliedCount++;
                }
                else { Debug.LogWarning($"ApplyInitialHexColors: Invalid centerNodeIndex ({initialColor.centerNodeIndex}) in list."); }
            }
        }

        private void OnGridGenerationComplete(int vertexCount, Dictionary<(int, int), List<int>> cellVertexMap, Node[] editableVerticesIndices)
        {
            EditableVerticesIndices = editableVerticesIndices;
            this.cellVertexMap = cellVertexMap;

            AdjacencyList = adjacencyBuilder.BuildAdjacencyList();
            EdgeVertices = edgeIdentifier.IdentifyEdgeVertices();

            // Check if calculation methods returned null
            if (AdjacencyList == null) { Debug.LogError("[HexGridManager.OnGridGenerationComplete] AdjacencyList is NULL after BuildAdjacencyList!"); }
            if (EdgeVertices == null) { Debug.LogError("[HexGridManager.OnGridGenerationComplete] EdgeVertices is NULL after IdentifyEdgeVertices!"); }

            edgeIdentifier.ForceEdgeVerticesToZero();

            // Ensure globalVertices reflects final positions (potentially adjusted by edge flattening)
            for (int i = 0; i < vertexCount; i++)
            {
                if (i < globalVertices.Length && EditableVerticesIndices[i] != null)
                {
                    globalVertices[i] = EditableVerticesIndices[i].Position;
                }
                else
                {
                    Debug.LogWarning($"Skipping global vertex update for index {i}. Out of bounds or Node is null.");
                }
            }

            // Set isEdgeNode property
            foreach (int edgeVertexIndex in EdgeVertices)
            {
                if (edgeVertexIndex >= 0 && edgeVertexIndex < EditableVerticesIndices.Length && EditableVerticesIndices[edgeVertexIndex] != null)
                {
                    EditableVerticesIndices[edgeVertexIndex].SetEdgeNode(true);
                }
                else
                {
                    Debug.LogError($"Invalid edge vertex index or null Node at index {edgeVertexIndex}");
                }
            }

            nodeSelector.Initialise(this);
            flagManager.Initialise(this, FlagPrefab);
            pathManager.Initialise(this, PathVisualPrefab, TempPathVisualPrefab);
            nodeManager.Initialise(this);
            buildingManager.Initialise(this, BuildingPrefabs);
            iconPicker.Initialise(this, IconPrefabs);
            characterManager.Initialise(this, CharacterPrefabs);
            resourceManager.Initialise(this, ResourcePrefabs);
            vertexManipulator.Initialise(this);

            cameraManager = cameraManager != null ? cameraManager : FindFirstObjectByType<CameraManager>();
            uiManager = uiManager != null ? uiManager : FindFirstObjectByType<UIManager>();

            ApplyInitialHexColors();

            isGenerating = false; // Generation finished
            IsGridGenerated = true; // Mark as fully generated

            GenerationProgress = 1.0f; // Ensure progress reports 100%
            OnGenerationProgress?.Invoke(GenerationProgress);
            OnGridComplete?.Invoke(); // Notify that the grid is fully ready

            input.playerInput.Player.Enable(); // Enable input actions

            #if UNITY_EDITOR
                UnityEditor.SceneView.RepaintAll();
            #endif
        }

        public Chunk CreateChunkObject(GameObject chunkObject)
        {
            int decalSplinesLayer = LayerMask.NameToLayer("Decal Splines");
            Renderer renderer = chunkObject.GetComponent<Renderer>();
            renderer.renderingLayerMask = (uint)decalSplinesLayer; // Set to Decal Splines layer

            return new Chunk(chunkObject);
        }

        #region UI_Button_Interactions

        public void CreateFlagButtonPressed() => OnCreateFlagButtonPressed?.Invoke(SelectedNode);
        public void RemoveFlagButtonPressed() => OnRemoveFlagButtonPressed?.Invoke();
        public void CreatePathButtonPressed() => pathManager.StartPathPlacement();
        public void RemovePathButtonPressed()
        {
            Path pathToRemove = pathManager.GetPathAtNode(SelectedNode);
            if (pathToRemove != null)
            {
                pathManager.RemovePath(pathToRemove);
                nodeSelector.ResetSelectedNodeIndex();
                uiManager.HideAllPanels();
            }
        }
        public void CancelButtonPressed()
        {
            nodeSelector.ResetSelectedNodeIndex();
            pathManager.PathBuilder.CancelPath();
            uiManager.HideAllPanels();
        }

        public void CreateBuildingButtonPressed(int buildingTypeIndex)
        {
            BuildingType buildingType = (BuildingType)buildingTypeIndex;
            OnCreateBuildingButtonPressed?.Invoke(SelectedNode, buildingType);
        }

        #endregion

        void OnDrawGizmosSelected()
        {
            if (EditableVerticesIndices == null)
            {
                return;
            }

            for (int i = 0; i < EditableVerticesIndices.Length; i++)
            {
                Vector3 worldPos = transform.TransformPoint(EditableVerticesIndices[i].Position);
                Color gizmoColor = Color.yellow; // Default color is yellow

                if (EditableVerticesIndices[i].IsEdgeNode)
                {
                    gizmoColor = Color.red;
                }

                // Show center Node in purple
                if (EditableVerticesIndices[i].IsCentreNode)
                {
                    gizmoColor = Color.magenta;
                }

                Gizmos.color = gizmoColor;
                Gizmos.DrawSphere(worldPos, Settings.vertexGizmoSize);
            }
        }
    }
}