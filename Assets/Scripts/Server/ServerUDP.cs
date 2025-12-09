using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;
using UnityEngine;

public class ServerUDP : MonoBehaviour
{
    Socket socket;
    Dictionary<EndPoint, int> users = new Dictionary<EndPoint, int>();
    bool running = false;

    public void StartServer(int port = 9050)
    {
        IPEndPoint ipep = new IPEndPoint(System.Net.IPAddress.Any, port);
        socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(ipep);

        running = true;
        Thread th = new Thread(Receive);
        th.Start();

        Debug.Log("SERVER UDP started on port " + port);
    }

    void Receive()
    {
        byte[] buffer = new byte[1024];
        EndPoint remote = new IPEndPoint(System.Net.IPAddress.Any, 0);

        while (running)
        {
            try
            {
                int recv = socket.ReceiveFrom(buffer, ref remote);

                if (!users.ContainsKey(remote))
                    users.Add(remote, 0); // Solo registro de nuevo usuario

                // Reenvío simple a todos los demás
                foreach (var u in users)
                {
                    if (!u.Key.Equals(remote))
                        socket.SendTo(buffer, 0, recv, SocketFlags.None, u.Key);
                }
            }
            catch { }
        }
    }

    private void OnApplicationQuit()
    {
        running = false;
        socket?.Close();
    }
}
