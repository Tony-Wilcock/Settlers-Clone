using UnityEngine;

namespace PunkyFruitBat
{
    // Base class providing common fields and potentially methods for specific managers
    public abstract class BaseSpecificCharacterManager : ICharacterTypeManager
    {
        public abstract CharacterType ManagedType { get; }

        protected CharacterManager mainManager;
        protected HexGridManager gridManager;
        protected CharacterPrefabs_SO characterPrefabs;

        protected Transform typeSpecificParentTransform;

        public virtual void Initialise(CharacterManager mainManager, HexGridManager gridManager, CharacterPrefabs_SO characterPrefabs, Transform parentTransform)
        {
            this.mainManager = mainManager;
            this.gridManager = gridManager;
            this.characterPrefabs = characterPrefabs;
            this.typeSpecificParentTransform = parentTransform;
        }

        // Abstract methods forcing implementation in derived classes
        public abstract Character GetCharacterInstance(int spawnNodeIndex = -1);
        public abstract void ReturnCharacterInstance(Character character);
        public abstract void InstantlyReturnCharacterInstance(Character character);

        // Virtual methods for event handling - derived classes can override if they care
        public virtual void HandlePathCreationOrConnectionChange(Path path) { } // Default: Do nothing
        public virtual void HandlePathRemoval(Path path) { } // Default: Do nothing
        public virtual void HandleGridComplete() { } // Default: Do nothing
        public virtual void Unsubscribe() { } // Default: Do nothing, but derived might need it
    }
}