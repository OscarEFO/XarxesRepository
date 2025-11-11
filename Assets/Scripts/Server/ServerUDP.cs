using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using System.Globalization;

public class ServerUDP : MonoBehaviour
{
    private Socket serverSocket;
    private Thread receiveThread;
    private Thread sendThread;
    private bool running = false;

    public int port = 9050;

    [Header("Scene Objects")]
    public GameObject serverPlayer;       // Player local
    public GameObject remoteClientPlayer; // Representaci�n del cliente

    private PlayerMovement serverMovement;

    // Vars autoritativas
    private volatile float client_x = 0f, client_y = 0f;
    private volatile float server_x = 0f, server_y = 0f;

    // �ltimo endpoint del cliente (para enviarle datos)
    private volatile EndPoint lastClientEP = null;

    void Start()
    {
        if (serverPlayer == null || remoteClientPlayer == null)
        {
            Debug.LogError("[SERVER] Asigna serverPlayer y remoteClientPlayer!");
            return;
        }

        serverMovement = serverPlayer.GetComponent<PlayerMovement>();

        StartServer();
    }

    void Update()
    {
        // Actualizamos la posici�n del serverPlayer por si se mueve localmente
        server_x = serverPlayer.transform.position.x;
        server_y = serverPlayer.transform.position.y;

        // Actualizamos la visualizaci�n del cliente
        remoteClientPlayer.transform.position = new Vector3(client_x, client_y, 0f);
    }

    void StartServer()
    {
        try
        {
            serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            serverSocket.Bind(new IPEndPoint(IPAddress.Any, port));

            running = true;

            // Hilo de recepci�n (inputs)
            receiveThread = new Thread(ReceiveLoop);
            receiveThread.IsBackground = true;
            receiveThread.Start();

            // Hilo de env�o (snapshots)
            sendThread = new Thread(SnapshotLoop);
            sendThread.IsBackground = true;
            sendThread.Start();

            Debug.Log("[SERVER] UDP escuchando en puerto " + port);
        }
        catch (Exception e)
        {
            Debug.LogError("[SERVER] Error al iniciar: " + e.Message);
        }
    }

    // ----------------------
    // HILO DE RECEPCI�N
    // ----------------------
    void ReceiveLoop()
    {
        byte[] buffer = new byte[2048];
        EndPoint clientEP = new IPEndPoint(IPAddress.Any, 0);

        while (running)
        {
            try
            {
                int received = serverSocket.ReceiveFrom(buffer, ref clientEP);
                string msg = Encoding.UTF8.GetString(buffer, 0, received);

                // Guardamos la IP del cliente para envi�rle snapshots
                lastClientEP = clientEP;

                ParseClientMessage(msg);
            }
            catch { }
        }
    }
    void ParseClientMessage(string json)
    {
        try
        {
            if (json.Contains("\"type\": \"playerState\""))
            {
                // Extract x, y, rotationZ
                float x = ExtractFloat(json, "x");
                float y = ExtractFloat(json, "y");
                float rotationZ = ExtractFloat(json, "rotationZ");

                client_x = x;
                client_y = y;

                remoteClientPlayer.transform.rotation = Quaternion.Euler(0,0,rotationZ);
            }
            else if (json.Contains("\"type\": \"spawnProjectile\""))
            {
                float x = ExtractFloat(json, "x");
                float y = ExtractFloat(json, "y");
                float rotationZ = ExtractFloat(json, "rotationZ");
                float dirX = ExtractFloat(json, "dirX");
                float dirY = ExtractFloat(json, "dirY");

                Vector2 spawnPos = new Vector2(x, y);
                Vector2 direction = new Vector2(dirX, dirY).normalized;

                SpawnProjectile(spawnPos, rotationZ, direction);
            }
        }
        catch { }
    }
    [Header("Projectile Prefab")]
    public GameObject projectilePrefab; 
    public float projectileSpeed = 10f; 

    void SpawnProjectile(Vector2 position, float rotationZ, Vector2 direction)
    {
        if (projectilePrefab == null) return;

        GameObject proj = Instantiate(projectilePrefab, position, Quaternion.Euler(0, 0, rotationZ));

        Rigidbody2D rb = proj.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = direction.normalized * projectileSpeed;
        }
    }
    float ExtractFloat(string json, string key)
    {
        int idx = json.IndexOf("\"" + key + "\":");
        if (idx == -1) return 0f;

        int start = idx + key.Length + 3;
        int end = json.IndexOfAny(new char[] { ',', '}' }, start);
        string value = json.Substring(start, end - start);
        return float.Parse(value, CultureInfo.InvariantCulture);
    }
    // ----------------------
    // HILO DE ENV�O CONTINUO
    // ----------------------
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
                        ", \"client_x\": " + client_x.ToString(CultureInfo.InvariantCulture) +
                        ", \"client_y\": " + client_y.ToString(CultureInfo.InvariantCulture) +
                        " }";

                    byte[] data = Encoding.UTF8.GetBytes(json);
                    serverSocket.SendTo(data, data.Length, SocketFlags.None, lastClientEP);
                }
            }
            catch { }

            Thread.Sleep(20); // Enviar 50 veces por segundo
        }
    }

    // ----------------------
    // PARSEAR INPUT
    // ----------------------
    string ParseInput(string json)
    {
        try
        {
            json = json.Trim().TrimStart('{').TrimEnd('}');
            string[] pairs = json.Split(',');

            string id = "";
            string input = "";

            foreach (string pair in pairs)
            {
                string[] kv = pair.Split(':');
                if (kv.Length != 2) continue;

                string key = kv[0].Trim().Replace("\"", "");
                string value = kv[1].Trim().Replace("\"", "");

                if (key == "id") id = value;
                if (key == "input") input = value;
            }

            if (id == "client")
                return input;
        }
        catch { }

        return null;
    }

    // ----------------------
    // APLICAR INPUT DEL CLIENTE
    // ----------------------
    void ApplyClientInput(string input)
    {
        float sp = 5f * Time.deltaTime;

        switch (input)
        {
            case "W": client_y += sp; break;
            case "S": client_y -= sp; break;
            case "A": client_x -= sp; break;
            case "D": client_x += sp; break;
        }
    }

    void OnApplicationQuit()
    {
        running = false;

        try { receiveThread?.Abort(); } catch { }
        try { sendThread?.Abort(); } catch { }
        try { serverSocket?.Close(); } catch { }
    }
}
