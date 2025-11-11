using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Globalization;

public class ClientUDP : MonoBehaviour
{
    private Socket clientSocket;
    private EndPoint serverEP;
    private Thread receiveThread;
    private bool running = false;

    public string serverIP = "127.0.0.1";
    public int port = 9050;

    public GameObject localPlayer;     // Player del CLIENTE
    public GameObject remoteServer;    // Player del SERVER en la escena

    private volatile float server_x, server_y;
    private volatile float client_x, client_y;

    private volatile bool hasUpdate = false;

    void Start()
    {
        if (!localPlayer || !remoteServer)
        {
            Debug.LogError("[CLIENT] Falta asignar localPlayer o remoteServer!");
            return;
        }

        StartClient();
    }


    void Update()
    {
        if (!running) return;

        SendInput();

        if (hasUpdate)
        {
            // el server es autoritativo → actualiza mi posición real
            localPlayer.transform.position = new Vector3(client_x, client_y, 0);

            // también actualiza el player del server
            remoteServer.transform.position = new Vector3(server_x, server_y, 0);

            hasUpdate = false;
        }
    }



    void SendInput()
    {
        string input = "NONE";

        if (Keyboard.current.wKey.isPressed) input = "W";
        else if (Keyboard.current.sKey.isPressed) input = "S";
        else if (Keyboard.current.aKey.isPressed) input = "A";
        else if (Keyboard.current.dKey.isPressed) input = "D";

        string json = "{ \"id\": \"client\", \"input\": \"" + input + "\" }";

        byte[] data = Encoding.UTF8.GetBytes(json);
        clientSocket.SendTo(data, data.Length, SocketFlags.None, serverEP);
    }



    void StartClient()
    {
        clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        serverEP = new IPEndPoint(IPAddress.Parse(serverIP), port);

        running = true;
        receiveThread = new Thread(ReceiveLoop);
        receiveThread.Start();

        Debug.Log("[CLIENT] Cliente iniciado.");
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

                ParseServerJSON(msg);
                hasUpdate = true;
            }
            catch { }
        }
    }



    void ParseServerJSON(string json)
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


    void OnApplicationQuit()
    {
        running = false;
        receiveThread?.Abort();
        clientSocket?.Close();
    }
}
