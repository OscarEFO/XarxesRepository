using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

public class ClientUDP : MonoBehaviour
{
    public static ClientUDP Instance { get; private set; }

    private Socket clientSocket;
    private EndPoint serverEP;
    private Thread receiveThread;
    private bool running = false;

    public string serverIP = "127.0.0.1";
    public int port = 9050;

    // State from server
    public volatile float server_x, server_y;
    public volatile float client_x, client_y;
    public volatile float server_rot, client_rot;
    public volatile bool hasUpdate = false;

    void Awake()
    {
        if (Instance != this && Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        StartClient();
    }

    void OnDisable()
    {
        OnApplicationQuit();
    }

    // ---------------------------------------------------------
    // INITIALIZATION
    // ---------------------------------------------------------
    public void StartClient()
    {
        try
        {
            clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            serverEP = new IPEndPoint(IPAddress.Parse(serverIP), port);

            running = true;
            receiveThread = new Thread(ReceiveLoop);
            receiveThread.IsBackground = true;
            receiveThread.Start();

            Debug.Log("[CLIENT] UDP client started (binary mode).");
        }
        catch (Exception e)
        {
            Debug.LogError("[CLIENT] Init error: " + e.Message);
        }
    }

    // ---------------------------------------------------------
    // SEND INPUT
    // ---------------------------------------------------------
    public void SendInput(Vector2 input, float rotationZ)
    {
        if (!running) return;

        byte[] data = new byte[1 + 4 * 3]; // packetType + 3 floats
        int offset = 0;

        data[offset++] = 0; // INPUT packet

        Buffer.BlockCopy(BitConverter.GetBytes(input.x), 0, data, offset, 4); offset += 4;
        Buffer.BlockCopy(BitConverter.GetBytes(input.y), 0, data, offset, 4); offset += 4;
        Buffer.BlockCopy(BitConverter.GetBytes(rotationZ), 0, data, offset, 4);

        clientSocket.SendTo(data, serverEP);
    }

    // ---------------------------------------------------------
    // SEND SHOOT REQUEST
    // ---------------------------------------------------------
    public void SendShootRequest(Vector2 pos, float rotationZ, Vector2 direction)
    {
        if (!running) return;

        byte[] data = new byte[1 + 4 * 5];
        int o = 0;

        data[o++] = 1; // SHOOT packet

        Buffer.BlockCopy(BitConverter.GetBytes(pos.x), 0, data, o, 4); o += 4;
        Buffer.BlockCopy(BitConverter.GetBytes(pos.y), 0, data, o, 4); o += 4;
        Buffer.BlockCopy(BitConverter.GetBytes(rotationZ), 0, data, o, 4); o += 4;
        Buffer.BlockCopy(BitConverter.GetBytes(direction.x), 0, data, o, 4); o += 4;
        Buffer.BlockCopy(BitConverter.GetBytes(direction.y), 0, data, o, 4);

        clientSocket.SendTo(data, serverEP);
    }

    // ---------------------------------------------------------
    // RECEIVE LOOP
    // ---------------------------------------------------------
    void ReceiveLoop()
    {
        byte[] buffer = new byte[1024];
        EndPoint remote = new IPEndPoint(IPAddress.Any, 0);

        while (running)
        {
            try
            {
                int length = clientSocket.ReceiveFrom(buffer, ref remote);
                if (length == 0) continue;

                byte packetType = buffer[0];

                switch (packetType)
                {
                    case 2: ParseStatePacket(buffer); break;
                    case 3: ParseProjectilePacket(buffer); break;
                }
            }
            catch { }
        }
    }

    // ---------------------------------------------------------
    // PARSE SERVER STATE PACKET
    // ---------------------------------------------------------
    void ParseStatePacket(byte[] buf)
    {
        try
        {
            int o = 1; // skip packet type

            server_x   = BitConverter.ToSingle(buf, o); o += 4;
            server_y   = BitConverter.ToSingle(buf, o); o += 4;
            client_x   = BitConverter.ToSingle(buf, o); o += 4;
            client_y   = BitConverter.ToSingle(buf, o); o += 4;
            server_rot = BitConverter.ToSingle(buf, o); o += 4;
            client_rot = BitConverter.ToSingle(buf, o); o += 4;

            hasUpdate = true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[CLIENT] State packet parse error: " + e.Message);
        }
    }

    // ---------------------------------------------------------
    // PARSE PROJECTILE SPAWN
    // ---------------------------------------------------------
    void ParseProjectilePacket(byte[] buf)
    {
        try
        {
            int o = 1;

            float x = BitConverter.ToSingle(buf, o); o += 4;
            float y = BitConverter.ToSingle(buf, o); o += 4;
            float rot = BitConverter.ToSingle(buf, o); o += 4;
            float dirX = BitConverter.ToSingle(buf, o); o += 4;
            float dirY = BitConverter.ToSingle(buf, o); o += 4;
            float speed = BitConverter.ToSingle(buf, o); o += 4;
            bool fromServer = BitConverter.ToSingle(buf, o) == 1f;

            NetworkShootingClient.SpawnInfo info = new NetworkShootingClient.SpawnInfo
            {
                x = x,
                y = y,
                rotationZ = rot,
                dirX = dirX,
                dirY = dirY,
                speed = speed,
                fromServer = fromServer
            };

            NetworkShootingClient.Instance?.EnqueueSpawn(info);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[CLIENT] Projectile packet error: " + e.Message);
        }
    }

    public void OnApplicationQuit()
    {
        running = false;
        try { receiveThread?.Abort(); } catch { }
        try { clientSocket?.Close(); } catch { }
    }
}
