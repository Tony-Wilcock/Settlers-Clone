using System;
using System.Collections.Generic;
using UnityEngine;

namespace PunkyFruitBat
{
    public class PathFinder
    {
        private readonly PathManager pathManager;
        private readonly HexGridManager manager;

        public PathFinder(PathManager pathManager, HexGridManager manager)
        {
            this.pathManager = pathManager;
            this.manager = manager;
        }

        public List<int> FindPath(int startVertexIndex, int endVertexIndex)
        {
            if (!manager.AdjacencyList.ContainsKey(startVertexIndex) || !manager.AdjacencyList.ContainsKey(endVertexIndex))
            {
                return null;
            }

            if (startVertexIndex == endVertexIndex)
            {
                return new List<int> { startVertexIndex };
            }

            var cameFrom = new Dictionary<int, int>();
            var costSoFar = new Dictionary<int, float>();
            var frontier = new PriorityQueue<int>();

            frontier.Enqueue(startVertexIndex, 0);
            cameFrom[startVertexIndex] = -1;
            costSoFar[startVertexIndex] = 0;

            while (!frontier.IsEmpty())
            {
                int current = frontier.Dequeue();
                if (current == endVertexIndex)
                {
                    return ReconstructPath(cameFrom, endVertexIndex);
                }

                if (!manager.AdjacencyList.TryGetValue(current, out var neighbors)) continue;

                foreach (int neighbor in neighbors)
                {
                    Node nodeData = manager.NodeManager.GetNode(neighbor);
                    if (nodeData.hasObstacle || nodeData.hasBuilding) continue;

                    bool isInCurrentPath = pathManager.IsInPathCreationMode && pathManager.PathBuilder.CurrentPath.Contains(neighbor);
                    if (isInCurrentPath) continue;
                    if (nodeData.hasPath && neighbor != endVertexIndex && !isInCurrentPath) continue;
                    if (nodeData.hasFlag && neighbor != endVertexIndex) continue;

                    float newCost = costSoFar[current] + GetCost(current, neighbor);
                    if (!costSoFar.TryGetValue(neighbor, out float oldCost) || newCost < oldCost)
                    {
                        costSoFar[neighbor] = newCost;
                        float priority = newCost + Heuristic(neighbor, endVertexIndex);
                        frontier.Enqueue(neighbor, priority);
                        cameFrom[neighbor] = current;
                    }
                }
            }
            return null;
        }

        private List<int> ReconstructPath(Dictionary<int, int> cameFrom, int endVertexIndex)
        {
            var path = new List<int>();
            int current = endVertexIndex;
            while (current != -1)
            {
                path.Add(current);
                if (!cameFrom.TryGetValue(current, out current)) break;
            }
            path.Reverse();
            return path;
        }

        private float GetCost(int startVertexIndex, int endVertexIndex)
        {
            Vector3 startPos = manager.globalVertices[startVertexIndex];
            Vector3 endPos = manager.globalVertices[endVertexIndex];
            float heightDifference = endPos.y - startPos.y;
            float slopeCost = heightDifference > 0 ? heightDifference * 2f : heightDifference < 0 ? heightDifference * -0.5f : 0f;
            return 1f + slopeCost;
        }

        private float Heuristic(int current, int target)
        {
            return Vector3.Distance(manager.globalVertices[current], manager.globalVertices[target]);
        }
    }

    // PriorityQueue remains unchanged as it’s already efficient
    public class PriorityQueue<T>
    {
        private readonly List<(float priority, int counter, T item)> heap = new();
        private int counter = 0;

        public int Count => heap.Count;

        public void Enqueue(T item, float priority)
        {
            heap.Add((priority, counter++, item));
            HeapifyUp(heap.Count - 1);
        }

        public T Dequeue()
        {
            if (Count == 0) throw new InvalidOperationException("PriorityQueue is empty");

            T item = heap[0].item;
            heap[0] = heap[Count - 1];
            heap.RemoveAt(Count - 1);

            if (Count > 0) HeapifyDown(0);

            return item;
        }

        public bool IsEmpty() => Count == 0;

        public void Clear()
        {
            heap.Clear();
            counter = 0;
        }

        private void HeapifyUp(int index)
        {
            while (index > 0)
            {
                int parent = (index - 1) / 2;
                if (Compare(heap[parent], heap[index]) <= 0) break;

                Swap(parent, index);
                index = parent;
            }
        }

        private void HeapifyDown(int index)
        {
            int minIndex = index;
            int leftChild = 2 * index + 1;
            int rightChild = 2 * index + 2;

            if (leftChild < Count && Compare(heap[leftChild], heap[minIndex]) < 0)
                minIndex = leftChild;
            if (rightChild < Count && Compare(heap[rightChild], heap[minIndex]) < 0)
                minIndex = rightChild;

            if (minIndex != index)
            {
                Swap(index, minIndex);
                HeapifyDown(minIndex);
            }
        }

        private void Swap(int i, int j)
        {
            (heap[j], heap[i]) = (heap[i], heap[j]);
        }

        private int Compare((float priority, int counter, T item) a, (float priority, int counter, T item) b)
        {
            int cmp = a.priority.CompareTo(b.priority);
            return cmp != 0 ? cmp : a.counter.CompareTo(b.counter);
        }
    }
}
