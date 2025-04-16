namespace PunkyFruitBat
{
    public class Building_Windmill : Building
    {
        protected override CharacterType RequiredWorkerType => CharacterType.Miller;

        protected override int WoodCost => 1;
        protected override int StoneCost => 0;

        private void Awake()
        {
            buildingType = BuildingType.Windmill;
            buildingSize = BuildingSize.Medium;
            if (buildingGFXTransform == null) buildingGFXTransform = transform.GetChild(0);
        }

        public override void AssignBuildingCost()
        {
            SetBuildingCost(WoodCost, StoneCost);
        }
    }
}
