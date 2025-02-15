using UnityEngine;

namespace TGS {
    public class UnityTerrainWrapper : ITerrainWrapper {

        Terrain terrain;

        public bool supportsMultipleObjects {
            get { return false; }
        }

        public bool supportsCustomHeightmap {
            get { return false; }
        }

        public bool supportsPivot {
            get { return false; }
        }

        public GameObject gameObject {
            get { return terrain.gameObject; }
        }

        public Bounds bounds {
            get {
                Bounds bounds = terrain.terrainData.bounds;
                bounds.center += terrain.GetPosition();
                return bounds;
            }
        }

        public void Dispose() {
        }

        public bool enabled {
            get { return terrain.drawHeightmap; }
            set {
                terrain.drawHeightmap = value;
                terrain.drawTreesAndFoliage = value;
            }
        }

        public void Refresh() {
        }

        public void SetupTriggers(TerrainGridSystem tgs) {
#if !ENABLE_INPUT_SYSTEM
            TerrainTrigger trigger = terrain.gameObject.GetComponent<TerrainTrigger>();
            if (trigger == null) {
                trigger = terrain.gameObject.AddComponent<TerrainTrigger>();
            }
            trigger.Init<TerrainCollider>(tgs);
#endif
        }



        public UnityTerrainWrapper(Terrain terrain) {
            this.terrain = terrain;
        }

        public bool includesGameObject(GameObject go) {
            return terrain.gameObject == go;
        }

        public TerrainData terrainData {
            get {
                return terrain.terrainData;
            }
        }

        public int heightmapMaximumLOD {
            get { return terrain.heightmapMaximumLOD; }
            set {
                terrain.heightmapMaximumLOD = value;
            }
        }

        public int heightmapWidth {
            get { return terrain.terrainData.heightmapResolution; }
        }

        public int heightmapHeight {
            get { return terrain.terrainData.heightmapResolution; }
        }

        public float[,] GetHeights(int xBase, int yBase, int width, int height) {
            return terrain.terrainData.GetHeights(xBase, yBase, width, height);
        }

        public void SetHeights(int xBase, int yBase, float[,] heights) {
            terrain.terrainData.SetHeights(xBase, yBase, heights);
        }

        public T GetComponent<T>() {
            return gameObject.GetComponent<T>();
        }


        public float GetInterpolatedHeight(Vector3 worldPosition) {
            return terrain.SampleHeight(worldPosition);
        }

        public Transform transform {
            get {
                return terrain.transform;
            }
        }

        public Vector3 GetInterpolatedNormal(float x, float y) {
            return terrain.terrainData.GetInterpolatedNormal(x, y);
        }

        public Vector3 size {
            get { if (terrain != null && terrain.terrainData != null) return terrain.terrainData.size; else return Misc.Vector3zero; }
        }

        public Vector3 localCenter {
            get {
                Vector3 size = terrain.terrainData.size;
                return new Vector3(size.x * 0.5f, 0f, size.z * 0.5f);
            }
        }

        public bool Contains(GameObject gameObject) {
            return terrain.gameObject == gameObject;
        }

        /// <summary>
        /// Returns the grid local position from a world space position
        /// </summary>
        public Vector3 GetLocalPoint(GameObject gameObject, Vector3 worldSpacePosition) {
            Vector3 localPoint = gameObject.transform.InverseTransformPoint(worldSpacePosition);
            localPoint.x = localPoint.x / terrain.terrainData.size.x - 0.5f;
            localPoint.y = localPoint.z / terrain.terrainData.size.z - 0.5f;
            return localPoint;
        }

        /// <summary>
        /// Get the index of the splat texture with higher opacity
        /// </summary>
        public int GetAlphamapIndex(Vector3 position, int maxTextureCount) {

            Vector2Int coords = WorldToAlphamapCoordinates(position);
            int maxAlphaIndex = 0;
            float maxAlpha = 0;
            for (int k = 0; k < maxTextureCount; k++) {
                float[,,] alphamap = GetAlphamap(coords);
                float alpha = alphamap[0, 0, k];
                if (alpha > maxAlpha) {
                    maxAlpha = alpha;
                    maxAlphaIndex = k;
                }
            }
            return maxAlphaIndex;
        }

        /// <summary>
        /// Returns the alphamap at a given world position
        /// </summary>
        public float[,,] GetAlphamap(Vector3 position) {
            Vector2Int coords = WorldToAlphamapCoordinates(position);
            return GetAlphamap(coords);
        }

        float[,,] GetAlphamap(Vector2Int coords) {
            return terrain.terrainData.GetAlphamaps(coords.x, coords.y, 1, 1);
        }

        /// <summary>
        /// Sets the opacity of a custom splat texture
        /// </summary>
        public void SetAlphamapOpacity(Vector3 position, int splatmapIndex, float opacity = 1f, bool hideOtherLayers = true) {
            Vector2Int coords = WorldToAlphamapCoordinates(position);
            SetAlphamapOpacity(coords, splatmapIndex, opacity, hideOtherLayers);
        }


        /// <summary>
        /// Sets the opacity of a custom splat texture
        /// </summary>
        public void SetAlphamapOpacity(Vector2Int coords, int splatmapIndex, float opacity = 1f, bool hideOtherLayers = true) {
            float[,,] alphamap = GetAlphamap(coords);
            if (hideOtherLayers) {
                int m = alphamap.GetUpperBound(2);
                for (int k = 0; k <= m; k++) {
                    alphamap[0, 0, k] = 0;
                }
            }
            alphamap[0, 0, splatmapIndex] = opacity;
            terrain.terrainData.SetAlphamaps(coords.x, coords.y, alphamap);
        }


        /// <summary>
        /// Converts world space coordinate to alphamap coordinates 
        /// </summary>
        public Vector2Int WorldToAlphamapCoordinates(Vector3 worldPosition) {
            TerrainData terrainData = terrain.terrainData;
            int alphamapWidth = terrainData.alphamapWidth;
            int alphamapHeight = terrainData.alphamapHeight;
            Vector3 anchor = terrain.GetPosition();
            int x = Mathf.Clamp((int)((worldPosition.x - anchor.x) / terrainData.size.x * alphamapWidth), 0, alphamapWidth - 1);
            int y = Mathf.Clamp((int)((worldPosition.z - anchor.z) / terrainData.size.z * alphamapHeight), 0, alphamapHeight - 1);
            return new Vector2Int(x, y);
        }

        /// <summary>
        /// Converts alphamap coords to world space
        /// </summary>
        public Vector3 AlphamapToWorldCoordinates(Vector2Int coords) {
            TerrainData terrainData = terrain.terrainData;
            int alphamapWidth = terrainData.alphamapWidth;
            int alphamapHeight = terrainData.alphamapHeight;
            Vector3 anchor = terrain.GetPosition();
            float x = (coords.x + 0.5f) / alphamapWidth * terrainData.size.x + anchor.x;
            float z = (coords.y + 0.5f) / alphamapHeight * terrainData.size.y + anchor.z;
            return new Vector3(x, 0, z);

        }



    }
}

