using UnityEngine;
using System.Collections.Generic;
using static NodeTypes;

public class WorkerManager : MonoBehaviour
{
    [SerializeField] private Transform workerParent;
    [SerializeField] private GameObject[] characterPrefabs;
    private Dictionary<CharacterType, Queue<Character>> workerPool = new Dictionary<CharacterType, Queue<Character>>();
    private Dictionary<Character, Building> assignedBuildingWorkers = new Dictionary<Character, Building>();
    private Dictionary<Character, (int startFlag, int endFlag)> assignedPathCarriers = new Dictionary<Character, (int, int)>();

    private int poolSize = 10; // Initial pool size (adjust as needed)

    private BuildingManager buildingManager;
    private NodeManager nodeManager;
    private PathManager pathManager;
    private ResourceManager resourceManager;

    public void Initialise(BuildingManager buildingManager, NodeManager nodeManager, PathManager pathManager, ResourceManager resourceManager, int hqNode)
    {
        this.buildingManager = buildingManager;
        this.nodeManager = nodeManager;
        this.pathManager = pathManager;
        this.resourceManager = resourceManager;

        InitialisePool(hqNode);
    }

    private void InitialisePool(int spawnNode)
    {
        foreach (CharacterType type in System.Enum.GetValues(typeof(CharacterType)))
        {
            if (type == CharacterType.None) continue; // Skip None
            int prefabIndex = (int)type - 1;
            if (prefabIndex < 0 || prefabIndex >= characterPrefabs.Length || characterPrefabs[prefabIndex] == null)
            {
                continue;
            }

            poolSize = type == CharacterType.Carrier ? 100 : 10;

            workerPool[type] = new Queue<Character>();

            for (int i = 0; i < poolSize; i++)
            {
                Character worker = CreateWorker(type, spawnNode);
                worker.gameObject.SetActive(false); // Inactive in pool
                workerPool[type].Enqueue(worker);
            }
        }
    }

    private Character CreateWorker(CharacterType type, int spawnNode)
    {
        int prefabIndex = (int)type - 1;
        GameObject workerObj = Instantiate(characterPrefabs[prefabIndex], nodeManager.GlobalVertices[spawnNode], Quaternion.identity);
        Character worker = workerObj.GetComponent<Character>();
        worker.Initialize(this, buildingManager, pathManager, spawnNode);
        workerObj.transform.SetParent(workerParent);
        return worker;
    }

    public Character GetWorker(CharacterType type, int spawnNode)
    {
        if (!workerPool.ContainsKey(type))
        {
            workerPool[type] = new Queue<Character>();
        }

        Character worker;
        if (workerPool[type].Count > 0)
        {
            worker = workerPool[type].Dequeue();
            worker.gameObject.SetActive(true);
            worker.Initialize(this, buildingManager, pathManager, spawnNode); // Reset position
        }
        else
        {
            worker = CreateWorker(type, spawnNode);
        }

        return worker;
    }

    public void ReturnWorker(Character worker)
    {
        if (worker == null)
        {
            return;
        }

        if (assignedBuildingWorkers.ContainsKey(worker))
        {
            assignedBuildingWorkers.Remove(worker);
        }
        else if (assignedPathCarriers.ContainsKey(worker))
        {
            assignedPathCarriers.Remove(worker);
        }

        worker.AssignPath(null); // Stop any movement

        worker.gameObject.SetActive(false);

        if (!workerPool.ContainsKey(worker.CharacterType))
        {
            workerPool[worker.CharacterType] = new Queue<Character>();
        }
        workerPool[worker.CharacterType].Enqueue(worker);
    }

    public void AssignWorkerToBuilding(Building building, CharacterType workerType)
    {
        if (building == null || building.IsConstructed)
        {
            return;
        }

        // Get a worker from the pool
        Character worker = GetWorker(workerType, building.EntranceNode);
        if (worker == null)
        {
            return;
        }

        // Find a path from the worker's current node to the building's entrance
        List<int> path = pathManager.FindPath(worker.CurrentNode, building.EntranceNode);
        if (path != null)
        {
            assignedBuildingWorkers[worker] = building;
            worker.AssignTask(building, path);
        }
        else
        {
            // If no path is found, return the worker to the pool
            ReturnWorker(worker);
        }
    }

    public int FindNearestStorehouseNode(int fromNode)
    {
        foreach (var buildingList in buildingManager.AllBuildings)
        {
            if (buildingList.Key == BuildingType.HQ || buildingList.Key == BuildingType.Storehouse)
            {
                foreach (var building in buildingList.Value)
                {
                    if (fromNode == -1 || pathManager.FindPath(fromNode, building.EntranceNode) != null)
                    {
                        return building.EntranceNode;
                    }
                }
            }
        }
        Debug.LogWarning("No reachable storehouse found");
        return -1;
    }
}