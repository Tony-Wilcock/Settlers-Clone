using System.Collections.Generic;
using UnityEngine;
using TGS;

public enum SplatTexture
{
    Grass = 0,
    Path = 1,
    Mountain = 2,
}

public class PathPainter : MonoBehaviour
{
    [SerializeField] private int pathBrushSize = 5;
    [SerializeField] private int removePathBrushSize = 6;
    [SerializeField] private int flagBrushSize = 8;
    [SerializeField] private int removeFlagBrushSize = 9;
    [SerializeField] private float brushStrength = 0.5f;
    [SerializeField] private int splatTextureIndex = 0;
    [SerializeField] private AnimationCurve pathFalloffCurve = AnimationCurve.Linear(0, 1, 1, 0); // Add falloff curve
    [SerializeField] private AnimationCurve flagFalloffCurve = AnimationCurve.Linear(0, 1, 1, 1); // Add falloff curve
    [SerializeField] private List<Terrain> terrains = new (3);
    [SerializeField] private LayerMask terrainLayerMask;
    [SerializeField] private int pathSegmentSamplePoints = 5;

    private int currentBrushSize;
    private List<Vector3> pathPoints = new (60); // List to store path points for multi-point path
    private TerrainGridSystem tgs;
    private Vector3? start = null;
    private Vector3? end = null;
    private bool isRemovingPath = false;

