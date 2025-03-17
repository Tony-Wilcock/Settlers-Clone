using System.Collections.Generic;
using UnityEngine;

namespace PunkyFruitBat
{
    public class HexGridAdjacencyBuilder
    {
        public Dictionary<int, List<int>> adjacencyList = new(); // Initialize here

        private float CellSize => settings.cellSize;
        private int Width => settings.width;
        private int Height => settings.height;
        private int ChunkSize => settings.chunkSize;
        private float AdjacencyDistanceToleranceFactor => settings.adjacencyDistanceToleranceFactor; // Access from Settings
        private Vector3[] GlobalVertices => manager.globalVertices;
        private List<Chunk> Chunks => manager.chunks;

        private HexGridManager manager;
        private HexGridSettings settings;

        public void Initialise(HexGridManager manager)
        {
            this.manager = manager;
            settings = manager.Settings;
        }

        public Dictionary<int, List<int>> BuildAdjacencyList()
        {
            if (manager.EditableVerticesIndices == null)
            {
                Debug.LogError("EditableVerticesIndices is null in BuildAdjacencyList");
                return new Dictionary<int, List<int>>(); // Return an empty dictionary
            }

            var adjacencyList = new Dictionary<int, List<int>>();
            int globalVertexCounter = GlobalVertices.Length;

            for (int i = 0; i < globalVertexCounter; i++)
            {
                adjacencyList[i] = new List<int>();
            }

            int numChunksX = Mathf.CeilToInt((float)Width / ChunkSize);
            int numChunksY = Mathf.CeilToInt((float)Height / ChunkSize);

            HashSet<(int, int)> processedAdjacencies = new();

            for (int chunkX = 0; chunkX < numChunksX; chunkX++)
            {
                for (int chunkY = 0; chunkY < numChunksY; chunkY++)
                {
                    int chunkIndex = chunkX * numChunksY + chunkY;
                    if (chunkIndex >= Chunks.Count)
                        continue;
                    Chunk currentChunk = Chunks[chunkIndex];

                    if (currentChunk == null)
                    {
                        continue;
                    }

                    if (currentChunk.vertices == null)
                    {
                        continue;
                    }

                    for (int i = 0; i < currentChunk.vertices.Length; i++)
                    {
                        if (currentChunk.localToGlobalVertexMap == null)
                        {
                            break;
                        }

                        int globalI = currentChunk.localToGlobalVertexMap[i];
                        for (int j = i + 1; j < currentChunk.vertices.Length; j++)
                        {
                            int globalJ = currentChunk.localToGlobalVertexMap[j];
                            if (Vector3.Distance(GlobalVertices[globalI], GlobalVertices[globalJ]) < CellSize * AdjacencyDistanceToleranceFactor)
                            {
                                var adjacencyPair = (Mathf.Min(globalI, globalJ), Mathf.Max(globalI, globalJ));
                                if (!processedAdjacencies.Contains(adjacencyPair))
                                {
                                    adjacencyList[globalI].Add(globalJ);
                                    adjacencyList[globalJ].Add(globalI);
                                    processedAdjacencies.Add(adjacencyPair);
                                }
                            }
                        }
                    }

                    List<Chunk> neighbors = GetNeighboringChunks(chunkX, chunkY);
                    foreach (Chunk neighborChunk in neighbors)
                    {
                        if (neighborChunk == null)
                        {
                            continue;
                        }

                        if (neighborChunk.localToGlobalVertexMap == null)
                        {
                            continue;
                        }

                        for (int i = 0; i < currentChunk.vertices.Length; i++)
                        {
                            int globalI = currentChunk.localToGlobalVertexMap[i];
                            for (int k = 0; k < neighborChunk.vertices.Length; k++)
                            {
                                int globalK = neighborChunk.localToGlobalVertexMap[k];
                                if (globalI == globalK)
                                    continue;
                                if (Vector3.Distance(GlobalVertices[globalI], GlobalVertices[globalK]) < CellSize * AdjacencyDistanceToleranceFactor)
                                {
                                    var adjacencyPair = (Mathf.Min(globalI, globalK), Mathf.Max(globalI, globalK));
                                    if (!processedAdjacencies.Contains(adjacencyPair))
                                    {
                                        adjacencyList[globalI].Add(globalK);
                                        adjacencyList[globalK].Add(globalI);
                                        processedAdjacencies.Add(adjacencyPair);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return adjacencyList;
        }

        public List<Chunk> GetNeighboringChunks(int chunkX, int chunkY)
        {
            List<Chunk> neighbors = new();
            int numChunksX = Mathf.CeilToInt((float)Width / ChunkSize);
            int numChunksY = Mathf.CeilToInt((float)Height / ChunkSize);

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0)
                        continue;

                    int nx = chunkX + dx;
                    int ny = chunkY + dy;

                    if (nx < 0 || nx >= numChunksX || ny < 0 || ny >= numChunksY)
                    {
                        continue;
                    }

                    int neighborIndex = nx * numChunksY + ny;

                    if (neighborIndex >= 0 && neighborIndex < Chunks.Count)
                    {
                        neighbors.Add(Chunks[neighborIndex]);
                    }
                    else
                    {
                        Debug.LogWarning($"Neighbor index {neighborIndex} is invalid");
                    }
                }
            }
            return neighbors;
        }
    }
}
