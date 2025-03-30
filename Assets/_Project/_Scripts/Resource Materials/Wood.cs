using UnityEngine;

namespace PunkyFruitBat
{
    public class Wood : Resource
    {
        protected override void Awake()
        {
            base.Awake();
            ResourceType = ResourceType.Wood;
        }
    }
}
