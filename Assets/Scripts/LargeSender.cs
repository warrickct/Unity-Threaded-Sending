using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using System.Threading;

public class LargeSender : MonoBehaviour {

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
        //SendModel(model);

        Thread sendThread = new Thread(SendModel);
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
                //DecodeMessage(recBuffer, dataSize);
                DecodeModel(recBuffer, dataSize);
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

    List<byte> byteList = new List<byte>();
    void DecodeModel(byte[] recBuffer, int bufferSize)
    {
        byteList.AddRange(recBuffer);
        if (bufferSize < 1024)
        {
            foreach(byte b in byteList)
            {
                Debug.Log(b);
            }

            byte[] byteArray = byteList.ToArray();
            MemoryStream ms = new MemoryStream(byteArray);
            BinaryFormatter bf = new BinaryFormatter();
            WireData wd = bf.Deserialize(ms) as WireData;

            Debug.Log("created model");
        }
    }

    void SendModel()
    {
        MeshFilter mf = model.GetComponent<MeshFilter>();
        Mesh m = mf.mesh;

        float[] verts = new float[m.vertices.Length * 3];
        for (int i = 0; i < m.vertices.Length; i += 3)
        {
            Vector3 vert = m.vertices[i];
            float fx = vert.x;
            verts[i] = (fx);

            float fy= vert.y;
            verts[i + 1] = (fx);

            float fz = vert.z;
            verts[i + 2] = (fx);
        }

        float[] uvs = new float[m.uv.Length * 2];
        for (int i = 0; i < m.uv.Length; i += 2)
        {
            Vector2 uv = m.uv[i];
            float fx = uv.x;
            uvs[i] = (fx);

            float fy = uv.y;
            uvs[i + 1] = (fx);
        }

        int[] triangles = m.triangles;

        WireData wd = new WireData(verts, uvs, triangles);

        MemoryStream ms = new MemoryStream();
        BinaryFormatter bf = new BinaryFormatter();
        bf.Serialize(ms, wd);
        byte[] data = ms.ToArray();


        /* deserializing works here. Need to make sure the data sent it exact same as received.
        MemoryStream ms2 = new MemoryStream(data);
        WireData wd2 = bf.Deserialize(ms2) as WireData;
        */

        Debug.Log("Serialized model wiredata size: " + data.Length);
        StartCoroutine("SendLargeSocketMessage", data);
    }
}

[Serializable]
public class WireData
{
    [SerializeField]
    float[] verts;

    [SerializeField]
    float[] uvs;

    [SerializeField]
    int[] triangles;

    public WireData(float[] verts,float[] uvs, int[] triangles)
    {
        this.verts = verts;
        this.uvs = uvs;
        this.triangles = triangles;
    }
}
