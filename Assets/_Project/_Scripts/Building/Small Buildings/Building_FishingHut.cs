namespace PunkyFruitBat
{
    public class Building_FishingHut : Building
    {
        protected override CharacterType RequiredWorkerType => CharacterType.Fisher;

        protected override int WoodCost => 1;
        protected override int StoneCost => 0;

        private void Awake()
        {
            buildingType = BuildingType.FishingHut;
            buildingSize = BuildingSize.Small;
            if (buildingGFXTransform == null) buildingGFXTransform = transform.GetChild(0);
        }

        public override void AssignBuildingCost()
        {
            SetBuildingCost(WoodCost, StoneCost);
        }
    }
}
