using System.Collections;

namespace PunkyFruitBat
{
    public class Building_HQ : Building
    {
        protected override CharacterType RequiredWorkerType => CharacterType.StorehousePorter;

        protected override int WoodCost => 0;
        protected override int StoneCost => 0;

        private void Awake()
        {
            buildingType = BuildingType.HQ;
            buildingSize = BuildingSize.Large;
            if (buildingGFXTransform == null) buildingGFXTransform = transform.GetChild(0);
        }

        public override void AssignBuildingCost()
        {
            SetBuildingCost(WoodCost, StoneCost);
        }

        protected override void AssignWorkerBasedOnBuildingType()
        {
            StartCoroutine(AssignCarrierWorker());
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

        private void AssignBuildingFlag()
        {
            entranceFlag = manager.FlagManager.TryGetFlag(EntranceIndex);
        }
    }
}
