using System.Collections.Generic;
using UnityEngine;

namespace PunkyFruitBat
{
    public enum CharacterType
    {
        Carrier = 0,
        Builder = 1,
    }

    // CharacterManager now acts as a coordinator
    public class CharacterManager
    {
        // Dependencies (can be protected or private)
        protected HexGridManager manager;
        protected CharacterPrefabs_SO characterPrefabs;

        // Dictionary to hold the specific managers, keyed by CharacterType
        private readonly Dictionary<CharacterType, ICharacterTypeManager> typeManagers = new();
        private Transform MainCharactersParent => manager != null ? manager.CharactersTransform : null;

        // Public property to access HexGridManager if needed by external systems via CharacterManager
        public HexGridManager GridManager => manager;
        // Public property to access prefabs if needed
        public CharacterPrefabs_SO CharacterPrefabs => characterPrefabs;


        public virtual void Initialise(HexGridManager manager, CharacterPrefabs_SO characterPrefabs)
        {
            Debug.Log("Initialising CharacterManager (Coordinator)...");
            this.manager = manager;
            this.characterPrefabs = characterPrefabs;

            // Instantiate and initialise all known specific managers
            InitialiseSpecificManagers();

            // Subscribe to events - these handlers will now delegate
            manager.OnGridComplete += HandleGridComplete;
            manager.PathManager.OnPathCreationCompleted += HandlePathCreationOrConnectionChange;
            manager.PathManager.OnPathRemoved += HandlePathRemoved; // Changed name for clarity
            Debug.Log("CharacterManager initialisation complete.");
        }

        private void InitialiseSpecificManagers()
        {
            // Create instances of your specific managers

            // --- CarrierManager Setup ---
            CarrierManager carrierManager = new();
            Transform carrierParent = CreateOrFindParentTransform(CharacterType.Carrier.ToString());
            RegisterAndInitialiseManager(carrierManager, carrierParent);

            // --- BuilderManager Setup ---
            BuilderManager builderManager = new();
            Transform builderParent = CreateOrFindParentTransform(CharacterType.Builder.ToString());
            RegisterAndInitialiseManager(builderManager, builderParent);
        }

        private Transform CreateOrFindParentTransform(string name)
        {
            // Optionally prefix names (e.g., "Carriers_Parent")
            string gameObjectName = name + 's'; // Use Enum.ToString() which gives "Carrier", "Builder" etc. Add "s" if desired: name + "s"

            Transform parent = null;

            // If we have a main organisational parent (e.g., "Characters"), look for the child there first.
            if (MainCharactersParent != null)
            {
                parent = MainCharactersParent.Find(gameObjectName);
            }
            else // Otherwise, search the scene root (less ideal for organisation)
            {
                GameObject existing = GameObject.Find(gameObjectName);
                if (existing != null) parent = existing.transform;
            }

            if (parent == null)
            {
                // Create the GameObject if it doesn't exist
                GameObject newParentGO = new(gameObjectName);
                Debug.Log($"Created new parent GameObject: {gameObjectName}");
                parent = newParentGO.transform;

                // Parent it under the main "Characters" transform if available
                if (MainCharactersParent != null)
                {
                    parent.SetParent(MainCharactersParent);
                }
            }

            return parent;
        }

        private void RegisterAndInitialiseManager(ICharacterTypeManager specificManager, Transform parentTransform)
        {
            if (specificManager == null) return;

            CharacterType type = specificManager.ManagedType;
            if (!typeManagers.ContainsKey(type))
            {
                typeManagers.Add(type, specificManager);
                // Pass dependencies down
                specificManager.Initialise(this, manager, characterPrefabs, parentTransform);
                Debug.Log($"Registered and Initialised manager for {type}.");
            }
            else
            {
                Debug.LogWarning($"Attempted to register a manager for {type} but one already exists.");
            }
        }

        // --- Event Handlers (Delegation) ---

        private void HandleGridComplete()
        {
            Debug.Log("CharacterManager: Grid Complete event received. Notifying specific managers.");
            // Notify all managers that the grid is complete
            foreach (var specificManager in typeManagers.Values)
            {
                specificManager.HandleGridComplete();
            }
        }

        private void HandlePathCreationOrConnectionChange(Path path)
        {
            // Decide which managers care about path creation/changes. Currently, only Carriers.
            if (typeManagers.TryGetValue(CharacterType.Carrier, out var carrierManager))
            {
                // Optional: Add check if carrierManager is null, though dictionary should prevent that if registration worked.
                Debug.Log($"CharacterManager: Path event for path {path.Id}. Delegating to CarrierManager.");
                carrierManager.HandlePathCreationOrConnectionChange(path);
            }
            else
            {
                Debug.LogWarning("CharacterManager: Received path event, but no CarrierManager registered.");
            }
            // If other types needed path info, delegate to them here too.
        }

        private void HandlePathRemoved(Path path)
        {
            // Decide which managers care about path removal. Currently, only Carriers (to unassign).
            if (typeManagers.TryGetValue(CharacterType.Carrier, out var carrierManager))
            {
                Debug.Log($"CharacterManager: Path removal event for path {path.Id}. Delegating to CarrierManager.");
                carrierManager.HandlePathRemoval(path);
            }
            else
            {
                Debug.LogWarning("CharacterManager: Received path removal event, but no CarrierManager registered.");
            }
            // If other types needed path info, delegate to them here too.
        }


        // --- Public Interface Methods (Delegation) ---

        public Character GetCharacter(CharacterType characterType, int spawnNodeIndex = -1)
        {
            if (typeManagers.TryGetValue(characterType, out var specificManager))
            {
                return specificManager.GetCharacterInstance(spawnNodeIndex);
            }
            else
            {
                Debug.LogError($"No manager registered for CharacterType: {characterType}. Cannot get character.");
                return null;
            }
        }

        // Return character (animated/normal return)
        public void ReturnCharacter(Character character)
        {
            if (character == null) return;

            if (typeManagers.TryGetValue(character.CharacterType, out var specificManager))
            {
                specificManager.ReturnCharacterInstance(character);
            }
            else
            {
                Debug.LogError($"No manager registered for CharacterType: {character.CharacterType}. Cannot return character {character.name}.");
                // Fallback: Maybe just deactivate it?
                // character.gameObject.SetActive(false);
            }
        }

        // Instantly return character (no animation/movement)
        public void InstantlyReturnCharacter(Character character)
        {
            if (character == null) return;

            if (typeManagers.TryGetValue(character.CharacterType, out var specificManager))
            {
                specificManager.InstantlyReturnCharacterInstance(character);
            }
            else
            {
                Debug.LogError($"No manager registered for CharacterType: {character.CharacterType}. Cannot instantly return character {character.name}.");
                // Fallback: Maybe just deactivate it?
                // character.gameObject.SetActive(false);
            }
        }

        // --- Cleanup ---
        public virtual void Unsubscribe()
        {
            Debug.Log("Unsubscribing CharacterManager and specific managers...");
            if (manager != null) // Check if manager exists before unsubscribing
            {
                manager.OnGridComplete -= HandleGridComplete;
                if (manager.PathManager != null) // Check if PathManager exists
                {
                    manager.PathManager.OnPathCreationCompleted -= HandlePathCreationOrConnectionChange;
                    manager.PathManager.OnPathRemoved -= HandlePathRemoved;
                }
            }

            // Unsubscribe specific managers
            foreach (var specificManager in typeManagers.Values)
            {
                specificManager.Unsubscribe();
            }
            typeManagers.Clear(); // Clear the dictionary
        }
    }
}