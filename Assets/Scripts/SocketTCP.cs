using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;


public class NewBehaviourScript : MonoBehaviour
{

    Thread clientThread;
    Thread serverThread;

    bool cancelToken = false;
    // Start is called before the first frame update
    void Start()
    {
        clientThread = new Thread(ClientProcess);
        serverThread = new Thread(ServerProcess);

        serverThread.Start();

    }


    //void Socket(AdressFamily addressFamily, SocketType socketType, ProtocolType protocolType);
    //public void Bind(EndPoint localEp);

    void ServerProcess()
    {
        Socket ServerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        IPEndPoint ipep = new IPEndPoint(IPAddress.Loopback, 9050);
        IPEndPoint ipep2 = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9050);

        ServerSocket.Bind(ipep);
        ServerSocket.Listen(10);

        Socket s;
        Debug.Log("XXX");

        clientThread.Start();
        // s.Listen(10);


        //todo 3: accept from the server    capture the return      log something from the return

        Socket clientSocket = ServerSocket.Accept();
        Debug.Log(clientSocket.ToString());



        string message = "hello";
        byte[] rawData = System.Text.Encoding.UTF8.GetBytes(message);   //UTF8 is like ascii but better
        byte[] rawData2 = System.Text.Encoding.ASCII.GetBytes(message);

        clientSocket.Send(rawData);


        // wait until receive

    }

    void ClientProcess()
    {
        Socket ClientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        IPEndPoint ServerEndPoint = new IPEndPoint(IPAddress.Loopback, 9050);

        ClientSocket.Connect(ServerEndPoint);

        //clientThread.Start();

        // create socket
        // get server (endpoint port + ip)
        //connecting



        byte[] buffer = new byte[4096];
        while(!cancelToken)
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

    private void OnDestroy()
    {
        cancelToken = true;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
