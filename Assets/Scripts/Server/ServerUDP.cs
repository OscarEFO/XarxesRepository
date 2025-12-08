using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using System.Globalization;
using System.Collections.Concurrent;

public class ServerUDP : MonoBehaviour
{
    private Socket serverSocket;
    private Thread receiveThread;
    private Thread sendThread;
    private bool running = false;

    public int port = 9050;

    [Header("Scene Objects")]
    public GameObject serverPlayer;       // server local player
    public GameObject remoteClientPlayer; // representation of client player on the server

    [Header("Projectiles")]
    public GameObject serverProjectilePrefab;   // prefab para proyectiles disparados por el server
    public float projectileSpeed = 10f;
    public float projectileLifetime = 3f;

    private volatile float client_x = 0f, client_y = 0f;
    private volatile float server_x = 0f, server_y = 0f;

    private volatile float clientInputX = 0f, clientInputY = 0f;

    private ConcurrentQueue<ShootRequest> shootQueue = new ConcurrentQueue<ShootRequest>();

    private volatile EndPoint lastClientEP = null;

    private volatile float server_rot = 0f;
    private volatile float client_rot = 0f;

    void Start()
    {
        if (serverPlayer == null || remoteClientPlayer == null)
        {
            Debug.LogError("[SERVER] Assign serverPlayer and remoteClientPlayer!");
            return;
        }

        StartServer();
    }

    void Update()
    {
        // authoritative positions
        server_x = serverPlayer.transform.position.x;
        server_y = serverPlayer.transform.position.y;
        server_rot = serverPlayer.transform.eulerAngles.z;

        // client movement simulation
        float speed = serverPlayer.GetComponent<PlayerMovement>()?.speed ?? 3f;
        Vector2 input = new Vector2(clientInputX, clientInputY);
        client_x += input.x * speed * Time.deltaTime;
        client_y += input.y * speed * Time.deltaTime;

        // update visual client representation in server scene
        remoteClientPlayer.transform.position = Vector3.Lerp(remoteClientPlayer.transform.position,
                        new Vector3(client_x, client_y, 0f),
                        10f * Time.deltaTime);

        remoteClientPlayer.transform.rotation =
            Quaternion.Lerp(remoteClientPlayer.transform.rotation,
            Quaternion.Euler(0, 0, client_rot),
            10f * Time.deltaTime);

        while (shootQueue.TryDequeue(out ShootRequest req))
        {
            // spawn on server
            SpawnProjectileOnServer(new Vector2(req.x, req.y), req.rotationZ, new Vector2(req.dirX, req.dirY));

            // send to client
            SendSpawnToClient(lastClientEP,
                new Vector2(req.x, req.y),
                req.rotationZ,
                new Vector2(req.dirX, req.dirY),
                projectileSpeed);
        }
    }

    void StartServer()
    {
        try
        {
            serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            serverSocket.Bind(new IPEndPoint(IPAddress.Any, port));

            running = true;

            receiveThread = new Thread(ReceiveLoop);
            receiveThread.IsBackground = true;
            receiveThread.Start();

            sendThread = new Thread(SnapshotLoop);
            sendThread.IsBackground = true;
            sendThread.Start();

            Debug.Log("[SERVER] UDP listening on port " + port);
        }
        catch (Exception e)
        {
            Debug.LogError("[SERVER] Error starting: " + e.Message);
        }
    }

    void ReceiveLoop()
    {
        byte[] buffer = new byte[4096];
        EndPoint clientEP = new IPEndPoint(IPAddress.Any, 0);

        while (running)
        {
            try
            {
                int received = serverSocket.ReceiveFrom(buffer, ref clientEP);
                string msg = Encoding.UTF8.GetString(buffer, 0, received);

                lastClientEP = clientEP;

                if (msg.Contains("\"type\": \"input\""))
                {
                    float ix = ExtractFloat(msg, "ix");
                    float iy = ExtractFloat(msg, "iy");
                    float rot = ExtractFloat(msg, "rot");

                    clientInputX = ix;
                    clientInputY = iy;
                    client_rot = rot;
                }
                else if (msg.Contains("\"type\": \"shoot\""))
                {
                    float x = ExtractFloat(msg, "x");
                    float y = ExtractFloat(msg, "y");
                    float rotZ = ExtractFloat(msg, "rotationZ");
                    float dirX = ExtractFloat(msg, "dirX");
                    float dirY = ExtractFloat(msg, "dirY");

                    shootQueue.Enqueue(new ShootRequest
                    {
                        x = x,
                        y = y,
                        rotationZ = rotZ,
                        dirX = dirX,
                        dirY = dirY
                    });
                }
            }
            catch { }
        }
    }

