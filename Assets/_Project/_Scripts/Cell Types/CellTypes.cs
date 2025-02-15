using UnityEngine;

public class CellTypes
{
    public bool hasObstacle = false;
    public bool hasFlag = false;
    public bool hasPath = false;
    public bool hasBuilding = false;

    public enum TerrainType { Water, Grass, Desert, Marsh, Mountain }

    public enum BuildingType
    {
        None, // Important: Always have a "None" or "Empty" option
        WoodcuttersHut,
        Forester,
        Farm,
        Sawmill,
        Mine,  // Generic mine, could be specialized later
        Storehouse,
        Barracks,
        Blacksmith,
        // ... add other buildings as you design them ...
        ChargingStation, // For your robots
        RobotFactory,
        Wall,
        Tower,
    }

    public enum ResourceType { None, Tree, Stone, Iron, Coal, Crystal } //From your unique ideas.
}
