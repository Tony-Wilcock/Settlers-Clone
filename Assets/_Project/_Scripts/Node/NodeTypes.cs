using System.Collections.Generic;

/// <summary>
/// Defines enumerations and constants related to cell properties in the game grid.
/// </summary>
public class NodeTypes
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

    public enum BuildingSize
    {
        None,
        Small,
        Medium,
        Large
    }

    /// <summary>
    /// Represents the size of each building type.
    /// </summary>
    public static Dictionary<BuildingType, BuildingSize> BuildingSizes = new Dictionary<BuildingType, BuildingSize>
    {
        { BuildingType.None, BuildingSize.None },
        { BuildingType.HQ, BuildingSize.Large },
        { BuildingType.Storehouse, BuildingSize.Medium },
        { BuildingType.WoodCuttersHut, BuildingSize.Small },
        { BuildingType.Forester, BuildingSize.Small },
        { BuildingType.Sawmill, BuildingSize.Medium },
        { BuildingType.Quarry, BuildingSize.Small },
        { BuildingType.Well, BuildingSize.Small },
        { BuildingType.FishingHut, BuildingSize.Small },
        { BuildingType.HuntersHut, BuildingSize.Small },
        { BuildingType.GrainFarm, BuildingSize.Large },
        { BuildingType.Windmill, BuildingSize.Medium },
        { BuildingType.Bakery, BuildingSize.Medium },
        { BuildingType.PigFarm, BuildingSize.Large },
        { BuildingType.Slaughterhouse, BuildingSize.Medium },
        { BuildingType.Mine, BuildingSize.Small },
        { BuildingType.Barracks, BuildingSize.Small },
        { BuildingType.Blacksmith, BuildingSize.Medium },
        { BuildingType.ChargingStation, BuildingSize.Small },
        { BuildingType.RobotFactory, BuildingSize.Large }
    };

    // Maybe set the reserved node pattern for each building type here

    /// <summary>
    /// Represents the type of resource that can be found on a cell.
    /// </summary>
    public enum WorldResourceType
    {
        None,
        FreshWater,
        Tree,
        Stone,
        IronOre,
        CoalOre,
        CrystalOre // From your unique ideas
    }

    public bool hasObstacle = false;
    public bool hasFlag = false;
    public bool hasPath = false;
    public bool hasBuilding = false;
    public bool hasResource = false;
}
