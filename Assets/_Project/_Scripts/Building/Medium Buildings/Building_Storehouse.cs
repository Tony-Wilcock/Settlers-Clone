namespace PunkyFruitBat
{
    public class Building_Storehouse : Building
    {
        protected override CharacterType RequiredWorkerType => CharacterType.StorehousePorter;

        protected override int WoodCost => 1;
        protected override int StoneCost => 0;

        private void Awake()
        {
            buildingType = BuildingType.Storehouse;
            buildingSize = BuildingSize.Medium;
            if (buildingGFXTransform == null) buildingGFXTransform = transform.GetChild(0);
        }

        public override void AssignBuildingCost()
        {
            SetBuildingCost(WoodCost, StoneCost);
        }
    }
}
