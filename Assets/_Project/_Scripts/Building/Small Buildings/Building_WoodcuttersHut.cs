namespace PunkyFruitBat
{
    public class Building_WoodcuttersHut : Building
    {
        protected override CharacterType RequiredWorkerType => CharacterType.WoodCutter;

        protected override int WoodCost => 2;
        protected override int StoneCost => 0;

        private void Awake()
        {
            buildingType = BuildingType.WoodcuttersHut;
            buildingSize = BuildingSize.Small;
            if (buildingGFXTransform == null) buildingGFXTransform = transform.GetChild(0);
        }

        public override void AssignBuildingCost()
        {
            SetBuildingCost(WoodCost, StoneCost);
        }
    }
}
