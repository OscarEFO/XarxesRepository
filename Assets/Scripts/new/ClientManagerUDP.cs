using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

public class ClientManagerUDP : MonoBehaviour
{
    [Header("Connection")]
    public string userName = "";
    public string serverIP = "127.0.0.1";
    public int serverPort = 9050;

    [Header("Prefabs")]
    public GameObject playerPrefab;
    public GameObject player2Prefab;
    public GameObject bulletPrefab;
    public GameObject bulletPrefabGreen;
    public float bulletSpeed = 15f;

    private Socket socket;
    private IPEndPoint serverEndPoint;
    private Thread receiveThread;
    private volatile bool running = false;

    public Player localPlayer;
    private int localId;

    private readonly Dictionary<int, Player> players = new Dictionary<int, Player>();
    private readonly object playersLock = new object();

    private int connectedPlayerCount = 0;

    void Start()
    {
        if (ClientServerInfo.Instance != null)
        {
            if (!string.IsNullOrEmpty(ClientServerInfo.Instance.userName))
                userName = ClientServerInfo.Instance.userName;
        }

        serverEndPoint = new IPEndPoint(IPAddress.Parse(serverIP), serverPort);

        localId = UnityEngine.Random.Range(100000, 999999);

        SetupSocket();
        StartReceiveThread();

        SendCreate();
    }

    void OnApplicationQuit() => Shutdown();
    void OnDestroy() => Shutdown();

    private void Shutdown()
    {
        running = false;
        try { receiveThread?.Join(200); } catch { }
        try { socket?.Close(); } catch { }
        socket = null;
    }

    private void SetupSocket()
    {
        socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Blocking = false;
    }

    private void StartReceiveThread()
    {
        running = true;
        receiveThread = new Thread(ReceiveLoop);
        receiveThread.IsBackground = true;
        receiveThread.Start();
    }


    public void SendCreate()
    {
        using (var ms = new System.IO.MemoryStream())
        using (var w = new System.IO.BinaryWriter(ms))
        {
            w.Write((byte)0);
            w.Write(localId);
            WriteString(w, userName);
            w.Write(0f); w.Write(0f);
            w.Write(0f); w.Write(0f);
            w.Write(0f);

            socket.SendTo(ms.ToArray(), serverEndPoint);
        }
    }

    public void SendUpdate(int id, Vector2 pos, float rot, Vector2 vel)
    {
        using (var ms = new System.IO.MemoryStream())
        using (var w = new System.IO.BinaryWriter(ms))
        {
            w.Write((byte)1);
            w.Write(id);
            w.Write(pos.x);
            w.Write(pos.y);
            w.Write(vel.x);
            w.Write(vel.y);
            w.Write(rot);

            socket.SendTo(ms.ToArray(), serverEndPoint);
        }
    }

    public void SendShoot(int id, Vector2 origin, Vector2 direction)
    {
        using (var ms = new System.IO.MemoryStream())
        using (var w = new System.IO.BinaryWriter(ms))
        {
            w.Write((byte)2);
            w.Write(id);
            w.Write(origin.x);
            w.Write(origin.y);
            w.Write(direction.x);
            w.Write(direction.y);

            socket.SendTo(ms.ToArray(), serverEndPoint);
        }
    }


    private void ReceiveLoop()
    {
        byte[] buffer = new byte[4096];
        EndPoint remote = new IPEndPoint(IPAddress.Any, 0);

        while (running)
        {
            try
            {
                int recv = socket.ReceiveFrom(buffer, ref remote);
                if (recv <= 0) continue;

                byte[] data = new byte[recv];
                Array.Copy(buffer, data, recv);

                ParseIncomingPacket(data);
            }
            catch (SocketException)
            {
                Thread.Sleep(1);
            }
        }
    }


    private void ParseIncomingPacket(byte[] buf)
    {
        int o = 0;
        byte type = buf[o++];

        switch (type)
        {
            case 0: ParseCreate(buf, ref o); break;
            case 1: ParseUpdate(buf, ref o); break;
            case 2: ParseShoot(buf, ref o); break;
            case 3: ParseAsteroid(buf, ref o); break;
        }
    }


