using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine.SceneManagement;
using UnityEngine;
using System.Linq;

/// <summary>
/// ClientManagerUDP - lightweight client networking compatible with the simple ServerUDP protocol:
/// Packet layout:
/// [byte] PacketType
/// Create(0): [int id][short nameLen][name bytes][float x][float y][float velX][float velY][float rot]
/// Update(1): [int id][float x][float y][float velX][float velY][float rot]
/// Shoot (2): [int id][float originX][float originY][float dirX][float dirY]
/// Delete(3): [int id]
/// </summary>
public class ClientManagerUDP : MonoBehaviour
{
    [Header("Connection")]
    public string userName = "";
    public string serverIP = "127.0.0.1";
    public int serverPort = 9050;

    [Header("Prefabs")]
    public GameObject playerPrefab;
    public GameObject player2Prefab;
    public GameObject bulletPrefabRed;
    public GameObject bulletPrefabGreen;
    public float bulletSpeed = 15f;

    // Networking
    private Socket socket;
    private IPEndPoint serverEndPoint;
    private Thread receiveThread;
    private volatile bool running = false;

    // Local player
    public Player localPlayer;
    private int localId;

    // Remote players
    private readonly Dictionary<int, Player> players = new Dictionary<int, Player>();
    private readonly object playersLock = new object();

    private int connectedPlayerCount = 0;

    public int GetLocalId()
    {
        return localId;
    }

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
                w.Write(0f); // pos.x (initial)
                w.Write(0f); // pos.y
                w.Write(0f); // vel.x
                w.Write(0f); // vel.y (health)
                w.Write(0f); // rot

