using System.Collections.Generic;
using UnityEngine;

namespace PunkyFruitBat
{
    public enum CharacterType // Ensure this matches the order in the inspector
    {
        Carrier,
        StorehousePorter,
        Builder,
        WoodCutter,
        Forester,
        Carpenter,
        GrainFarmer,
        PigFarmer,
        Miller,
        Baker,
        Butcher,
        Fisher,
        Hunter,
        Stonecutter,
        WellDigger,
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
            this.manager = manager;
            this.characterPrefabs = characterPrefabs;

            // Instantiate and initialise all known specific managers
            InitialiseSpecificManagers();

            // Subscribe to events - these handlers will now delegate
            manager.OnGridComplete += HandleGridComplete;
        }

        private void InitialiseSpecificManagers()
        {
            // Create instances of your specific managers

            // --- CarrierManager Setup ---
            CarrierManager carrierManager = new();
            Transform carrierParent = CreateOrFindParentTransform(CharacterType.Carrier.ToString());
            RegisterAndInitialiseManager(carrierManager, carrierParent);

            // --- StorehousePorterManager Setup ---
            StorehousePorterManager storehousePorterManager = new();
            Transform storehousePorterParent = CreateOrFindParentTransform(CharacterType.StorehousePorter.ToString());
            RegisterAndInitialiseManager(storehousePorterManager, storehousePorterParent);

            // --- BuilderManager Setup ---
            BuilderManager builderManager = new();
            Transform builderParent = CreateOrFindParentTransform(CharacterType.Builder.ToString());
            RegisterAndInitialiseManager(builderManager, builderParent);

            // --- WoodCutterManager Setup ---
            WoodCutterManager woodCutterManager = new();
            Transform woodCutterParent = CreateOrFindParentTransform(CharacterType.WoodCutter.ToString());
            RegisterAndInitialiseManager(woodCutterManager, woodCutterParent);

            // --- ForesterManager Setup ---
            ForesterManager foresterManager = new();
            Transform foresterParent = CreateOrFindParentTransform(CharacterType.Forester.ToString());
            RegisterAndInitialiseManager(foresterManager, foresterParent);

            // --- CarpenterManager Setup ---
            CarpenterManager carpenterManager = new();
            Transform carpenterParent = CreateOrFindParentTransform(CharacterType.Carpenter.ToString());
            RegisterAndInitialiseManager(carpenterManager, carpenterParent);

            // --- GrainFarmerManager Setup ---
            GrainFarmerManager farmerGrainManager = new();
            Transform farmerGrainParent = CreateOrFindParentTransform(CharacterType.GrainFarmer.ToString());
            RegisterAndInitialiseManager(farmerGrainManager, farmerGrainParent);

            // --- PigFarmerManager Setup ---
            PigFarmerManager farmerPigManager = new();
            Transform farmerPigParent = CreateOrFindParentTransform(CharacterType.PigFarmer.ToString());
            RegisterAndInitialiseManager(farmerPigManager, farmerPigParent);

            // --- MillerManager Setup ---
            MillerManager millerManager = new();
            Transform millerParent = CreateOrFindParentTransform(CharacterType.Miller.ToString());
            RegisterAndInitialiseManager(millerManager, millerParent);

            // --- BakerManager Setup ---
            BakerManager bakerManager = new();
            Transform bakerParent = CreateOrFindParentTransform(CharacterType.Baker.ToString());
            RegisterAndInitialiseManager(bakerManager, bakerParent);

            // --- ButcherManager Setup ---
            ButcherManager butcherManager = new();
            Transform butcherParent = CreateOrFindParentTransform(CharacterType.Butcher.ToString());
            RegisterAndInitialiseManager(butcherManager, butcherParent);

            // --- FisherManager Setup ---
            FisherManager fisherManager = new();
            Transform fisherParent = CreateOrFindParentTransform(CharacterType.Fisher.ToString());
            RegisterAndInitialiseManager(fisherManager, fisherParent);

            // --- HunterManager Setup ---
            HunterManager hunterManager = new();
            Transform hunterParent = CreateOrFindParentTransform(CharacterType.Hunter.ToString());
            RegisterAndInitialiseManager(hunterManager, hunterParent);

            // --- StonecutterManager Setup ---
            StonecutterManager stonecutterManager = new();
            Transform stonecutterParent = CreateOrFindParentTransform(CharacterType.Stonecutter.ToString());
            RegisterAndInitialiseManager(stonecutterManager, stonecutterParent);

            // --- WellDiggerManager Setup ---
            WellDiggerManager wellDiggerManager = new();
            Transform wellDiggerParent = CreateOrFindParentTransform(CharacterType.WellDigger.ToString());
            RegisterAndInitialiseManager(wellDiggerManager, wellDiggerParent);
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
            }
            else
            {
                Debug.LogWarning($"Attempted to register a manager for {type} but one already exists.");
            }
        }

        public ICharacterTypeManager GetSpecificManager(CharacterType type)
        {
            if (typeManagers.TryGetValue(type, out var specificManager))
            {
                return specificManager;
            }
            else
            {
                Debug.LogError($"No manager registered for CharacterType: {type}. Cannot get specific manager.");
                return null;
            }
        }

        // --- Event Handlers (Delegation) ---

        private void HandleGridComplete()
        {
            // Notify all managers that the grid is complete
            foreach (var specificManager in typeManagers.Values)
            {
                specificManager.HandleGridComplete();
            }
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
            if (manager != null) // Check if manager exists before unsubscribing
            {
                manager.OnGridComplete -= HandleGridComplete;
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