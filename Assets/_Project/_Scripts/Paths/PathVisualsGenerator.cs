using DecalSplines;
using System;
using System.Collections.Generic;
using UnityEngine;

public class PathVisualsGenerator : MonoBehaviour
{
    [SerializeField] private DecalSplineStyle defaultPathStyle; // Default style if no TerrainType style is found
    [SerializeField] private DecalSpline decalSplinePrefab; // Use prefab for pooling

    [SerializeField] private int poolSize = 100; // Initial pool size (adjust as needed)
    private Queue<DecalSpline> decalSplinePool; // Object pool for DecalSplines

    private HexGridManager manager;
    private Dictionary<NodeTypes.TerrainType, DecalSplineStyle> terrainPathStyles = new Dictionary<NodeTypes.TerrainType, DecalSplineStyle>(); // For TerrainType styles (explained later)
    [SerializeField] private List<TerrainStylePair> terrainStylePairs = new List<TerrainStylePair>(); // For Inspector overrides

    [Serializable]
    private class TerrainStylePair
    {
        public NodeTypes.TerrainType terrainType;
        public DecalSplineStyle style;
    }

    public void Initialise(HexGridManager manager)
    {
        this.manager = manager;
        InitialisePool(); // Initialize the object pool at startup
        InitialiseTerrainStyles(); // Initialize TerrainType styles (explained later)
    }

    // **Object Pooling Methods:**

    private void InitialisePool()
    {
        decalSplinePool = new Queue<DecalSpline>();
        for (int i = 0; i < poolSize; i++)
        {
            DecalSpline splineInstance = Instantiate(decalSplinePrefab);
            splineInstance.transform.SetParent(manager.decalSplineTransform); // Parent it
            splineInstance.gameObject.SetActive(false); // Deactivate initially
            decalSplinePool.Enqueue(splineInstance);     // Add to the pool
        }
    }

    private void InitialiseTerrainStyles()
    {
        // Populate with defaults for all TerrainType values
        foreach (NodeTypes.TerrainType type in System.Enum.GetValues(typeof(NodeTypes.TerrainType)))
        {
            terrainPathStyles[type] = defaultPathStyle; // Default fallback
        }

        // Apply overrides from Inspector
        foreach (var pair in terrainStylePairs)
        {
            if (pair.style != null)
            {
                terrainPathStyles[pair.terrainType] = pair.style;
            }
            else
            {
                Debug.LogWarning($"Null style assigned for TerrainType.{pair.terrainType} in terrainStylePairs. Using defaultPathStyle.");
            }
        }

        // Validate defaultPathStyle
        if (defaultPathStyle == null)
        {
            Debug.LogError("PathVisualsGenerator: defaultPathStyle is null! Assign a valid style in the Inspector.");
        }
    }

    private DecalSplineStyle GetStyleForTerrain(NodeTypes.TerrainType terrainType)
    {
        return terrainPathStyles.TryGetValue(terrainType, out DecalSplineStyle style) ? style : defaultPathStyle;
    }

    private DecalSpline GetSplineFromPool()
    {
        if (decalSplinePool.Count > 0)
        {
            DecalSpline pooledSpline = decalSplinePool.Dequeue();
            pooledSpline.gameObject.SetActive(true); // Activate when taken from pool
            return pooledSpline;
        }
        else
        {
            // If pool is empty, instantiate a new one (optional: you could resize pool instead)
            Debug.LogWarning("PathVisualsGenerator: Pool is empty, instantiating new DecalSpline (consider increasing pool size).");
            DecalSpline newSpline = Instantiate(decalSplinePrefab);
            newSpline.transform.SetParent(manager.decalSplineTransform);
            return newSpline;
        }
    }

    private void ReturnSplineToPool(DecalSpline spline)
    {
        if (spline == null || !spline.gameObject.activeSelf) return; // Skip if already inactive

        spline.ClearDecalSpline();
        spline.gameObject.SetActive(false);
        decalSplinePool.Enqueue(spline);
    }


    public void DrawPath(List<int> pathToDraw)
    {
        if (pathToDraw == null) return; // Return if no path found

        if (!CanCreatePath(pathToDraw.ToArray()))
        {
            return;
        }

        //ClearPathVisuals(); // Clear existing path visuals (now using pooling)

        DecalSpline newSpline = GetSplineFromPool(); // **Get from pool instead of Instantiate**
        AddSegmentsToSpline(newSpline, pathToDraw.ToArray());
        newSpline.UpdateDecalSpline();
    }


    private bool CanCreatePath(int[] points)
    {
        if (decalSplinePrefab == null) // Changed to decalSplinePrefab for pooling
        {
            Debug.LogError("PathGenerator: decalSplinePrefab is not assigned in the Inspector!");
            return false;
        }
        if (defaultPathStyle == null) // Changed to defaultPathStyle
        {
            Debug.LogError("PathGenerator: defaultPathStyle is not assigned in the Inspector!");
            return false;
        }
        if (points == null || points.Length < 2)
        {
            Debug.LogError("PathGenerator: points array is null or has fewer than 2 points!");
            return false;
        }
        return true;
    }

    private void AddSegmentsToSpline(DecalSpline spline, int[] points)
    {
        foreach (int point in points)
        {
            // **Get style based on TerrainType from dictionary (explained later)**
            NodeTypes.TerrainType terrainType = manager.NodeManager.nodeDataDictionary[point].TerrainType;
            DecalSplineStyle currentStyle = GetStyleForTerrain(terrainType);
            spline.AddSegment(manager.globalVertices[point], currentStyle);
        }
    }

    public void ClearPathVisuals()
    {
        if (manager.decalSplineTransform == null)
        {
            Debug.LogWarning("ClearPathVisuals: manager.decalSplineTransform is null! Make sure it's assigned in HexGridManager.");
            return;
        }

        // Iterate through all children to ensure complete cleanup
        int childCount = manager.decalSplineTransform.childCount;
        for (int i = childCount - 1; i >= 0; i--)
        {
            Transform child = manager.decalSplineTransform.GetChild(i);
            DecalSpline spline = child.GetComponent<DecalSpline>();
            if (spline != null)
            {
                ReturnSplineToPool(spline);
            }
            else
            {
                Debug.LogWarning($"Child {child.name} under decalSplineTransform lacks DecalSpline component.");
            }
        }

        // Verify pool state (optional debugging)
        if (decalSplinePool.Count > poolSize)
        {
            Debug.LogWarning($"DecalSpline pool has {decalSplinePool.Count} items, exceeding initial size {poolSize}.");
        }
    }
}