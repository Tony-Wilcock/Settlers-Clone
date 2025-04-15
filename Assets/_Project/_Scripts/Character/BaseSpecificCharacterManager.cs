using System.Collections.Generic;
using UnityEngine;

namespace PunkyFruitBat
{
    // Base class providing common fields and potentially methods for specific managers
    public abstract class BaseSpecificCharacterManager<T> : ICharacterTypeManager where T : Character
    {
        public abstract CharacterType ManagedType { get; }

        protected CharacterManager mainManager;
        protected HexGridManager gridManager;
        protected CharacterPrefabs_SO characterPrefabs;
        protected Transform typeSpecificParentTransform;

        protected Queue<T> characterPool = new();

        public virtual void Initialise(CharacterManager mainManager, HexGridManager gridManager, CharacterPrefabs_SO characterPrefabs, Transform parentTransform)
        {
            this.mainManager = mainManager;
            this.gridManager = gridManager;
            this.characterPrefabs = characterPrefabs;
            this.typeSpecificParentTransform = parentTransform;
        }

        /// <summary>
        /// Initialises the character pool. Call this from HandleGridComplete in derived classes or base Initialise.
        /// </summary>
        protected virtual void InitialisePool(int initialSize = 50) // Example initial size
        {
            characterPool = new Queue<T>();
            IncreasePool(initialSize); // Use the generic IncreasePool
        }

        /// <summary>
        /// Increases the size of the character pool.
        /// </summary>
        /// <param name="amount">Number of characters to add.</param>
        protected virtual void IncreasePool(int amount)
        {
            // Use ManagedType which MUST be correctly implemented in the derived class
            GameObject prefab = characterPrefabs.characterPrefabs[(int)ManagedType];
            if (prefab == null)
            {
                Debug.LogError($"[{GetType().Name}] Prefab for {ManagedType} not found in CharacterPrefabs_SO!");
                return;
            }

            // Cache values that don't change per loop iteration
            int storehouseNode = gridManager.BuildingManager.GetStorehouseNode();
            Vector3 initialPosition = gridManager.NodeManager.GetNodePosition(storehouseNode);

            for (int i = 0; i < amount; i++)
            {
                GameObject characterGO = GameObject.Instantiate(prefab);
                characterGO.transform.position = initialPosition;

                if (typeSpecificParentTransform != null)
                    characterGO.transform.SetParent(typeSpecificParentTransform);
                else
                    Debug.LogWarning($"[{GetType().Name}] Parent transform for {ManagedType} not set. Character '{characterGO.name}' will be at scene root.", characterGO);

                // Use the generic type T here
                if (!characterGO.TryGetComponent<T>(out T characterComponent))
                {
                    Debug.LogError($"[{GetType().Name}] Prefab for {ManagedType} is missing the required component: {typeof(T).Name}! Prefab: {prefab.name}", prefab);
                    GameObject.Destroy(characterGO); // Clean up unusable instance
                    continue;
                }

                // Assuming InitialiseCharacter is on the base Character class
                characterComponent.InitialiseCharacter(ManagedType, storehouseNode);
                characterGO.SetActive(false);
                characterPool.Enqueue(characterComponent); // Enqueue the component of type T
            }
        }

        /// <summary>
        /// Gets a character instance from the pool.
        /// </summary>
        /// <param name="spawnNodeIndex">Optional node index to spawn at immediately.</param>
        /// <returns>A character of type T, or null if unavailable.</returns>
        public virtual Character GetCharacterInstance(int spawnNodeIndex = -1) // Return Character for interface compatibility
        {
            if (characterPool.Count == 0)
            {
                Debug.Log($"[{GetType().Name}] Pool for {ManagedType} empty, increasing size.");
                IncreasePool(20); // Increase by a smaller amount when needed, configurable
            }

            if (characterPool.Count == 0) // Check again after trying to increase
            {
                Debug.LogError($"[{GetType().Name}] Failed to increase pool for {ManagedType} or pool still empty. Cannot get instance.");
                return null;
            }

            T character = characterPool.Dequeue(); // Dequeue type T

            if (character != null)
            {
                if (spawnNodeIndex != -1)
                {
                    // Ensure the node is valid before setting position
                    Node spawnNode = gridManager.NodeManager.GetNode(spawnNodeIndex);
                    if (spawnNode != null)
                    {
                        character.transform.position = spawnNode.Position; // Use Node's position
                        character.CurrentNodeIndex = spawnNodeIndex; // Update current node
                    }
                    else
                    {
                        Debug.LogWarning($"[{GetType().Name}] Invalid spawnNodeIndex ({spawnNodeIndex}) provided. Spawning at default pool location.", character.gameObject);
                        // Optionally default to storehouse or keep initial pool pos
                        character.transform.position = gridManager.NodeManager.GetNodePosition(gridManager.BuildingManager.GetStorehouseNode());
                    }
                }
                else // If no specific spawn index, ensure it's at the work node (usually storehouse for pool)
                {
                    character.transform.position = gridManager.NodeManager.GetNodePosition(character.WorkNodeIndex);
                }

                character.gameObject.SetActive(true);
                return character; // Return as base Character type
            }
            else
            {
                Debug.LogError($"[{GetType().Name}] Dequeued a null character from the pool for {ManagedType}!", this as UnityEngine.Object);
                return null;
            }
        }

