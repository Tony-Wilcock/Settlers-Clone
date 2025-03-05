using System.Collections.Generic;
using UnityEngine;
using static NodeTypes;

public abstract class Building : MonoBehaviour
{
    [field: SerializeField] public BuildingType BuildingType { get; protected set; }
    [field: SerializeField] public Transform BuildingGFXTransform { get; protected set; }
    public GameObject BuildingGFX { get; protected set; }
    public int CentralNode { get; protected set; }
    public int EntranceNode { get; protected set; }
    public List<int> ReservedNodes { get; protected set; }
    public Dictionary<StockResourceType, int> Cost { get; protected set; }

    protected BuildingManager buildingManager;
    protected NodeManager nodeManager;
    protected PathManager pathManager;
    protected WorkerManager workerManager;
    public bool IsConstructed { get; private set; }
    public bool IsSiteLeveled { get; private set; }
    public Dictionary<StockResourceType, int> resourcesDelivered = new Dictionary<StockResourceType, int>();
    private bool hasRequestedGroundworker;
    private bool hasRequestedBuilder;

    public virtual void Initialise(BuildingManager buildingManager, NodeManager nodeManager, PathManager pathManager, WorkerManager workerManager, BuildingType buildingType, int centralNode, int entranceNode, List<int> reservedNodes)
    {
        this.buildingManager = buildingManager;
        this.nodeManager = nodeManager;
        this.pathManager = pathManager;
        this.workerManager = workerManager;
        BuildingType = buildingType;
        CentralNode = centralNode;
        EntranceNode = entranceNode;
        ReservedNodes = reservedNodes;

        if (BuildingType != BuildingType.HQ && buildingManager.BuildingCosts.ContainsKey(BuildingType))
        {
            Cost = new Dictionary<StockResourceType, int>(buildingManager.BuildingCosts[BuildingType]);
            foreach (var resource in Cost.Keys)
            {
                resourcesDelivered[resource] = 0;
            }
        }

        Build(centralNode, entranceNode, reservedNodes);
    }

    private void Build(int centralNode, int entranceNode, List<int> reservedNodes)
    {
        NodeData centralData = nodeManager.GetNodeData(centralNode);
        nodeManager.SetNodeBuildingType(centralNode, BuildingType);
        centralData.HasBuilding = true;
        SetBuildingID(centralNode, centralData);

        foreach (int node in reservedNodes)
        {
            if (node != centralNode)
            {
                nodeManager.SetNodeBuildingType(node, BuildingType.None);
                NodeData nodeData = nodeManager.GetNodeData(node);
                nodeData.HasBuilding = true;
                nodeData.HasObstacle = true;
                SetBuildingID(centralNode, nodeData);
            }
        }

        nodeManager.heldVertexIndex = entranceNode;
        nodeManager.PlaceFlag();

        nodeManager.heldVertexIndex = -1;

        SetPositionRotationScaleOfGFX(this);

        if (BuildingType == BuildingType.HQ)
        {
            SetConstructed(true);
        }
        else
        {
            Debug.Log($"Building site marked for {BuildingType} at {CentralNode}");
        }
    }

    private void SetBuildingID(int centralNode, NodeData nodeData)
    {
        nodeData.BuildingID = centralNode;
    }

    public void SetConstructed(bool constructed)
    {
        IsConstructed = constructed;
        if (IsConstructed)
        {
            BuildingGFXTransform.gameObject.SetActive(true);
            DispatchSpecificWorker();
        }
    }

    public void LevelSite()
    {
        IsSiteLeveled = true;
        hasRequestedGroundworker = false;
        Debug.Log($"Site for {BuildingType} at {CentralNode} leveled.");
    }

