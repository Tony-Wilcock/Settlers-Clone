using System;
using System.Collections.Generic;
using UnityEngine;

namespace PunkyFruitBat
{
    public enum Direction
    {
        Northeast,
        East,
        Southeast,
        Southwest,
        West,
        Northwest,
    }

    public class NodeManager
    {
        public event Action<int> OnNearestVertexUpdated;
        public int NearestNodeIndex { get; private set; }

        private readonly List<int> nodeNeighbours = new();
        private HexGridManager manager;
        private int lastNearestVertex;
        private Vector3 LastMousePosition;

        public void Initialise(HexGridManager manager)
        {
            this.manager = manager;
        }

        public void UpdateCurrentNodeIndex(Vector3 mousePosition)
        {
            if (Input.mousePosition != LastMousePosition)
            {
                Ray ray = manager.MainCamera.ScreenPointToRay(mousePosition);

                if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, manager.Settings.hexGridLayerMask))
                {
                    float closestDistance = float.MaxValue;
                    NearestNodeIndex = -1;

                    for (int c = 0; c < manager.chunks.Count; c++)
                    {
                        Chunk chunk = manager.chunks[c];
                        if (chunk.chunkObject == hit.collider.gameObject)
                        {
                            Vector3 localHitPoint = chunk.chunkObject.transform.InverseTransformPoint(hit.point);
                            for (int i = 0; i < chunk.vertices.Length; i++)
                            {
                                float distance = Vector3.Distance(localHitPoint, chunk.vertices[i]);
                                if (distance < closestDistance && distance < manager.Settings.cellSize * 0.6f)
                                {
                                    LastMousePosition = Input.mousePosition;
                                    closestDistance = distance;
                                    NearestNodeIndex = chunk.localToGlobalVertexMap[i];

                                    if (NearestNodeIndex != lastNearestVertex)
                                    {
                                        OnNearestVertexUpdated?.Invoke(NearestNodeIndex);
                                    }

                                    lastNearestVertex = NearestNodeIndex;
                                }
                            }
                            break; // Chunk found, no need to check other chunks
                        }
                    }
                }
                else
                {
                    if (NearestNodeIndex != -1)
                    {
                        NearestNodeIndex = -1;
                        OnNearestVertexUpdated?.Invoke(-1);
                    }
                }
            }
        }

        public List<int> GetNodeNieghbors(int vertexIndex)
        {
            nodeNeighbours.Clear();
            if (manager.AdjacencyList != null && manager.AdjacencyList.ContainsKey(vertexIndex))
            {
                nodeNeighbours.AddRange(manager.AdjacencyList[vertexIndex]);
            }
            else
            {
                // Optional: Log for debugging
                if (manager.AdjacencyList == null) Debug.LogWarning($"AdjacencyList is null for vertex {vertexIndex}. Returning empty neighbor list.");
            }
            return nodeNeighbours;
        }

        public Vector3 GetNodePosition(int vertexIndex)
        {
            return manager.globalVertices[vertexIndex];
        }

        public int GetNeighbourInDirection(int vertexIndex, Direction direction)
        {
            List<int> neighbors = GetNodeNieghbors(vertexIndex);
            if (neighbors.Count == 0) return -1;

            Vector3 centerPos = manager.globalVertices[vertexIndex];
            int bestMatchIndex = -1;
            float smallestAngleDiff = float.MaxValue;

            // Define movementDirection angles based on orientation
            float targetAngle = direction switch
            {
                Direction.Northeast => 60f,  // NE
                Direction.East => 0f,        // E
                Direction.Southeast => 300f, // SE
                Direction.Southwest => 240f, // SW
                Direction.West => 180f,      // W
                Direction.Northwest => 120f, // NW
                _ => throw new System.ArgumentException($"Unknown movementDirection: {direction}")
            };

            Vector3 targetDirection = new(Mathf.Cos(targetAngle * Mathf.Deg2Rad), 0, Mathf.Sin(targetAngle * Mathf.Deg2Rad));

            foreach (int neighborIndex in neighbors)
            {
                Vector3 neighborPos = manager.globalVertices[neighborIndex];
                Vector3 directionVec = (neighborPos - centerPos).normalized;
                float angleDiff = Vector3.Angle(directionVec, targetDirection);

                if (angleDiff < smallestAngleDiff && angleDiff < 30f) // Tolerance of 30° to ensure correct neighbor
                {
                    smallestAngleDiff = angleDiff;
                    bestMatchIndex = neighborIndex;
                }
            }

            if (bestMatchIndex == -1)
            {
                Debug.LogWarning($"No neighbor found in movementDirection {direction} for vertex {vertexIndex}");
            }

            return bestMatchIndex;
        }

        public Node GetNode(int vertexIndex)
        {
            return manager.EditableVerticesIndices[vertexIndex];
        }
    }
}
