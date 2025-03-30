using System.Collections.Generic;

namespace PunkyFruitBat
{
    public class Building_WoodcuttersHut : Building
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
            WoodCutter woodCutter = manager.CharacterManager.GetCharacter(CharacterType.WoodCutter) as WoodCutter;
            if (woodCutter != null)
            {
                AssignedWorker = woodCutter;
            }

            StartCoroutine(woodCutter.MoveCharacter(CenterIndex));
        }
    }
}
