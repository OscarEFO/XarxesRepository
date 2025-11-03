using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

public class ServerUDP : MonoBehaviour
{
    private readonly Queue<Action> mainThreadActions = new Queue<Action>();
    private Socket serverSocket;
    private EndPoint remoteEP;
    private Thread receiveThread;
    private bool isRunning = false;

    [Header("Network Settings")]
    public int port = 9050;

    [Header("Scene Objects")]
    public GameObject player;
    public GameObject serverObject;

    private readonly Dictionary<string, GameObject> playerInstances = new Dictionary<string, GameObject>();
    private readonly Dictionary<string, EndPoint> clientEndpoints = new Dictionary<string, EndPoint>();


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

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        StartServer();
    }

void Update()
{
    while (mainThreadActions.Count > 0)
    {
        Action action = null;
        lock (mainThreadActions)
        {
            if (mainThreadActions.Count > 0)
                action = mainThreadActions.Dequeue();
        }
        action?.Invoke();
    }

    if (serverObject != null)
    {
        serverObject.transform.position = new Vector3(
            Mathf.Sin(Time.time) * 3f,
            0,
            Mathf.Cos(Time.time) * 3f
        );
    }
    
    foreach (var kvp in clientEndpoints)
    {
        SendPlayerPositionToClient(kvp.Key, kvp.Value);
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

            // Iniciar hilo para escuchar mensajes
            receiveThread = new Thread(ReceiveLoop);
            receiveThread.Start();
        }
        catch (Exception e)
        {
            Debug.LogError($"[SERVER UDP] Error al iniciar: {e.Message}");
        }
    }

   private void ReceiveLoop()
    {
        byte[] buffer = new byte[2048];

        while (isRunning)
        {
            try
            {
                EndPoint clientEP = new IPEndPoint(IPAddress.Any, 0);
                int received = serverSocket.ReceiveFrom(buffer, ref clientEP);
                string message = Encoding.UTF8.GetString(buffer, 0, received);

                Debug.Log($"[SERVER UDP] Received from {clientEP}: {message}");

                PlayerData playerData = null;
                try
                {
                    playerData = JsonConvert.DeserializeObject<PlayerData>(message);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[SERVER UDP] Invalid JSON from {clientEP}: {ex.Message}");
                }

                if (playerData != null)
                {
                    lock (playerInstances)
                    {
                        ProcessPlayerInput(playerData, clientEP);
                    }

                    SendServerObjectToClient(clientEP);
                }
                else
                {
                    byte[] response = Encoding.UTF8.GetBytes("pong");
                    serverSocket.SendTo(response, response.Length, SocketFlags.None, clientEP);
                    Debug.Log($"[SERVER UDP] Sent handshake to {clientEP}");
                }
            }
            catch (SocketException ex)
            {
                Debug.LogWarning($"[SERVER UDP] Socket error: {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SERVER UDP] Receive loop error: {ex.Message}");
            }
        }
    }

    private void ProcessPlayerInput(PlayerData data, EndPoint clientEP)
    {
        if (data == null || string.IsNullOrEmpty(data.id)) return;

        if (!playerInstances.ContainsKey(data.id))
        {
            GameObject newPlayer = Instantiate(player, Vector3.zero, Quaternion.identity);
            playerInstances[data.id] = newPlayer;
            clientEndpoints[data.id] = clientEP;
            Debug.Log($"[SERVER UDP] New player registered: {data.id} from {clientEP}");
        }

        Vector3 movement = Vector3.zero;
        switch (data.command)
        {
            case "W": movement = Vector3.forward; break;
            case "S": movement = Vector3.back; break;
            case "A": movement = Vector3.left; break;
            case "D": movement = Vector3.right; break;
        }

        // Queue movement on main thread
        lock (mainThreadActions)
        {
            mainThreadActions.Enqueue(() =>
            {
                GameObject playerObj = playerInstances[data.id];
                playerObj.transform.position += movement * 0.2f; // Fixed step (not deltaTime)
                Debug.Log($"[SERVER UDP] Player {data.id} moved {data.command} to {playerObj.transform.position}");
            });
        }
    }


    void SendServerObjectToClient(EndPoint clientEP)
    {
        if (serverObject == null || clientEP == null) return;

        try
        {
            ServerObjectData objData = new ServerObjectData();
            objData.SetPosition(serverObject.transform.position);

            string json = JsonConvert.SerializeObject(objData);
            byte[] data = Encoding.UTF8.GetBytes(json);

            serverSocket.SendTo(data, data.Length, SocketFlags.None, clientEP);
            Debug.Log($"[SERVER UDP] Sent object data to {clientEP}: {json}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SERVER UDP] Error sending to client: {e.Message}");
        }
    }
    private void SendPlayerPositionToClient(string playerId, EndPoint clientEP)
{
    if (!playerInstances.ContainsKey(playerId) || clientEP == null) return;

    try
    {
        GameObject playerObj = playerInstances[playerId];

        PlayerData data = new PlayerData
        {
            id = playerId
        };
        data.SetPosition(playerObj.transform.position);

        string json = JsonConvert.SerializeObject(data);
        byte[] bytes = Encoding.UTF8.GetBytes(json);

        serverSocket.SendTo(bytes, bytes.Length, SocketFlags.None, clientEP);

        Debug.Log($"[SERVER UDP] Sent updated position to {clientEP}: {json}");
    }
    catch (Exception e)
    {
        Debug.LogWarning($"[SERVER UDP] Error sending player position: {e.Message}");
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
