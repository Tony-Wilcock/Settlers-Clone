using UnityEngine;

public abstract class Resource : MonoBehaviour
{
    [SerializeField] private StockResourceType resourceType;
    [SerializeField] private int sourceBuildingID;
    [SerializeField] private int destinationBuildingID;
    [SerializeField] private int currentFlagID;
    [SerializeField] private bool isInTransit;
    [SerializeField] private bool isInStorage;
    [SerializeField] private bool isCosumed;
}