                var data = ms.ToArray();
                socket.SendTo(data, serverEndPoint);
            }
            Debug.Log($"[CLIENT] Sent CREATE id={localId} name='{userName}'");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[CLIENT] SendCreate failed: " + e.Message);
        }
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

                var data = ms.ToArray();
                socket.SendTo(data, serverEndPoint);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[CLIENT] SendUpdate failed: " + e.Message);
        }
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

                var data = ms.ToArray();
                socket.SendTo(data, serverEndPoint);
            }
            Debug.Log($"[CLIENT] Sent SHOOT id={id} dir={direction} origin={origin}");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[CLIENT] SendShoot failed: " + e.Message);
        }
    }

    public void SendDelete(int id)
    {
        try
        {
            using (var ms = new System.IO.MemoryStream())
            using (var w = new System.IO.BinaryWriter(ms))
            {
                w.Write((byte)4);
                w.Write(id);
                var data = ms.ToArray();
                socket.SendTo(data, serverEndPoint);
            }
            Debug.Log($"[CLIENT] Sent DELETE id={id}");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[CLIENT] SendDelete failed: " + e.Message);
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
                if (recv <= 0) { Thread.Sleep(1); continue; }

                byte[] data = new byte[recv];
                Array.Copy(buffer, 0, data, 0, recv);

                ParseIncomingPacket(data, (IPEndPoint)remote);
            }
            catch (SocketException)
            {
                Thread.Sleep(1);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[CLIENT] ReceiveLoop error: " + ex.Message);
                Thread.Sleep(1);
            }
        }
    }

    private void ParseIncomingPacket(byte[] buf, IPEndPoint sender)
    {
        try
        {
            int o = 0;
            if (buf.Length < 1) return;
            byte type = buf[o++];

            switch (type)
            {
                case 0: // CREATE
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
                    break;

                case 1: // UPDATE
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
                    break;

                case 2: // SHOOT
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
                    break;

                case 3: // ASTEROID
                    {
                        float x = BitConverter.ToSingle(buf, o); o += 4;
                        float y = BitConverter.ToSingle(buf, o); o += 4;
                        float dx = BitConverter.ToSingle(buf, o); o += 4;
                        float dy = BitConverter.ToSingle(buf, o); o += 4;
                        float sp = BitConverter.ToSingle(buf, o); o += 4;

                        MainThreadDispatcher.Enqueue(() =>
                        {
                            NetworkAsteroidClient.Instance.EnqueueSpawn(new NetworkAsteroidClient.SpawnInfo
                            {
                                x = x,
                                y = y,
                                dirX = dx,
                                dirY = dy,
                                speed = sp
                            });
                        });
                    }
                    break;

                case 4:
                    {
                        int id = BitConverter.ToInt32(buf, o); o += 4;

                        MainThreadDispatcher.Enqueue(() =>
                        {

                            if (players.TryGetValue(id, out var p))
                            {
                                if (p.isLocalPlayer)
                                {
                                    HardResetNetworking();
                                    SceneManager.LoadScene("Lose");
                                }
                                else
                                {
                                    HardResetNetworking();
                                    SceneManager.LoadScene("Win");
                                }
                            }
                        });
                    }
                    break;

                default:
                    Debug.LogWarning("[CLIENT] Unknown packet type: " + type);
                    break;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[CLIENT] ParseIncomingPacket error: " + e.Message);
        }
    }

    public void HardResetNetworking()
    {
        Debug.Log("[CLIENT] HARD RESET – cleaning all network state");

        try
        {
            // Parar el loop de recepción
            running = false;

            // Intentar un join corto del hilo receptor
            try { receiveThread?.Join(200); } catch { }

            // Si sigue vivo, intentar abort (último recurso)
            try { receiveThread?.Abort(); } catch { }

            // Cerrar socket
            try { socket?.Close(); } catch { }
            socket = null;

            // Destruir todos los players locales y limpiar diccionario
            lock (playersLock)
            {
                foreach (var kv in players)
                {
                    if (kv.Value != null)
                        Destroy(kv.Value.gameObject);
                }
                players.Clear();
            }

            // Destruir jugador local si existe (por si quedó)
            if (localPlayer != null)
            {
                try { Destroy(localPlayer.gameObject); } catch { }
                localPlayer = null;
            }

            // Destruir proyectiles/asteroides que queden en escena
            var projs = GameObject.FindObjectsOfType<Projectile>();
            foreach (var p in projs) Destroy(p.gameObject);

            // Si tienes un script Bullet aparte
            var bullets = GameObject.FindObjectsOfType(typeof(MonoBehaviour))
                          .Cast<MonoBehaviour>()
                          .Where(m => m.GetType().Name == "Bullet")
                          .ToArray();
            foreach (var b in bullets) Destroy(b.gameObject);

            // También destruye asteroides si persisten (buscar por componente o tag)
            // Ajusta el tipo "Asteroid" por el script real si existe:
            var asteroids = GameObject.FindObjectsOfType(typeof(MonoBehaviour))
                            .Cast<MonoBehaviour>()
                            .Where(m => m.GetType().Name.ToLower().Contains("asteroid"))
                            .ToArray();
            foreach (var a in asteroids) Destroy(a.gameObject);

            Debug.Log("[CLIENT] Hard reset completed.");
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[CLIENT] HardResetNetworking failed: " + ex.Message);
        }
    }


    private void SpawnOrUpdatePlayer(int id, string name, Vector2 pos, float rot, Vector2 vel)
    {
        bool isLocal = (id == localId);

        lock (playersLock)
        {
            // NEW PLAYER
            if (!players.ContainsKey(id))
            {
                connectedPlayerCount++;

                GameObject prefabToUse = playerPrefab;
                if (connectedPlayerCount == 2 && player2Prefab != null)
                    prefabToUse = player2Prefab;

                GameObject go = Instantiate(prefabToUse, new Vector3(pos.x, pos.y, 0f), Quaternion.Euler(0, 0, rot));
                go.name = $"Player_{id}";
                Player p = go.GetComponent<Player>();

                if (p == null)
                {
                    Debug.LogError("[CLIENT] Player prefab missing Player component.");
                    Destroy(go);
                    return;
                }

                p.networkId = id;
                p.clientManager = this;
                p.isLocalPlayer = isLocal;

                string finalName = (connectedPlayerCount == 1) ? "Player1" : "Player2";
                p.userName = finalName;
                if (p.tmp != null)
                    p.tmp.SetText(finalName);

                if (connectedPlayerCount == 1)
                    p.bulletPrefab = bulletPrefabGreen;
                else if (connectedPlayerCount == 2)
                    p.bulletPrefab = bulletPrefabRed;

                p.currentHealth = Mathf.Max(0, Mathf.RoundToInt(vel.y));

                players[id] = p;

                if (isLocal)
                    localPlayer = p;

                Debug.Log($"[CLIENT] Spawned {finalName} (id={id})");
                return;
            }
        }

        if (!isLocal)
        {
            players[id].ApplyNetworkState(pos, rot, vel);
        }
    }

    private void HandleIncomingShoot(int shooterId, Vector2 origin, Vector2 direction)
    {
        players.TryGetValue(shooterId, out var shooter);

        GameObject prefab = shooter != null ? shooter.bulletPrefab : bulletPrefabGreen;
        if (prefab == null) return;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        GameObject b = Instantiate(prefab, origin, Quaternion.Euler(0, 0, angle));

        var proj = b.GetComponent<Projectile>();
        if (proj != null)
            proj.ownerId = shooterId;

        if (b.TryGetComponent<Rigidbody2D>(out var rb))
        {
            var bulletCol = b.GetComponent<Collider2D>();
            Collider2D shooterCol = (shooter != null) ? shooter.GetComponent<Collider2D>() : null;
            if (bulletCol != null && shooterCol != null)
            {
                Physics2D.IgnoreCollision(bulletCol, shooterCol, true);
            }

            rb.linearVelocity = direction.normalized * bulletSpeed;
        }

        var bScript = b.GetComponent<Bullet>();
        if (bScript != null)
            bScript.ownerId = shooterId;
    }

    private static void WriteString(System.IO.BinaryWriter w, string s)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(s);
        short len = (short)bytes.Length;
        w.Write(len);
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
