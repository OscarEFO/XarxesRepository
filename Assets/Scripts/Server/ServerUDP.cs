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
    private volatile bool running = false;

    public int port = 9050;

    public GameObject serverPlayer;
    public GameObject remoteClientPlayer;

    public GameObject projectilePrefab;
    public float projectileSpeed = 10f;
    public float projectileLifetime = 3f;

    public GameObject asteroidPrefab;
    public float asteroidSpeed = 2f;
    public float asteroidLifetime = 10f;
    public float asteroidSpawnInterval = 3f;

    private float asteroidTimer = 0f;

    private volatile float client_x = 0f, client_y = 0f;
    private volatile float server_x = 0f, server_y = 0f;

    private volatile float clientInputX = 0f, clientInputY = 0f;

    private ConcurrentQueue<ShootRequest> shootQueue = new ConcurrentQueue<ShootRequest>();

    private volatile EndPoint lastClientEP = null;

    private volatile float server_rot = 0f;
    private volatile float client_rot = 0f;

    void Start()
    {
        StartServer();
    }

    void Update()
    {
        if (serverPlayer != null)
        {
            server_x = serverPlayer.transform.position.x;
            server_y = serverPlayer.transform.position.y;
            server_rot = serverPlayer.transform.eulerAngles.z;
        }

        float speed = serverPlayer?.GetComponent<PlayerMovement>()?.speed ?? 3f;
        Vector2 input = new Vector2(clientInputX, clientInputY);

        client_x += input.x * speed * Time.deltaTime;
        client_y += input.y * speed * Time.deltaTime;

        if (remoteClientPlayer != null)
        {
            remoteClientPlayer.transform.position =
                Vector3.Lerp(remoteClientPlayer.transform.position,
                    new Vector3(client_x, client_y, 0f),
                    10f * Time.deltaTime);

            remoteClientPlayer.transform.rotation =
                Quaternion.Lerp(remoteClientPlayer.transform.rotation,
                    Quaternion.Euler(0, 0, client_rot),
                    10f * Time.deltaTime);
        }

        while (shootQueue.TryDequeue(out ShootRequest req))
        {
            SpawnProjectileOnServer(new Vector2(req.x, req.y),
                                    req.rotationZ,
                                    new Vector2(req.dirX, req.dirY));

            SendSpawnToClient(lastClientEP,
                new Vector2(req.x, req.y),
                req.rotationZ,
                new Vector2(req.dirX, req.dirY),
                projectileSpeed);
        }

        asteroidTimer += Time.deltaTime;
        if (asteroidTimer >= asteroidSpawnInterval)
        {
            asteroidTimer = 0f;
            SpawnAsteroid();
        }
    }

    void StartServer()
    {
        serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        serverSocket.Bind(new IPEndPoint(IPAddress.Any, port));

        running = true;

        receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
        receiveThread.Start();

        sendThread = new Thread(SnapshotLoop) { IsBackground = true };
        sendThread.Start();

        Debug.Log("[SERVER] UDP listening on port " + port);
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
                if (received <= 0) { Thread.Sleep(1); continue; }

                string msg = Encoding.UTF8.GetString(buffer, 0, received);

                if (msg.Contains("\"handshake\""))
                {
                    lastClientEP = clientEP;
                    Debug.Log("[SERVER] Handshake received, client endpoint saved.");
                }

                if (msg.Contains("\"type\": \"input\""))
                {
                    lastClientEP = clientEP;
                    clientInputX = ExtractFloat(msg, "ix");
                    clientInputY = ExtractFloat(msg, "iy");
                    client_rot = ExtractFloat(msg, "rot");
                }
                else if (msg.Contains("\"type\": \"shoot\""))
                {
                    lastClientEP = clientEP;

                    ShootRequest req = new ShootRequest
                    {
                        x = ExtractFloat(msg, "x"),
                        y = ExtractFloat(msg, "y"),
                        rotationZ = ExtractFloat(msg, "rotationZ"),
                        dirX = ExtractFloat(msg, "dirX"),
                        dirY = ExtractFloat(msg, "dirY")
                    };

                    shootQueue.Enqueue(req);
                }
            }
            catch (SocketException)
            {
                if (!running) break;
            }
            catch (Exception)
            {
                // ignore and continue
            }

            Thread.Sleep(1);
        }
    }

    void SpawnProjectileOnServer(Vector2 position, float rotationZ, Vector2 direction)
    {
        if (projectilePrefab == null) return;

        GameObject proj = Instantiate(projectilePrefab,
            new Vector3(position.x, position.y, 0f),
            Quaternion.Euler(0, 0, rotationZ));

        Rigidbody2D rb = proj.GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = direction.normalized * projectileSpeed;

        Destroy(proj, projectileLifetime);
    }

    void SpawnAsteroid()
    {
        float radius = 12f;
        Vector2 spawnPos = UnityEngine.Random.insideUnitCircle.normalized * radius;
        Vector2 dir = (Vector2.zero - spawnPos).normalized;

        if (asteroidPrefab != null)
        {
            GameObject ast = Instantiate(asteroidPrefab, spawnPos, Quaternion.identity);
            Rigidbody2D rb = ast.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = dir * asteroidSpeed;
            Destroy(ast, asteroidLifetime);
        }

        SendAsteroidSpawn(lastClientEP, spawnPos, dir, asteroidSpeed);
    }

    void SendAsteroidSpawn(EndPoint ep, Vector2 pos, Vector2 dir, float speed)
    {
        if (ep == null) return;

        string json =
            "{ \"type\": \"spawnAsteroid\", " +
            "\"x\": " + pos.x.ToString(CultureInfo.InvariantCulture) + ", " +
            "\"y\": " + pos.y.ToString(CultureInfo.InvariantCulture) + ", " +
            "\"dirX\": " + dir.x.ToString(CultureInfo.InvariantCulture) + ", " +
            "\"dirY\": " + dir.y.ToString(CultureInfo.InvariantCulture) + ", " +
            "\"speed\": " + speed.ToString(CultureInfo.InvariantCulture) +
            " }";

        byte[] data = Encoding.UTF8.GetBytes(json);
        serverSocket.SendTo(data, data.Length, SocketFlags.None, ep);
    }

    void SendSpawnToClient(EndPoint ep, Vector2 pos, float rotationZ, Vector2 dir, float speed)
    {
        if (ep == null) return;

        string json =
            "{ \"type\": \"spawnProjectile\", " +
            "\"x\": " + pos.x.ToString(CultureInfo.InvariantCulture) + ", " +
            "\"y\": " + pos.y.ToString(CultureInfo.InvariantCulture) + ", " +
            "\"rotationZ\": " + rotationZ.ToString(CultureInfo.InvariantCulture) + ", " +
            "\"dirX\": " + dir.x.ToString(CultureInfo.InvariantCulture) + ", " +
            "\"dirY\": " + dir.y.ToString(CultureInfo.InvariantCulture) + ", " +
            "\"speed\": " + speed.ToString(CultureInfo.InvariantCulture) +
            " }";

        byte[] data = Encoding.UTF8.GetBytes(json);
        serverSocket.SendTo(data, data.Length, SocketFlags.None, ep);
    }

    float ExtractFloat(string json, string key)
    {
        int idx = json.IndexOf("\"" + key + "\":");
        if (idx == -1) return 0f;

        int start = idx + key.Length + 3;
        int end = json.IndexOfAny(new char[] { ',', '}' }, start);
        if (end == -1) end = json.Length;
        string v = json.Substring(start, end - start).Trim();
        if (string.IsNullOrEmpty(v)) return 0f;
        float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out float result);
        return result;
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
            catch (Exception)
            {
                // ignore
            }

            Thread.Sleep(20); // 50Hz snapshot
        }
    }

    void OnApplicationQuit()
    {
        running = false;
        try
        {
            if (receiveThread != null && receiveThread.IsAlive)
            {
                receiveThread.Join(200);
                if (receiveThread.IsAlive) receiveThread.Abort();
            }
        }
        catch { }

        try
        {
            if (sendThread != null && sendThread.IsAlive)
            {
                sendThread.Join(200);
                if (sendThread.IsAlive) sendThread.Abort();
            }
        }
        catch { }

        try { serverSocket?.Close(); } catch { }
    }

    private struct ShootRequest
    {
        public float x, y, rotationZ, dirX, dirY;
    }
}
