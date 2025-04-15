using System.Collections.Generic;
using UnityEngine;

namespace PunkyFruitBat
{
    public class VertexManipulator
    {
        private HexGridManager hexGridManager;
        private HexGridSettings settings;
        private Vector3[] globalVertices;
        private Dictionary<int, List<int>> adjacencyList;
        private HashSet<int> edgeVertices;
        private List<Chunk> chunks; // Add reference to chunks
        private Node[] editableNodes; // Reference to the Node components array
        private Dictionary<(int x, int y), List<int>> cellVertexMap; // Map from cell coords to global vertex indices

        public void Initialise(HexGridManager hexGridManager)
        {
            this.hexGridManager = hexGridManager;
            settings = hexGridManager.Settings;
            globalVertices = hexGridManager.globalVertices;
            adjacencyList = hexGridManager.AdjacencyList;
            edgeVertices = hexGridManager.EdgeVertices;
            chunks = hexGridManager.chunks; // Get chunks reference
            editableNodes = hexGridManager.EditableVerticesIndices; // Get reference to Nodes array
            cellVertexMap = hexGridManager.cellVertexMap;

            // --- Detailed Null Check ---
            List<string> nullReferences = new List<string>();
            if (settings == null) nullReferences.Add("settings");
            if (globalVertices == null) nullReferences.Add("globalVertices");
            if (adjacencyList == null) nullReferences.Add("adjacencyList");
            if (edgeVertices == null) nullReferences.Add("edgeVertices");
            if (chunks == null) nullReferences.Add("chunks");
            if (editableNodes == null) nullReferences.Add("editableNodes");
            if (cellVertexMap == null) nullReferences.Add("cellVertexMap");

            if (nullReferences.Count > 0)
            {
                Debug.LogError($"[VertexManipulator.Initialise] Failed! The following required references were null: {string.Join(", ", nullReferences)}");
            }
        }

        /// <summary>
        /// Adjusts the height of a specific vertex and applies smoothing to neighbours.
        /// Triggers updates for the affected chunk meshes and Node component positions.
        /// </summary>
        /// <param name="vertexIndex">The global index of the vertex to modify.</param>
        /// <param name="yMovement">The amount to change the height by.</param>
        public void AdjustVertexHeight(int vertexIndex, float yMovement)
        {
            // --- Input Validation ---
            if (globalVertices == null) { Debug.LogError("globalVertices is null!"); return; }
            if (vertexIndex < 0 || vertexIndex >= globalVertices.Length) { return; } // Invalid index
            if (edgeVertices == null || edgeVertices.Contains(vertexIndex)) { return; } // Cannot modify edges
            if (adjacencyList == null) { Debug.LogError("adjacencyList is null!"); return; }
            if (editableNodes == null) { Debug.LogError("editableNodes array is null!"); return; } // Check nodes array

            // --- Apply Initial Height Change ---
            Vector3 pos = globalVertices[vertexIndex];
            pos.y += yMovement;
            globalVertices[vertexIndex] = pos;

            HashSet<int> modifiedVertices = new HashSet<int> { vertexIndex };

            // --- Height Smoothing Logic ---
            Queue<int> verticesToCheck = new Queue<int>();
            verticesToCheck.Enqueue(vertexIndex);
            HashSet<int> processedForSmoothing = new HashSet<int>();

            while (verticesToCheck.Count > 0)
            {
                int currentIndex = verticesToCheck.Dequeue();
                if (processedForSmoothing.Contains(currentIndex)) continue;
                processedForSmoothing.Add(currentIndex);

                if (!adjacencyList.ContainsKey(currentIndex)) continue; // Skip if no neighbours defined

                float currentHeight = globalVertices[currentIndex].y;

                foreach (int neighborIndex in adjacencyList[currentIndex])
                {
                    if (neighborIndex < 0 || neighborIndex >= globalVertices.Length) continue;
                    if (edgeVertices.Contains(neighborIndex)) continue;

                    float heightDifference = globalVertices[neighborIndex].y - currentHeight;
                    float tolerance = 0.001f;
                    if (Mathf.Abs(heightDifference) > settings.maxHeightDifference + tolerance)
                    {
                        float targetHeight = currentHeight + Mathf.Sign(heightDifference) * settings.maxHeightDifference;
                        float adjustment = (targetHeight - globalVertices[neighborIndex].y) * settings.smoothingFactor;

                        Vector3 neighborPos = globalVertices[neighborIndex];
                        neighborPos.y += adjustment;
                        globalVertices[neighborIndex] = neighborPos;

                        modifiedVertices.Add(neighborIndex);

                        if (!processedForSmoothing.Contains(neighborIndex))
                        {
                            verticesToCheck.Enqueue(neighborIndex);
                        }
                    }
                }
            }
            // --- End Smoothing Logic ---

            // --- Update Chunks and Nodes ---
            // Apply the changes from globalVertices to the chunk meshes AND Node components.
            UpdateAffectedChunksAndNodes(modifiedVertices);
            // --- End Update ---
        }

