using UnityEngine;

namespace PunkyFruitBat
{
    public class StorehousePorterManager : BaseSpecificCharacterManager
    {
        public override CharacterType ManagedType => CharacterType.StorehousePorter;

        public override void Initialise(CharacterManager mainManager, HexGridManager gridManager, CharacterPrefabs_SO characterPrefabs, Transform parentTransform)
        {
            base.Initialise(mainManager, gridManager, characterPrefabs, parentTransform);
        }

        public override Character GetCharacterInstance(int spawnNodeIndex = -1)
        {
            GameObject prefab = characterPrefabs.characterPrefabs[(int)ManagedType];
            if (prefab == null)
            {
                Debug.LogError($"Prefab for {ManagedType} not found!");
                return null;
            }

            int storehouseNode = gridManager.BuildingManager.GetStorehouseNode(); // Get once
            Vector3 initialPosition = gridManager.NodeManager.GetNodePosition(storehouseNode);
            
            GameObject characterGO = GameObject.Instantiate(prefab);
            characterGO.transform.position = initialPosition;

            if (typeSpecificParentTransform != null) characterGO.transform.SetParent(typeSpecificParentTransform);
            else Debug.LogWarning($"Parent transform for {ManagedType} not set. Character '{characterGO.name}' will be at scene root.", characterGO);
            if (!characterGO.TryGetComponent<StorehousePorter>(out StorehousePorter porter))
            {
                Debug.LogError($"Prefab for {ManagedType} is missing StorehousePorter component!");
                GameObject.Destroy(characterGO); // Clean up unusable instance
                return null;
            }

            porter.InitialiseCharacter(ManagedType, storehouseNode);
            porter.SetHomeNodeIndex(storehouseNode);

            return porter;
        }

        public override void ReturnCharacterInstance(Character character)
        {
            throw new System.NotImplementedException();
        }

        public override void InstantlyReturnCharacterInstance(Character character)
        {
            throw new System.NotImplementedException();
        }
    }
}
