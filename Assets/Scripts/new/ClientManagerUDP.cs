using System.Net;
using System.Net.Sockets;
using UnityEngine;
using System.Threading;
using System.Collections.Generic;
using System;

public class ClientManagerUDP : MonoBehaviour
{
    // ---------------- NETWORK ----------------
    Socket socket;
    IPEndPoint serverEndPoint;

    Packet.Packet pReader;
    Packet.Packet pWriter;

    public string serverIP = "127.0.0.1";
    public int serverPort = 9050;
    public string userName = "Player";

    // ---------------- PLAYER ----------------
    public GameObject playerPrefab;
    public Transform playersParent;

    public Dictionary<int, Player> players = new Dictionary<int, Player>(); // ID Player
    private Player localPlayer;

    // ---------------- UPDATES ----------------
    public float updateRate = 0.05f;
    private float updateTimer;

    // ---------------- MS CHECK ----------------
    public short ms = 0;
    private float msSendTimer = 0f;
    private float msCalcTimer = 0f;
    private bool calculatingMS = false;

    // ---------------- THREAD ----------------
    private volatile bool running = true;
    private Queue<Action> mainThreadActions = new Queue<Action>();


    // ============================================================
    // START
    // ============================================================
    void Start()
    {
        pReader = new Packet.Packet();
        pWriter = new Packet.Packet();

        userName = ClientServerInfo.Instance.userName;
        serverIP = ClientServerInfo.Instance.serverIP;

        StartClient();
    }

    public void SetLocalPlayer(Player p)
    {
        localPlayer = p;
    }


    // ============================================================
    // MAIN THREAD QUEUE
    // ============================================================
    public void EnqueueMainThread(Action a)
    {
        lock (mainThreadActions)
            mainThreadActions.Enqueue(a);
    }

    void Update()
    {
        // Execute queued actions
        while (mainThreadActions.Count > 0)
        {
            Action a;
            lock (mainThreadActions)
                a = mainThreadActions.Dequeue();
            a?.Invoke();
        }

        if (localPlayer == null) return;

        // ================== MS CHECK ====================
        msSendTimer += Time.deltaTime;
        if (msSendTimer >= 1f)
        {
            SendMSCheck();
            msSendTimer = 0f;
        }
        if (calculatingMS) msCalcTimer += Time.deltaTime;


        // ================= UPDATE PACKETS =================
        updateTimer += Time.deltaTime;
        if (updateTimer >= updateRate)
        {
            SendUpdatePacket();
            updateTimer = 0f;
        }
    }

    // ============================================================
    // CONNECT
    // ============================================================
    public void StartClient()
    {
        serverEndPoint = new IPEndPoint(IPAddress.Parse(serverIP), serverPort);
        socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Connect(serverEndPoint);
        socket.Blocking = false;

        Debug.Log("Client UDP started.");

        // -------- SPAWN LOCAL PLAYER --------
        GameObject obj = Instantiate(playerPrefab, playersParent);
        Player p = obj.GetComponent<Player>();

        int id = GenerateRandomID();
        p.networkId = id;
        p.userName = userName;
        p.isLocalPlayer = true;

        localPlayer = p;
        players.Add(id, p);

        // -------- SEND CREATE PACKET --------
        SendCreatePacket(p);

        Thread th = new Thread(ReceiveThread);
        th.Start();
    }


    // ============================================================
    // SEND PACKETS
    // ============================================================
    public void SendUpdate(int id, Vector2 pos, float rot, Vector2 vel, bool shooting)
    {
        Packet.ShipDataPacket data = new Packet.ShipDataPacket(
            id,
            userName,
            new System.Numerics.Vector2(pos.x, pos.y),
            new System.Numerics.Vector2(vel.x, vel.y),
            rot,
            shooting ? 1 : 0
        );

        pWriter.Serialize(Packet.Packet.PacketType.UPDATE, data);
        Send();
    }

    public void SendShoot(int id, Vector2 pos, Vector2 direction)
    {
        Packet.ActionShootDataPacket data = new Packet.ActionShootDataPacket(
            id,
            new System.Numerics.Vector2(pos.x, pos.y),
            new System.Numerics.Vector2(direction.x, direction.y)
        );

        pWriter.Serialize(Packet.Packet.PacketType.ACTION_SHOOT, data);
        Send();
    }