        /// <summary>
        /// Finds all chunks containing the modified vertices, updates their local vertex arrays,
        /// calls UpdateMesh() on them, AND updates the corresponding Node component's position data.
        /// </summary>
        /// <param name="changedGlobalIndices">A set of global vertex indices that have been modified.</param>
        private void UpdateAffectedChunksAndNodes(HashSet<int> changedGlobalIndices) // Renamed method
        {
            if (chunks == null) { Debug.LogError("Chunks list is null!"); return; }
            if (editableNodes == null) { Debug.LogError("EditableNodes array is null!"); return; } // Check nodes array

            HashSet<Chunk> chunksToUpdate = new();

            foreach (int globalIndex in changedGlobalIndices)
            {
                // --- Update Node Component ---
                if (globalIndex >= 0 && globalIndex < editableNodes.Length)
                {
                    Node node = editableNodes[globalIndex];
                    if (node != null)
                    {
                        Vector3 updatedPosition = globalVertices[globalIndex];
                        node.SetPosition(updatedPosition); // Update the Node's internal position property
                        node.transform.position = updatedPosition; // Update the actual transform position
                    }
                    else
                    {
                        Debug.LogWarning($"Node component at index {globalIndex} is null in EditableVerticesIndices array.");
                    }
                }
                else
                {
                    Debug.LogWarning($"Global index {globalIndex} is out of bounds for EditableVerticesIndices array (Length: {editableNodes.Length}).");
                }
                // --- End Node Update ---


                // --- Update Chunk Mesh Vertex ---
                foreach (Chunk chunk in chunks)
                {
                    if (chunk.globalToLocalVertexMap != null && chunk.globalToLocalVertexMap.TryGetValue(globalIndex, out int localIndex))
                    {
                        if (chunk.vertices != null && localIndex >= 0 && localIndex < chunk.vertices.Length)
                        {
                            chunk.vertices[localIndex] = globalVertices[globalIndex]; // Sync chunk vertex
                            chunksToUpdate.Add(chunk); // Mark chunk for mesh update
                        }
                        // No warning here if localIndex is invalid, handled elsewhere if needed
                    }
                    // Vertex not in this chunk's map
                }
                // --- End Chunk Vertex Update ---
            }

            // Update the mesh for each affected chunk exactly once
            foreach (Chunk chunk in chunksToUpdate)
            {
                if (chunk != null)
                {
                    chunk.UpdateMesh();
                }
            }
        }

        /// <summary>
        /// Sets the vertex colours for a specific hex identified by its center node index.
        /// </summary>
        /// <param name="centerNodeIndex">The global index of the center node of the hex.</param>
        /// <param name="newColor">The new colour to apply to the hex vertices.</param>
        public void SetHexColor(int centerNodeIndex, Color newColor)
        {
            if (cellVertexMap == null) { Debug.LogError("cellVertexMap is null!"); return; }
            if (editableNodes == null) { Debug.LogError("editableNodes array is null!"); return; }
            if (chunks == null) { Debug.LogError("Chunks list is null!"); return; }
            if (centerNodeIndex < 0 || centerNodeIndex >= editableNodes.Length) { Debug.LogWarning($"Invalid centerNodeIndex: {centerNodeIndex}"); return; }

            //(int, int) targetCellCoords = (-1, -1);
            List<int> hexGlobalIndices = null;
            foreach (var kvp in cellVertexMap)
            {
                // The first index in the list stored in cellVertexMap should be the center vertex.
                if (kvp.Value != null && kvp.Value.Count > 0 && kvp.Value[0] == centerNodeIndex)
                {
                    //targetCellCoords = kvp.Key;
                    hexGlobalIndices = kvp.Value;
                    break;
                }
            }

            if (hexGlobalIndices == null)
            {
                Debug.LogError($"Could not find cell data for center node index {centerNodeIndex} in cellVertexMap.");
                return;
            }
            // --- End Alternative ---


            // Update the stored color data (e.g., on the center Node)
            Node centerNode = editableNodes[centerNodeIndex];

            HashSet<Chunk> chunksToUpdate = new HashSet<Chunk>();

            // Iterate through the 7 global vertex indices for this hex
            foreach (int globalIndex in hexGlobalIndices)
            {
                // Find which chunk(s) contain this vertex
                foreach (Chunk chunk in chunks)
                {
                    if (chunk.globalToLocalVertexMap != null && chunk.globalToLocalVertexMap.TryGetValue(globalIndex, out int localIndex))
                    {
                        // Update the color in the chunk's color array
                        if (chunk.colors != null && localIndex >= 0 && localIndex < chunk.colors.Length)
                        {
                            chunk.colors[localIndex] = newColor;
                            chunksToUpdate.Add(chunk); // Mark chunk for mesh update
                        }
                        else
                        {
                            Debug.LogWarning($"Chunk {chunk.chunkObject?.name} colors array invalid or localIndex {localIndex} out of bounds.");
                        }
                    }
                }
            }

            // Update the mesh colors for each affected chunk
            foreach (Chunk chunk in chunksToUpdate)
            {
                if (chunk != null && chunk.mesh != null)
                {
                    // Directly update mesh colors - potentially more efficient than full UpdateMesh if only color changed
                    chunk.mesh.colors = chunk.colors;

                    // If you ONLY changed color and nothing else (like position),
                    // you might skip calling chunk.UpdateMesh() fully,
                    // but UpdateMesh() is safer as it recalculates normals/bounds etc.
                    // For now, let's assume UpdateMesh handles assigning colors too.
                     chunk.UpdateMesh(); // Call this if UpdateMesh assigns mesh.colors = colors;
                }
            }
        }
    }
}