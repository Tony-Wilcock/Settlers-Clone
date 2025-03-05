public class Groundworker : Character
{
    public override void Initialize(WorkerManager workerManager, BuildingManager buildingManager, PathManager pathManager, int startNode)
    {
        base.Initialize(workerManager, buildingManager, pathManager, startNode);
        CharacterType = CharacterType.Groundworker;
    }

    protected override void StartTask()
    {
        if (assignedBuilding == null || assignedBuilding.IsSiteLeveled) return;
        assignedBuilding.LevelSite();
        workerManager.ReturnWorker(this);
    }
}