using System.Collections.Generic;
using UnityEngine;

public enum StockResourceType // Define the types of resources that can be stored in the stockpile
{
    None,
    Wood,
    Stone,
    Iron,
    Crystal
}

public class ResourceManager : Singleton<ResourceManager>
{
    private Dictionary<StockResourceType, int> stockResources = new Dictionary<StockResourceType, int>(); // Store the amount of each resource

    public void Initialise()
    {
        stockResources.Clear(); // Reset in case of re-initialization
        foreach (StockResourceType resource in System.Enum.GetValues(typeof(StockResourceType)))
        {
            if (resource != StockResourceType.None) // Skip None
            {
                stockResources[resource] = 5; // Start with 50 of each
            }
        }
    }

    public static int GetStockResourceAmount(StockResourceType resourceType)
    {
        if (Instance == null)
        {
            Debug.LogError("ResourceManager instance not found!");
            return 0;
        }
        return Instance.stockResources.TryGetValue(resourceType, out int amount) ? amount : 0;
    }

    public static bool AddStockResource(StockResourceType resourceType, int amount)
    {
        if (Instance == null)
        {
            Debug.LogError("ResourceManager instance not found!");
            return false;
        }
        if (resourceType == StockResourceType.None || amount < 0)
        {
            Debug.LogWarning($"Invalid resource type {resourceType} or amount {amount}");
            return false;
        }
        if (Instance.stockResources.ContainsKey(resourceType))
        {
            Instance.stockResources[resourceType] += amount;
            Debug.Log($"Added {amount} {resourceType}. New total: {Instance.stockResources[resourceType]}");
            return true;
        }
        Debug.LogError($"Resource type {resourceType} not found!");
        return false;
    }

    public static bool RemoveStockResource(StockResourceType resourceType, int amount)
    {
        if (Instance == null)
        {
            Debug.LogError("ResourceManager instance not found!");
            return false;
        }
        if (resourceType == StockResourceType.None || amount < 0)
        {
            Debug.LogWarning($"Invalid resource type {resourceType} or amount {amount}");
            return false;
        }
        if (Instance.stockResources.ContainsKey(resourceType))
        {
            int currentAmount = Instance.stockResources[resourceType];
            if (currentAmount >= amount)
            {
                Instance.stockResources[resourceType] -= amount;
                Debug.Log($"Removed {amount} {resourceType}.Remaining: {Instance.stockResources[resourceType]}");
                return true;
            }
            Debug.LogWarning($"Not enough {resourceType} to remove {amount}. Current: {currentAmount}");
            return false;
        }
        Debug.LogError($"Resource type {resourceType} not found!");
        return false;
    }
}
