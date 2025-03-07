using UnityEngine;
using System.Collections.Generic;
using System;

public enum CharacterType
{
    None,
    Carrier,
    Builder,
    Groundworker,
    WoodCutter,
    Forester,
    Carpenter,
    Fisher,
    Hunter,
    Stonemason,
    Farmer,
    Miller,
    Baker,
    PigBreeder,
    Butcher,
    WaterWellDigger
}

public abstract class Character : MonoBehaviour
{
    [field: SerializeField] public CharacterType CharacterType { get; protected set; }
    [SerializeField] protected float moveSpeed = 5f;
    [field: SerializeField] public int CurrentNode { get; protected set; }

    protected WorkerManager workerManager;
    protected BuildingManager buildingManager;
    protected PathManager pathManager;
    protected List<int> currentPath = new List<int>();
    protected bool isMoving = false;
    protected Vector3 targetPosition;
    protected Building assignedBuilding;
    protected Action onPathComplete;

    public virtual void Initialize(WorkerManager workerManager, BuildingManager buildingManager, PathManager pathManager, int startNode)
    {
        this.workerManager = workerManager;
        this.buildingManager = buildingManager;
        this.pathManager = pathManager;
        CurrentNode = startNode;
        transform.position = pathManager.Manager.globalVertices[startNode];
        currentPath.Clear();
        isMoving = false;
        assignedBuilding = null;
        onPathComplete = null;
    }

    protected virtual void Update()
    {
        if (isMoving && currentPath.Count > 0)
        {
            MoveAlongPath();
        }
    }

    protected void MoveAlongPath()
    {
        if (Vector3.Distance(transform.position, targetPosition) < 0.1f)
        {
            CurrentNode = currentPath[0];
            currentPath.RemoveAt(0);

            if (currentPath.Count > 0)
            {
                targetPosition = pathManager.Manager.globalVertices[currentPath[0]];
            }
            else
            {
                isMoving = false;
                transform.position = pathManager.Manager.globalVertices[CurrentNode];
                OnReachedDestination();
                if (onPathComplete != null)
                {
                    Action callback = onPathComplete; // Store to prevent race condition
                    onPathComplete = null;
                    callback.Invoke();
                }
            }
        }
        else
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
        }
    }

    public void AssignPath(List<int> path, Action callback = null)
    {
        if (path != null && path.Count > 0)
        {
            currentPath = new List<int>(path);
            targetPosition = pathManager.Manager.globalVertices[currentPath[0]];
            isMoving = true;
            onPathComplete = callback;
        }
        else
        {
            currentPath.Clear();
            isMoving = false;
            onPathComplete = null;
        }
    }

    public void AssignTask(Building building, List<int> path)
    {
        assignedBuilding = building;
        AssignPath(path, StartTask);
    }

    protected virtual void OnReachedDestination()
    {
    }

    protected abstract void StartTask();
}