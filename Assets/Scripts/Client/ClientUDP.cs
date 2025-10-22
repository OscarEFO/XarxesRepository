using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class ClientUDP : MonoBehaviour
{
    private Socket clientSocket;
    private EndPoint serverEP;
    private Thread receiveThread;
    private bool isRunning = false;

    public string serverIP = "127.0.0.1"; // localhost
    public int port = 9050;

    void Start()
    {
        StartClient();
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

    void ReceiveLoop()
    {
        byte[] buffer = new byte[1024];
        EndPoint remote = new IPEndPoint(IPAddress.Any, 0);

        while (isRunning)
        {
            try
            {
                int received = clientSocket.ReceiveFrom(buffer, ref remote);
                string message = Encoding.ASCII.GetString(buffer, 0, received);
                Debug.Log("[CLIENT UDP] Recibido: " + message);
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
        receiveThread?.Abort();
        clientSocket?.Close();
        Debug.Log("[CLIENT UDP] Cliente cerrado.");
    }
}