    private void ParseCreate(byte[] buf, ref int o)
    {
        int id = BitConverter.ToInt32(buf, o); o += 4;
        string name = ReadString(buf, ref o);

        float x = BitConverter.ToSingle(buf, o); o += 4;
        float y = BitConverter.ToSingle(buf, o); o += 4;
        float velx = BitConverter.ToSingle(buf, o); o += 4;
        float vely = BitConverter.ToSingle(buf, o); o += 4;
        float rot = BitConverter.ToSingle(buf, o); o += 4;

        MainThreadDispatcher.Enqueue(() =>
        {
            SpawnOrUpdatePlayer(id, name, new Vector2(x, y), rot, new Vector2(velx, vely));
        });
    }

    private void SpawnOrUpdatePlayer(int id, string name, Vector2 pos, float rot, Vector2 vel)
    {
        bool isLocal = id == localId;

        lock (playersLock)
        {
            if (!players.ContainsKey(id))
            {
                connectedPlayerCount++;

                GameObject prefabToUse = (connectedPlayerCount == 2 && player2Prefab != null)
                    ? player2Prefab
                    : playerPrefab;

                GameObject go = Instantiate(prefabToUse, pos, Quaternion.Euler(0, 0, rot));
                Player p = go.GetComponent<Player>();

                p.networkId = id;
                p.clientManager = this;
                p.isLocalPlayer = isLocal;

                p.bulletPrefab = (connectedPlayerCount == 2) ? bulletPrefabGreen : bulletPrefab;

                players[id] = p;

                if (isLocal)
                    localPlayer = p;
            }
            else if (!isLocal)
            {
                players[id].ApplyNetworkState(pos, rot, vel);
            }
        }
    }


    private void ParseUpdate(byte[] buf, ref int o)
    {
        int id = BitConverter.ToInt32(buf, o); o += 4;

        float x = BitConverter.ToSingle(buf, o); o += 4;
        float y = BitConverter.ToSingle(buf, o); o += 4;
        float velx = BitConverter.ToSingle(buf, o); o += 4;
        float vely = BitConverter.ToSingle(buf, o); o += 4;
        float rot = BitConverter.ToSingle(buf, o); o += 4;

        MainThreadDispatcher.Enqueue(() =>
        {
            if (players.ContainsKey(id) && !players[id].isLocalPlayer)
                players[id].ApplyNetworkState(new Vector2(x, y), rot, new Vector2(velx, vely));
        });
    }

    private void ParseShoot(byte[] buf, ref int o)
    {
        int id = BitConverter.ToInt32(buf, o); o += 4;

        float ox = BitConverter.ToSingle(buf, o); o += 4;
        float oy = BitConverter.ToSingle(buf, o); o += 4;
        float dx = BitConverter.ToSingle(buf, o); o += 4;
        float dy = BitConverter.ToSingle(buf, o); o += 4;

        MainThreadDispatcher.Enqueue(() =>
        {
            HandleIncomingShoot(id, new Vector2(ox, oy), new Vector2(dx, dy));
        });
    }

    private void HandleIncomingShoot(int shooterId, Vector2 origin, Vector2 direction)
    {
        if (!players.TryGetValue(shooterId, out var shooter))
            return;

        GameObject prefab = shooter.bulletPrefab;
        if (prefab == null) return;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        GameObject b = Instantiate(prefab, origin, Quaternion.Euler(0, 0, angle));

        if (b.TryGetComponent<Rigidbody2D>(out var rb))
            rb.linearVelocity = direction.normalized * bulletSpeed;
    }


    private void ParseAsteroid(byte[] buf, ref int o)
    {
        float x = BitConverter.ToSingle(buf, o); o += 4;
        float y = BitConverter.ToSingle(buf, o); o += 4;
        float dirX = BitConverter.ToSingle(buf, o); o += 4;
        float dirY = BitConverter.ToSingle(buf, o); o += 4;
        float speed = BitConverter.ToSingle(buf, o); o += 4;

        MainThreadDispatcher.Enqueue(() =>
        {
            NetworkAsteroidClient.Instance.EnqueueSpawn(
                new NetworkAsteroidClient.SpawnInfo
                {
                    x = x,
                    y = y,
                    dirX = dirX,
                    dirY = dirY,
                    speed = speed
                });
        });
    }


    private static void WriteString(System.IO.BinaryWriter w, string s)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(s);
        w.Write((short)bytes.Length);
        w.Write(bytes);
    }

    private static string ReadString(byte[] buf, ref int o)
    {
        short len = BitConverter.ToInt16(buf, o); o += 2;
        string s = System.Text.Encoding.UTF8.GetString(buf, o, len);
        o += len;
        return s;
    }
}
