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
    private Socket socket;
    private EndPoint serverEP;
    private bool running = false;

    public string serverIP = "127.0.0.1";
    public int port = 9050;

    public GameObject player;

    [System.Serializable]
    public class PlayerData
    {
        public float posX, posY, posZ;
        public string command;

        public PlayerData(Vector3 pos, string cmd)
        {
            posX = pos.x;
            posY = pos.y;
            posZ = pos.z;
            command = cmd;
        }
    }

    void Start()
    {
        if (player == null)
        {
            player = GameObject.CreatePrimitive(PrimitiveType.Cube);
            player.name = "ClientPlayer";
            player.transform.position = Vector3.zero;
        }

        StartClient();
    }

    void Update()
    {
        if (!running) return;

        string cmd = GetCommand();

        if (cmd != null)
        {
            MovePlayer(cmd); // movimiento local inmediato
            SendData(cmd);
        }
    }

    string GetCommand()
    {
        var k = Keyboard.current;
        if (k == null) return null;

        if (k.wKey.isPressed) return "W";
        if (k.sKey.isPressed) return "S";
        if (k.aKey.isPressed) return "A";
        if (k.dKey.isPressed) return "D";
        return null;
    }

    void MovePlayer(string cmd)
    {
        Vector3 pos = player.transform.position;

        if (cmd == "W") pos += Vector3.up * 0.1f;
        if (cmd == "S") pos += Vector3.down * 0.1f;
        if (cmd == "A") pos += Vector3.left * 0.1f;
        if (cmd == "D") pos += Vector3.right * 0.1f;

        player.transform.position = pos;
    }

    void StartClient()
    {
        try
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            serverEP = new IPEndPoint(IPAddress.Parse(serverIP), port);

            running = true;

            Debug.Log("[CLIENT UDP] Cliente iniciado.");
        }
        catch (Exception e)
        {
            Debug.LogError("[CLIENT UDP] Error: " + e.Message);
        }
    }

    void SendData(string command)
    {
        PlayerData data = new PlayerData(player.transform.position, command);

        string json = JsonConvert.SerializeObject(data);
        byte[] bytes = Encoding.UTF8.GetBytes(json);

        socket.SendTo(bytes, bytes.Length, SocketFlags.None, serverEP);

        Debug.Log("[CLIENT UDP] Enviado JSON: " + json);
    }

    void OnApplicationQuit()
    {
        running = false;
        socket?.Close();
    }
}
