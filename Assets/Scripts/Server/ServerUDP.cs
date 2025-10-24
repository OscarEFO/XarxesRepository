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
    private EndPoint remoteEP;
    private Thread receiveThread;
    private bool isRunning = false;

    [Header("Network Settings")]
    public int port = 9050;

    [Header("Scene Objects")]
    public GameObject player;
    public GameObject serverObject;

    private GameObject playerInstance;

    public class PlayerData
    {
        public string id;
        public float posX, posY, posZ;
        public string command;

        public Vector3 GetPosition() => new Vector3(posX, posY, posZ);
        public void SetPosition(Vector3 pos)
        {
            posX = pos.x;
            posY = pos.y;
            posZ = pos.z;
        }
    }

    public class ServerObjectData
    {
        public float posX, posY, posZ;
        public void SetPosition(Vector3 pos)
        {
            posX = pos.x;
            posY = pos.y;
            posZ = pos.z;
        }
    }

    void Start()
    {
        StartServer();
    }

 void Update()
    {
        if (serverObject != null)
        {
            serverObject.transform.position = new Vector3(
                Mathf.Sin(Time.time) * 3f,
                0,
                Mathf.Cos(Time.time) * 3f
            );
        }
    }

    void StartServer()
    {
        try
        {
            // Crear socket UDP
            serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            // Asociar el socket a una direcci�n local (cualquier IP, puerto 9050)
            IPEndPoint localEP = new IPEndPoint(IPAddress.Any, port);
            serverSocket.Bind(localEP);

            Debug.Log($"[SERVER UDP] Escuchando en puerto {port}...");
            isRunning = true;

            playerInstance = Instantiate(player, Vector3.zero, Quaternion.identity);

            // Iniciar hilo para escuchar mensajes
            receiveThread = new Thread(ReceiveLoop);
            receiveThread.Start();
        }
        catch (Exception e)
        {
            Debug.LogError($"[SERVER UDP] Error al iniciar: {e.Message}");
        }
    }

    void ReceiveLoop()
    {
        byte[] buffer = new byte[1024];

        while (isRunning)
        {
            try
            {
                // Recibir datos de cualquier cliente
                EndPoint clientEP = new IPEndPoint(IPAddress.Any, 0);
                int received = serverSocket.ReceiveFrom(buffer, ref clientEP);

                string message = Encoding.ASCII.GetString(buffer, 0, received);
                Debug.Log($"[SERVER UDP] Mensaje recibido de {clientEP}: {message}");

                // Guardamos el �ltimo cliente para responderle
                remoteEP = clientEP;

                // Enviar "ping" de respuesta 
                /*
                byte[] data = Encoding.ASCII.GetBytes("ping");
                serverSocket.SendTo(data, data.Length, SocketFlags.None, remoteEP);
                Debug.Log($"[SERVER UDP] Enviado 'ping' a {clientEP}");*/
                PlayerData data = null;
                try
                {
                    data = JsonConvert.DeserializeObject<PlayerData>(message);
                }
           
            catch (Exception ex)
            {
                Debug.LogWarning($"[SERVER UDP] Error en el JSON del cliente: {ex.Message}");
            }
             if (data != null)
                {
                    ProcessPlayerInput(data);
                    SendServerObjectToClient(clientEP);
                }
                else
                {
                    // Optional: respond to initial handshake
                    byte[] response = Encoding.UTF8.GetBytes("pong");
                    serverSocket.SendTo(response, response.Length, SocketFlags.None, clientEP);
                }                
            }

            catch (Exception e)
            {
                Debug.LogWarning($"[SERVER UDP] Error en recepci�n: {e.Message}");
            }
        }   
    }

    void ProcessPlayerInput(PlayerData data)
    {
        if (playerInstance == null) return;

        Vector3 movement = Vector3.zero;
        switch (data.command)
        {
            case "W": movement = Vector3.forward; break;
            case "S": movement = Vector3.back; break;
            case "A": movement = Vector3.left; break;
            case "D": movement = Vector3.right; break;
        }

        // Move player's representation on the server
        playerInstance.transform.position += movement * Time.deltaTime * 5f;
    }

    void SendServerObjectToClient(EndPoint clientEP)
    {
        if (serverObject == null) return;

        try
        {
            ServerObjectData objData = new ServerObjectData();
            objData.SetPosition(serverObject.transform.position);

            string json = JsonConvert.SerializeObject(objData);
            byte[] data = Encoding.UTF8.GetBytes(json);

            serverSocket.SendTo(data, data.Length, SocketFlags.None, clientEP);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SERVER UDP] Error sending to client: {e.Message}");
        }
    }

    void OnApplicationQuit()
    {
        isRunning = false;
        if (receiveThread != null && receiveThread.IsAlive){ 
            receiveThread.Abort();
        }
        serverSocket?.Close();
        Debug.Log("[SERVER UDP] Servidor cerrado.");
    }
}
