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

                if (!manager.AdjacencyList.TryGetValue(current, out var neighbors)) continue; // No neighbours for this node

                for (int i = 0; i < neighbors.Count; i++)
                {
                    int neighbor = neighbors[i];
                    Node nodeData = manager.NodeManager.GetNode(neighbor);
                    if (nodeData.HasObstacle || nodeData.HasBuilding) continue;

                    bool isInCurrentPath = pathManager.IsInPathCreationMode && pathManager.PathBuilder.CurrentPath.Contains(neighbor);
                    if (isInCurrentPath) continue;
                    if (nodeData.HasPath && neighbor != endVertexIndex && !isInCurrentPath) continue;
                    if (nodeData.HasFlag && neighbor != endVertexIndex) continue;

                    CalculatePathFindingCost(endVertexIndex, cameFrom, costSoFar, frontier, current, neighbor);
                }
            }
            return null;
        }

        public List<int> FindRoute(int startVertexIndex, int endVertexIndex)
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

                Node currentNode = manager.NodeManager.GetNode(current);
                if (!currentNode.HasPath && !currentNode.HasFlag) continue; // CRITICAL CHANGE: Must be on a path or flag.

                // --- Get neighbors based on path and flag connections ---
                List<int> neighbors = new();

                if (currentNode.HasPath)
                {
                    Path path = currentNode.GetPathOnNode();

                    // Get neighbors *along the current path*.
                    int currentIndexInPath = path.Nodes.IndexOf(current);
                    if (currentIndexInPath > 0)
                    {
                        neighbors.Add(path.Nodes[currentIndexInPath - 1]); // Previous node on path
                    }
                    if (currentIndexInPath < path.Nodes.Count - 1)
                    {
                        neighbors.Add(path.Nodes[currentIndexInPath + 1]); // Next node on path
                    }
                }

                if (currentNode.HasFlag)
                {
                    // Get connected paths from the flag.
                    Flag flag = currentNode.GetFlagOnNode();
                    foreach (Path path in pathManager.GetAllPaths.Values) //Iterate paths
                    {
                        if ((path.Flag1 == flag || path.Flag2 == flag))  // Check if connected, and no carrier.
                        {
                            // Add the *first* node of the connected path (that isn't the current flag)
                            if (path.Nodes[0] == current)
                            {
                                if (path.Nodes.Count > 1)
                                {
                                    neighbors.Add(path.Nodes[1]); // Add the next node after the flag.
                                }
                            }
                            else if (path.Nodes[path.Nodes.Count - 1] == current)
                            {
                                if (path.Nodes.Count > 1)
                                {
                                    neighbors.Add(path.Nodes[path.Nodes.Count - 2]);
                                }
                            }
                        }
                    }
                }
                // --- End of neighbor calculation ---


                for (int i = 0; i < neighbors.Count; i++)
                {
                    int neighbor = neighbors[i];

                    CalculatePathFindingCost(endVertexIndex, cameFrom, costSoFar, frontier, current, neighbor);
                }
            }
            return null;
        }

        public List<int> FindWalkableRouteThroughPaths(int start, int end)
        {
            // Find a path for the characters to walk from their current node to the end node. 
            // Must only walk along paths, from flag to flag.

            List<int> path = new();

            List<int> route = FindRoute(start, end); // Store the result

            if (route != null) // Check if a route was found
            {
                path.AddRange(route);
            }

            return path;
        }

        public List<int> FindDirectRoute(int startVertexIndex, int endVertexIndex)
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

                if (!manager.AdjacencyList.TryGetValue(current, out var neighbors)) continue; // No neighbours for this node

                for (int i = 0; i < neighbors.Count; i++)
                {
                    int neighbor = neighbors[i];
                    Node nodeData = manager.NodeManager.GetNode(neighbor);
                    if (nodeData.HasObstacle || nodeData.HasBuilding) continue;

                    CalculatePathFindingCost(endVertexIndex, cameFrom, costSoFar, frontier, current, neighbor);
                }
            }
            return null;
        }

        public bool IsPathConnectedToStorehouse(Path path)
        {
            if (path == null) return false;

            // Get the starting flag's node index (either flag will do).
            int startNodeIndex = path.Flag1.Id;
            int storehouseNodeIndex = manager.BuildingManager.GetStorehouseEntranceNode();

            // Use a HashSet for efficient "visited" tracking.
            HashSet<int> visited = new HashSet<int>();
            Stack<int> stack = new Stack<int>();

            stack.Push(startNodeIndex);
            visited.Add(startNodeIndex);

            while (stack.Count > 0)
            {
                int current = stack.Pop();

                if (current == storehouseNodeIndex)
                {
                    return true; // Found a connection!
                }

                Node currentNode = manager.NodeManager.GetNode(current);

                if (currentNode.HasFlag)
                {
                    // Get connected paths from the flag.
                    Flag flag = currentNode.GetFlagOnNode();
                    foreach (var connectedPath in pathManager.GetAllPaths.Values) //Iterate paths
                    {
                        if (connectedPath.Flag1 == flag || connectedPath.Flag2 == flag)
                        {
                            // Get next node
                            int nextNodeIndex;
                            if (connectedPath.Nodes[0] == current)
                            {
                                nextNodeIndex = connectedPath.Nodes[1];
                            }
                            else
                            {
                                nextNodeIndex = connectedPath.Nodes[connectedPath.Nodes.Count - 2];
                            }


                            // If not visited, add to the stack for exploration.
                            if (!visited.Contains(nextNodeIndex))
                            {
                                visited.Add(nextNodeIndex);
                                stack.Push(nextNodeIndex);
                            }
                        }
                    }
                }
                else if (currentNode.HasPath)
                {
                    // Get neighbors *along the current path*.
                    Path currentPath = currentNode.GetPathOnNode();
                    int currentIndexInPath = currentPath.Nodes.IndexOf(current);
                    if (currentIndexInPath > 0)
                    {
                        int nextNodeIndex = currentPath.Nodes[currentIndexInPath - 1];
                        if (!visited.Contains(nextNodeIndex))
                        {
                            visited.Add(nextNodeIndex);
                            stack.Push(nextNodeIndex);
                        }
                    }
                    if (currentIndexInPath < currentPath.Nodes.Count - 1)
                    {
                        int nextNodeIndex = currentPath.Nodes[currentIndexInPath + 1];
                        if (!visited.Contains(nextNodeIndex))
                        {
                            visited.Add(nextNodeIndex);
                            stack.Push(nextNodeIndex);
                        }
                    }
                }
            }

            return false; // No path to HQ found.
        }

        private void CalculatePathFindingCost(int endVertexIndex, Dictionary<int, int> cameFrom, Dictionary<int, float> costSoFar, PriorityQueue<int> frontier, int current, int neighbor)
        {
            float newCost = costSoFar[current] + GetCost(current, neighbor);
            if (!costSoFar.TryGetValue(neighbor, out float oldCost) || newCost < oldCost)
            {
                costSoFar[neighbor] = newCost;
                float priority = newCost + Heuristic(neighbor, endVertexIndex);
                frontier.Enqueue(neighbor, priority);
                cameFrom[neighbor] = current;
            }
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
