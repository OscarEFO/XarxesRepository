using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using Newtonsoft.Json;

public class ServerUDP : MonoBehaviour
{
    private Socket serverSocket;
    private Thread receiveThread;
    private bool running = false;

    public int port = 9050;

    [Header("Player object controlled by client")]
    public GameObject clientGhost;

    private Vector3 pendingPosition;
    private bool hasNewPosition = false;

    [System.Serializable]
    public class PlayerData
    {
        public float posX, posY, posZ;
        public string command;

        public Vector3 GetPosition()
        {
            return new Vector3(posX, posY, posZ);
        }
    }

    void Start()
    {
        Debug.Log("[SERVER UDP] Inicializando servidor...");

        if (clientGhost == null)
        {
            clientGhost = GameObject.CreatePrimitive(PrimitiveType.Cube);
            clientGhost.name = "ServerGhostPlayer";
            clientGhost.transform.position = Vector3.zero;
        }

        StartServer();
    }

    void Update()
    {
        if (hasNewPosition)
        {
            clientGhost.transform.position = pendingPosition;
            hasNewPosition = false;
        }
    }

    void StartServer()
    {
        try
        {
            serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            serverSocket.Bind(new IPEndPoint(IPAddress.Any, port));

            Debug.Log($"[SERVER UDP] Servidor iniciado en puerto {port}");

            running = true;

            receiveThread = new Thread(ReceiveLoop);
            receiveThread.Start();
        }
        catch (Exception e)
        {
            Debug.LogError("[SERVER UDP] Error al iniciar: " + e.Message);
        }
    }

    void ReceiveLoop()
    {
        EndPoint clientEP = new IPEndPoint(IPAddress.Any, 0);
        byte[] buffer = new byte[2048];

        while (running)
        {
            try
            {
                int received = serverSocket.ReceiveFrom(buffer, ref clientEP);
                string json = Encoding.UTF8.GetString(buffer, 0, received);

                Debug.Log("[SERVER UDP] Recibido JSON: " + json);

                PlayerData data = JsonConvert.DeserializeObject<PlayerData>(json);

                if (data != null)
                {
                    Vector3 pos = data.GetPosition();

                    Debug.Log($"[SERVER UDP] Posición recibida: {pos}");

                    // Guardamos la posición para aplicarla en el hilo principal
                    pendingPosition = pos;
                    hasNewPosition = true;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SERVER UDP] Error en recepción: " + e.Message);
            }
        }
    }

    void OnApplicationQuit()
    {
        running = false;
        receiveThread?.Abort();
        serverSocket?.Close();
    }
}
