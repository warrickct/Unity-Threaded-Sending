using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

public class LargeListener : MonoBehaviour {

    public GameObject model;

    int reliableSequencedId;
    int socketId;
    int socketPort = 12345;

    int connectionId;

    //init network transport
    private void Start()
    {
        NetworkTransport.Init();
        ConnectionConfig config = new ConnectionConfig();

        reliableSequencedId = config.AddChannel(QosType.ReliableSequenced);
        int maxConnections = 10;

        HostTopology topology = new HostTopology(config, maxConnections);

        socketId = NetworkTransport.AddHost(topology, socketPort);
        Debug.Log("Socket open. Socket id is: " + socketId);

        Connect();

        //StartCoroutine("MakeLargeMessage");
        SendModel(model);
    }

    //set up connection
    public void Connect()
    {
        byte error;
        connectionId = NetworkTransport.Connect(socketId, "127.0.0.1", socketPort, 0, out error);
        Debug.Log("Connected to server. Connection id: " + connectionId);
    }

    //init net receiver
    private void Update()
    {
        int recHostId;
        int recConnectionId;
        int recChannelId;
        byte[] recBuffer = new byte[1024];
        int bufferSize = 1024;
        int dataSize;
        byte error;
        NetworkEventType recNetworkEvent = NetworkTransport.Receive(out recHostId, out recConnectionId, out recChannelId, recBuffer, bufferSize, out dataSize, out error);
        switch (recNetworkEvent)
        {
            case NetworkEventType.Nothing:
                break;
            case NetworkEventType.ConnectEvent:
                Debug.Log("incoming connection event received");
                //SendSocketMessage("Hello server");
                break;
            case NetworkEventType.DataEvent:
                DecodeMessage(recBuffer, dataSize);
                //Stream stream = new MemoryStream(recBuffer);
                //BinaryFormatter formatter = new BinaryFormatter();
                //string message = formatter.Deserialize(stream) as string;
                //Debug.Log("incoming message event received: " + message);
                break;
            case NetworkEventType.DisconnectEvent:
                Debug.Log("remote client event disconnected");
                break;
        }
    }

    //chunk sender
    public void SendSocketMessage(string message)
    {
        byte error;
        byte[] buffer = new byte[1024];
        Stream stream = new MemoryStream(buffer);
        BinaryFormatter formatter = new BinaryFormatter();
        formatter.Serialize(stream, message);

        int bufferSize = 1024;

        NetworkTransport.Send(socketId, connectionId, reliableSequencedId, buffer, bufferSize, out error);
    }

    //coroutine sender
    IEnumerator SendLargeSocketMessage(byte[] data)
    {
        for (int j = 0; j < data.Length; j += 1024)
        {
            Byte[] chunk = data.Skip(j).Take(1024).ToArray();

            byte error;
            NetworkTransport.Send(socketId, connectionId, reliableSequencedId, chunk, chunk.Length, out error);
            Debug.Log("Sent chunk of size: " + chunk.Length);
            yield return null;
        }
    }

    //coroutine message maker.
    IEnumerator MakeLargeMessage()
    {
        string s = String.Empty;
        string add = "repeated message... ";

        for (int j= 0; j < 10; j++)
        {
            add += add;
        }

        for( int i=0; i < 1200; i++)
        {
            s += add;
            i++;
            Debug.Log(i);
            yield return null;
        }
        Debug.Log("string gen finished");

        MemoryStream ms = new MemoryStream();
        BinaryFormatter bf = new BinaryFormatter();
        bf.Serialize(ms, s);
        Byte[] data = ms.ToArray();
        Debug.Log(data.Length);

        StartCoroutine("SendLargeSocketMessage", data);
    }

    MemoryStream receiveStream = new MemoryStream();
    void DecodeMessage(byte[] recBuffer, int bufferSize)
    {
        Debug.Log("Received buffer of size "+ recBuffer.Length);
        Debug.Log("Buffer size" + bufferSize);
        receiveStream.Write(recBuffer, 0, recBuffer.Length);

        if (bufferSize < 1024)
        {
            byte[] fullData = receiveStream.ToArray();
            Debug.Log(fullData.Length);


            Stream stream = new MemoryStream(fullData);
            BinaryFormatter formatter = new BinaryFormatter();
            string message = formatter.Deserialize(stream) as string;
            Debug.Log(message);
            receiveStream.Position = 0;
            receiveStream.SetLength(0);
        }
    }

    void DecodeModel(byte[] recBuffer, int bufferSize)
    {
        if (bufferSize < 1024)
        {
            byte[] fullData = receiveStream.ToArray();

            BinaryFormatter bf = new BinaryFormatter();
            WireData wd = bf.Deserialize(receiveStream) as WireData;

            Debug.Log("created model");
        }
    }

    void SendModel(GameObject model)
    {
        MeshFilter mf = model.GetComponent<MeshFilter>();
        Mesh m = mf.mesh;

        List<float[]> verts = new List<float[]>();
        foreach(Vector3 vert in m.vertices)
        {
            float[] f = { vert.x, vert.y, vert.z };
            verts.Add(f);
        }

        List<float[]> uvs = new List<float[]>();
        foreach (Vector2 uv in m.uv)
        {
            float[] f = { uv.x, uv.y };
            uvs.Add(f);
        }

        int[] triangles = m.triangles;

        WireData wd = new WireData(verts, uvs, triangles);

        MemoryStream ms = new MemoryStream();
        BinaryFormatter bf = new BinaryFormatter();
        bf.Serialize(ms, wd);
        byte[] data = ms.ToArray();

        Debug.Log("Serialized model wiredata size: " + data.Length);
        StartCoroutine("SendLargeSocketMessage", data);
    }
}

[Serializable]
public class WireData
{
    [SerializeField]
    List<float[]> verts;

    [SerializeField]
    List<float[]> uvs;

    [SerializeField]
    int[] triangles;

    public WireData(List<float[]> verts, List<float[]> uvs, int[] triangles)
    {
        this.verts = verts;
        this.uvs = uvs;
        this.triangles = triangles;
    }
}