    public void EnqueueServerShot(Vector2 pos, float rotZ, Vector2 dir)
    {
        shootQueue.Enqueue(new ShootRequest
        {
            x = pos.x,
            y = pos.y,
            rotationZ = rotZ,
            dirX = dir.x,
            dirY = dir.y
        });
    }

    // main-thread instantiation
    void SpawnProjectileOnServer(Vector2 pos, float rotZ, Vector2 dir)
    {
        // El server SIEMPRE usa su propio prefab
        GameObject prefabToUse = serverProjectilePrefab;

        if (prefabToUse == null)
        {
            Debug.LogError("[SERVER] No serverProjectilePrefab assigned!");
            return;
        }

        GameObject proj = Instantiate(
            prefabToUse,
            new Vector3(pos.x, pos.y, 0f),
            Quaternion.Euler(0, 0, rotZ)
        );

        Rigidbody2D rb = proj.GetComponent<Rigidbody2D>();
        if (rb != null)
            rb.linearVelocity = dir.normalized * projectileSpeed;

        Destroy(proj, projectileLifetime);
    }


    void SendSpawnToClient(EndPoint clientEP, Vector2 pos, float rotZ, Vector2 dir, float speed)
    {
        if (clientEP == null) return;

        string json =
            "{ \"type\": \"spawnProjectile\", " +
            "\"x\": " + pos.x.ToString(CultureInfo.InvariantCulture) + ", " +
            "\"y\": " + pos.y.ToString(CultureInfo.InvariantCulture) + ", " +
            "\"rotationZ\": " + rotZ.ToString(CultureInfo.InvariantCulture) + ", " +
            "\"dirX\": " + dir.x.ToString(CultureInfo.InvariantCulture) + ", " +
            "\"dirY\": " + dir.y.ToString(CultureInfo.InvariantCulture) + ", " +
            "\"speed\": " + speed.ToString(CultureInfo.InvariantCulture) + ", " +
            "\"fromServer\": 1 }";

        byte[] data = Encoding.UTF8.GetBytes(json);
        serverSocket.SendTo(data, data.Length, SocketFlags.None, clientEP);
    }

    float ExtractFloat(string json, string key)
    {
        int idx = json.IndexOf("\"" + key + "\":");
        if (idx == -1) return 0;
        int start = idx + key.Length + 3;
        int end = json.IndexOfAny(new char[] { ',', '}' }, start);
        return float.Parse(json.Substring(start, end - start), CultureInfo.InvariantCulture);
    }

    void SnapshotLoop()
    {
        while (running)
        {
            try
            {
                if (lastClientEP != null)
                {
                    string json =
                        "{ \"server_x\": " + server_x.ToString(CultureInfo.InvariantCulture) +
                        ", \"server_y\": " + server_y.ToString(CultureInfo.InvariantCulture) +
                        ", \"server_rot\": " + server_rot.ToString(CultureInfo.InvariantCulture) +
                        ", \"client_x\": " + client_x.ToString(CultureInfo.InvariantCulture) +
                        ", \"client_y\": " + client_y.ToString(CultureInfo.InvariantCulture) +
                        ", \"client_rot\": " + client_rot.ToString(CultureInfo.InvariantCulture) +
                        " }";

                    byte[] data = Encoding.UTF8.GetBytes(json);
                    serverSocket.SendTo(data, data.Length, SocketFlags.None, lastClientEP);
                }
            }
            catch { }

            Thread.Sleep(20);
        }
    }

    void OnApplicationQuit()
    {
        running = false;
        try { receiveThread?.Abort(); } catch { }
        try { sendThread?.Abort(); } catch { }
        try { serverSocket?.Close(); } catch { }
    }

    private struct ShootRequest
    {
        public float x, y, rotationZ, dirX, dirY;
    }
}
