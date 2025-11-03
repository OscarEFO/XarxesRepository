using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using Newtonsoft.Json;
using UnityEngine.InputSystem;

public class ClientUDP : MonoBehaviour
{
    private Socket clientSocket;
    private EndPoint serverEP;
    private Thread receiveThread;
    private bool isRunning = false;

    [Header("Connection Settings")]
    public string serverIP = "127.0.0.1"; // localhost
    public int port = 9050;

    [Header("Scene Objects")]
    public GameObject player;       // Objeto del player controlado localmente
    public GameObject serverObject;

    private Vector3 serverObjectTargetPos = Vector3.zero;
    private string clientId;

    public class PlayerData
    {
        public string id;
        public float posX, posY, posZ;
        public string command;

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

        public Vector3 GetPosition()
        {
            return new Vector3(posX, posY, posZ);
        }
    }
    void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }
    void Start()
    {
        StartClient();
        clientId = Guid.NewGuid().ToString();

        if (player == null)
        {
            Debug.LogWarning("[CLIENT UDP] No hay player asignado, creando uno.");
            player = new GameObject("ClientPlayer");
            player.transform.position = Vector3.zero;
        }
    }

    
    void Update()
    {
        if (!isRunning) return;

        string command = GetInputCommand();
        if (!string.IsNullOrEmpty(command))
        {
            SendPlayerCommand(command);
        }

        if (serverObject != null)
        {
            serverObject.transform.position = Vector3.Lerp(
                serverObject.transform.position,
                serverObjectTargetPos,
                Time.deltaTime * 5f
            );
        }
    }

    private string GetInputCommand()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return null;

        if (keyboard.wKey.isPressed) return "W";
        if (keyboard.sKey.isPressed) return "S";
        if (keyboard.aKey.isPressed) return "A";
        if (keyboard.dKey.isPressed) return "D";
        return null;
    }


    void StartClient()
    {
        try
        {
            // Crear socket UDP
            clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            // Endpoint del servidor
            serverEP = new IPEndPoint(IPAddress.Parse(serverIP), port);

            // Enviar primer mensaje al servidor
            string msg = "Hola desde el cliente UDP!";
            byte[] data = Encoding.ASCII.GetBytes(msg);
            clientSocket.SendTo(data, data.Length, SocketFlags.None, serverEP);
            Debug.Log("[CLIENT UDP] Enviado: " + msg);

            // Empezar a escuchar respuestas
            isRunning = true;
            receiveThread = new Thread(ReceiveLoop);
            receiveThread.Start();
        }
        catch (Exception e)
        {
            Debug.LogError("[CLIENT UDP] Error al iniciar: " + e.Message);
        }
    }

    private void SendPlayerCommand(string command)
    {
        if (player == null)
        {
            Debug.LogError("[CLIENT UDP] Player is NULL! Assign it in the Inspector.");
            return;
        }

        if (clientSocket == null)
        {
            Debug.LogError("[CLIENT UDP] Client socket is NULL! Did StartClient() fail?");
            return;
        }

        if (serverEP == null)
        {
            Debug.LogError("[CLIENT UDP] Server endpoint is NULL!");
            return;
        }
        try
        {
            PlayerData data = new PlayerData
            {
                id = "player",
                command = command
            };
            data.SetPosition(player.transform.position);
            Debug.Log($"[CLIENT UDP] Sending {command} | Pos: {player.transform.position}");


            string json = JsonConvert.SerializeObject(data);
            byte[] bytes = Encoding.UTF8.GetBytes(json);

            clientSocket.SendTo(bytes, bytes.Length, SocketFlags.None, serverEP);
            Debug.Log($"[CLIENT UDP] Sent: {command}");

        }
        catch (Exception e)
        {
            Debug.LogWarning("[CLIENT UDP] Send error: " + e.Message);
        }
    }

    private void ReceiveLoop()
    {
        byte[] buffer = new byte[2048];
        EndPoint remote = new IPEndPoint(IPAddress.Any, 0);

        while (isRunning)
        {
            try
            {
                int received = clientSocket.ReceiveFrom(buffer, ref remote);
                string message = Encoding.UTF8.GetString(buffer, 0, received);

                try
                {
                    ServerObjectData serverData = JsonConvert.DeserializeObject<ServerObjectData>(message);
                    if (serverData != null)
                    {
                        Vector3 newServerPos = serverData.GetPosition();
                        serverObjectTargetPos = newServerPos;
                        continue; 
                    }
                }
                catch { }

                try
                {
                    PlayerData playerData = JsonConvert.DeserializeObject<PlayerData>(message);
                    if (playerData != null && playerData.id == "player")
                    {
                        Vector3 newPos = new Vector3(playerData.posX, playerData.posY, playerData.posZ);
                        player.transform.position = newPos;
                        Debug.Log($"[CLIENT UDP] Updated player position from server: {newPos}");
                        continue;
                    }
                }
                catch { }

                Debug.Log($"[CLIENT UDP] Unrecognized message: {message}");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[CLIENT UDP] Error en recepción: " + e.Message);
            }
        }
    }


    void OnApplicationQuit()
    {
        isRunning = false;
        if (receiveThread != null && receiveThread.IsAlive)
        {
            receiveThread.Abort();
        }
        clientSocket?.Close();
        Debug.Log("[CLIENT UDP] Cliente cerrado.");
    }
}
