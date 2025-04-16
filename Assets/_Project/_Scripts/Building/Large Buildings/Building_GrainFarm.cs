namespace PunkyFruitBat
{
    public class Building_GrainFarm : Building
    {
        protected override CharacterType RequiredWorkerType => CharacterType.GrainFarmer;

        protected override int WoodCost => 6;
        protected override int StoneCost => 2;

        private void Awake()
        {
            buildingType = BuildingType.GrainFarm;
            buildingSize = BuildingSize.Large;
            if (buildingGFXTransform == null) buildingGFXTransform = transform.GetChild(0);
        }

        public override void AssignBuildingCost()
        {
            SetBuildingCost(WoodCost, StoneCost);
        }
    }
}
