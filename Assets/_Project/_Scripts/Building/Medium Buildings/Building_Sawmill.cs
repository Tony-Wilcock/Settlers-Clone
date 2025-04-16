namespace PunkyFruitBat
{
    public class Building_Sawmill : Building
    {
        protected override CharacterType RequiredWorkerType => CharacterType.Carpenter;

        protected override int WoodCost => 1;
        protected override int StoneCost => 0;

        private void Awake()
        {
            buildingType = BuildingType.Sawmill;
            buildingSize = BuildingSize.Medium;
            if (buildingGFXTransform == null) buildingGFXTransform = transform.GetChild(0);
        }

        public override void AssignBuildingCost()
        {
            SetBuildingCost(WoodCost, StoneCost);
        }

        //protected override void AssignWorkerBasedOnBuildingType()
        //{
        //    Carpenter carpenter = manager.CharacterManager.GetCharacter(CharacterType.Carpenter) as Carpenter;
        //    if (carpenter != null)
        //    {
        //        AssignedWorker = carpenter;
        //    }
        //    StartCoroutine(carpenter.MoveCharacter(CenterIndex));
        //}
    }
}
