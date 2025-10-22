using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

public class SocketUDP : MonoBehaviour
{
    // Start is called before the first frame update

    Thread clientThread;
    Thread serverThread;

    bool cancelToken = false;

    void Start()
    {
        clientThread = new Thread(ClientProcess);
        serverThread = new Thread(ServerProcess);

        serverThread.Start();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void ServerProcess()
    {
        Socket ServerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Tcp);
        IPEndPoint ipep = new IPEndPoint(IPAddress.Loopback, 9050);
        IPEndPoint ipep2 = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9050);
        ServerSocket.Bind(ipep);
        ServerSocket.Listen(10);

        Debug.Log("XXX");

        clientThread.Start();



    }

    void ClientProcess()
    {
        Socket ClientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        IPEndPoint ServerEndPoint = new IPEndPoint(IPAddress.Loopback, 9050);

        string message = "hello";
        byte[] rawData = System.Text.Encoding.UTF8.GetBytes(message);   //UTF8 is like ascii but better

        

        ClientSocket.SendTo(rawData, 5, SocketFlags.None, ServerEndPoint);

        //ClientSocket.Connect(ServerEndPoint);

        //clientThread.Start();

        // create socket
        // get server (endpoint port + ip)
        //connecting



        byte[] buffer = new byte[4096];
        while (!cancelToken)
        {
            //receive
            // System.Text.Encoding.UTF8.GetString(buffer, 0, received);


            int received = ClientSocket.Receive(buffer);
            if (received > 0)
            {
                string receivedText = System.Text.Encoding.UTF8.GetString(buffer, 0, received);
                Debug.Log(receivedText);
            }

            Thread.Sleep(10);
        }
    }
}
