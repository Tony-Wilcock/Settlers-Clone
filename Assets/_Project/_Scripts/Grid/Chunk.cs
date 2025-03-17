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
            localToGlobalVertexMap = new Dictionary<int, int>();
            globalToLocalVertexMap = new Dictionary<int, int>();
            chunkObject.GetComponent<MeshFilter>().mesh = mesh;
            chunkObject.GetComponent<MeshCollider>().sharedMesh = mesh;
        }

        public void UpdateMesh()
        {
            if (mesh != null)
            {
                mesh.vertices = vertices;
                mesh.triangles = triangles;
                mesh.RecalculateNormals();
                if (chunkObject != null)
                {
                    MeshCollider collider = chunkObject.GetComponent<MeshCollider>();
                    if (collider != null)
                    {
                        collider.sharedMesh = mesh;
                    }
                }
            }
        }
    }
}