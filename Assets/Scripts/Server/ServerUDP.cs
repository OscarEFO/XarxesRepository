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
    public GameObject remoteClientPlayer; // Representación del cliente

    private PlayerMovement serverMovement;

    // Vars autoritativas
    private volatile float client_x = 0f, client_y = 0f;
    private volatile float server_x = 0f, server_y = 0f;

    // Último endpoint del cliente (para enviarle datos)
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
        // Actualizamos la posición del serverPlayer por si se mueve localmente
        server_x = serverPlayer.transform.position.x;
        server_y = serverPlayer.transform.position.y;

        // Actualizamos la visualización del cliente
        remoteClientPlayer.transform.position = new Vector3(client_x, client_y, 0f);
    }

    void StartServer()
    {
        try
        {
            serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            serverSocket.Bind(new IPEndPoint(IPAddress.Any, port));

            running = true;

            // Hilo de recepción (inputs)
            receiveThread = new Thread(ReceiveLoop);
            receiveThread.IsBackground = true;
            receiveThread.Start();

            // Hilo de envío (snapshots)
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
    // HILO DE RECEPCIÓN
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

                // Guardamos la IP del cliente para enviárle snapshots
                lastClientEP = clientEP;

                string input = ParseInput(msg);
                if (input != null)
                    ApplyClientInput(input);
            }
            catch { }
        }
    }

    // ----------------------
    // HILO DE ENVÍO CONTINUO
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
