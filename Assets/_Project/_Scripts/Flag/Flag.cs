using UnityEngine;

namespace PunkyFruitBat
{
    public class Flag : MonoBehaviour
    {
        [field: SerializeField] public int Id { get; private set; }
        [field: SerializeField] public bool IsFlagAttachedToBuilding { get; private set; } = false;

        public void SetFlagId(int id)
        {
            // Set the id to the manager selected vertex
            Id = id;
        }

        public void SetFlagAttachedToBuilding(bool isAttached)
        {
            IsFlagAttachedToBuilding = isAttached;
        }
    }
}
