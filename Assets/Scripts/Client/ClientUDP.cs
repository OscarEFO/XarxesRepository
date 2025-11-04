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

    public GameObject player;

    void Start()
    {
        if (player == null)
        {
            Debug.LogError("[CLIENT] No se asignó Player en el inspector!");
            return;
        }

        StartClient();
    }

    void Update()
    {
        if (!running || player == null) return;

        if (IsMoving())
        {
            SendPosition();
        }
    }

    bool IsMoving()
    {
        if (Keyboard.current == null) return false;

        return
            Keyboard.current.wKey.isPressed ||
            Keyboard.current.aKey.isPressed ||
            Keyboard.current.sKey.isPressed ||
            Keyboard.current.dKey.isPressed;
    }

    void StartClient()
    {
        try
        {
            clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            serverEP = new IPEndPoint(IPAddress.Parse(serverIP), port);

            running = true;
            receiveThread = new Thread(ReceiveLoop);
            receiveThread.Start();

            Debug.Log("[CLIENT] Cliente UDP iniciado.");
        }
        catch (Exception e)
        {
            Debug.LogError("[CLIENT] Error al iniciar: " + e.Message);
        }
    }

    void SendPosition()
    {
        Vector3 p = player.transform.position;

        if (float.IsNaN(p.x) || float.IsNaN(p.y))
            return;

        string json = "{ \"posx\": " + p.x.ToString(CultureInfo.InvariantCulture) +
                      ", \"posy\": " + p.y.ToString(CultureInfo.InvariantCulture) +
                      " }";

        byte[] data = Encoding.UTF8.GetBytes(json);
        clientSocket.SendTo(data, data.Length, SocketFlags.None, serverEP);

        Debug.Log("[CLIENT] Enviado: " + json);
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

                Debug.Log("[CLIENT] ECO del servidor: " + msg);
            }
            catch { }
        }
    }

    void OnApplicationQuit()
    {
        running = false;
        receiveThread?.Abort();
        clientSocket?.Close();
    }
}
