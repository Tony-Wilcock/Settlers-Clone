using System.Collections.Generic;

namespace PunkyFruitBat
{
    public class Building_ForestersHut : Building
    {
        public override void SetBuildingCost()
        {
            buildingCost = new Dictionary<ResourceType, int>
            {
                { ResourceType.Wood, 2 },
                { ResourceType.Stone, 0 }
            };
        }

        protected override void AssignWorkerBasedOnBuildingType()
        {
            Forester forester = manager.CharacterManager.GetCharacter(CharacterType.Forester) as Forester;
            if (forester != null)
            {
                AssignedWorker = forester;
            }
            StartCoroutine(forester.MoveCharacter(CenterIndex));
        }
    }
}
