/// <summary>
/// Defines enumerations and constants related to cell properties in the game grid.
/// </summary>
public class CellTypes
{
    /// <summary>
    /// Represents the type of terrain a cell can have.
    /// </summary>
    public enum TerrainType
    {
        None,
        Water,
        Marsh,
        Desert,
        Grass,
        Mountain,
        MountainTop
    }
    
    /// <summary>
    /// Represents the type of building that can be placed on a cell.
    /// </summary>
    public enum BuildingType
    {
        None,
        HQ,
        Storehouse,
        WoodCuttersHut,
        Forester,
        Sawmill,
        Quarry,
        Well,
        FishingHut,
        HuntersHut,
        GrainFarm,
        Windmill,
        Bakery,
        PigFarm,
        Slaughterhouse,
        Mine,  // Generic mine, could be specialized later
        Barracks,
        Blacksmith,
        ChargingStation, // For robots
        RobotFactory
    }

    /// <summary>
    /// Represents the type of resource that can be found on a cell.
    /// </summary>
    public enum ResourceType
    {
        None,
        Tree,
        Stone,
        Iron,
        Coal,
        Crystal // From your unique ideas
    }

    public bool hasObstacle = false;
    public bool hasFlag = false;
    public bool hasPath = false;
    public bool hasBuilding = false;
    public bool hasResource = false;
}
