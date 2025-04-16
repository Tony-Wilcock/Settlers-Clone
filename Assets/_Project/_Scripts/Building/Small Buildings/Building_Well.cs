namespace PunkyFruitBat
{
    public class Building_Well : Building
    {
        protected override CharacterType RequiredWorkerType => CharacterType.WellDigger;

        protected override int WoodCost => 1;
        protected override int StoneCost => 0;

        private void Awake()
        {
            buildingType = BuildingType.Well;
            buildingSize = BuildingSize.Small;
            if (buildingGFXTransform == null) buildingGFXTransform = transform.GetChild(0);
        }

        public override void AssignBuildingCost()
        {
            SetBuildingCost(WoodCost, StoneCost);
        }
    }
}
