using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

public class ThreadNet : MonoBehaviour {

    public GameObject model;

	// Use this for initialization
	void Start () {

        Mesh mesh = model.GetComponent<MeshFilter>().mesh;
        Vector3[] vertices = mesh.vertices;
        Vector2[] uv = mesh.uv;
        int[] triangles = mesh.triangles;

        byte[] data;
        Thread newThread = new Thread(() => SerializeModel(vertices, uv, triangles));
        newThread.IsBackground = true;
        newThread.Start();
    }

    void SerializeModel(Vector3[] verts, Vector2[] uvs, int[] triangles)
    {
        List<float> vertFloats = new List<float>();
        foreach( Vector3 vert in verts)
        {
            vertFloats.Add(vert.x);
            vertFloats.Add(vert.y);
            vertFloats.Add(vert.z);
        }

        List<float> uvFloats = new List<float>();
        foreach (Vector2 uv in uvs)
        {
            vertFloats.Add(uv.x);
            vertFloats.Add(uv.y);
        }

        float[] vertFloatArray = vertFloats.ToArray();
        float[] uvFloatArray = vertFloats.ToArray();

        Debug.Log("Completed array conversion: " + vertFloatArray.Length + " " + uvFloatArray.Length);

        WireData2 wd2 = new WireData2(vertFloatArray, uvFloatArray, triangles);

        MemoryStream ms = new MemoryStream();
        BinaryFormatter bf = new BinaryFormatter();
        bf.Serialize(ms, wd2);
    }
}

[Serializable]
public class WireData2
{
    [SerializeField]
    float[] verts, uvs;

    [SerializeField]
    int[] triangles;

    public WireData2(float[] verts, float[] uvs, int[] triangles)
    {
        this.verts = verts;
        this.uvs = uvs;
        this.triangles = triangles;
    }
}
