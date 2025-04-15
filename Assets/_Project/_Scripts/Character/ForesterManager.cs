using System.Collections.Generic;
using UnityEngine;

namespace PunkyFruitBat
{
    public class ForesterManager : BaseSpecificCharacterManager<Forester>
    {
        public override CharacterType ManagedType => CharacterType.Forester;

        private Queue<Forester> foresterPool = new();

        public override void HandleGridComplete()
        {
            InitialiseForesterPool();
        }

        private void InitialiseForesterPool()
        {
            foresterPool = new Queue<Forester>();
            IncreaseForesterPool(5); // Initial pool size
        }

        private void IncreaseForesterPool(int amount)
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
                if (!characterGO.TryGetComponent<Forester>(out Forester forester))
                {
                    Debug.LogError($"Prefab for {ManagedType} is missing Forester component!");
                    GameObject.Destroy(characterGO); // Clean up unusable instance
                    continue;
                }

                forester.InitialiseCharacter(ManagedType, storehouseNode);
                characterGO.SetActive(false);
                foresterPool.Enqueue(forester);
            }
        }

        public override Character GetCharacterInstance(int spawnNodeIndex = -1)
        {
            if (foresterPool.Count == 0)
            {
                Debug.LogWarning("Forester pool empty, increasing size.");
                IncreaseForesterPool(5); // Increase pool if empty
            }

            if (foresterPool.Count == 0)
            {
                Debug.LogError("Forester pool still empty after increase! Cannot spawn character.");
                return null;
            }

            Forester forester = foresterPool.Dequeue();

            if (spawnNodeIndex != -1)
            {
                forester.transform.position = gridManager.NodeManager.GetNodePosition(spawnNodeIndex);
            }

            forester.gameObject.SetActive(true);
            return forester;
        }

        public override void ReturnCharacterInstance(Character character)
        {
            if (character is not Forester forester)
            {
                Debug.LogError($"Attempted to return a character of type {character.CharacterType}, but expected Forester.");
                return;
            }

            if (forester == null || !forester.gameObject.activeInHierarchy) return; // Already returned

            forester.StopAllCoroutines(); // Stop any running coroutines

            forester.StartCoroutine(forester.MoveCharacter(forester.WorkNodeIndex, () =>
            {
                forester.gameObject.SetActive(false); // Deactivate the character
                foresterPool.Enqueue(forester); // Return to pool
            }));
        }

        public override void InstantlyReturnCharacterInstance(Character character)
        {
            if (character is not Forester forester)
            {
                Debug.LogError($"Attempted to instantly return a character of type {character.CharacterType}, but expected Forester.");
                return;
            }
            if (forester == null || !forester.gameObject.activeInHierarchy) return; // Already returned
            Debug.Log($"Instantly returning {forester.name} to pool.");
            forester.StopAllCoroutines(); // Stop any running coroutines
            forester.gameObject.SetActive(false); // Deactivate the character
            foresterPool.Enqueue(forester); // Return to pool
        }
    }
}
