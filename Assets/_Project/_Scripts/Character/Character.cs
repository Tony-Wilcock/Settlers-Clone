using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PunkyFruitBat
{
    public abstract class Character : MonoBehaviour
    {
        [field: SerializeField] protected CharacterType characterType;

        public CharacterType CharacterType => characterType;

        protected HexGridManager manager;
        protected CharacterManager characterManager;

        protected float moveSpeed = 5f;
        [field: SerializeField] public int WorkNodeIndex { get; protected set; }
        [field: SerializeField] public int CurrentNodeIndex { get; set; }

        protected virtual void Awake()
        {
            manager = HexGridManager.Instance;
            characterManager = manager.CharacterManager;
        }

        public void InitialiseCharacter(CharacterType characterType, int startNode)
        {
            this.characterType = characterType;
            CurrentNodeIndex = startNode;
            WorkNodeIndex = startNode;
        }

        public void SetWorkNodeIndex(int index)
        {
            WorkNodeIndex = index;
        }

        /// <summary>
        /// Coroutine to move the character from its current node to a specified end node.
        /// Handles moving out of/into buildings via their entrance nodes.
        /// Executes an optional callback action upon completion of all movement.
        /// </summary>
        /// <param name="endNodeIndex">The index of the final target node.</param>
        /// <param name="onComplete">Action to execute once movement is finished (optional).</param>
        public IEnumerator MoveCharacter(int endNodeIndex, Action onComplete = null)
        {
            // --- Basic Validation ---
            if (manager == null || manager.NodeManager == null)
            {
                Debug.LogError("NodeManager is not available!", this);
                yield break; // Cannot proceed
            }

            Node currentNode = manager.NodeManager.GetNode(CurrentNodeIndex);
            Node endNode = manager.NodeManager.GetNode(endNodeIndex);

            if (currentNode == null || endNode == null)
            {
                Debug.LogError($"MoveCharacter: Invalid start ({CurrentNodeIndex}) or end ({endNodeIndex}) node.", this);
                yield break; // Cannot proceed
            }

            if (CurrentNodeIndex == endNodeIndex)
            {
                Debug.LogWarning($"MoveCharacter: Already at the destination node {endNodeIndex}. Executing callback immediately.", this);
                onComplete?.Invoke(); // Already there, just run callback
                yield break;
            }

            // --- Movement Logic ---

             yield return StartCoroutine(WaitForSecondsFactory.WaitCoroutine(0.1f));

            int pathfindingStartIndex = CurrentNodeIndex;
            int pathfindingEndIndex = endNodeIndex;
            bool movingToBuildingInterior = false;

            // --- Step 1: Handle Starting Inside a Building ---
            // If currently on a building node (not its entrance), first move to the entrance.
            if (currentNode.HasBuilding)
            {
                Building startBuilding = currentNode.GetBuildingOnNode();
                if (startBuilding != null && startBuilding.EntranceIndex != CurrentNodeIndex)
                {
                    // Use MoveAlongRoute for direct node-to-node (inside building to entrance)
                    yield return StartCoroutine(MoveAlongRoute(new List<int> { CurrentNodeIndex, startBuilding.EntranceIndex }));
                    // StartNodeIndex should now be the entrance index after MoveAlongRoute completes
                    pathfindingStartIndex = CurrentNodeIndex; // Update the starting point for the main pathfinding
                }
                else if (startBuilding == null)
                {
                    Debug.LogWarning($"Node {CurrentNodeIndex} marked HasBuilding but GetBuildingOnNode() returned null.", this);
                }
            }

            // --- Step 2: Handle Destination Being a Building ---
            // If the final destination is a building node (not its entrance),
            // the pathfinding should target the building's entrance first.
            if (endNode.HasBuilding)
            {
                Building targetBuilding = endNode.GetBuildingOnNode();
                if (targetBuilding != null && targetBuilding.EntranceIndex != endNodeIndex)
                {
                    // Only pathfind to the entrance if we aren't already there
                    if (pathfindingStartIndex != targetBuilding.EntranceIndex)
                    {
                        pathfindingEndIndex = targetBuilding.EntranceIndex; // Target the entrance for the main move
                    }
                    else
                    {
                        // We are starting at the entrance of the target building.
                        // No main pathfinding needed, just the final step inside.
                        pathfindingEndIndex = pathfindingStartIndex; // Prevent MoveCharacterToDestinationCoroutine call
                    }
                    movingToBuildingInterior = true; // Flag that we need the final step into the building
                }
                else if (targetBuilding == null)
                {
                    Debug.LogWarning($"Node {endNodeIndex} marked HasBuilding but GetBuildingOnNode() returned null.", this);
                    // Proceed as if it's a normal node? Or stop? For now, proceed.
                    movingToBuildingInterior = false;
                }
                else // Target node *is* the entrance node itself
                {
                    movingToBuildingInterior = false;
                    pathfindingEndIndex = endNodeIndex; // Target the entrance node directly
                }
            }

            // --- Step 3: Perform Main Movement (if necessary) ---
            // Move from the adjusted start index to the adjusted end index (often the building entrance).
            if (pathfindingStartIndex != pathfindingEndIndex)
            {
                yield return StartCoroutine(MoveCharacterToDestinationCoroutine(pathfindingStartIndex, pathfindingEndIndex));
                // StartNodeIndex should now be pathfindingEndIndex
            }

            // --- Step 4: Handle Final Step Into Building Interior (if flagged) ---
            if (movingToBuildingInterior && CurrentNodeIndex == pathfindingEndIndex) // Check we reached the entrance
            {
                // Use MoveAlongRoute for direct node-to-node (entrance to inside building)
                yield return StartCoroutine(MoveAlongRoute(new List<int> { CurrentNodeIndex, endNodeIndex }));
                // StartNodeIndex should now be endNodeIndex
            }
            else if (movingToBuildingInterior && CurrentNodeIndex != pathfindingEndIndex)
            {
                Debug.LogError($"Intended to move into building {endNodeIndex}, but failed to reach entrance {pathfindingEndIndex}. Current node: {CurrentNodeIndex}", this);
                // Don't invoke callback on failure? Or invoke anyway?
            }

            // --- Step 5: Execute Callback ---
            // This point is reached after all potential movement steps are completed or skipped.
            try // Wrap callback invocation in try-catch for safety
            {
                onComplete?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"Error executing MoveCharacter onComplete callback: {e}", this);
            }
        }

        private IEnumerator MoveCharacterToDestinationCoroutine(int startPosition, int endPosition)
        {
            List<int> path = manager.PathManager.PathFinder.FindWalkableRouteThroughPaths(startPosition, endPosition);

            if (path == null || path.Count < 1)
            {
                Debug.LogWarning($"Character {GetInstanceID()}: No WalkableRouteThroughPaths found. Trying FindDirectRoute."); // Log Y
                path = manager.PathManager.PathFinder.FindDirectRoute(startPosition, endPosition);
                if (path == null || path.Count < 1)
                {
                    Debug.LogError($"Character {GetInstanceID()}: No path found from {startPosition} to {endPosition}! Movement cancelled."); // Log Z
                    yield break; // Exit if no path found
                }
            }

            yield return StartCoroutine(MoveAlongRoute(path));
        }

        protected virtual IEnumerator MoveAlongRoute(List<int> route)
        {
            for (int i = 0; i < route.Count; i++)
            {
                Vector3 targetPosition = manager.NodeManager.GetNodePosition(route[i]);
                while (transform.position != targetPosition)
                {
                    transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
                    yield return null;
                }
                CurrentNodeIndex = route[i];
            }
        }
    }
}
