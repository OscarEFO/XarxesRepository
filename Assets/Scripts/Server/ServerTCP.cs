using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class ServerTCP : MonoBehaviour
{
    private Socket serverSocket;
    private Socket clientSocket;
    private Thread listenThread;
    private Thread receiveThread;
    private bool isRunning = false;

    public int port = 9050;

    void Start()
    {
        StartServer();
    }

    void StartServer()
    {
        try
        {
            // Crear socket TCP
            serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // Enlazarlo a cualquier IP local y al puerto definido
            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, port);
            serverSocket.Bind(localEndPoint);

            // Escuchar hasta 10 conexiones pendientes
            serverSocket.Listen(10);
            Debug.Log($"[SERVER] Esperando clientes en puerto {port}...");

            isRunning = true;

            // Hilo para aceptar conexiones
            listenThread = new Thread(AcceptClients);
            listenThread.Start();
        }
        catch (Exception e)
        {
            Debug.LogError($"[SERVER] Error al iniciar: {e.Message}");
        }
    }

    void AcceptClients()
    {
        try
        {
            clientSocket = serverSocket.Accept();
            Debug.Log("[SERVER] Cliente conectado: " + clientSocket.RemoteEndPoint.ToString());

            // Iniciar hilo para recibir mensajes del cliente
            receiveThread = new Thread(ReceiveMessages);
            receiveThread.Start();
        }
        catch (Exception e)
        {
            Debug.LogError($"[SERVER] Error al aceptar cliente: {e.Message}");
        }
    }

    void ReceiveMessages()
    {
        byte[] buffer = new byte[1024];
        while (isRunning)
        {
            try
            {
                int received = clientSocket.Receive(buffer);
                if (received > 0)
                {
                    string message = Encoding.ASCII.GetString(buffer, 0, received);
                    Debug.Log("[SERVER] Mensaje recibido: " + message);

                    // Enviar respuesta "ping"
                    byte[] data = Encoding.ASCII.GetBytes("ping");
                    clientSocket.Send(data);
                    Debug.Log("[SERVER] Enviado: ping");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SERVER] Cliente desconectado o error: " + e.Message);
                break;
            }
        }
    }

    void OnApplicationQuit()
    {
        isRunning = false;
        receiveThread?.Abort();
        listenThread?.Abort();
        clientSocket?.Close();
        serverSocket?.Close();
        Debug.Log("[SERVER] Servidor cerrado.");
    }
}