    private void SendCreatePacket(Player p)
    {
        Packet.ShipDataPacket data = new Packet.ShipDataPacket(
            p.networkId,
            p.userName,
            new System.Numerics.Vector2(p.transform.position.x, p.transform.position.y),
            new System.Numerics.Vector2(0, 0),
            p.transform.eulerAngles.z,
            1
        );

        pWriter.Serialize(Packet.Packet.PacketType.CREATE, data);
        Send();
    }

    private void SendUpdatePacket()
    {
        if (localPlayer == null) return;

        Rigidbody2D rb = localPlayer.GetComponent<Rigidbody2D>();

        SendUpdate(
            localPlayer.networkId,
            localPlayer.transform.position,
            rb.rotation,
            rb.linearVelocity,
            false
        );
    }

    private void SendMSCheck()
    {
        if (localPlayer == null) return;

        pWriter.Serialize(Packet.Packet.PacketType.MSCHECKER,
            new Packet.MSCheckerDataPacket(localPlayer.networkId, ms));

        calculatingMS = true;
        Send();
    }

    private void Send()
    {
        pWriter.Send(ref socket, serverEndPoint);
        pWriter.Restart();
    }


    // ============================================================
    // RECEIVE THREAD
    // ============================================================
    void ReceiveThread()
    {
        byte[] buffer = new byte[1024];
        EndPoint remote = new IPEndPoint(IPAddress.Any, 0);

        while (running)
        {
            try
            {
                if (socket.Available > 0)
                {
                    int recv = socket.ReceiveFrom(buffer, ref remote);
                    pReader.Restart(buffer);
                    pReader.Start();

                    int amt = pReader.DeserializeGetGameObjectsAmount();

                    for (int i = 0; i < amt; i++)
                    {
                        HandlePacketFromServer();
                    }
                }
                else Thread.Sleep(1);
            }
            catch { }
            finally { pReader.Close(); }
        }

        Debug.Log("Receive thread stopped.");
    }

    void HandlePacketFromServer()
    {
        Packet.Packet.PacketType type = pReader.DeserializeGetType();

        switch (type)
        {
            case Packet.Packet.PacketType.CREATE:
            case Packet.Packet.PacketType.UPDATE:
                {
                    Packet.ShipDataPacket data = pReader.DeserializeShipDataPacket();
                    EnqueueMainThread(() => ApplyShipData(data));
                    break;
                }

            case Packet.Packet.PacketType.DELETE:
                {
                    Packet.DeleteDataPacket data = pReader.DeserializeDeleteDataPacket();
                    EnqueueMainThread(() => DeletePlayer(data));
                    break;
                }

            case Packet.Packet.PacketType.MSCHECKER:
                {
                    Packet.MSCheckerDataPacket data = pReader.DeserializeMSCheckerDataPacket();
                    EnqueueMainThread(() => ReceiveMSCheck(data));
                    break;
                }
        }
    }


    // ============================================================
    // HANDLE PACKETS
    // ============================================================
    private void ApplyShipData(Packet.ShipDataPacket d)
    {
        // Local player should not be moved by server
        if (localPlayer != null && d.id == localPlayer.networkId)
            return;

        if (!players.ContainsKey(d.id))
        {
            // CREATE remote player
            GameObject obj = Instantiate(playerPrefab, playersParent);
            Player p = obj.GetComponent<Player>();
            p.networkId = d.id;
            p.userName = d.name;
            p.isLocalPlayer = false;

            players.Add(d.id, p);
        }

        Player ship = players[d.id];
        ship.ApplyNetworkState(
            new Vector2(d.pos.X, d.pos.Y),
            d.rotation,
            new Vector2(d.vel.X, d.vel.Y)
        );
    }

    private void DeletePlayer(Packet.DeleteDataPacket d)
    {
        if (players.ContainsKey(d.id))
        {
            Destroy(players[d.id].gameObject);
            players.Remove(d.id);
        }
    }

    private void ReceiveMSCheck(Packet.MSCheckerDataPacket d)
    {
        calculatingMS = false;
        ms = (short)(msCalcTimer * 1000f);
        msCalcTimer = 0f;

        Debug.Log("MS: " + ms);
    }


    // ============================================================
    // UTILS
    // ============================================================
    public int GenerateRandomID()
    {
        return UnityEngine.Random.Range(10000000, 99999999);
    }

    private void OnDestroy()
    {
        running = false;
        socket?.Close();
    }
}
