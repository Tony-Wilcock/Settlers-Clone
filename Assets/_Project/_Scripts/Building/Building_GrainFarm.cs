using System.Collections.Generic;
using UnityEngine;

namespace PunkyFruitBat
{
    public class Building_GrainFarm : Building
    {
        public override void SetBuildingCost()
        {
            buildingCost = new Dictionary<ResourceType, int>
            {
                {ResourceType.Wood, 2},
                {ResourceType.Stone, 2}
            };
        }

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
        
        }

        // Update is called once per frame
        void Update()
        {
        
        }
    }
}
