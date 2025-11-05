using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class ServerUDP : MonoBehaviour
{
    private Socket serverSocket;
    private Thread receiveThread;
    private bool running = false;

    public int port = 9050;

    public GameObject ghost;

    private volatile bool hasNewPos = false;
    private volatile float nx, ny;

    void Start()
    {
        StartServer();
    }

    void Update()
    {
        if (hasNewPos)
        {
            ghost.transform.position = new Vector3(nx, ny, 0f);
            hasNewPos = false;
        }
    }

    void StartServer()
    {
        try
        {
            serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            serverSocket.Bind(new IPEndPoint(IPAddress.Any, port));

            running = true;

            receiveThread = new Thread(ReceiveLoop);
            receiveThread.Start();

            Debug.Log("[SERVER] Servidor UDP escuchando en puerto " + port);
        }
        catch (Exception e)
        {
            Debug.LogError("[SERVER] Error al iniciar: " + e.Message);
        }
    }

    void ReceiveLoop()
    {
        byte[] buffer = new byte[2048];
        EndPoint clientEP = new IPEndPoint(IPAddress.Any, 0);

        while (running)
        {
            try
            {
                int received = serverSocket.ReceiveFrom(buffer, ref clientEP);
                string msg = Encoding.UTF8.GetString(buffer, 0, received);

                //Debug.Log("[SERVER] Recibido: " + msg);

                if (ParseJSON(msg))
                    hasNewPos = true;

                Debug.Log("[SERVER] posicion x despues del parse: " + nx + "y: " + ny);

                string response =
                    "{ \"gx\": " + nx +
                    ", \"gy\": " + ny + " }";

                byte[] data = Encoding.UTF8.GetBytes(response);
                serverSocket.SendTo(data, data.Length, SocketFlags.None, clientEP);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SERVER] Error: " + e.Message);
            }
        }
    }

    bool ParseJSON(string json)
    {
        try
        {
            // Ejemplo: { "posx": 1.52, "posy": -3.4 }
            // Limpiar bordes
            json = json.Trim();
            json = json.TrimStart('{').TrimEnd('}');

            // Separar pares key:value
            string[] pairs = json.Split(',');

            foreach (string pair in pairs)
            {
                string[] kv = pair.Split(':');

                if (kv.Length != 2)
                    continue;

                string key = kv[0].Trim().Replace("\"", "");
                string value = kv[1].Trim();

                // Usamos CultureInfo.InvariantCulture para aceptar floats con "." siempre
                float f = float.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

                if (key == "posx")
                    nx = f;
                else if (key == "posy")
                    ny = f;
            }

            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SERVER] Parse error: " + e.Message + " | JSON: " + json);
            return false;
        }
    }


    void OnApplicationQuit()
    {
        running = false;
        receiveThread?.Abort();
        serverSocket?.Close();
    }
}
