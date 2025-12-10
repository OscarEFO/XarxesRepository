// ServerManagerUDP.cs
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

public class UserData
{
    public EndPoint ep;
    public Packet.ShipDataPacket data;
    public Int16 ms;

    public UserData(EndPoint ep, Packet.ShipDataPacket data, Int16 ms)
    {
        this.ep = ep;
        this.data = data;
        this.ms = ms;
    }
}

public class ServerManagerUDP : MonoBehaviour
{
    private Socket socket;
    private IPEndPoint ipep;
    private bool socketCreated = false;

    private readonly Dictionary<string, UserData> connectedClients = new Dictionary<string, UserData>();
    public int serverPort = 9050;

    // Keep MSChecker behavior
    bool msPacket = false;

    void Start()
    {
        connectedClients.Clear();
        StartServer();
    }

    public void StartServer()
    {
        if (!socketCreated)
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            ipep = new IPEndPoint(IPAddress.Any, serverPort);
            socket.Bind(ipep);
            socketCreated = true;
            Debug.Log("Server Socket Created");
        }
        Thread receiveThread = new Thread(Receive);
        receiveThread.Start();
    }

    public void StopServer()
    {
        if (socket != null)
        {
            socket.Close();
            socket.Dispose();
            socket = null;
        }
    }

    void Send(EndPoint remote, Packet.Packet p)
    {
        try
        {
            p.Send(ref socket, (IPEndPoint)remote);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error sending packet to {remote}: {e.Message}");
        }
    }

    void Receive()
    {
        byte[] data = new byte[4096];
        EndPoint remote = new IPEndPoint(IPAddress.Any, serverPort);

        while (true)
        {
            try
            {
                int recv = socket.ReceiveFrom(data, ref remote);
                Packet.Packet pReader = new Packet.Packet(data);
                pReader.Start();

                int goNumber = pReader.DeserializeGetGameObjectsAmount();
                Packet.Packet pWriter = new Packet.Packet();
                pWriter.Start();

                for (int i = 0; i < goNumber; i++)
                {
                    HandlePacket(pReader, pWriter, remote);
                }

                Broadcast(pWriter, remote);

                pReader.Close();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error receiving data: {ex.Message}");
            }
        }
    }

    void HandlePacket(Packet.Packet pReader, Packet.Packet pWriter, EndPoint remote)
    {
        Packet.Packet.PacketType pType = pReader.DeserializeGetType();
        switch (pType)
        {
            case Packet.Packet.PacketType.CREATE:
            case Packet.Packet.PacketType.UPDATE:
                HandleCreateOrUpdate(pReader, pWriter, remote);
                break;

            case Packet.Packet.PacketType.DELETE:
                HandleDelete(pReader, pWriter, remote);
                break;

            case Packet.Packet.PacketType.TEXT:
                HandleText(pReader, pWriter, remote);
                break;

            case Packet.Packet.PacketType.ACTION_SHOOT:
                HandleActionShoot(pReader, pWriter, remote);
                break;

            case Packet.Packet.PacketType.MSCHECKER:
                HandleMSChecker(pReader, pWriter, remote);
                break;

            default:
                Debug.LogWarning($"Unhandled packet type: {pType}");
                break;
        }
    }
    void HandleCreateOrUpdate(Packet.Packet pReader, Packet.Packet pWriter, EndPoint remote)
    {
        Packet.ShipDataPacket dsData = pReader.DeserializeShipDataPacket();
        string key = $"{remote}:{dsData.id}";

        bool isNew = !connectedClients.ContainsKey(key);

        if (isNew)
        {
            connectedClients[key] = new UserData(remote, dsData, 50);
            Debug.Log($"New player connected: {dsData.name} ({dsData.id})");

            SendExistingPlayersToNewClient(remote);

            BroadcastCreateOfNewPlayer(dsData, remote);
        }
        else
        {
            connectedClients[key].data = dsData;
        }

        pWriter.Serialize(isNew ? Packet.Packet.PacketType.CREATE :
                                Packet.Packet.PacketType.UPDATE, dsData);
    }
    void SendExistingPlayersToNewClient(EndPoint remoteNewClient)
    {
        foreach (var client in connectedClients.Values)
        {
            Packet.Packet p = new Packet.Packet();
            p.Start();
            p.Serialize(Packet.Packet.PacketType.CREATE, client.data);
            Send(remoteNewClient, p);
            p.Close();
        }
    }
    void BroadcastCreateOfNewPlayer(Packet.ShipDataPacket newPlayerData, EndPoint sender)
    {
        foreach (var client in connectedClients.Values)
        {
            if (!client.ep.Equals(sender))
            {
                Packet.Packet p = new Packet.Packet();
                p.Start();
                p.Serialize(Packet.Packet.PacketType.CREATE, newPlayerData);
                Send(client.ep, p);
                p.Close();
            }
        }
    }



    void HandleDelete(Packet.Packet pReader, Packet.Packet pWriter, EndPoint remote)
    {
        Packet.DeleteDataPacket dsData = pReader.DeserializeDeleteDataPacket();
        string key = $"{remote}:{dsData.id}";
        if (connectedClients.ContainsKey(key))
        {
            connectedClients.Remove(key);
            Debug.Log($"Player {dsData.id} removed");
        }
        pWriter.Serialize(Packet.Packet.PacketType.DELETE, dsData);
    }

    void HandleText(Packet.Packet pReader, Packet.Packet pWriter, EndPoint remote)
    {
        Packet.TextDataPacket dsData = pReader.DeserializeTextDataPacket();
        pWriter.Serialize(Packet.Packet.PacketType.TEXT, dsData);
    }

    void HandleActionShoot(Packet.Packet pReader, Packet.Packet pWriter, EndPoint remote)
    {
        Packet.ActionShootDataPacket dsData = pReader.DeserializeActionShootDataPacket();
        // Broadcast the shooting action to other clients so they can spawn projectiles locally
        pWriter.Serialize(Packet.Packet.PacketType.ACTION_SHOOT, dsData);
    }

    void HandleMSChecker(Packet.Packet pReader, Packet.Packet pWriter, EndPoint remote)
    {
        msPacket = true;
        Packet.MSCheckerDataPacket dsData = pReader.DeserializeMSCheckerDataPacket();
        string key = dsData.id.ToString();

        if (connectedClients.ContainsKey(key))
        {
            connectedClients[key].ms = dsData.ms;
        }
        pWriter.Serialize(Packet.Packet.PacketType.MSCHECKER, dsData);
    }

    void Broadcast(Packet.Packet pWriter, EndPoint remote, bool toEveryone = false)
    {
        if (msPacket)
        {
            // MS responses go only to the sender
            Send(remote, pWriter);
            pWriter.Close();
            msPacket = false;
            return;
        }

        foreach (var client in connectedClients.Values)
        {
            if (toEveryone || !client.ep.Equals(remote))
            {
                Send(client.ep, pWriter);
            }
        }
        pWriter.Close();
    }

    // Predict small movement to compensate latency (simple linear prediction)
    public static System.Numerics.Vector2 PredictPositionWithMS(System.Numerics.Vector2 pos, System.Numerics.Vector2 vel, Int16 ms)
    {
        return (pos + vel * (float)(ms / 2.0f / 1000.0f));
    }

    // Utility: get random connected client id (kept for possible use)
    public int GetRandomClientId()
    {
        if (connectedClients.Count == 0) return 0;
        System.Random random = new System.Random();
        List<UserData> clients = new List<UserData>(connectedClients.Values);
        int randomIndex = random.Next(0, clients.Count);
        return clients[randomIndex].data.id;
    }
}
