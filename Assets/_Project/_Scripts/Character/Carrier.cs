using UnityEngine;

namespace PunkyFruitBat
{
    public class Carrier : Character
    {
        protected override void Awake()
        {
            base.Awake();

            characterType = CharacterType.Carrier;
        }
    }
}
