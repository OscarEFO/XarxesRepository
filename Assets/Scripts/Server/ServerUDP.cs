using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Globalization;

public class ServerUDP : MonoBehaviour
{
    private Socket serverSocket;
    private Thread receiveThread;
    private bool running = false;

    public int port = 9050;

    // Player del servidor (lo mueve WASD de la ventana Server)
    public GameObject serverPlayer;

    // Player del cliente representado en el servidor
    public GameObject remoteClient;

    // Posición oficial del cliente (la controla el server)
    private Vector2 clientPos = Vector2.zero;

    // Input recibido del cliente
    private volatile string lastClientInput = "NONE";

    public float moveSpeed = 5f;

    void Start()
    {
        if (serverPlayer == null || remoteClient == null)
        {
            Debug.LogError("[SERVER] Debes asignar serverPlayer y remoteClient!");
            return;
        }

        StartServer();
    }


    void Update()
    {
        // 1. Mover al server (servidor es autoritativo de sí mismo)
        HandleServerMovement();

        // 2. Mover player del cliente según inputs recibidos
        HandleClientMovement();

        // 3. Actualizar posiciones en escena
        remoteClient.transform.position = clientPos;
    }


    void HandleServerMovement()
    {
        if (Keyboard.current == null) return;

        Vector2 move = Vector2.zero;

        if (Keyboard.current.wKey.isPressed) move += Vector2.up;
        if (Keyboard.current.sKey.isPressed) move += Vector2.down;
        if (Keyboard.current.aKey.isPressed) move += Vector2.left;
        if (Keyboard.current.dKey.isPressed) move += Vector2.right;

        serverPlayer.transform.Translate(move.normalized * moveSpeed * Time.deltaTime);
    }


    void HandleClientMovement()
    {
        Vector2 dir = Vector2.zero;

        switch (lastClientInput)
        {
            case "W": dir = Vector2.up; break;
            case "S": dir = Vector2.down; break;
            case "A": dir = Vector2.left; break;
            case "D": dir = Vector2.right; break;
        }

        clientPos += dir.normalized * moveSpeed * Time.deltaTime;
    }


    void StartServer()
    {
        try
        {
            serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            serverSocket.Bind(new IPEndPoint(IPAddress.Any, port));

            running = true;

            receiveThread = new Thread(ReceiveLoop);
            receiveThread.Start();

            Debug.Log("[SERVER] Servidor UDP escuchando en puerto " + port);
        }
        catch (Exception e)
        {
            Debug.LogError("[SERVER] Error al iniciar: " + e.Message);
        }
    }



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

                //Debug.Log("[SERVER] Recibido: " + msg);

                ParseClientJSON(msg);

                // server envía las POSICIONES OFICIALES
                string response =
                    "{ \"server_x\": " + serverPlayer.transform.position.x.ToString(CultureInfo.InvariantCulture) +
                    ", \"server_y\": " + serverPlayer.transform.position.y.ToString(CultureInfo.InvariantCulture) +
                    ", \"client_x\": " + clientPos.x.ToString(CultureInfo.InvariantCulture) +
                    ", \"client_y\": " + clientPos.y.ToString(CultureInfo.InvariantCulture) +
                    " }";

                byte[] data = Encoding.UTF8.GetBytes(response);
                serverSocket.SendTo(data, data.Length, SocketFlags.None, clientEP);
            }
            catch { }
        }
    }



    void ParseClientJSON(string json)
    {
        json = json.Trim().TrimStart('{').TrimEnd('}');
        string[] pairs = json.Split(',');

        foreach (string pair in pairs)
        {
            string[] kv = pair.Split(':');
            if (kv.Length != 2) continue;

            string key = kv[0].Trim().Replace("\"", "");
            string value = kv[1].Trim().Replace("\"", "");

            if (key == "input")
                lastClientInput = value;
        }
    }



    void OnApplicationQuit()
    {
        running = false;
        receiveThread?.Abort();
        serverSocket?.Close();
    }
}