    private void Start()
    {
        terrains.Clear(); //Clear the Terrain List
        pathPoints.Clear(); // Clear the path points list
        currentBrushSize = pathBrushSize;
        splatTextureIndex = (int)SplatTexture.Path;
        tgs = TerrainGridSystem.instance;

        if (terrains.Count == 0)
        {
            Terrain[] activeTerrains = Terrain.activeTerrains;
            if (activeTerrains.Length == 0)
            {
                Debug.LogError("No Terrains found!");
                enabled = false;
                return;
            }
            terrains.AddRange(activeTerrains);
        }

        /////////////////////////// TESTING PaintPathList //////////////////////////////////////

        int startInt = 163; int endInt = 166;
        List<int> path = tgs.FindPath(startInt, endInt);
        pathPoints.Add(tgs.CellGetPosition(startInt));  // IMPORTANT: Need to add the first point to the list!!!
        for (int i = 0; i < path.Count; i++)
        {
            pathPoints.Add(tgs.CellGetPosition(path[i]));
        }
        start = tgs.CellGetPosition(startInt);
        end = tgs.CellGetPosition(endInt);
        PaintSinglePoint(start.Value);
        PaintSinglePoint(end.Value);
        PaintPathList(pathPoints);
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0)) // Left mouse button
        {
            HandleMouseClick();
        }
        if (Input.GetMouseButtonDown(1)) // Right mouse button
        {
            HandleMultiPointMouseClick();
        }
        if (Input.GetKeyDown(KeyCode.Space)) // Space key
        {
            FinalizeMultiPointPath();
        }


        if (Input.GetKeyDown(KeyCode.U)) // U key
        {
            isRemovingPath = true;
            currentBrushSize = removePathBrushSize;
            brushStrength = 1f;
            splatTextureIndex = (int)SplatTexture.Grass;
            pathFalloffCurve = AnimationCurve.Linear(0, 1, 1, 1);
            PaintPathList(pathPoints);
            currentBrushSize = pathBrushSize;
            brushStrength = 0.5f;
            splatTextureIndex = (int)SplatTexture.Path;
            pathFalloffCurve = AnimationCurve.Linear(0, 1, 1, 0);
            PaintSinglePoint((Vector3)start);
            PaintSinglePoint((Vector3)end);
            start = null;
            end = null;
            isRemovingPath = false;
        }
    }

    private void HandleMouseClick()
    {
        // TESTING GetTextureWeightsAtPoint
        if (terrains.Count > 0)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, Mathf.Infinity, terrainLayerMask))
            {
                Terrain terrain = hit.collider.GetComponent<Terrain>();
                if (terrain != null)
                {
                    Debug.Log(GetGroundTextureIndex(terrain, hit.point));
                }
                else
                {
                    Debug.Log("Clicked on something, but it's not a terrain.");
                }
            }
            else
            {
                Debug.Log("Raycast did not hit anything on the terrain layer.");
            }
        }
    }

    private void HandleMultiPointMouseClick()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, terrainLayerMask))
        {
            Terrain hitTerrain = hit.collider.GetComponent<Terrain>();
            if (hitTerrain != null && terrains.Contains(hitTerrain))
            {
                pathPoints.Add(hit.point);
                Debug.Log("Added point to path: " + hit.point);
            }
        }
    }

    private void FinalizeMultiPointPath()
    {
        if (pathPoints.Count >= 2)
        {
            PaintPathList(pathPoints);
            pathPoints.Clear(); // Clear points after painting
            Debug.Log("Painting multi-point path and clearing points.");
        }
        else
        {
            Debug.LogWarning("Not enough points to paint multi-point path. Need at least 2 points.");
            pathPoints.Clear(); // Clear points if not enough
        }
    }

    private void PaintPathList(List<Vector3> points)
    {
        if (points == null || points.Count < 2)
        {
            Debug.LogError("PaintPathList requires a list of at least two points.");
            return;
        }

        for (int i = 0; i < points.Count - 1; i++)
        {
            PaintPath(points[i], points[i + 1]);
        }
    }

    private void PaintSinglePoint(Vector3 point)
    {
        currentBrushSize = flagBrushSize;

        Terrain targetTerrain = GetTerrainAtPosition(point); // Get the terrain at the point
        if (targetTerrain != null) // Only paint if we hit a terrain
        {
            TerrainData terrainData = targetTerrain.terrainData;
            int heightmapWidth = terrainData.heightmapResolution;
            int heightmapHeight = terrainData.heightmapResolution;
            int alphamapWidth = terrainData.alphamapWidth;
            int alphamapHeight = terrainData.alphamapHeight;
            float[,,] splatmapData = terrainData.GetAlphamaps(0, 0, alphamapWidth, alphamapHeight);
            int numTextures = splatmapData.GetLength(2);

            Vector3 localPoint = point - targetTerrain.transform.position;
            Vector2Int pointCoord = new Vector2Int(
                Mathf.RoundToInt(localPoint.x / terrainData.size.x * terrainData.heightmapResolution),
                Mathf.RoundToInt(localPoint.z / terrainData.size.z * terrainData.heightmapResolution)
            );

            if (isRemovingPath)
            {
                PaintPathBetweenPoints(pointCoord.x, pointCoord.y, heightmapWidth, heightmapHeight, splatmapData, alphamapWidth, alphamapHeight, numTextures, splatTextureIndex, flagFalloffCurve, currentBrushSize);
            }
            else
            {
                PaintPathBetweenPoints(pointCoord.x, pointCoord.y, heightmapWidth, heightmapHeight, splatmapData, alphamapWidth, alphamapHeight, numTextures, splatTextureIndex, flagFalloffCurve, currentBrushSize);
            }

            //PaintPathBetweenPoints(pointCoord.x, pointCoord.y, heightmapWidth, heightmapHeight, splatmapData, alphamapWidth, alphamapHeight, numTextures, splatTextureIndex, flagFalloffCurve, currentBrushSize);

            terrainData.SetAlphamaps(0, 0, splatmapData);
            targetTerrain.Flush();
        }
    }

    private void PaintPath(Vector3 startPoint, Vector3 endPoint)
    {
        currentBrushSize = pathBrushSize;

        HashSet<Terrain> terrainsToPaint = new HashSet<Terrain>(); // Use HashSet to avoid painting same terrain multiple times

        // Check which terrains the path segment intersects with
        foreach (Terrain terrain in terrains)
        {
            if (IsPathOverTerrain(startPoint, endPoint, terrain))
            {
                terrainsToPaint.Add(terrain);
            }
        }

        if (terrainsToPaint.Count > 0)
        {
            foreach (Terrain terrainToPaint in terrainsToPaint)
            {
                TerrainData terrainData = terrainToPaint.terrainData;
                int heightmapWidth = terrainData.heightmapResolution;
                int heightmapHeight = terrainData.heightmapResolution;
                int alphamapWidth = terrainData.alphamapWidth;
                int alphamapHeight = terrainData.alphamapHeight;
                float[,,] splatmapData = terrainData.GetAlphamaps(0, 0, alphamapWidth, alphamapHeight);
                int numTextures = splatmapData.GetLength(2);

                List<Vector2Int> linePoints = GetPointsOnLine(startPoint, endPoint, terrainData, terrainToPaint);
                Debug.Log($"Line points on terrain {terrainToPaint.name}: {linePoints.Count}"); // Debug log per terrain
                foreach (Vector2Int point in linePoints)
                {
                    if (isRemovingPath)
                    {
                        PaintPathBetweenPoints(point.x, point.y, heightmapWidth, heightmapHeight, splatmapData, alphamapWidth, alphamapHeight, numTextures, GetGroundTextureIndex(terrainToPaint, new Vector3(point.x, 0, point.y)), pathFalloffCurve, currentBrushSize);
                    }
                    else
                    {
                        PaintPathBetweenPoints(point.x, point.y, heightmapWidth, heightmapHeight, splatmapData, alphamapWidth, alphamapHeight, numTextures, splatTextureIndex, pathFalloffCurve, currentBrushSize);
                    }
                }

                terrainData.SetAlphamaps(0, 0, splatmapData);
                terrainToPaint.Flush();
            }
        }
        else
        {
            Debug.LogWarning("Path does not intersect any known terrains. Path not painted.");
        }
    }

    private bool IsPathOverTerrain(Vector3 start, Vector3 end, Terrain terrain)
    {
        // Enhanced check: Sample points along the path segment
        for (int i = 0; i <= pathSegmentSamplePoints; i++)
        {
            float t = (float)i / pathSegmentSamplePoints;
            Vector3 samplePoint = Vector3.Lerp(start, end, t);
            if (GetTerrainAtPosition(samplePoint) == terrain)
            {
                return true; // Path segment is over this terrain
            }
        }
        return false; // Path segment is not over this terrain
    }


    private void PaintPathBetweenPoints(int x, int y, int heightmapWidth, int heightmapHeight, float[,,] splatmapData, int alphamapWidth, int alphamapHeight, int numTextures, int textureIndex, AnimationCurve falloffCurve, int brushSize)
    {
        // Calculate brush range and clamp to alphamap bounds.
        float alphaMapWidthRatio = (float)alphamapWidth / heightmapWidth;
        float alphaMapHeightRatio = (float)alphamapHeight / heightmapHeight;
        int alphaMapX = (int)(x * alphaMapWidthRatio);
        int alphaMapY = (int)(y * alphaMapHeightRatio);
        int alphaMapBrushStartX = Mathf.Max(0, alphaMapX - brushSize);
        int alphaMapBrushEndX = Mathf.Min(alphamapWidth - 1, alphaMapX + brushSize);
        int alphaMapBrushStartY = Mathf.Max(0, alphaMapY - brushSize);
        int alphaMapBrushEndY = Mathf.Min(alphamapHeight - 1, alphaMapY + brushSize);

        // Modify Splatmap ONLY.
        for (int ax = alphaMapBrushStartX; ax <= alphaMapBrushEndX; ax++)
        {
            for (int ay = alphaMapBrushStartY; ay <= alphaMapBrushEndY; ay++)
            {
                // Distance check for circular brush
                float dist = Vector2.Distance(new Vector2(alphaMapX, alphaMapY), new Vector2(ax, ay));
                if (dist <= brushSize)
                {
                    // Use the falloff curve
                    float normalizedDistance = dist / brushSize;
                    float strength = falloffCurve.Evaluate(normalizedDistance) * brushStrength;

                    float existingValue = splatmapData[ay, ax, textureIndex];
                    float newTextureValue = Mathf.Lerp(existingValue, 1f, strength);
                    splatmapData[ay, ax, textureIndex] = newTextureValue;

                    float sumOfOthers = 0f;
                    for (int i = 0; i < numTextures; i++)
                    {
                        if (i != textureIndex)
                        {
                            sumOfOthers += splatmapData[ay, ax, i];
                        }
                    }

                    if (sumOfOthers > 0)
                    {
                        float scaleFactor = (1f - newTextureValue) / sumOfOthers;
                        for (int i = 0; i < numTextures; i++)
                        {
                            if (i != textureIndex)
                            {
                                splatmapData[ay, ax, i] *= scaleFactor;
                            }
                        }
                    }
                    else
                    {
                        for (int i = 0; i < numTextures; i++)
                        {
                            if (i != textureIndex)
                            {
                                splatmapData[ay, ax, i] = 0f;
                            }
                        }
                        splatmapData[ay, ax, textureIndex] = 1f;
                    }
                }
            }
        }
    }

    // Bresenham's Line Algorithm
    private List<Vector2Int> GetPointsOnLine(Vector3 start, Vector3 end, TerrainData terrainData, Terrain terrain)
    {
        List<Vector2Int> points = new List<Vector2Int>();
        Vector3 localStart = start - terrain.transform.position;
        Vector3 localEnd = end - terrain.transform.position;
        Vector2Int startPoint = new Vector2Int(
            Mathf.RoundToInt(localStart.x / terrainData.size.x * terrainData.heightmapResolution),
            Mathf.RoundToInt(localStart.z / terrainData.size.z * terrainData.heightmapResolution)
        );
        Vector2Int endPoint = new Vector2Int(
            Mathf.RoundToInt(localEnd.x / terrainData.size.x * terrainData.heightmapResolution),
            Mathf.RoundToInt(localEnd.z / terrainData.size.z * terrainData.heightmapResolution)
        );
        int x0 = startPoint.x;
        int y0 = startPoint.y;
        int x1 = endPoint.x;
        int y1 = endPoint.y;
        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;
        while (true)
        {
            points.Add(new Vector2Int(x0, y0));
            if (x0 == x1 && y0 == y1) { break; }
            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x0 += sx; }
            if (e2 < dx) { err += dx; y0 += sy; }
        }
        return points;
    }

    private int GetGroundTextureIndex(Terrain terrain, Vector3 worldPosition)
    {
        TerrainData terrainData = terrain.terrainData;

        // Get the world position relative to the terrain and normalize to 0-1
        Vector3 terrainLocalPos = worldPosition - terrain.transform.position;
        float normalizedX = Mathf.Clamp01(terrainLocalPos.x / terrainData.size.x);
        float normalizedZ = Mathf.Clamp01(terrainLocalPos.z / terrainData.size.z);

        // Get the corresponding point in the alpha map texture
        int mapX = (int)(normalizedX * terrainData.alphamapWidth);
        int mapZ = (int)(normalizedZ * terrainData.alphamapHeight);

        // Get the splatmap (alphamap) data at this point.
        // GetAlphamaps returns a float[,,] array.
        float[,,] rawAlphamaps = terrainData.GetAlphamaps(mapX, mapZ, 1, 1);
        float[] splatmapData = new float[terrainData.alphamapLayers]; // Create a float array to hold the weights

        // Extract the weights for each texture layer from the 3D array
        for (int i = 0; i < terrainData.alphamapLayers; i++)
        {
            splatmapData[i] = rawAlphamaps[0, 0, i];
        }

        return GetTerrainTextureWithLowestWeight(splatmapData);
        //return GetTerrainTextureWithHighestWeight(splatmapData);
    }

    private int GetTerrainTextureWithHighestWeight(float[] splatmapData)
    {
        if (splatmapData != null && splatmapData.Length > 1)
        {
            int maxIndex = 0;
            float maxWeight = splatmapData[0];
            for (int i = 1; i < splatmapData.Length; i++)
            {
                if (splatmapData[i] > maxWeight)
                {
                    maxWeight = splatmapData[i];
                    maxIndex = i;
                }
            }
            return maxIndex;
        }
        else
        {
            Debug.LogWarning("Only 1 texture found. Must be the ground texture.");
            return -1;
        }
    }

    private int GetTerrainTextureWithLowestWeight(float[] splatmapData)
    {
        if (splatmapData != null && splatmapData.Length > 1)
        {
            int minIndex = 0;
            float minWeight = splatmapData[0];

            for (int i = 1; i < splatmapData.Length; i++)
            {
                if (splatmapData[i] < minWeight)
                {
                    minWeight = splatmapData[i];
                    minIndex = i;
                }
                Debug.Log($"Weight: {splatmapData[i]:F3}");
            }


            return minIndex;
        }
        else
        {
            Debug.LogWarning("Only 1 texture found. Must be the ground texture.");
            return -1;
        }
    }

    private Terrain GetTerrainAtPosition(Vector3 worldPosition)
    {
        Ray ray = new Ray(worldPosition + Vector3.up * 1000f, Vector3.down); // Raycast downwards from high above
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, terrainLayerMask))
        {
            return hit.collider.GetComponent<Terrain>();
        }
        return null;
    }

    private int GetTerrainTextureAtPosition(Vector3 worldPosition)
    {
        Ray ray = new Ray(worldPosition + Vector3.up * 1000f, Vector3.down); // Raycast downwards from high above
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, terrainLayerMask))
        {
            Terrain terrain = hit.collider.GetComponent<Terrain>();
            if (terrain != null)
            {
                return GetGroundTextureIndex(terrain, worldPosition);
            }
        }
        return -1;
    }
}