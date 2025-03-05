using UnityEngine;

public class Builder : Character
{
    public override void Initialize(WorkerManager workerManager, BuildingManager buildingManager, PathManager pathManager, int startNode)
    {
        base.Initialize(workerManager, buildingManager, pathManager, startNode);
        CharacterType = CharacterType.Builder;
    }

    protected override void StartTask()
    {
        if (assignedBuilding == null || assignedBuilding.IsConstructed) return;

        bool allResourcesDelivered = true;
        foreach (var resource in assignedBuilding.Cost)
        {
            if (assignedBuilding.resourcesDelivered[resource.Key] < resource.Value)
            {
                allResourcesDelivered = false;
                break;
            }
        }

        if (allResourcesDelivered)
        {
            assignedBuilding.FinishConstruction();
            workerManager.ReturnWorker(this);
        }
        else
        {
            Debug.Log("Builder waiting for resources");
        }
    }
}