        /// <summary>
        /// Returns a character instance to the pool (typically involving movement).
        /// </summary>
        /// <param name="character">The character to return.</param>
        public virtual void ReturnCharacterInstance(Character character)
        {
            // Type check
            if (character is not T typedCharacter)
            {
                Debug.LogError($"[{GetType().Name}] Tried to return a character of incorrect type ({character.GetType().Name}) to the pool for {typeof(T).Name}. Expected: {typeof(T).Name}", character);
                return;
            }

            if (typedCharacter == null || !typedCharacter.gameObject.activeInHierarchy) return; // Already returned or destroyed

            // Standard return: move back to work node (e.g., storehouse) then deactivate and enqueue.
            typedCharacter.StopAllCoroutines(); // Stop any current actions
            int storehouseNode = gridManager.BuildingManager.GetStorehouseNode();
            typedCharacter.SetWorkNodeIndex(storehouseNode); // Reset home node

            // Use the Character's MoveCharacter coroutine with a callback
            typedCharacter.StartCoroutine(typedCharacter.MoveCharacter(typedCharacter.WorkNodeIndex, () =>
            {
                // This code runs *after* the character reaches the WorkNodeIndex
                if (typedCharacter != null && typedCharacter.gameObject != null) // Double-check it wasn't destroyed
                {
                    typedCharacter.gameObject.SetActive(false);
                    if (!characterPool.Contains(typedCharacter)) // Avoid duplicates
                    {
                        characterPool.Enqueue(typedCharacter);
                    }
                }
            }));
        }

        /// <summary>
        /// Instantly returns a character instance to the pool without movement.
        /// </summary>
        /// <param name="character">The character to return instantly.</param>
        public virtual void InstantlyReturnCharacterInstance(Character character)
        {
            if (character is not T typedCharacter)
            {
                Debug.LogError($"[{GetType().Name}] Tried to instantly return a character of incorrect type ({character.GetType().Name}) to the pool for {typeof(T).Name}.", character);
                return;
            }

            if (typedCharacter == null) return;

            //Debug.Log($"[{GetType().Name}] Instantly returning {typedCharacter.name} to pool for {ManagedType}.");
            typedCharacter.StopAllCoroutines();
            typedCharacter.gameObject.SetActive(false);

            // Optionally reset position to work node instantly
            typedCharacter.transform.position = gridManager.NodeManager.GetNodePosition(typedCharacter.WorkNodeIndex);

            // Avoid adding duplicates if already somehow in the pool
            if (!characterPool.Contains(typedCharacter))
            {
                characterPool.Enqueue(typedCharacter);
            }
            else
            {
                Debug.LogWarning($"[{GetType().Name}] Character {typedCharacter.name} was already in the pool during instant return?", typedCharacter);
            }
        }

        // Abstract methods forcing implementation in derived classes
        public abstract void HandleGridComplete();
        //public abstract Character GetCharacterInstance(int spawnNodeIndex = -1);
        //public abstract void ReturnCharacterInstance(Character character);
        //public abstract void InstantlyReturnCharacterInstance(Character character);

        // Virtual methods for event handling - derived classes can override if they care
        public virtual void Unsubscribe() { } // Default: Do nothing, but derived might need it
    }
}