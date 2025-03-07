using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static NodeTypes;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class HexGridManager : MonoBehaviour
{
    [field: SerializeField] public int HqStartingNode { get; private set; } = 90; // Example starting node

    public bool isDebugModeActive = false;
    public Transform chunksTransform;
    public Transform nodeIconsTransform;
    public Transform flagsTransform;
    public Transform decalSplineTransform;
    public Transform tempPathTransform;

    #region Components

    public HexGridSettings settings = new HexGridSettings();
    [SerializeField] private HexGridBuilder gridBuilder;
    [SerializeField] private HexGridAdjacencyBuilder adjacencyBuilder;
    [SerializeField] private HexGridEdgeIdentifier edgeIdentifier;
    [SerializeField] private PathVisualsGenerator pathVisualsGenerator;
    [SerializeField] private HexGridInteraction interactionHandler;
    [SerializeField] private Input_SO input;
    [SerializeField] private UIManager uiManager;
    [SerializeField] private NodeManager nodeManager;
    [SerializeField] private PathManager pathManager;
    [SerializeField] private BuildingManager buildingManager;
    [SerializeField] private ResourceManager resourceManager;
    [SerializeField] private WorkerManager workerManager;
    [SerializeField] private CameraManager cameraManager;

    public Input_SO Input_SO => input;
    public UIManager UIManager => uiManager;
    public NodeManager NodeManager => nodeManager;
    public PathManager PathManager => pathManager;
    public HexGridInteraction HexGridInteraction => interactionHandler;
    public PathVisualsGenerator PathVisualsGenerator => pathVisualsGenerator;
    public HexGridEdgeIdentifier EdgeIdentifier => edgeIdentifier;
    public BuildingManager BuildingManager => buildingManager;
    public ResourceManager ResourceManager => resourceManager;
    public WorkerManager WorkerManager => workerManager;
    public CameraManager CameraManager => cameraManager;

    #endregion ^Components^

    public List<Chunk> chunks = new List<Chunk>();
    public int[] editableVerticesIndices;

    public Dictionary<(int x, int y), List<int>> cellVertexMap;

    public Camera MainCamera { get; private set; }

    public Dictionary<int, Vector3> globalVertices = new Dictionary<int, Vector3>();
    public HashSet<int> EdgeVertices { get => edgeIdentifier?.edgeVertices; set => edgeIdentifier.edgeVertices = value; }
    public Dictionary<int, List<int>> AdjacencyList { get => adjacencyBuilder?.adjacencyList; set => adjacencyBuilder.adjacencyList = value; }

    private int globalVertexCounter = 0;

    public Chunk CreateChunkObject(GameObject chunkObject)
    {
        int decalSplinesLayer = LayerMask.NameToLayer("Decal Splines");
        Renderer renderer = chunkObject.GetComponent<Renderer>();
        renderer.renderingLayerMask = (uint)decalSplinesLayer; // Set to Decal Splines layer

        return new Chunk(chunkObject);
    }

    void Awake()
    {
        if (transform.position != Vector3.zero) transform.position = Vector3.zero; // Ensure grid is at origin

        edgeIdentifier = edgeIdentifier != null ? edgeIdentifier : GetComponent<HexGridEdgeIdentifier>();
        gridBuilder = gridBuilder != null ? gridBuilder : GetComponent<HexGridBuilder>();
        uiManager = uiManager != null ? uiManager : FindFirstObjectByType<UIManager>();
        nodeManager = nodeManager != null ? nodeManager : GetComponent<NodeManager>();
        pathManager = pathManager != null ? pathManager : GetComponent<PathManager>();
        interactionHandler = interactionHandler != null ? interactionHandler : GetComponent<HexGridInteraction>();
        pathVisualsGenerator = pathVisualsGenerator != null ? pathVisualsGenerator : GetComponent<PathVisualsGenerator>();
        adjacencyBuilder = adjacencyBuilder != null ? adjacencyBuilder : GetComponent<HexGridAdjacencyBuilder>();
        buildingManager = buildingManager != null ? buildingManager : GetComponent<BuildingManager>();
        workerManager = workerManager != null ? workerManager : GetComponent<WorkerManager>();
        cameraManager = cameraManager != null ? cameraManager : GetComponent<CameraManager>();
        resourceManager = ResourceManager.Instance;
    }

    private void Start()
    {
        MainCamera = Camera.main;
        StartCoroutine(InitializeGame());
    }

    private IEnumerator InitializeGame()
    {
        edgeIdentifier?.Initialise(this);
        gridBuilder?.Initialise(this);
        adjacencyBuilder?.Initialise(this);
        nodeManager?.Initialise(this);
        interactionHandler?.Initialise(this);

        // Generate grid and wait for completion
        yield return StartCoroutine(GenerateGridAsync());

        pathVisualsGenerator?.Initialise(this);
        pathManager?.Initialise(this);
        buildingManager?.Initialise(this);
        resourceManager?.Initialise(this);
        workerManager?.Initialise(this);

        pathManager.AssignCarriersToPaths();
    }

    private void OnDestroy()
    {
        interactionHandler?.OnDestroy();
    }

    private void Update()
    {
        interactionHandler.HighlightNode(); // Highlight node under mouse on every frame

        if (Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.D) && !pathManager.IsInPathCreationMode)
        {
            isDebugModeActive = !isDebugModeActive;
        }
    }

    [ContextMenu("Generate Grid")]
    public IEnumerator GenerateGridAsync()
    {
        ClearChunks();
        yield return StartCoroutine(gridBuilder.CreateHexGridAsync(
            chunks,
            globalVertices,
            (globalVertexCounter, cellVertexMap, editableVerticesIndices) =>
            {
                this.globalVertexCounter = globalVertexCounter;
                this.cellVertexMap = cellVertexMap;
                this.editableVerticesIndices = editableVerticesIndices;
                AdjacencyList = adjacencyBuilder.BuildAdjacencyList();
                EdgeVertices = edgeIdentifier.IdentifyEdgeVertices();
                edgeIdentifier.ForceEdgeVerticesToZero();
            }
        ));
    }

    void ClearChunks()
    {
        foreach (var chunk in chunks)
        {
            if (chunk.chunkObject != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(chunk.chunkObject); // Runtime: Destroy at end of frame
                }
                else
                {
                    DestroyImmediate(chunk.chunkObject); // Editor: Destroy immediately
                }
            }
        }
        chunks.Clear();
        globalVertices.Clear();
        globalVertexCounter = 0;
        nodeManager.nodeDataDictionary.Clear(); // Clear Node Data Dictionary when grid is cleared
    }

    void OnDrawGizmosSelected()
    {
        if (editableVerticesIndices == null) return;

        GUIStyle style = new GUIStyle();
        style.normal.textColor = Color.black;

        for (int i = 0; i < editableVerticesIndices.Length; i++)
        {
            int globalIndex = editableVerticesIndices[i];
            Vector3 worldPos = transform.TransformPoint(globalVertices[globalIndex]);
            int adjacencyCount = AdjacencyList.ContainsKey(globalIndex) ? AdjacencyList[globalIndex].Count : 0;
            Color gizmoColor = Color.yellow; // Default color is yellow

            if (EdgeVertices.Contains(globalIndex))
            {
                gizmoColor = Color.red;
            }
            else if (nodeManager.nodeDataDictionary.ContainsKey(globalIndex) && nodeManager.nodeDataDictionary[globalIndex].HasObstacle) // Obstacle vertices - Check NodeData
            {
                gizmoColor = Color.black; // Obstacle vertices are black
            }

            Gizmos.color = gizmoColor;
            Gizmos.DrawSphere(worldPos, settings.vertexGizmoSize);
            //UnityEditor.Handles.Label(worldPos, $"{globalIndex} ({adjacencyCount})", style);
        }
    }
}