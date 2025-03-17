using UnityEngine;

namespace PunkyFruitBat
{
    [CreateAssetMenu(fileName = "BuildingPrefabs", menuName = "Scriptable Objects/BuildingPrefabs")]
    public class BuildingPrefabs_SO : ScriptableObject
    {
        [Tooltip("Prefabs for all buildings.")]
        public GameObject[] buildingPrefabs;
    }
}
