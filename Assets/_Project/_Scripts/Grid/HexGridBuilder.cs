using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PunkyFruitBat
{
    [Serializable]
    public struct VertexKey
    {
        public float x, z;
        private const float TOLERANCE = 0.001f;

        public VertexKey(float x, float z)
        {
            this.x = Mathf.Round(x / TOLERANCE) * TOLERANCE;
            this.z = Mathf.Round(z / TOLERANCE) * TOLERANCE;
        }

        public override readonly int GetHashCode()
        {
            return x.GetHashCode() ^ z.GetHashCode();
        }

        public override readonly bool Equals(object obj)
        {
            if (obj is not VertexKey) return false;
            VertexKey other = (VertexKey)obj;
            return x == other.x && z == other.z;
        }
    }

    public class HexGridBuilder
    {
        private HexGridManager manager;
        private HexGridSettings settings;
        private Dictionary<VertexKey, int> vertexMap;
        private float CellSize => settings.cellSize;
        private int Width => settings.width;
        private int Height => settings.height;
        private int ChunkSize => settings.chunkSize;

        public void Initialise(HexGridManager manager)
        {
            this.manager = manager;
            settings = manager.Settings;
        }

        public IEnumerator CreateHexGridAsync(List<Chunk> chunks, Vector3[] globalVertices, Action<int, Dictionary<(int, int), List<int>>, Node[]> onComplete)
        {
            float outerRadius = CellSize;
            float innerRadius = outerRadius * Mathf.Sqrt(3) / 2;
            vertexMap = new Dictionary<VertexKey, int>();
            var cellVertexMap = new Dictionary<(int, int), List<int>>();
            int globalVertexCounter = 0;

            int chunkWidth = Mathf.CeilToInt((float)Width / ChunkSize);
            int chunkHeight = Mathf.CeilToInt((float)Height / ChunkSize);

            int batchSize = 5;
            int chunkCounter = 0;

            List<int> centreVertices = new();

            for (int chunkX = 0; chunkX < chunkWidth; chunkX++)
            {
                for (int chunkY = 0; chunkY < chunkHeight; chunkY++)
                {
                    int estimatedVertexCount = ChunkSize * ChunkSize * 7;
                    int estimatedTriangleCount = ChunkSize * ChunkSize * 18;

                    var chunkVertices = new List<Vector3>(estimatedVertexCount);
                    var chunkTriangles = new List<int>(estimatedTriangleCount);
                    var localToGlobal = new Dictionary<int, int>();
                    var globalToLocal = new Dictionary<int, int>();

                    int maxX = Mathf.Min((chunkX + 1) * ChunkSize, Width);
                    int maxY = Mathf.Min((chunkY + 1) * ChunkSize, Height);

                    for (int row = chunkX * ChunkSize; row < maxX; row++)
                    {
                        for (int col = chunkY * ChunkSize; col < maxY; col++)
                        {
                            Vector2 center = CalculateHexCenter(row, col, outerRadius, innerRadius);
                            Vector3[] hexVertices = GenerateHexVertices(center, outerRadius);
                            int[] vertexIndices = new int[7];
                            List<int> currentCellVertices = new();

                            for (int i = 0; i < 7; i++)
                            {
                                VertexKey key = new(hexVertices[i].x, hexVertices[i].z);
                                if (vertexMap.TryGetValue(key, out int globalIndex))
                                {
                                    if (!globalToLocal.ContainsKey(globalIndex))
                                    {
                                        localToGlobal[chunkVertices.Count] = globalIndex;
                                        globalToLocal[globalIndex] = chunkVertices.Count;
                                        chunkVertices.Add(globalVertices[globalIndex]);
                                    }
                                    currentCellVertices.Add(globalIndex);
                                }
                                else
                                {
                                    vertexMap[key] = globalVertexCounter;
                                    globalVertices[globalVertexCounter] = hexVertices[i];
                                    localToGlobal[chunkVertices.Count] = globalVertexCounter;
                                    globalToLocal[globalVertexCounter] = chunkVertices.Count;
                                    chunkVertices.Add(hexVertices[i]);
                                    currentCellVertices.Add(globalVertexCounter);
                                    globalVertexCounter++;
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

                            // Set the center node's isCentreNode property
                            VertexKey centerKey = new(center.x, center.y);
                            if (vertexMap.TryGetValue(centerKey, out int centerGlobalIndex))
                            {
                                centreVertices.Add(centerGlobalIndex);
                            }
                        }
                    }

                    GameObject chunkObj = new($"Chunk_{chunkX}_{chunkY}");
                    chunkObj.transform.SetParent(manager.ChunksTransform, false);

                    MeshFilter meshFilter = chunkObj.AddComponent<MeshFilter>();
                    MeshRenderer meshRenderer = chunkObj.AddComponent<MeshRenderer>();
                    MeshCollider meshCollider = chunkObj.AddComponent<MeshCollider>();

                    if (meshFilter == null || meshRenderer == null || meshCollider == null)
                    {
                        Debug.LogError($"Failed to add required components to Chunk_{chunkX}_{chunkY}. Skipping chunk creation.");
                        GameObject.DestroyImmediate(chunkObj);
                        continue;
                    }

                    Material defaultMaterial = manager.GetComponent<MeshRenderer>().material;
                    if (defaultMaterial == null)
                    {
                        Debug.LogWarning($"No material found on HexGridManager for Chunk_{chunkX}_{chunkY}. Using default Unity material.");
                        defaultMaterial = new Material(Shader.Find("Standard"));
                    }
                    meshRenderer.material = defaultMaterial;

                    chunkObj.layer = manager.gameObject.layer;

                    Chunk chunk = manager.CreateChunkObject(chunkObj);

                    chunk.meshFilter = meshFilter;
                    chunk.meshRenderer = meshRenderer;
                    chunk.meshCollider = meshCollider;

                    Vector3[] verticesArray = chunkVertices.ToArray();
                    int[] trianglesArray = chunkTriangles.ToArray();

                    Mesh combinedMesh = new()
                    {
                        vertices = verticesArray,
                        triangles = trianglesArray
                    };
                    combinedMesh.RecalculateNormals();

                    chunk.vertices = verticesArray;
                    chunk.mesh = combinedMesh;

                    chunk.meshFilter.mesh = combinedMesh;
                    chunk.meshCollider.sharedMesh = combinedMesh;

                    chunk.localToGlobalVertexMap = localToGlobal;

                    chunks.Add(chunk);

                    chunkCounter++;
                    if (chunkCounter % batchSize == 0)
                    {
                        yield return null;
                    }
                }
            }

            Node[] editableVerticesIndices = new Node[globalVertexCounter];

            for (int i = 0; i < globalVertexCounter; i++)
            {
                GameObject nodeObject = GameObject.Instantiate(manager.NodePrefab, globalVertices[i], Quaternion.identity, manager.NodesTransform);
                nodeObject.name = $"Node_{i}";

                if (!nodeObject.TryGetComponent<Node>(out var node))
                {
                    Debug.LogError("Node prefab does not have a Node component.");
                    continue;
                }

                node.SetVertexIndex(i);
                node.SetPosition(globalVertices[i]);
                editableVerticesIndices[i] = node;

                if (centreVertices.Contains(i))
                {
                    node.SetCenterNode(true);
                }
            }

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
            Vector3[] hexVertices = new Vector3[7];
            hexVertices[0] = new Vector3(center.x, 0, center.y);
            for (int i = 0; i < 6; i++)
            {
                float angleDeg = 60f * i;
                float angleRad = Mathf.Deg2Rad * angleDeg;
                hexVertices[i + 1] = new Vector3(
                    center.x + outerRadius * Mathf.Cos(angleRad),
                    0,
                    center.y + outerRadius * Mathf.Sin(angleRad)
                );
            }
            return hexVertices;
        }
    }
}