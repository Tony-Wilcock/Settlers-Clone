namespace PunkyFruitBat
{
    public class Building_Bakery : Building
    {
        protected override CharacterType RequiredWorkerType => CharacterType.Baker;

        protected override int WoodCost => 1;
        protected override int StoneCost => 0;

        private void Awake()
        {
            buildingType = BuildingType.Bakery;
            buildingSize = BuildingSize.Medium;
            if (buildingGFXTransform == null) buildingGFXTransform = transform.GetChild(0);
        }

        public override void AssignBuildingCost()
        {
            SetBuildingCost(WoodCost, StoneCost);
        }
    }
}
