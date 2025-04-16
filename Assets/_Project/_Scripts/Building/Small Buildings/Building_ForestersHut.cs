namespace PunkyFruitBat
{
    public class Building_ForestersHut : Building
    {
        protected override CharacterType RequiredWorkerType => CharacterType.Forester;

        protected override int WoodCost => 1;
        protected override int StoneCost => 0;

        private void Awake()
        {
            buildingType = BuildingType.ForestersHut;
            buildingSize = BuildingSize.Small;
            if (buildingGFXTransform == null) buildingGFXTransform = transform.GetChild(0);
        }

        public override void AssignBuildingCost()
        {
            SetBuildingCost(WoodCost, StoneCost);
        }
    }
}
