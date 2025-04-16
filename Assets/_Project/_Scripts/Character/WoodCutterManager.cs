using System.Collections.Generic;
using UnityEngine;

namespace PunkyFruitBat
{
    public class WoodCutterManager : BaseSpecificCharacterManager<WoodCutter>
    {
        public override CharacterType ManagedType => CharacterType.WoodCutter;

        public override void HandleGridComplete()
        {
            base.InitialisePool(5);
        }
    }
}
