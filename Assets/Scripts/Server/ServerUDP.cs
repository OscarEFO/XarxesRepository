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
    public GameObject serverPlayer;       // player local on server (applyMovementLocally = true)
    public GameObject remoteClientPlayer; // representation of client player on server

    [Header("Projectiles")]
    public GameObject projectilePrefab;
    public float projectileSpeed = 10f;
    public float projectileLifetime = 3f;

    // authoritative positions
    private volatile float client_x = 0f, client_y = 0f;
    private volatile float server_x = 0f, server_y = 0f;

    // last input vector from client (updated in receive thread)
    private volatile float clientInputX = 0f, clientInputY = 0f;

    // queue of shoot requests (thread-safe)
    private ConcurrentQueue<ShootRequest> shootQueue = new ConcurrentQueue<ShootRequest>();

    // last client endpoint (to send snapshots/spawns)
    private volatile EndPoint lastClientEP = null;

    // autoritativos del servidor
    private volatile float server_rot = 0f;

    // ultimo rot enviado por cliente
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
        // serverPlayer is moved locally by PlayerMovement (applyMovementLocally = true)
        server_x = serverPlayer.transform.position.x;
        server_y = serverPlayer.transform.position.y;

        // Apply client's authoritative input to the client's official position
        // Move client according to input (server authoritative simulation)
        float speed = serverPlayer.GetComponent<PlayerMovement>()?.speed ?? 3f;
        Vector2 input = new Vector2(clientInputX, clientInputY);
        client_x += input.x * speed * Time.deltaTime;
        client_y += input.y * speed * Time.deltaTime;

        // Update the visual representation on server
        remoteClientPlayer.transform.position =
    Vector3.Lerp(remoteClientPlayer.transform.position,
                 new Vector3(client_x, client_y, 0f),
                 10f * Time.deltaTime);
        remoteClientPlayer.transform.rotation =
    Quaternion.Lerp(remoteClientPlayer.transform.rotation,
                    Quaternion.Euler(0, 0, client_rot),
                    10f * Time.deltaTime);
        server_rot = serverPlayer.transform.eulerAngles.z;

        // Process pending shoot requests (spawn into server world and notify client)
        while (shootQueue.TryDequeue(out ShootRequest req))
        {
            // Spawn authoritative projectile on server main thread
            SpawnProjectileOnServer(new Vector2(req.x, req.y), req.rotationZ, new Vector2(req.dirX, req.dirY));
            // Notify client so it instantiates a replica
            SendSpawnToClient(lastClientEP, new Vector2(req.x, req.y), req.rotationZ, new Vector2(req.dirX, req.dirY), projectileSpeed);
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

                // Determine message type quickly
                if (msg.Contains("\"type\": \"input\"") || msg.Contains("\"input\"") && msg.Contains("ix"))
                {
                    // parse input vector
                    float ix = ExtractFloat(msg, "ix");
                    float iy = ExtractFloat(msg, "iy");
                    float rot = ExtractFloat(msg, "rot");

                    clientInputX = ix;
                    clientInputY = iy;
                    client_rot = rot;
                }
                else if (msg.Contains("\"type\": \"shoot\"") || msg.Contains("\"shoot\""))
                {
                    float x = ExtractFloat(msg, "x");
                    float y = ExtractFloat(msg, "y");
                    float rotationZ = ExtractFloat(msg, "rotationZ");
                    float dirX = ExtractFloat(msg, "dirX");
                    float dirY = ExtractFloat(msg, "dirY");

                    // enqueue to be processed on main thread
                    ShootRequest req = new ShootRequest { x = x, y = y, rotationZ = rotationZ, dirX = dirX, dirY = dirY };
                    shootQueue.Enqueue(req);
                }
                else if (msg.Contains("\"type\": \"playerState\""))
                {
                    // ignore: old mode; we operate on input-only
                }
            }
            catch (Exception) { }
        }
    }

    // Create projectile in server scene (main thread)
    void SpawnProjectileOnServer(Vector2 position, float rotationZ, Vector2 direction)
    {
        if (projectilePrefab == null) return;
        GameObject proj = Instantiate(projectilePrefab, new Vector3(position.x, position.y, 0f), Quaternion.Euler(0, 0, rotationZ));
        Rigidbody2D rb = proj.GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = direction.normalized * projectileSpeed;
        Destroy(proj, projectileLifetime);
    }

    void SendSpawnToClient(EndPoint clientEP, Vector2 position, float rotationZ, Vector2 direction, float speed)
    {
        if (clientEP == null) return;

        string json =
            "{ \"type\": \"spawnProjectile\", " +
            "\"x\": " + position.x.ToString(CultureInfo.InvariantCulture) + ", " +
            "\"y\": " + position.y.ToString(CultureInfo.InvariantCulture) + ", " +
            "\"rotationZ\": " + rotationZ.ToString(CultureInfo.InvariantCulture) + ", " +
            "\"dirX\": " + direction.x.ToString(CultureInfo.InvariantCulture) + ", " +
            "\"dirY\": " + direction.y.ToString(CultureInfo.InvariantCulture) + ", " +
            "\"speed\": " + speed.ToString(CultureInfo.InvariantCulture) +
            " }";

        try
        {
            byte[] data = Encoding.UTF8.GetBytes(json);
            serverSocket.SendTo(data, data.Length, SocketFlags.None, clientEP);
        }
        catch (Exception) { }
    }

    float ExtractFloat(string json, string key)
    {
        int idx = json.IndexOf("\"" + key + "\":");
        if (idx == -1) return 0f;
        int start = idx + key.Length + 3;
        int end = json.IndexOfAny(new char[] { ',', '}' }, start);
        string value = json.Substring(start, end - start).Trim();
        if (value == "") return 0f;
        return float.Parse(value, CultureInfo.InvariantCulture);
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
            catch (Exception) { }

            Thread.Sleep(20); // 50 Hz
        }
    }

    void OnApplicationQuit()
    {
        running = false;
        try { receiveThread?.Abort(); } catch { }
        try { sendThread?.Abort(); } catch { }
        try { serverSocket?.Close(); } catch { }
    }

    // simple container
    private struct ShootRequest
    {
        public float x, y, rotationZ, dirX, dirY;
    }
}