    public void DeliverResource(StockResourceType resource, int amount)
    {
        if (Cost.ContainsKey(resource))
        {
            resourcesDelivered[resource] = Mathf.Min(resourcesDelivered[resource] + amount, Cost[resource]);
            Debug.Log($"Delivered {amount} {resource} to {BuildingType}. Total: {resourcesDelivered[resource]}/{Cost[resource]}");
        }
    }

    public virtual void UpdateState()
    {
        if (IsConstructed || !pathManager.IsConnectedToStorehouse(EntranceNode)) return;

        if (!IsSiteLeveled && !hasRequestedGroundworker)
        {
            workerManager.AssignWorkerToBuilding(this, CharacterType.Groundworker);
            hasRequestedGroundworker = true;
            return;
        }

        bool allResourcesDelivered = true;
        foreach (var resource in Cost)
        {
            if (resourcesDelivered[resource.Key] < resource.Value)
            {
                allResourcesDelivered = false;
                RequestResource(resource.Key);
                break;
            }
        }

        if (allResourcesDelivered && !hasRequestedBuilder)
        {
            workerManager.AssignWorkerToBuilding(this, CharacterType.Builder);
            hasRequestedBuilder = true;
        }
    }

    private void RequestResource(StockResourceType resource)
    {
        int nearestStorehouseFlag = FindNearestConnectedStorehouseFlag();
        if (nearestStorehouseFlag != -1)
        {
            pathManager.AddResourceToQueue(nearestStorehouseFlag, resource, 1);
            Debug.Log($"Requested 1 {resource} for {BuildingType} from flag {nearestStorehouseFlag}");
        }
    }

    public void FinishConstruction()
    {
        SetConstructed(true);
        hasRequestedBuilder = false;
    }

    private void DispatchSpecificWorker()
    {
        CharacterType workerType = GetSpecificWorkerType();
        if (workerType != CharacterType.None)
        {
            workerManager.AssignWorkerToBuilding(this, workerType);
        }
    }

    private CharacterType GetSpecificWorkerType()
    {
        return BuildingType switch
        {
            BuildingType.WoodCuttersHut => CharacterType.WoodCutter,
            BuildingType.Storehouse => CharacterType.None,
            // Add other building-specific workers here
            _ => CharacterType.None
        };
    }

    private int FindNearestConnectedStorehouseFlag()
    {
        foreach (var buildingList in buildingManager.AllBuildings)
        {
            if (buildingList.Key == BuildingType.HQ || buildingList.Key == BuildingType.Storehouse)
            {
                foreach (var building in buildingList.Value)
                {
                    if (pathManager.IsConnectedToStorehouse(building.EntranceNode))
                    {
                        return building.EntranceNode;
                    }
                }
            }
        }
        return -1;
    }

    protected void SetPositionRotationScaleOfGFX(Building building)
    {
        Transform buildingGfxTransform = building.BuildingGFXTransform;
        BuildingType type = building.BuildingType;

        switch (BuildingSizes[type])
        {
            case BuildingSize.Small:
                buildingGfxTransform.localPosition = new Vector3(0f, 1f, 0f);
                buildingGfxTransform.localRotation = Quaternion.Euler(0f, -30f, 0f);
                buildingGfxTransform.localScale = new Vector3(3f, 3f, 3f);
                break;
            case BuildingSize.Medium:
                buildingGfxTransform.localPosition = new Vector3(0f, 3.5f, 0f);
                buildingGfxTransform.localRotation = Quaternion.Euler(0f, -30f, 0f);
                buildingGfxTransform.localScale = new Vector3(7f, 7f, 7f);
                break;
            case BuildingSize.Large:
                buildingGfxTransform.localPosition = new Vector3(-1.7f, 5f, 2.9f);
                buildingGfxTransform.localRotation = Quaternion.Euler(0f, -30f, 0f);
                buildingGfxTransform.localScale = new Vector3(12.5f, 10f, 11.5f);
                break;
        }
        buildingGfxTransform.gameObject.SetActive(IsConstructed);
    }

    public abstract void ProduceResources();
}