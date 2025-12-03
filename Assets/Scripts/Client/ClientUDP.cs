using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using System.Globalization;

public class ClientUDP : MonoBehaviour
{
    public static ClientUDP Instance { get; private set; }

    private Socket clientSocket;
    private EndPoint serverEP;
    private Thread receiveThread;
    private volatile bool running = false;

    public string serverIP = "127.0.0.1";
    public int port = 9050;

    // authoritative values from server
    public volatile float server_x, server_y;
    public volatile float client_x, client_y;
    public volatile float server_rot = 0f;
    public volatile float client_rot = 0f;
    public volatile bool hasUpdate = false;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        StartClient();
        SendHandshake();
    }

    void OnDisable()
    {
        OnApplicationQuit();
    }

    public void StartClient()
    {
        try
        {
            clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            serverEP = new IPEndPoint(IPAddress.Parse(serverIP), port);

            running = true;
            receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
            receiveThread.Start();

            Debug.Log("[CLIENT] UDP client started.");
        }
        catch (Exception e)
        {
            Debug.LogError("[CLIENT] Error starting: " + e.Message);
        }
    }

    void SendHandshake()
    {
        if (clientSocket == null) return;
        string json = "{ \"type\": \"handshake\" }";
        byte[] data = Encoding.UTF8.GetBytes(json);
        clientSocket.SendTo(data, data.Length, SocketFlags.None, serverEP);
        Debug.Log("[CLIENT] Handshake sent.");
    }

    // called by NetworkPlayer to send input vector and rotation
    public void SendInput(Vector2 input, float rotationZ)
    {
        if (!running) return;

        try
        {
            string json =
                "{ \"type\": \"input\", " +
                "\"ix\": " + input.x.ToString(CultureInfo.InvariantCulture) + ", " +
                "\"iy\": " + input.y.ToString(CultureInfo.InvariantCulture) + ", " +
                "\"rot\": " + rotationZ.ToString(CultureInfo.InvariantCulture) +
                " }";

            byte[] data = Encoding.UTF8.GetBytes(json);
            clientSocket.SendTo(data, data.Length, SocketFlags.None, serverEP);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[CLIENT] SendInput error: " + e.Message);
        }
    }

    public void SendShootRequest(Vector2 position, float rotationZ, Vector2 direction)
    {
        if (!running) return;

        try
        {
            string json =
                "{ \"type\": \"shoot\", " +
                "\"x\": " + position.x.ToString(CultureInfo.InvariantCulture) + ", " +
                "\"y\": " + position.y.ToString(CultureInfo.InvariantCulture) + ", " +
                "\"rotationZ\": " + rotationZ.ToString(CultureInfo.InvariantCulture) + ", " +
                "\"dirX\": " + direction.x.ToString(CultureInfo.InvariantCulture) + ", " +
                "\"dirY\": " + direction.y.ToString(CultureInfo.InvariantCulture) +
                " }";

            byte[] data = Encoding.UTF8.GetBytes(json);
            clientSocket.SendTo(data, data.Length, SocketFlags.None, serverEP);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[CLIENT] SendShootRequest error: " + e.Message);
        }
    }

    void ReceiveLoop()
    {
        byte[] buffer = new byte[4096];
        EndPoint remote = new IPEndPoint(IPAddress.Any, 0);

        while (running)
        {
            try
            {
                // this blocks until data arrives
                int received = clientSocket.ReceiveFrom(buffer, ref remote);
                if (received <= 0) { Thread.Sleep(1); continue; }

                string msg = Encoding.UTF8.GetString(buffer, 0, received);

                // --- Asteroids ---
                if (msg.Contains("\"type\": \"spawnAsteroid\""))
                {
                    // avoid expensive logging on every packet
                    ParseAsteroidSpawn(msg);
                }
                // --- Projectiles ---
                else if (msg.Contains("\"type\": \"spawnProjectile\""))
                {
                    ParseProjectileJSON(msg);
                }
                // --- Server snapshot update ---
                else if (msg.Contains("\"server_x\""))
                {
                    ParseServerJSON(msg);
                    hasUpdate = true;
                }
            }
            catch (SocketException)
            {
                // socket closed or interrupted; break loop if not running
                if (!running) break;
            }
            catch (Exception)
            {
                // swallow to keep thread alive; consider logging less-frequent errors
            }

            // tiny sleep to avoid burning CPU (keeps main thread healthy)
            Thread.Sleep(1);
        }
    }

    void ParseAsteroidSpawn(string json)
    {
        float x = ExtractFloat(json, "x");
        float y = ExtractFloat(json, "y");
        float dirX = ExtractFloat(json, "dirX");
        float dirY = ExtractFloat(json, "dirY");
        float speed = ExtractFloat(json, "speed");

        NetworkAsteroidClient.Instance?.EnqueueSpawn(
            new NetworkAsteroidClient.SpawnInfo
            {
                x = x,
                y = y,
                dirX = dirX,
                dirY = dirY,
                speed = speed
            });
    }

    void ParseProjectileJSON(string json)
    {
        float x = ExtractFloat(json, "x");
        float y = ExtractFloat(json, "y");
        float dirX = ExtractFloat(json, "dirX");
        float dirY = ExtractFloat(json, "dirY");
        float speed = ExtractFloat(json, "speed");
        float rotationZ = ExtractFloat(json, "rotationZ");

        NetworkShootingClient.Instance?.EnqueueSpawn(
            new NetworkShootingClient.SpawnInfo
            {
                x = x,
                y = y,
                dirX = dirX,
                dirY = dirY,
                speed = speed,
                rotationZ = rotationZ
            });
    }

    void ParseServerJSON(string json)
    {
        try
        {
            json = json.Trim().TrimStart('{').TrimEnd('}');
            string[] pairs = json.Split(',');

            foreach (string pair in pairs)
            {
                string[] kv = pair.Split(':');
                if (kv.Length != 2) continue;

                string key = kv[0].Trim().Replace("\"", "");
                string value = kv[1].Trim().Replace("\"", "");

                float f = float.Parse(value, CultureInfo.InvariantCulture);

                switch (key)
                {
                    case "server_x": server_x = f; break;
                    case "server_y": server_y = f; break;
                    case "client_x": client_x = f; break;
                    case "client_y": client_y = f; break;
                    case "server_rot": server_rot = f; break;
                    case "client_rot": client_rot = f; break;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[CLIENT] ParseServerJSON error: " + e.Message + " | raw: " + json);
        }
    }

    float ExtractFloat(string json, string key)
    {
        int idx = json.IndexOf("\"" + key + "\":");
        if (idx == -1) return 0f;
        int start = idx + key.Length + 3;
        int end = json.IndexOfAny(new char[] { ',', '}' }, start);
        if (end == -1) end = json.Length;
        string value = json.Substring(start, end - start).Trim();
        if (string.IsNullOrEmpty(value)) return 0f;
        float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float result);
        return result;
    }

    public void OnApplicationQuit()
    {
        running = false;
        try
        {
            if (receiveThread != null && receiveThread.IsAlive)
            {
                // give thread a moment to exit gracefully
                receiveThread.Join(200);
                if (receiveThread.IsAlive) receiveThread.Abort();
            }
        }
        catch { }

        try { clientSocket?.Close(); } catch { }
    }
}
