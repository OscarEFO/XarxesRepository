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

    // FIX --------------------------
    // Instead of blindly incrementing connectedPlayerCount incorrectly,
    // we track join order PER PLAYER ID.
    private readonly Dictionary<int, int> joinOrder = new Dictionary<int, int>();
    private int nextJoinOrder = 1;
    // ----------------------------------

    void Start()
    {
        if (ClientServerInfo.Instance != null)
        {
            if (!string.IsNullOrEmpty(ClientServerInfo.Instance.userName))
                userName = ClientServerInfo.Instance.userName;
            if (!string.IsNullOrEmpty(ClientServerInfo.Instance.serverIP))
                serverIP = ClientServerInfo.Instance.serverIP;
        }

        serverEndPoint = new IPEndPoint(IPAddress.Parse(serverIP), serverPort);

        localId = UnityEngine.Random.Range(100000, 999999);

        SetupSocket();
        StartReceiveThread();
        SendCreate();

        Debug.Log($"[CLIENT] Started localId={localId} userName='{userName}' server={serverIP}:{serverPort}");
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
        try
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Blocking = false;
            serverEndPoint = new IPEndPoint(IPAddress.Parse(serverIP), serverPort);
        }
        catch (Exception e)
        {
            Debug.LogError("[CLIENT] Socket setup failed: " + e.Message);
        }
    }

    private void StartReceiveThread()
    {
        running = true;
        receiveThread = new Thread(ReceiveLoop);
        receiveThread.IsBackground = true;
        receiveThread.Start();
    }

    public void SetLocalPlayer(Player p)
    {
        localPlayer = p;
    }

    public void SendCreate()
    {
        try
        {
            using (var ms = new System.IO.MemoryStream())
            using (var w = new System.IO.BinaryWriter(ms))
            {
                w.Write((byte)0);
                w.Write(localId);
                WriteString(w, userName);
                w.Write(0f);
                w.Write(0f);
                w.Write(0f);
                w.Write(0f);
                w.Write(0f);

                socket.SendTo(ms.ToArray(), serverEndPoint);
            }
        }
        catch { }
    }

    public void SendUpdate(int id, Vector2 pos, float rot, Vector2 vel)
    {
        try
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
        catch { }
    }

    public void SendShoot(int id, Vector2 origin, Vector2 direction)
    {
        try
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
        catch { }
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
                if (recv <= 0) { Thread.Sleep(1); continue; }

                byte[] data = new byte[recv];
                Array.Copy(buffer, data, recv);

                ParseIncomingPacket(data);
            }
            catch { Thread.Sleep(1); }
        }
    }

    private void ParseIncomingPacket(byte[] buf)
    {
        try
        {
            int o = 0;
            byte type = buf[o++];

            switch (type)
            {
                case 0: 
                {
                    int id = BitConverter.ToInt32(buf, o); o += 4;
                    string name = ReadString(buf, ref o);
                    float x = BitConverter.ToSingle(buf, o); o += 4;
                    float y = BitConverter.ToSingle(buf, o); o += 4;
                    float velx = BitConverter.ToSingle(buf, o); o += 4;
                    float vely = BitConverter.ToSingle(buf, o); o += 4;
                    float rot = BitConverter.ToSingle(buf, o); o += 4;

                    MainThreadDispatcher.Enqueue(() =>
                        SpawnOrUpdatePlayer(id, name, new Vector2(x,y), rot, new Vector2(velx,vely)));
                }
                break;

                case 1:
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
                            players[id].ApplyNetworkState(new Vector2(x,y), rot, new Vector2(velx,vely));
                    });
                }
                break;

                case 2:
                {
                    int id = BitConverter.ToInt32(buf, o); o += 4;
                    float ox = BitConverter.ToSingle(buf, o); o += 4;
                    float oy = BitConverter.ToSingle(buf, o); o += 4;
                    float dx = BitConverter.ToSingle(buf, o); o += 4;
                    float dy = BitConverter.ToSingle(buf, o); o += 4;

                    MainThreadDispatcher.Enqueue(() =>
                        HandleIncomingShoot(id, new Vector2(ox,oy), new Vector2(dx,dy)));
                }
                break;
            }
        }
        catch { }
    }

    private void SpawnOrUpdatePlayer(int id, string name, Vector2 pos, float rot, Vector2 vel)
    {
        bool isLocal = (id == localId);

        lock (playersLock)
        {
            // NEW PLAYER
            if (!players.ContainsKey(id))
            {
                // FIX: Assign join order **once**, per unique player ID
                if (!joinOrder.ContainsKey(id))
                {
                    joinOrder[id] = nextJoinOrder;
                    nextJoinOrder++;
                }

                int order = joinOrder[id];

                GameObject prefabToUse = (order == 1) ? playerPrefab : player2Prefab;

                GameObject go = Instantiate(prefabToUse, pos, Quaternion.Euler(0, 0, rot));
                go.name = $"Player_{id}";

                Player p = go.GetComponent<Player>();

                p.networkId = id;
                p.clientManager = this;
                p.isLocalPlayer = isLocal;

                // FIX: Assign Player1 / Player2 names PROPERLY
                string finalName = (order == 1) ? "Player1" : "Player2";
                p.userName = finalName;
                if (p.tmp != null)
                    p.tmp.SetText(finalName);

                // FIX: Assign correct bullet prefab
                p.bulletPrefab = (order == 1) ?
                    bulletPrefab :
                    bulletPrefabGreen;

                players[id] = p;

                if (isLocal)
                    localPlayer = p;

                Debug.Log($"[CLIENT] Spawned {finalName} (id={id}) order={order}");
                return;
            }
        }

        // UPDATE REMOTE PLAYER
        if (!isLocal)
            players[id].ApplyNetworkState(pos, rot, vel);
    }

    private void HandleIncomingShoot(int shooterId, Vector2 origin, Vector2 direction)
    {
        if (!players.TryGetValue(shooterId, out var shooter))
            return;

        GameObject prefab = shooter.bulletPrefab;
        if (prefab == null) return;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;

        GameObject b = Instantiate(prefab, origin, Quaternion.Euler(0,0,angle));

        if (b.TryGetComponent<Rigidbody2D>(out var rb))
            rb.linearVelocity = direction.normalized * bulletSpeed;

        var bulletCol = b.GetComponent<Collider2D>();
        var shooterCol = shooter.GetComponent<Collider2D>();
        if (bulletCol && shooterCol)
            Physics2D.IgnoreCollision(bulletCol, shooterCol);
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
