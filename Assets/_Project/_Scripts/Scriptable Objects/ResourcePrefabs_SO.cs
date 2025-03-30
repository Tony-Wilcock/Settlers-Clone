using UnityEngine;

namespace PunkyFruitBat
{
    [CreateAssetMenu(fileName = "Resource Prefabs", menuName = "Scriptable Objects/Resource Prefabs")]
    public class ResourcePrefabs_SO : ScriptableObject
    {
        [Tooltip("Prefabs for all the resources.")]
        public GameObject[] ResourcePrefabs;
    }
}
