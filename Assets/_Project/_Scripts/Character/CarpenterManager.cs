using UnityEngine;

namespace PunkyFruitBat
{
    public class CarpenterManager : BaseSpecificCharacterManager<Carpenter>
    {
        public override CharacterType ManagedType => CharacterType.Carpenter;

        public override void HandleGridComplete()
        {
            base.InitialisePool(5);
        }
    }
}
