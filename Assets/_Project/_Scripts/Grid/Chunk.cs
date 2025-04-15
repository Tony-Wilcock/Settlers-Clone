using UnityEngine;
using System.Collections.Generic;

namespace PunkyFruitBat
{
    [System.Serializable]
    public class Chunk
    {
        public GameObject chunkObject;
        public Mesh mesh;
        public Vector3[] vertices;
        public int[] triangles;
        public Color[] colors;
        public Dictionary<int, int> localToGlobalVertexMap;
        public Dictionary<int, int> globalToLocalVertexMap;

        // Add these properties
        public MeshFilter meshFilter;
        public MeshRenderer meshRenderer;
        public MeshCollider meshCollider;

        public Chunk(GameObject chunkObject)
        {
            this.chunkObject = chunkObject;
            mesh = new Mesh();
            mesh.MarkDynamic();
            localToGlobalVertexMap = new Dictionary<int, int>();
            globalToLocalVertexMap = new Dictionary<int, int>();

            // Cache components for efficiency
            meshFilter = chunkObject.GetComponent<MeshFilter>();
            meshRenderer = chunkObject.GetComponent<Renderer>() as MeshRenderer; // Assuming MeshRenderer
            meshCollider = chunkObject.GetComponent<MeshCollider>();

            if (meshFilter != null) meshFilter.mesh = mesh; // Assign the new mesh instance
        }

        /// <summary>
        /// Updates the mesh with current vertex positions, triangles, and colors.
        /// Also recalculates normals/bounds and updates the collider.
        /// </summary>
        public void UpdateMesh()
        {
            string chunkName = chunkObject?.name ?? "Unnamed Chunk"; // Get name safely

            if (mesh == null)
            {
                Debug.LogError($"[Chunk.UpdateMesh] Mesh is null for chunk: {chunkName}");
                return;
            }
            if (vertices == null || vertices.Length == 0)
            {
                Debug.LogWarning($"[Chunk.UpdateMesh] Vertices array is null or empty for chunk: {chunkName}. Cannot update mesh.");
                return;
            }
            if (triangles == null) // Check if triangles array itself is null
            {
                Debug.LogWarning($"[Chunk.UpdateMesh] Triangles array is null for chunk: {chunkName}. Cannot update mesh.");
                return;
            }
            if (colors == null || colors.Length != vertices.Length) // <<<--- ADD THIS CHECK: Ensure colors array is valid
            {
                Debug.LogWarning($"[Chunk.UpdateMesh] Colors array is null or length mismatch ({colors?.Length ?? -1} vs vertices {vertices.Length}) for chunk: {chunkName}. Cannot update colors.");
                // Optionally create a default white array here if needed:
                colors = new Color[vertices.Length];
                for (int i = 0; i < colors.Length; i++) { colors[i] = Color.white; }
                // Or just skip assigning colors to the mesh below
            }


            mesh.vertices = vertices; // Apply the updated local vertices
            mesh.triangles = triangles; // Re-apply triangles (in case they were somehow cleared)
            if (colors != null && colors.Length == vertices.Length) // <<<--- ADD THIS CHECK before assigning
            {
                mesh.colors = colors; // <<<--- ASSIGN COLORS TO MESH
            }
            mesh.RecalculateNormals(); // Important for lighting
            mesh.RecalculateBounds(); // Good practice after modifying vertices

            // --- Validity Check before Collider Assignment ---
            if (mesh.triangles.Length == 0 || mesh.triangles.Length % 3 != 0)
            {
                Debug.LogWarning($"[Chunk.UpdateMesh] Chunk {chunkName} has ZERO or invalid triangles ({mesh.triangles.Length}). Skipping collider update.");
                if (meshCollider != null)
                {
                    // Optionally disable the collider if the mesh becomes invalid
                    // meshCollider.enabled = false;
                }
            }
            else
            {
                // Only update collider if the mesh is valid
                if (meshCollider != null)
                {
                    // meshCollider.enabled = true; // Re-enable if previously disabled
                    meshCollider.sharedMesh = null; // Required before assigning new mesh to collider
                    meshCollider.sharedMesh = mesh; // This line throws the error if mesh is invalid
                }
                else
                {
                    Debug.LogWarning($"[Chunk.UpdateMesh] MeshCollider component is missing on chunk: {chunkName}");
                }
            }
            // --- End Validity Check ---
        }

        /// <summary>
        /// Call this from HexGridBuilder AFTER setting vertices, triangles, and colors arrays.
        /// Assigns data to the mesh and performs initial calculations/collider assignment.
        /// </summary>
        public void FinaliseInitialMesh()
        {
            if (mesh == null) mesh = new Mesh(); // Ensure mesh exists
            if (vertices == null) { Debug.LogError($"FinaliseInitialMesh: vertices array is null for {chunkObject?.name}"); return; }
            if (triangles == null) { Debug.LogError($"FinaliseInitialMesh: triangles array is null for {chunkObject?.name}"); return; }
            if (colors == null || colors.Length != vertices.Length) { Debug.LogError($"FinaliseInitialMesh: colors array is null or length mismatch for {chunkObject?.name}"); return; }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.colors = colors;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            if (meshFilter != null) meshFilter.mesh = mesh;

            // Initial collider assignment with check
            if (meshCollider != null)
            {
                if (mesh.triangles.Length > 0 && mesh.triangles.Length % 3 == 0)
                {
                    meshCollider.sharedMesh = mesh;
                }
                else
                {
                    Debug.LogWarning($"[Chunk.FinaliseInitialMesh] Initial mesh for {chunkObject?.name} has invalid triangles ({mesh.triangles.Length}). Collider not assigned.");
                    // meshCollider.enabled = false;
                }
            }
        }
    }
}