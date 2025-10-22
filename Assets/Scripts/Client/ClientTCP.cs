using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class ClientTCP : MonoBehaviour
{
    private Socket clientSocket;
    private Thread receiveThread;
    private bool isConnected = false;

    public string serverIP = "127.0.0.1"; // localhost
    public int port = 9050;

    void Start()
    {
        ConnectToServer();
    }

    void ConnectToServer()
    {
        try
        {
            // Crear socket TCP
            clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // Crear endpoint remoto (servidor)
            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Parse(serverIP), port);

            // Intentar conexión
            clientSocket.Connect(remoteEP);
            isConnected = true;
            Debug.Log("[CLIENT] Conectado al servidor " + serverIP + ":" + port);

            // Enviar mensaje inicial
            string msg = "Hola desde el cliente!";
            byte[] data = Encoding.ASCII.GetBytes(msg);
            clientSocket.Send(data);
            Debug.Log("[CLIENT] Enviado: " + msg);

            // Iniciar hilo para recibir mensajes del servidor
            receiveThread = new Thread(ReceiveMessages);
            receiveThread.Start();
        }
        catch (Exception e)
        {
            Debug.LogError("[CLIENT] Error al conectar: " + e.Message);
        }
    }

    void ReceiveMessages()
    {
        byte[] buffer = new byte[1024];
        while (isConnected)
        {
            try
            {
                int received = clientSocket.Receive(buffer);
                if (received > 0)
                {
                    string message = Encoding.ASCII.GetString(buffer, 0, received);
                    Debug.Log("[CLIENT] Recibido: " + message);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[CLIENT] Desconectado o error: " + e.Message);
                break;
            }
        }
    }

    void OnApplicationQuit()
    {
        isConnected = false;
        receiveThread?.Abort();
        clientSocket?.Close();
        Debug.Log("[CLIENT] Conexión cerrada.");
    }
}
