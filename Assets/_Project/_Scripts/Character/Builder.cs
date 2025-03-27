using UnityEngine;

namespace PunkyFruitBat
{
    public class Builder : Character
    {
        protected override void Awake()
        {
            base.Awake();

            characterType = CharacterType.Builder;
        }
    }
}
