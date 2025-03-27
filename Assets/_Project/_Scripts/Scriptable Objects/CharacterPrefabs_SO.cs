using UnityEngine;

namespace PunkyFruitBat
{
    [CreateAssetMenu(fileName = "CharacterPrefabs", menuName = "Scriptable Objects/CharacterPrefabs")]
    public class CharacterPrefabs_SO : ScriptableObject
    {
        [Tooltip("Prefabs for all characters.")]
        public GameObject[] characterPrefabs;
    }
}
