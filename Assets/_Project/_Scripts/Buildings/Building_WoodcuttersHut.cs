using UnityEngine;
using static ResourceManager;

public class Building_WoodcuttersHut : Building
{
    private void Start()
    {
        Debug.Log("Wood left: " + GetStockResourceAmount(StockResourceType.Wood));
        foreach (var kvp in Cost)
        {
            StockResourceType resource = kvp.Key;
            int amount = kvp.Value;
            Debug.Log($"{BuildingType} needs {amount} of {resource}");
        }
    }

    public override void ProduceResources()
    {

    }
}
