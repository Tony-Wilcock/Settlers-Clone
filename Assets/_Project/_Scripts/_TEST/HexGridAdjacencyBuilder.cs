using System.Collections.Generic;
using UnityEngine;

public class HexGridAdjacencyBuilder : MonoBehaviour
{
    public Dictionary<int, List<int>> adjacencyList = new Dictionary<int, List<int>>(); // Initialize here

    private float CellSize => settings.cellSize;
    private int Width => settings.width;
    private int Height => settings.height;
    private int ChunkSize => settings.chunkSize;
    private float AdjacencyDistanceToleranceFactor => settings.adjacencyDistanceToleranceFactor; // Access from settings
    private Dictionary<int, Vector3> GlobalVertices => manager.globalVertices;
    private List<Chunk> Chunks => manager.chunks;

    private HexGridManager manager;
    private HexGridSettings settings;

    private float adjacencyDistanceToleranceFactor = 1.15f;

    public void Initialise(HexGridManager manager, HexGridSettings settings)
    {
        this.manager = manager;
        this.settings = settings;
    }

    public Dictionary<int, List<int>> BuildAdjacencyList()
    {
        var adjacencyList = new Dictionary<int, List<int>>();
        int globalVertexCounter = GlobalVertices.Count; // Use globalVertices.Count directly

        for (int i = 0; i < globalVertexCounter; i++)
        {
            adjacencyList[i] = new List<int>();
        }

        int numChunksX = Mathf.CeilToInt((float)Width / ChunkSize);
        int numChunksY = Mathf.CeilToInt((float)Height / ChunkSize);

        HashSet<(int, int)> processedAdjacencies = new HashSet<(int, int)>(); // Updated to ValueTuple (see next suggestion)

        for (int chunkX = 0; chunkX < numChunksX; chunkX++)
        {
            for (int chunkY = 0; chunkY < numChunksY; chunkY++)
            {
                int chunkIndex = chunkX * numChunksY + chunkY;
                if (chunkIndex >= Chunks.Count) continue;
                Chunk currentChunk = Chunks[chunkIndex];

                // Step 1: Add adjacency within the current chunk
                for (int i = 0; i < currentChunk.vertices.Length; i++)
                {
                    int globalI = currentChunk.localToGlobalVertexMap[i];
                    for (int j = i + 1; j < currentChunk.vertices.Length; j++)
                    {
                        int globalJ = currentChunk.localToGlobalVertexMap[j];
                        if (Vector3.Distance(GlobalVertices[globalI], GlobalVertices[globalJ]) < CellSize * adjacencyDistanceToleranceFactor)
                        {
                            // Check for double counting - Within Chunk - Not really needed here, but good practice
                            var adjacencyPair = (Mathf.Min(globalI, globalJ), Mathf.Max(globalI, globalJ)); // Updated to ValueTuple
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
                    for (int i = 0; i < currentChunk.vertices.Length; i++)
                    {
                        int globalI = currentChunk.localToGlobalVertexMap[i];
                        for (int k = 0; k < neighborChunk.vertices.Length; k++)
                        {
                            int globalK = neighborChunk.localToGlobalVertexMap[k];
                            if (globalI == globalK) continue; // Skip if same vertex
                            if (Vector3.Distance(GlobalVertices[globalI], GlobalVertices[globalK]) < CellSize * AdjacencyDistanceToleranceFactor)
                            {
                                // Prevent Double Counting - Inter-Chunk - CRITICAL HERE
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
        List<Chunk> neighbors = new List<Chunk>();
        int numChunksX = Mathf.CeilToInt((float)Width / ChunkSize);
        int numChunksY = Mathf.CeilToInt((float)Height / ChunkSize);

        // Check all 8 surrounding chunks (including diagonals)
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue; // Skip the current chunk

                int nx = chunkX + dx;
                int ny = chunkY + dy;

                // Explicit bounds checking
                if (nx < 0 || nx >= numChunksX || ny < 0 || ny >= numChunksY)
                {
                    // Optional: Log for debugging if invalid chunks are accessed
                    // Debug.LogWarning($"Attempted to access out-of-bounds chunk at ({nx}, {ny}) from ({chunkX}, {chunkY})");
                    continue;
                }

                int neighborIndex = nx * numChunksY + ny;
                if (neighborIndex >= 0 && neighborIndex < Chunks.Count) // Double-check list bounds
                {
                    neighbors.Add(Chunks[neighborIndex]);
                }
                else
                {
                    Debug.LogWarning($"Chunk index {neighborIndex} out of range for Chunks list (size: {Chunks.Count}) from ({chunkX}, {chunkY})");
                }
            }
        }
        return neighbors;
    }
}