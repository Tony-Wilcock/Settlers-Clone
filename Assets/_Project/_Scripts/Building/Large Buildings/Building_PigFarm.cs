namespace PunkyFruitBat
{
    public class Building_PigFarm : Building
    {
        protected override CharacterType RequiredWorkerType => CharacterType.PigFarmer;

        protected override int WoodCost => 1;
        protected override int StoneCost => 0;

        private void Awake()
        {
            buildingType = BuildingType.PigFarm;
            buildingSize = BuildingSize.Large;
            if (buildingGFXTransform == null) buildingGFXTransform = transform.GetChild(0);
        }

        public override void AssignBuildingCost()
        {
            SetBuildingCost(WoodCost, StoneCost);
        }
    }
}
