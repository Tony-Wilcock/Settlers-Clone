using System.Collections.Generic;
using UnityEngine;

namespace PunkyFruitBat
{
    public class HexGridEdgeIdentifier
    {
        private HexGridManager manager;

        private Dictionary<int, List<int>> AdjacencyList => manager.AdjacencyList;
        private Vector3[] GlobalVertices => manager.globalVertices;

        public HashSet<int> edgeVertices = new();

        public void Initialise(HexGridManager manager)
        {
            this.manager = manager;
        }

        public HashSet<int> IdentifyEdgeVertices()
        {
            edgeVertices.Clear();
            for (int i = 0; i < manager.EditableVerticesIndices.Length; i++)
            {
                if (manager.AdjacencyList[manager.EditableVerticesIndices[i].VertexIndex].Count < 6)
                {
                    edgeVertices.Add(manager.EditableVerticesIndices[i].VertexIndex); //Make sure you are adding the vertex index here.
                }
            }
            return edgeVertices;
        }

        // Flattens the edges of the hex grid to ground level for visual consistency
        public void ForceEdgeVerticesToZero()
        {
            foreach (int vertexIndex in edgeVertices)
            {
                Vector3 pos = GlobalVertices[vertexIndex];
                pos.y = 0; // Force edge vertices to height zero
                GlobalVertices[vertexIndex] = pos;
                SyncVertexToChunks(vertexIndex); // Ensure all chunks reflect this
            }
        }

        private void SyncVertexToChunks(int globalIndex)
        {
            Vector3 pos = GlobalVertices[globalIndex];
            foreach (var chunk in manager.chunks)
            {
                if (chunk.globalToLocalVertexMap.TryGetValue(globalIndex, out int localIndex))
                {
                    chunk.vertices[localIndex] = pos;
                }
            }
        }
    }
}
