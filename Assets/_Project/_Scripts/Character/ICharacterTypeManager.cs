using UnityEngine;
using System.Collections.Generic; // For List if needed in methods

namespace PunkyFruitBat
{
    // Interface for managers handling specific character types
    public interface ICharacterTypeManager
    {
        CharacterType ManagedType { get; } // Property to know which type it handles

        // Initialise this specific manager
        void Initialise(CharacterManager mainManager, HexGridManager gridManager, CharacterPrefabs_SO characterPrefabs, Transform parentTransform);

        // Get an instance of the character this manager handles
        Character GetCharacterInstance(int spawnNodeIndex = -1);

        // Return a character instance to this manager's pool/control
        void ReturnCharacterInstance(Character character);

        // Instantly return a character instance without animations/movement
        void InstantlyReturnCharacterInstance(Character character);

        // Optional: Methods to handle specific events if needed
        void HandlePathCreationOrConnectionChange(Path path);
        void HandlePathRemoval(Path path);
        void HandleGridComplete(); // If specific setup is needed after grid is done

        // Clean up resources or subscriptions
        void Unsubscribe();
    }
}