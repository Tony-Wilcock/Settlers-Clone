using UnityEngine;

public static class HexMetrics
{
    public const float outerRadius = 10f;  // Radius of the entire hexagon
    public const float innerRadius = outerRadius * 0.866025404f; //  sqrt(3) / 2

    // Define the six corners of a hexagon relative to its center.
    public static Vector3[] corners = {
        new Vector3(0f, 0f, outerRadius),
        new Vector3(innerRadius, 0f, 0.5f * outerRadius),
        new Vector3(innerRadius, 0f, -0.5f * outerRadius),
        new Vector3(0f, 0f, -outerRadius),
        new Vector3(-innerRadius, 0f, -0.5f * outerRadius),
        new Vector3(-innerRadius, 0f, 0.5f * outerRadius),
        new Vector3(0f, 0f, outerRadius) // Add this to close the loop for mesh generation
    };

    // Add methods to derive width and height
    public static float HexWidth()
    {
        return innerRadius * 2f;
    }

    public static float HexHeight()
    {
        return outerRadius * 1.5f;
    }
}