using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class ServerUDP : MonoBehaviour
{
    private Socket serverSocket;
    private EndPoint remoteEP;
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
            // Crear socket UDP
            serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            // Asociar el socket a una dirección local (cualquier IP, puerto 9050)
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

                // Guardamos el último cliente para responderle
                remoteEP = clientEP;

                // Enviar "ping" de respuesta
                byte[] data = Encoding.ASCII.GetBytes("ping");
                serverSocket.SendTo(data, data.Length, SocketFlags.None, remoteEP);
                Debug.Log($"[SERVER UDP] Enviado 'ping' a {clientEP}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SERVER UDP] Error en recepción: {e.Message}");
            }
        }
    }

    void OnApplicationQuit()
    {
        isRunning = false;
        receiveThread?.Abort();
        serverSocket?.Close();
        Debug.Log("[SERVER UDP] Servidor cerrado.");
    }
}
