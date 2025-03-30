using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PunkyFruitBat
{
    public class Building_HQ : Building
    {
        public override void SetBuildingCost()
        {
            buildingCost = new Dictionary<ResourceType, int>
            {
                { ResourceType.Wood, 0 },
                { ResourceType.Stone, 0 }
            };
        }

        protected override void AssignWorkerBasedOnBuildingType()
        {
            StartCoroutine(AssignCarrierWorker());
        }

        private IEnumerator AssignCarrierWorker()
        {
            yield return WaitForSecondsFactory.WaitCoroutine(1f);

            StorehousePorter porter = manager.CharacterManager.GetCharacter(CharacterType.StorehousePorter) as StorehousePorter;
            if (porter != null)
            {
                AssignedWorker = porter;
            }
        }
    }
}
