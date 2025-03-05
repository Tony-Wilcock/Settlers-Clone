using System.Collections.Generic;
using System;
using UnityEngine;

[Serializable]
public class Chunk
{
    public Mesh mesh;
    public Vector3[] vertices;
    public int[] triangles;
    public Dictionary<int, int> localToGlobalVertexMap;
    public Dictionary<int, int> globalToLocalVertexMap;
    public GameObject chunkObject;

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
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        chunkObject.GetComponent<MeshCollider>().sharedMesh = mesh;
    }
}