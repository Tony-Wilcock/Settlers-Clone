using static CellTypes;

[System.Serializable]
public class CellData
{
    public TerrainType terrainType;
    public BuildingType buildingType;
    public ResourceType resourceType;
    public bool hasObstacle; // Simple boolean for obstacles
    public bool hasFlag;
    public bool hasPath;
    public int buildingID;
    public int resourceAmount;

    public CellData(TerrainType terrain)
    {
        terrainType = terrain;
        buildingType = BuildingType.None;
        resourceType = ResourceType.None;
        hasObstacle = false; // Default: no obstacle
        hasFlag = false;
        hasPath = false;
        buildingID = -1;
        resourceAmount = 0;
    }
}