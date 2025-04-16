namespace PunkyFruitBat
{
    public class Building_Quarry : Building
    {
        protected override CharacterType RequiredWorkerType => CharacterType.Stonecutter;

        protected override int WoodCost => 1;
        protected override int StoneCost => 0;

        private void Awake()
        {
            buildingType = BuildingType.Quarry;
            buildingSize = BuildingSize.Small;
            if (buildingGFXTransform == null) buildingGFXTransform = transform.GetChild(0);
        }

        public override void AssignBuildingCost()
        {
            SetBuildingCost(WoodCost, StoneCost);
        }
    }
}
