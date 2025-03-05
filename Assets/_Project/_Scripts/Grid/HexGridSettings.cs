using System;
using UnityEngine;

[Serializable]
public class HexGridSettings
{
    public float cellSize = 7f;
    public int width = 20;
    public int height = 20;
    public LayerMask hexGridLayerMask;
    public float vertexGizmoSize = 0.5f;
    [Range(0.1f, 0.5f)] public float movementAmount = 0.1f;
    [Range(0.1f, 1f)] public float maxHeightDifference = 0.2f;
    [Range(0f, 1f)] public float smoothingFactor = 0.5f;
    [Range(5, 20)] public int chunkSize = 5;
    [Range(1f, 2f)] public float adjacencyDistanceToleranceFactor = 1.15f; // Configurable tolerance
}