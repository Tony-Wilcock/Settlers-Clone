using System.Collections.Generic;
using UnityEngine;

namespace PunkyFruitBat
{
    public class BuilderManager : BaseSpecificCharacterManager
    {
        public override CharacterType ManagedType => CharacterType.Builder;

        private Queue<Builder> builderPool = new();

        // Override Initialise to add specific setup if needed, like pool creation
        public override void Initialise(CharacterManager mainManager, HexGridManager gridManager, CharacterPrefabs_SO characterPrefabs, Transform parentTransform)
        {
            base.Initialise(mainManager, gridManager, characterPrefabs, parentTransform); // Call base initialisation
            // Note: Pool initialisation is now tied to HandleGridComplete
        }

        // --- Pooling Logic (Moved from CharacterManager) ---
        private void InitialiseBuilderPool()
        {
            Debug.Log("Initialising Builder Pool...");
            builderPool = new Queue<Builder>();
            IncreaseBuilderPool(5); // Or read from config
        }

        private void IncreaseBuilderPool(int amount)
        {
            GameObject prefab = characterPrefabs.characterPrefabs[(int)ManagedType];
            if (prefab == null)
            {
                Debug.LogError($"Prefab for {ManagedType} not found!");
                return;
            }

            int storehouseNode = gridManager.BuildingManager.GetStorehouseNode(); // Get once
            Vector3 initialPosition = gridManager.NodeManager.GetNodePosition(storehouseNode);

            for (int i = 0; i < amount; i++)
            {
                GameObject characterGO = GameObject.Instantiate(prefab);
                characterGO.transform.position = initialPosition;

                if (typeSpecificParentTransform != null) characterGO.transform.SetParent(typeSpecificParentTransform);
                else Debug.LogWarning($"Parent transform for {ManagedType} not set. Character '{characterGO.name}' will be at scene root.", characterGO);

                Builder builder = characterGO.GetComponent<Builder>();
                if (builder == null)
                {
                    Debug.LogError($"Prefab for {ManagedType} is missing Builder component!");
                    GameObject.Destroy(characterGO); // Clean up unusable instance
                    continue;
                }

                builder.InitialiseCharacter(ManagedType, storehouseNode);
                characterGO.SetActive(false);
                builderPool.Enqueue(builder);
            }
            Debug.Log($"Increased builder pool by {amount}. Total: {builderPool.Count}");
        }

        public override Character GetCharacterInstance(int spawnNodeIndex = -1)
        {
            throw new System.NotImplementedException();
        }

        public override void InstantlyReturnCharacterInstance(Character character)
        {
            throw new System.NotImplementedException();
        }

        public override void ReturnCharacterInstance(Character character)
        {
            throw new System.NotImplementedException();
        }

        public override void HandleGridComplete()
        {
            InitialiseBuilderPool();
        }
    }
}
