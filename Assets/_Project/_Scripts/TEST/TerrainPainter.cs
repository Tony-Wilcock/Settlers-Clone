using UnityEngine;
using System.Collections.Generic;

public class TerrainPainter : MonoBehaviour
{
    public int pathBrushSize = 5;
    public int removePathBrushSize = 6;
    public int flagBrushSize = 8;
    public int removeFlagBrushSize = 9;
    public float brushStrength = 1f;
    public int splatTextureIndex = 0;
    public AnimationCurve pathFalloffCurve = AnimationCurve.Linear(0, 1, 1, 0); // Add falloff curve
    public AnimationCurve flagFalloffCurve = AnimationCurve.Linear(0, 1, 1, 1); // Add falloff curve
    public List<Terrain> terrains = new List<Terrain>(3);
    public LayerMask terrainLayerMask;

    private Vector3? startPoint = null; // Use nullable Vector3 to store start point
    private int currentBrushSize;

    void Start()
    {
        terrains.Clear(); //Clear the Terrain List
        currentBrushSize = pathBrushSize;

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

        PaintPath(new Vector3(0, 0, 0), new Vector3(0, 0, 10));
        PaintSinglePoint(new Vector3(0, 0, 0));
        PaintPath(new Vector3(0, 0, 10), new Vector3(10, 0, 10));
        PaintPath(new Vector3(10, 0, 10), new Vector3(10, 0, 0));
        PaintPath(new Vector3(0, 0, 0), new Vector3(10, 0, 0));
        PaintSinglePoint(new Vector3(10, 0, 0));
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0)) // Left mouse button
        {
            HandleMouseClick();
        }

        if (Input.GetKeyDown(KeyCode.U)) // U key
        {
            currentBrushSize = removePathBrushSize;
            splatTextureIndex = (int)SplatTexture.Grass;
            pathFalloffCurve = AnimationCurve.Linear(0, 1, 1, 1);
            PaintPath(new Vector3(0, 0, 0), new Vector3(10, 0, 0));
            pathFalloffCurve = AnimationCurve.Linear(0, 1, 1, 0);
            currentBrushSize = pathBrushSize;
            splatTextureIndex = (int)SplatTexture.Path;
            PaintSinglePoint(new Vector3(0, 0, 0));
            PaintSinglePoint(new Vector3(10, 0, 0));
        }
    }

    void HandleMouseClick()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, terrainLayerMask))
        {
            Terrain hitTerrain = hit.collider.GetComponent<Terrain>();
            if (hitTerrain != null && terrains.Contains(hitTerrain))
            {
                if (startPoint == null)
                {
                    // First click: Store the start point
                    startPoint = hit.point;
                    Debug.Log("Start point: " + startPoint);
                }
                else
                {
                    // Second click: We have a start and end point. Paint!
                    Debug.Log("End point: " + hit.point);
                    PaintPath(startPoint.Value, hit.point); // Use .Value to get the Vector3 from the nullable
                    startPoint = null; // Reset for the next path
                }
            }
        }
    }

    void PaintSinglePoint(Vector3 point)
    {
        currentBrushSize = flagBrushSize;

        foreach (Terrain terrain in terrains)
        {
            TerrainData terrainData = terrain.terrainData;
            int heightmapWidth = terrainData.heightmapResolution;
            int heightmapHeight = terrainData.heightmapResolution;
            int alphamapWidth = terrainData.alphamapWidth;
            int alphamapHeight = terrainData.alphamapHeight;
            float[,,] splatmapData = terrainData.GetAlphamaps(0, 0, alphamapWidth, alphamapHeight);
            int numTextures = splatmapData.GetLength(2);

            Vector3 localPoint = point - terrain.transform.position;
            Vector2Int pointCoord = new Vector2Int(
                Mathf.RoundToInt(localPoint.x / terrainData.size.x * terrainData.heightmapResolution),
                Mathf.RoundToInt(localPoint.z / terrainData.size.z * terrainData.heightmapResolution)
            );

            PaintPathBetweenPoints(pointCoord.x, pointCoord.y, heightmapWidth, heightmapHeight, splatmapData, alphamapWidth, alphamapHeight, numTextures, splatTextureIndex, flagFalloffCurve, currentBrushSize);

            terrainData.SetAlphamaps(0, 0, splatmapData);
            terrain.Flush();
        }
    }

    void PaintPath(Vector3 start, Vector3 end)
    {
        currentBrushSize = pathBrushSize;

        foreach (Terrain terrain in terrains)
        {
            TerrainData terrainData = terrain.terrainData;
            int heightmapWidth = terrainData.heightmapResolution;
            int heightmapHeight = terrainData.heightmapResolution;
            int alphamapWidth = terrainData.alphamapWidth;
            int alphamapHeight = terrainData.alphamapHeight;
            float[,,] splatmapData = terrainData.GetAlphamaps(0, 0, alphamapWidth, alphamapHeight);
            int numTextures = splatmapData.GetLength(2);

            List<Vector2Int> linePoints = GetPointsOnLine(start, end, terrainData, terrain);
            foreach (Vector2Int point in linePoints)
            {
                PaintPathBetweenPoints(point.x, point.y, heightmapWidth, heightmapHeight, splatmapData, alphamapWidth, alphamapHeight, numTextures, splatTextureIndex, pathFalloffCurve, currentBrushSize);
            }

            terrainData.SetAlphamaps(0, 0, splatmapData);
            terrain.Flush();
        }
    }


    void PaintPathBetweenPoints(int x, int y, int heightmapWidth, int heightmapHeight, float[,,] splatmapData, int alphamapWidth, int alphamapHeight, int numTextures, int textureIndex, AnimationCurve falloffCurve, int brushSize)
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
                    float strength = falloffCurve.Evaluate(normalizedDistance) * brushSize;

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
    List<Vector2Int> GetPointsOnLine(Vector3 start, Vector3 end, TerrainData terrainData, Terrain terrain)
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
}