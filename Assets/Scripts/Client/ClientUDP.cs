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
    private bool running = false;

    public string serverIP = "127.0.0.1";
    public int port = 9050;

    // Estado recibido del servidor (public para lectura por NetworkPlayer)
    public volatile float server_x, server_y;
    public volatile float client_x, client_y;
    public volatile bool hasUpdate = false;

    void Awake()
    {
        // Singleton simple
        if (Instance != null && Instance != this)
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

            Debug.Log("[CLIENT] Cliente UDP iniciado. Server: " + serverIP + ":" + port);
        }
        catch (Exception e)
        {
            Debug.LogError("[CLIENT] Error al iniciar: " + e.Message);
        }
    }

    // API pública: enviar input (W/A/S/D o NONE)
    public void SendPlayerState(Vector2 position, float rotationZ)
    {
        if (!running || clientSocket == null || serverEP == null) return;

        try
        {
            string json = "{ \"type\": \"playerState\", \"x\": " + position.x.ToString(CultureInfo.InvariantCulture) +
                      ", \"y\": " + position.y.ToString(CultureInfo.InvariantCulture) +
                      ", \"rotationZ\": " + rotationZ.ToString(CultureInfo.InvariantCulture) + " }";

            byte[] data = Encoding.UTF8.GetBytes(json);
            clientSocket.SendTo(data, data.Length, SocketFlags.None, serverEP);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[CLIENT] SendPlayerState error: " + e.Message);
        }
    }
    public void SendProjectile(Vector2 position, float rotationZ, Vector2 direction)
    {   
        if (!running || clientSocket == null || serverEP == null) return;

        try
        {
            string json = "{ \"type\": \"spawnProjectile\", " +
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
            Debug.LogWarning("[CLIENT] SendProjectile error: " + e.Message);
        }
    }

    void ReceiveLoop()
    {
        byte[] buffer = new byte[2048];
        EndPoint remote = new IPEndPoint(IPAddress.Any, 0);

        while (running)
        {
            try
            {
                int received = clientSocket.ReceiveFrom(buffer, ref remote);
                string msg = Encoding.UTF8.GetString(buffer, 0, received);

                // Parse server message of form:
                // { "server_x": ..., "server_y": ..., "client_x": ..., "client_y": ... }
                ParseServerJSON(msg);

                hasUpdate = true;
            }
            catch (Exception) { }
        }
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
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[CLIENT] ParseServerJSON error: " + e.Message + " | raw: " + json);
        }
    }

    public void OnApplicationQuit()
    {
        running = false;
        try { receiveThread?.Abort(); } catch { }
        try { clientSocket?.Close(); } catch { }
    }
}
