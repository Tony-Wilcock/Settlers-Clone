using UnityEngine;

namespace PunkyFruitBat
{
    [CreateAssetMenu(fileName = "IconPrefabs", menuName = "Scriptable Objects/IconPrefabs")]
    public class IconPrefabs_SO : ScriptableObject
    {
        [Tooltip("Prefabs for node icons, corresponding to NodeIconIndex enum.")]
        public GameObject[] iconPrefabs;
    }
}
