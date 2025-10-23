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

    void Start()
    {
        StartClient();
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
        try
        {
            PlayerData data = new PlayerData
            {
                id = "Player1",
                command = command
            };
            data.SetPosition(player.transform.position);

            string json = JsonConvert.SerializeObject(data);
            byte[] bytes = Encoding.UTF8.GetBytes(json);

            clientSocket.SendTo(bytes, bytes.Length, SocketFlags.None, serverEP);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[CLIENT UDP] Send error: " + e.Message);
        }
    }

    private void ReceiveLoop()
    {
        byte[] buffer = new byte[1024];
        EndPoint remote = new IPEndPoint(IPAddress.Any, 0);

        while (isRunning)
        {
            try
            {
                int received = clientSocket.ReceiveFrom(buffer, ref remote);
                string message = Encoding.ASCII.GetString(buffer, 0, received);
                 try
                {
                    ServerObjectData serverData = JsonConvert.DeserializeObject<ServerObjectData>(message);
                    serverObjectTargetPos = serverData.GetPosition();
                }
                catch
                {
                    Debug.Log("[CLIENT UDP] Recibido: " + message);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[CLIENT UDP] Error en recepci�n: " + e.Message);
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
