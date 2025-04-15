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

        private void AssignBuildingFlag()
        {
            entranceFlag = manager.FlagManager.TryGetFlag(EntranceIndex);
        }

        private IEnumerator AssignCarrierWorker()
        {
            yield return WaitForSecondsFactory.WaitCoroutine(1f);

            AssignBuildingFlag();

            StorehousePorter porter = manager.CharacterManager.GetCharacter(CharacterType.StorehousePorter) as StorehousePorter;
            if (porter != null)
            {
                porter.SetWorkingLocation(CenterIndex, EntranceIndex, EntranceFlag);
                AssignedWorker = porter;
            }
        }
    }
}
