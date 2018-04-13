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
    bool connected = false;

    WireData2 wd2 = null;

    bool isConstructing = false;
    bool meshArrayConstructionDone = false;

    Vector3[] receivedVerts;
    Vector2[] receivedUvs;
    int[] receivedTriangles;

    //Made to handle when to instantiate game obj (as it can only be done from update/start.
    bool hasMesh = false;
    MeshData meshData;

    //made to prevent the repeat calls to generate the model.
    bool isGenerated = false;

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
            uvFloats.Add(uv.x);
            uvFloats.Add(uv.y);
        }

        float[] vertFloatArray = vertFloats.ToArray();
        float[] uvFloatArray = uvFloats.ToArray();

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

        /**
         * This is the test string sent over network.
        client.SendFile("test.txt");
        Debug.Log("Sent");
        client.Shutdown(SocketShutdown.Send);
        
        string message = "sending this message";
        byte[] messageBytes = Encoding.ASCII.GetBytes(message);
        */

        int dataLength = data.Length;
        byte[] sizeData = BitConverter.GetBytes(dataLength);

        //client.Send(sizeData);
        client.Send(data);
        client.Close();
    }

    void Listener()
    {
        IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 12001);
        TcpListener tcpListener = new TcpListener(ipEndPoint);

        tcpListener.Start();

        byte[] bytes = new byte[1024];
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
                Debug.Log("receiving");
            }
            byte[] fullDataBytes = fullData.ToArray();
            BinaryFormatter bf = new BinaryFormatter();
            MemoryStream ms = new MemoryStream(fullDataBytes);
            wd2 = bf.Deserialize(ms) as WireData2;

            HandleWireData(wd2);
        }
    }

    /// <summary>
    /// Parent function for running wiredata handling and conversion into model.
    /// </summary>
    /// <param name="wd"></param>
    void HandleWireData(WireData2 wd)
    {
        meshData = ReconstructMeshArrays(wd);
    }

    MeshData ReconstructMeshArrays(WireData2 wd2)
    {
        isConstructing = true;

        float[] verts = wd2.verts;
        List<Vector3> vectorVertices = new List<Vector3>();
        for (int i=0; i < verts.Length; i+=3)
        {
            Vector3 vertex = new Vector3( verts[i], verts[i+1], verts[i+2] );
            vectorVertices.Add(vertex);
        }

        float[] uvs = wd2.uvs;
        List<Vector2> vectorUvs = new List<Vector2>();
        for (int i=0; i < uvs.Length; i+=2 )
        {
            Vector2 uv = new Vector2(uvs[i], uvs[i + 1]);
            vectorUvs.Add(uv);
        }

        //dont need to do anything for triangles.

        Vector3[] vecVertArray = vectorVertices.ToArray();
        Vector2[] vecUvsArray = vectorUvs.ToArray();
        int[] intTrianglesArray = wd2.triangles;

        //cast to meshdata object for easier return.
        MeshData meshData = new MeshData(intTrianglesArray, vecUvsArray, vecVertArray);
        return meshData;
    }

    void GenerateModel(MeshData meshData)
    {
        GameObject genModel = new GameObject
        {
            name = "GeneratedModel"
        };

        Mesh genMesh = new Mesh
        {
            vertices = meshData.vertices,
            uv = meshData.uv,
            triangles = meshData.triangles,
        };

        genMesh.RecalculateNormals();
        genMesh.RecalculateBounds();
        genMesh.RecalculateTangents();

        genModel.AddComponent<MeshFilter>();
        genModel.GetComponent<MeshFilter>().mesh = genMesh;

        MeshRenderer generatedRenderer = genModel.AddComponent<MeshRenderer>();

        Material genMaterial = generatedRenderer.material = new Material(Shader.Find("Standard"));
        genMaterial.name = "GeneratedMaterial";

        isGenerated = true;
    }

    private void Update()
    {
        if (meshData.triangles != null && isGenerated == false)
        {
            GenerateModel(meshData);
        }
    }

}

public struct MeshData
{
    public Vector3[] vertices;
    public Vector2[] uv;
    public int[] triangles;

    public MeshData(int[] triangles, Vector2[] uv, Vector3[] vertices)
    {
        this.triangles = triangles;
        this.uv = uv;
        this.vertices = vertices;
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
