using System.Linq;
using UnityEngine;

public class Building_Storehouse : Building
{
    private void Start()
    {
        string costDetails = string.Join(", ", Cost.Select(kv => $"{kv.Key}: {kv.Value}"));
        Debug.Log($"{BuildingType} needs {costDetails}");
    }

    public override void ProduceResources()
    {

    }
}
