using System.Collections.Generic;
using UnityEngine;

namespace PunkyFruitBat
{
    public class WoodCutterManager : BaseSpecificCharacterManager<WoodCutter>
    {
        public override CharacterType ManagedType => CharacterType.WoodCutter;

        private Queue<WoodCutter> woodCutterPool = new();

        public override void HandleGridComplete()
        {
            InitialiseWoodCutterPool();
        }

        private void InitialiseWoodCutterPool()
        {
            woodCutterPool = new Queue<WoodCutter>();

            IncreaseWoodCutterPool(5); // Initial pool size
        }

        private void IncreaseWoodCutterPool(int amount)
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

                if (!characterGO.TryGetComponent<WoodCutter>(out WoodCutter woodCutter))
                {
                    Debug.LogError($"Prefab for {ManagedType} is missing WoodCutter component!");
                    GameObject.Destroy(characterGO); // Clean up unusable instance
                    continue;
                }

                woodCutter.InitialiseCharacter(ManagedType, storehouseNode);
                characterGO.SetActive(false);
                woodCutterPool.Enqueue(woodCutter);
            }
        }

        public override Character GetCharacterInstance(int spawnNodeIndex = -1)
        {
            if (woodCutterPool.Count == 0)
            {
                Debug.LogWarning("Builder pool empty, increasing size.");
                IncreaseWoodCutterPool(5); // Or read from config
            }

            if (woodCutterPool.Count == 0) // Check again after trying to increase
            {
                Debug.LogError("Failed to increase builder pool or pool still empty. Cannot get builder.");
                return null;
            }

            WoodCutter woodCutter = woodCutterPool.Dequeue();

            if (spawnNodeIndex != -1) woodCutter.transform.position = gridManager.NodeManager.GetNodePosition(spawnNodeIndex);

            woodCutter.gameObject.SetActive(true);
            return woodCutter;
        }

        public override void ReturnCharacterInstance(Character character)
        {
            if (character is not WoodCutter woodCutter)
            {
                Debug.LogError($"Tried to return non-WoodCutter character to WoodCutter pool: {character.name}");
                return;
            }

            if (woodCutter == null || !woodCutter.gameObject.activeInHierarchy) return; // Already returned

            woodCutter.StopAllCoroutines(); // Stop any running coroutines

            woodCutter.StartCoroutine(woodCutter.MoveCharacter(woodCutter.WorkNodeIndex, () =>
            {
                woodCutter.gameObject.SetActive(false);
                woodCutterPool.Enqueue(woodCutter);
            }));
        }

        public override void InstantlyReturnCharacterInstance(Character character)
        {
            if (character is not WoodCutter woodCutter)
            {
                Debug.LogError($"Tried to instantly return non-WoodCutter: {character?.name}");
                return;
            }
            if (woodCutter == null) return;

            Debug.Log($"Instantly returning woodCutter {woodCutter.GetInstanceID()} to pool");
            woodCutter.StopAllCoroutines();

            woodCutter.gameObject.SetActive(false);
            // Reset position?
            woodCutter.transform.position = gridManager.NodeManager.GetNodePosition(woodCutter.WorkNodeIndex); // Assuming WorkNodeIndex is storehouse

            // Avoid double-adding
            if (!woodCutterPool.Contains(woodCutter))
            {
                woodCutterPool.Enqueue(woodCutter);
            }
            else
            {
                Debug.LogWarning($"Builder {woodCutter.GetInstanceID()} already in pool during instant return?");
            }
        }
    }
}
