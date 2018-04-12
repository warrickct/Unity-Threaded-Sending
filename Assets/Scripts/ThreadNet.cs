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

    //Debugging 
    bool connected = false;

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

        string message = "sending this message";
        byte[] messageBytes = Encoding.ASCII.GetBytes(message);
        client.Send(messageBytes);
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

            NetworkStream netStream = client.GetStream();

            int i;

            while ((i = netStream.Read(bytes, 0, bytes.Length)) != 0)
            {
                data = Encoding.ASCII.GetString(bytes, 0, i);
                data.ToUpper();
                debugData = "received " + data;
            }
        }
    }

    private void Update()
    {
        //Debugging 
        if (debugData != null)
        {
            Debug.Log(debugData);
        }

        //Debugging 
        Debug.Log(connected);
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
