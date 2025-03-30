using System;
using System.Collections.Generic;
using UnityEngine;

namespace PunkyFruitBat
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public class HexGridManager : Singleton<HexGridManager>
    {
        public event Action<int, bool> OnCreateFlagButtonPressed;
        public event Action<int> OnRemoveFlagButtonPressed;

        public event Action<int, BuildingType> OnCreateBuildingButtonPressed;

        public event Action OnGridComplete;

        [field: SerializeField] public Flag FlagPrefab { get; private set; }
        [field: SerializeField] public GameObject TempPathVisualPrefab { get; private set; }
        [field: SerializeField] public GameObject PathVisualPrefab { get; private set; }
        [field: SerializeField] public GameObject NodePrefab { get; private set; }
        [field: SerializeField] public IconPrefabs_SO IconPrefabs { get; private set; }
        [field: SerializeField] public BuildingPrefabs_SO BuildingPrefabs { get; private set; }
        [field: SerializeField] public CharacterPrefabs_SO CharacterPrefabs { get; private set; }
        [field: SerializeField] public ResourcePrefabs_SO ResourcePrefabs { get; private set; }

        public int NearestNode => nodeManager.NearestNodeIndex;

        [field: SerializeField] public bool IsDebugModeActive { get; set; } = false;
        [field: SerializeField] public int SelectedNode { get; private set; }
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

        #endregion Components

        public List<Chunk> chunks = new();
        public Node[] EditableVerticesIndices { get; private set; }

        public Dictionary<(int x, int y), List<int>> cellVertexMap;

        public Camera MainCamera { get; private set; }

        public Vector3[] globalVertices;
        public HashSet<int> EdgeVertices { get => edgeIdentifier.edgeVertices; set => edgeIdentifier.edgeVertices = value; }
        public Dictionary<int, List<int>> AdjacencyList { get => adjacencyBuilder.adjacencyList; set => adjacencyBuilder.adjacencyList = value; }

        private bool isGridGenerated = false;

        public Chunk CreateChunkObject(GameObject chunkObject)
        {
            int decalSplinesLayer = LayerMask.NameToLayer("Decal Splines");
            Renderer renderer = chunkObject.GetComponent<Renderer>();
            renderer.renderingLayerMask = (uint)decalSplinesLayer; // Set to Decal Splines layer

            return new Chunk(chunkObject);
        }

        protected override void Awake()
        {
            if (transform.position != Vector3.zero) transform.position = Vector3.zero; // Ensure grid is at origin
        }

        private void Start()
        {
            MainCamera = Camera.main;
            adjacencyBuilder.Initialise(this);
            edgeIdentifier.Initialise(this);
            gridBuilder.Initialise(this);

            //nodeSelector.Initialise(this);
            //flagManager.Initialise(this, flagPrefab);
            //pathManager.Initialise(this, PathVisualPrefab, tempPathVisualPrefab);
            //nodeManager.Initialise(this);
            //buildingManager.Initialise(this, BuildingPrefabs);
            //iconPicker.Initialise(this, IconPrefabs);
            //characterManager.Initialise(this, CharacterPrefabs);

            //cameraManager = cameraManager != null ? cameraManager : FindFirstObjectByType<CameraManager>();
            //uiManager = uiManager != null ? uiManager : FindFirstObjectByType<UIManager>();

            InitializeGame();

            nodeSelector.OnNodeSelected += (nodeIndex) => SelectedNode = nodeIndex;
        }

        private void OnDisable()
        {
            iconPicker?.Unsubscribe();
            nodeSelector?.Unsubscribe();
            flagManager?.Unsubscribe();
            pathManager?.Unsubscribe();
            buildingManager?.Unsubscribe();
            characterManager?.Unsubscribe();

            nodeSelector.OnNodeSelected -= (nodeIndex) => SelectedNode = nodeIndex;
        }

        private void InitializeGame()
        {
            globalVertices = new Vector3[Settings.width * Settings.height * 7]; // 7 vertices per hexagon

            GenerateGrid();
        }

        private void Update()
        {
            if (!isGridGenerated)
            {
                return;
            }
            nodeManager.UpdateCurrentNodeIndex(Input.mousePosition);
            uiManager.debugText.text = $"Nearest Node: {NearestNode}";
        }

        [ContextMenu("Generate Grid")]
        public void GenerateGrid()
        {
            StartCoroutine(gridBuilder.CreateHexGridAsync(chunks, globalVertices, OnGridGenerationComplete));
        }

        private void OnGridGenerationComplete(int vertexCount, Dictionary<(int, int), List<int>> cellVertexMap, Node[] editableVerticesIndices)
        {
            EditableVerticesIndices = editableVerticesIndices;
            this.cellVertexMap = cellVertexMap;

            AdjacencyList = adjacencyBuilder.BuildAdjacencyList();
            EdgeVertices = edgeIdentifier.IdentifyEdgeVertices();
            edgeIdentifier.ForceEdgeVerticesToZero();

            for (int i = 0; i < vertexCount; i++)
            {
                globalVertices[i] = EditableVerticesIndices[i].Position;
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

            isGridGenerated = true;

            nodeSelector.Initialise(this);
            flagManager.Initialise(this, FlagPrefab);
            pathManager.Initialise(this, PathVisualPrefab, TempPathVisualPrefab);
            nodeManager.Initialise(this);
            buildingManager.Initialise(this, BuildingPrefabs);
            iconPicker.Initialise(this, IconPrefabs);
            characterManager.Initialise(this, CharacterPrefabs);
            resourceManager.Initialise(this, ResourcePrefabs);

            cameraManager = cameraManager != null ? cameraManager : FindFirstObjectByType<CameraManager>();
            uiManager = uiManager != null ? uiManager : FindFirstObjectByType<UIManager>();

            OnGridComplete?.Invoke();

            #if UNITY_EDITOR
                UnityEditor.SceneView.RepaintAll();
            #endif
        }

        public void CreateFlagButtonPressed() => OnCreateFlagButtonPressed?.Invoke(SelectedNode, false);
        public void RemoveFlagButtonPressed() => OnRemoveFlagButtonPressed?.Invoke(SelectedNode);
        public void CreatePathButtonPressed() => pathManager.StartPathPlacement();
        public void RemovePathButtonPressed()
        {
            Path pathToRemove = pathManager.GetPathAtNode(SelectedNode);
            if (pathToRemove != null)
            {
                pathManager.RemovePath(pathToRemove);
                uiManager.HideAllPanels();
            }
        }
        public void CancelButtonPressed() => pathManager.PathBuilder.CancelPath();

        public void CreateBuildingButtonPressed(int buildingTypeIndex)
        {
            BuildingType buildingType = (BuildingType)buildingTypeIndex;
            OnCreateBuildingButtonPressed?.Invoke(SelectedNode, buildingType);
        }

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