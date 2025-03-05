using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Add this struct at the top of HexGridBuilder
[Serializable]
public struct VertexKey
{
    public float x, z;
    private const float TOLERANCE = 0.001f; // Adjust as needed

    public VertexKey(Vector2 position)
    {
        x = Mathf.Round(position.x / TOLERANCE) * TOLERANCE;
        z = Mathf.Round(position.y / TOLERANCE) * TOLERANCE;
    }

    public override bool Equals(object obj)
    {
        if (!(obj is VertexKey other)) return false;
        return Mathf.Abs(x - other.x) < TOLERANCE && Mathf.Abs(z - other.z) < TOLERANCE;
    }

    public override int GetHashCode()
    {
        return (int)(x / TOLERANCE) * 397 ^ (int)(z / TOLERANCE); // Simple hash combining x and z
    }
}

public class HexGridBuilder : MonoBehaviour
{
    private HexGridManager manager; // Reference back to the main manager
    private HexGridSettings settings;
    private Dictionary<VertexKey, int> vertexMap;
    private float CellSize => settings.cellSize;
    private int Width => settings.width;
    private int Height => settings.height;
    private int ChunkSize => settings.chunkSize;

    public void Initialise(HexGridManager manager, HexGridSettings settings)
    {
        this.manager = manager;
        this.settings = settings;
    }

    public IEnumerator CreateHexGridAsync(List<Chunk> chunks, Dictionary<int, Vector3> globalVertices, Action<int, Dictionary<(int, int), List<int>>, int[]> onComplete)
    {
        float outerRadius = CellSize;
        float innerRadius = outerRadius * Mathf.Sqrt(3) / 2;
        vertexMap = new Dictionary<VertexKey, int>();
        var cellVertexMap = new Dictionary<(int, int), List<int>>();
        int globalVertexCounter = 0;

        int chunkWidth = Mathf.CeilToInt((float)Width / ChunkSize);
        int chunkHeight = Mathf.CeilToInt((float)Height / ChunkSize);

        for (int chunkX = 0; chunkX < chunkWidth; chunkX++)
        {
            for (int chunkY = 0; chunkY < chunkHeight; chunkY++)
            {
                var chunkVertices = new List<Vector3>();
                var chunkTriangles = new List<int>();
                var localToGlobal = new Dictionary<int, int>();
                var globalToLocal = new Dictionary<int, int>();

                for (int row = chunkX * ChunkSize; row < Mathf.Min((chunkX + 1) * ChunkSize, Width); row++)
                {
                    for (int col = chunkY * ChunkSize; col < Mathf.Min((chunkY + 1) * ChunkSize, Height); col++)
                    {
                        Vector2 center = CalculateHexCenter(row, col, outerRadius, innerRadius);
                        Vector3[] hexVertices = GenerateHexVertices(center, outerRadius);
                        int[] vertexIndices = new int[7];
                        List<int> currentCellVertices = new List<int>();

                        for (int i = 0; i < 7; i++)
                        {
                            VertexKey key = new VertexKey(new Vector2(hexVertices[i].x, hexVertices[i].z)); // Use VertexKey
                            if (!vertexMap.ContainsKey(key))
                            {
                                vertexMap[key] = globalVertexCounter;
                                globalVertices[globalVertexCounter] = hexVertices[i];
                                localToGlobal[chunkVertices.Count] = globalVertexCounter;
                                globalToLocal[globalVertexCounter] = chunkVertices.Count;
                                chunkVertices.Add(hexVertices[i]);
                                currentCellVertices.Add(globalVertexCounter);
                                manager.NodeManager.nodeDataDictionary[globalVertexCounter] = new NodeData(); // Create and add NodeData
                                manager.NodeManager.SetNodeTerrainType(globalVertexCounter, NodeTypes.TerrainType.Grass);
                                globalVertexCounter++;
                            }
                            else
                            {
                                int globalIndex = vertexMap[key];
                                if (!globalToLocal.ContainsKey(globalIndex))
                                {
                                    localToGlobal[chunkVertices.Count] = globalIndex;
                                    globalToLocal[globalIndex] = chunkVertices.Count;
                                    chunkVertices.Add(globalVertices[globalIndex]);
                                }
                                currentCellVertices.Add(globalIndex);
                            }
                            vertexIndices[i] = globalToLocal[vertexMap[key]];
                        }
                        cellVertexMap[(row, col)] = currentCellVertices;

                        for (int i = 0; i < 6; i++)
                        {
                            chunkTriangles.Add(vertexIndices[0]);
                            chunkTriangles.Add(vertexIndices[(i + 1) % 6 + 1]);
                            chunkTriangles.Add(vertexIndices[i + 1]);
                        }
                    }
                }

                GameObject chunkObj = new GameObject($"Chunk_{chunkX}_{chunkY}");
                chunkObj.transform.SetParent(manager.chunksTransform, false);

                MeshFilter meshFilter = chunkObj.AddComponent<MeshFilter>();
                MeshRenderer meshRenderer = chunkObj.AddComponent<MeshRenderer>();
                MeshCollider meshCollider = chunkObj.AddComponent<MeshCollider>();

                if (meshFilter == null || meshRenderer == null || meshCollider == null)
                {
                    Debug.LogError($"Failed to add required components to Chunk_{chunkX}_{chunkY}. Skipping chunk creation.");
                    DestroyImmediate(chunkObj); // Clean up if creation fails
                    continue; // Skip to next chunk
                }

                // Set material with error checking
                Material defaultMaterial = manager.GetComponent<MeshRenderer>()?.material;
                if (defaultMaterial == null)
                {
                    Debug.LogWarning($"No material found on HexGridManager for Chunk_{chunkX}_{chunkY}. Using default Unity material.");
                    defaultMaterial = new Material(Shader.Find("Standard")); // Fallback
                }
                meshRenderer.material = defaultMaterial;

                chunkObj.layer = manager.gameObject.layer;

                Chunk chunk = manager.CreateChunkObject(chunkObj); // Use manager's method to create chunk instance
                chunk.vertices = chunkVertices.ToArray();
                chunk.triangles = chunkTriangles.ToArray();
                chunk.localToGlobalVertexMap = localToGlobal;
                chunk.globalToLocalVertexMap = globalToLocal;
                chunk.UpdateMesh();
                chunks.Add(chunk);

                yield return null; // Wait for next frame
            }
        }
        int[] editableVerticesIndices = globalVertices.Keys.ToArray();
        onComplete?.Invoke(globalVertexCounter, cellVertexMap, editableVerticesIndices);
    }


    private Vector2 CalculateHexCenter(int row, int col, float outerRadius, float innerRadius)
    {
        float rowOffset = row * outerRadius * 1.5f;
        float colOffset = col * innerRadius * 2f - (row % 2 == 1 ? innerRadius : 0);
        return new Vector2(rowOffset, colOffset);
    }

    private Vector3[] GenerateHexVertices(Vector2 center, float outerRadius)
    {
        Vector3[] hexVertices = new Vector3[7]; // 7 vertices for a hexagon
        hexVertices[0] = new Vector3(center.x, 0, center.y); // Center vertex
        for (int i = 0; i < 6; i++)
        {
            float angleDeg = 60f * i; // Pointy side up offset
            float angleRad = Mathf.Deg2Rad * angleDeg; // Convert to radians
            hexVertices[i + 1] = new Vector3(
                center.x + outerRadius * Mathf.Cos(angleRad),
                0,
                center.y + outerRadius * Mathf.Sin(angleRad)
            );
        }
        return hexVertices;
    }
}