using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Net.Sockets;
using System.Net;
using System.Text;

public class ThreadNet : MonoBehaviour {

    public GameObject model;

    //Debugging 
    string debugData = null;
    string debugData2 = null;

    //Debugging 
    bool connected = false;

    WireData2 wd2 = null;

    MeshData meshData = new MeshData();

    bool meshConstructionRunning = false;

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

        Thread listenerThread = new Thread(Listener);
        listenerThread.IsBackground = true;
        listenerThread.Start();
    }

    /// <summary>
    /// Extracts model vert, uv, triangle. Converts them into float arrays, constructs into serializable class, serializes into byte array.
    /// </summary>
    /// <param name="verts"></param>
    /// <param name="uvs"></param>
    /// <param name="triangles"></param>
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
        byte[] data = ms.ToArray();

        Debug.Log("hello");
        Thread senderThread = new Thread(() => SenderThreaded(data));
        senderThread.IsBackground = true;
        senderThread.Start();

        Debug.Log("sender thread started");
    }

    /// <summary>
    /// Sends byte array using sockets
    /// </summary>
    /// <param name="data"></param>
    void SenderThreaded(byte[] data)
    {
        IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 12001);

        Socket client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        Debug.Log("client set up");

        client.Connect(ipEndPoint);
        Debug.Log("connected");

        /*
        client.SendFile("test.txt");
        Debug.Log("Sent");
        client.Shutdown(SocketShutdown.Send);
        */

        //string message = "sending this message";
        //byte[] messageBytes = Encoding.ASCII.GetBytes(message);

        client.Send(data);
        client.Close();
    }

    void Listener()
    {
        IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 12001);
        TcpListener tcpListener = new TcpListener(ipEndPoint);

        tcpListener.Start();

        byte[] bytes = new byte[256];
        String data = null;

        while (true)
        {
            TcpClient client = tcpListener.AcceptTcpClient();

            //Debugging 
            connected = true;

            data = null;
            List<byte> fullData = new List<byte>();

            NetworkStream netStream = client.GetStream();

            int i;

            while ((i = netStream.Read(bytes, 0, bytes.Length)) != 0)
            {
                fullData.AddRange(bytes);
                data = Encoding.ASCII.GetString(bytes, 0, i);
                data.ToUpper();
                debugData = "received " + data;
                debugData2 = "fullData length " + fullData.Count;
            }
            byte[] fullDataBytes = fullData.ToArray();
            BinaryFormatter bf = new BinaryFormatter();
            MemoryStream ms = new MemoryStream(fullDataBytes);
            wd2 = bf.Deserialize(ms) as WireData2;
        }
    }

    private void ReconstructMeshArrays(WireData2 wd2)
    {
        meshConstructionRunning = true;
        for (int i=0; i < wd2.verts.Length; i+=3)
        {
            float[] verts = wd2.verts;
            float x = verts[i];
            float y = verts[i+1];
            float z = verts[i+2];

            meshData.AddVert(x, y, z);
        }

        for (int i = 0; i < wd2.uvs.Length; i += 2)
        {
            float[] uvs = wd2.verts;
            float x = uvs[i];
            float y = uvs[i + 1];

            meshData.AddUv(x, y);
        }

        meshData.SetTriangles(wd2.triangles);

        //stops it from playing again after running once as well as during running.
        wd2 = null;
        meshConstructionRunning = false;
    }

    void GenerateModel()
    {
        GameObject genModel = new GameObject();
        Mesh genMesh = new Mesh
        {
            vertices = meshData.vertices.ToArray(),
            uv = meshData.uv.ToArray(),
            triangles = meshData.triangles
        };

        genMesh.RecalculateNormals();
        genMesh.RecalculateBounds();
        genMesh.RecalculateTangents();

        genModel.GetComponent<MeshFilter>().sharedMesh = genMesh;
    }

    private void Update()
    {
        //Debugging 
        if (debugData != null)
        {
            Debug.Log(debugData);
        }

        if (debugData2 != null)
        {
            Debug.Log(debugData2);
        }

        if (wd2 != null)
        {
            Debug.Log("wd2 not null");
            int lastIndex = wd2.verts.Length -1;
            Debug.Log("wd2 first vert: " + wd2.verts[0] + " " + wd2.verts[1] + " " + wd2.verts[2]);
            Debug.Log("wd2 last vert: " + wd2.verts[lastIndex-2] + " " + wd2.verts[lastIndex-1] + " " + wd2.verts[lastIndex]);

            Vector3 modelFirstVert = model.GetComponent<MeshFilter>().mesh.vertices[0];
            int modelLastIndex = model.GetComponent<MeshFilter>().mesh.vertices.Length - 1;
            Vector3 modelLastVert = model.GetComponent<MeshFilter>().mesh.vertices[modelLastIndex];

            Debug.Log("model first and last verts " + modelFirstVert.ToString() + " " + modelLastVert.ToString());
        }
        else
        {
            Debug.Log("wd2 null");
        }

        //Debugging 
        Debug.Log(connected);
    }
}

public struct MeshData
{
    public List<Vector3> vertices;
    public List<Vector2> uv;
    public int[] triangles;

    public void AddVert(float x, float y, float z)
    {
        Vector3 vert = new Vector3(x, y, z);
        vertices.Add(vert);
    }

    public void AddUv(float x, float y)
    {
        Vector2 uv = new Vector2(x, y);
    }

    public void SetTriangles(int[] triangles)
    {
        this.triangles = triangles;
    }
}

[Serializable]
public class WireData2
{
    [SerializeField]
    public float[] verts, uvs;

    [SerializeField]
    public int[] triangles;

    public WireData2(float[] verts, float[] uvs, int[] triangles)
    {
        this.verts = verts;
        this.uvs = uvs;
        this.triangles = triangles;
    }
}
