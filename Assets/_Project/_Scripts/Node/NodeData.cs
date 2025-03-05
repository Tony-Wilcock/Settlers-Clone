// --- CellData.cs ---
using System.Collections.Generic;
using UnityEngine;
using static NodeTypes;

[System.Serializable]
public class NodeData
{
    #region Fields

    [SerializeField] private TerrainType terrainType = TerrainType.None;
    [SerializeField] private BuildingType buildingType = BuildingType.None;
    [SerializeField] private WorldResourceType resourceType = WorldResourceType.None;
    [SerializeField] private bool hasObstacle; // Indicates if an obstacle is present
    [SerializeField] private bool hasFlag;     // Indicates if a flag is placed
    [SerializeField] private bool hasPath;     // Indicates if part of a path
    [SerializeField] private bool hasBuilding; // Indicates if a building is present
    [SerializeField] private bool hasResource; // Indicates if a resource is present
    [SerializeField] private int buildingID = -1; // Unique identifier for a building, -1 if none
    [SerializeField] private int resourceAmount;  // Amount of resource, 0 if none

    #endregion

    #region Properties

    /// <summary>Gets or sets the terrain type of the cell.</summary>
    public TerrainType TerrainType { get => terrainType; set => terrainType = value; }
    public BuildingType BuildingType { get => buildingType; set => buildingType = value; }
    public WorldResourceType ResourceType { get => resourceType; set => resourceType = value; }
    public bool HasObstacle { get => hasObstacle; set => hasObstacle = value; }
    public bool HasFlag { get => hasFlag; set => hasFlag = value; }
    public bool HasPath { get => hasPath; set => hasPath = value; }
    public bool HasBuilding { get => hasBuilding; set => hasBuilding = value; }
    public bool HasResource { get => hasResource; set => hasResource = value; }
    public int BuildingID { get => buildingID; set => buildingID = value; }
    public int ResourceAmount { get => resourceAmount; set => resourceAmount = value; }

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of CellData with default values.
    /// </summary>
    public NodeData()
    {
        ResetToDefault();
    }

    /// <summary>
    /// Initializes a new instance of CellData by copying values from another CellData instance.
    /// </summary>
    /// <param name="other">The CellData instance to copy from.</param>
    public NodeData(NodeData other)
    {
        if (other == null)
        {
            ResetToDefault();
            return;
        }

        TerrainType = other.TerrainType;
        BuildingType = other.BuildingType;
        ResourceType = other.ResourceType;
        HasObstacle = other.HasObstacle;
        HasFlag = other.HasFlag;
        HasPath = other.HasPath;
        BuildingID = other.BuildingID;
        ResourceAmount = other.ResourceAmount;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Resets all fields to their default values.
    /// </summary>
    public void ResetToDefault()
    {
        TerrainType = TerrainType.None;
        BuildingType = BuildingType.None;
        ResourceType = WorldResourceType.None;
        HasObstacle = false;
        HasFlag = false;
        HasPath = false;
        HasBuilding = false;
        BuildingID = -1;
        ResourceAmount = 0;
    }

    /// <summary>
    /// Creates a deep copy of the current CellData instance.
    /// </summary>
    /// <returns>A new CellData instance with the same values.</returns>
    public NodeData Clone() => new(this);

    #endregion
}