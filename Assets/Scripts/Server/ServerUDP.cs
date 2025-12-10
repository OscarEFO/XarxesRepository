using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

public class ServerUDP : MonoBehaviour
{
    public int port = 9050;
    public float snapshotRate = 0.05f; // 20Hz

    private Socket socket;
    private Thread thread;
    private volatile bool running = false;

    private class PlayerState
    {
        public int id;
        public string name;
        public Vector2 pos;
        public Vector2 vel;
        public float rot;
        public IPEndPoint ep;
    }

    private readonly Dictionary<int, PlayerState> players = new Dictionary<int, PlayerState>();
    private readonly Dictionary<string, IPEndPoint> endpoints = new Dictionary<string, IPEndPoint>();

    void Start()
    {
        socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(new IPEndPoint(IPAddress.Any, port));
        socket.Blocking = false;

        running = true;
        thread = new Thread(ReceiveLoop);
        thread.Start();

        InvokeRepeating(nameof(BroadcastSnapshot), snapshotRate, snapshotRate);

        // ASTEROID SPAWNING
        InvokeRepeating(nameof(ServerSpawnAsteroid), 2f, 3f);

        Debug.Log($"[SERVER] Running on UDP:{port}");
    }

    void OnApplicationQuit()
    {
        running = false;
        socket?.Close();
    }

    private void ReceiveLoop()
    {
        byte[] buffer = new byte[1024];
        EndPoint remote = new IPEndPoint(IPAddress.Any, port);

        while (running)
        {
            try
            {
                int len = socket.ReceiveFrom(buffer, ref remote);
                IPEndPoint ep = (IPEndPoint)remote;

                string key = ep.ToString();
                if (!endpoints.ContainsKey(key))
                    endpoints[key] = ep;

                ParsePacket(buffer, len, ep);
            }
            catch (SocketException)
            {
                Thread.Sleep(1);
            }
            catch (Exception e)
            {
                Debug.LogError("[SERVER ERROR] " + e);
            }
        }
    }

    private void ParsePacket(byte[] buf, int len, IPEndPoint ep)
    {
        int o = 0;
        byte type = buf[o++];

        switch (type)
        {
            case 0: HandleCreate(buf, ref o, ep); break;
            case 1: HandleUpdate(buf, ref o, ep); break;
            case 2: HandleShoot(buf, ref o, ep); break;
            case 4: HandleDelete(buf, ref o, ep); break;
                    
        }
    }

    private void HandleCreate(byte[] buf, ref int o, IPEndPoint ep)
    {
        int id = BitConverter.ToInt32(buf, o); o += 4;
        string name = ReadString(buf, ref o);
        float x = BitConverter.ToSingle(buf, o); o += 4;
        float y = BitConverter.ToSingle(buf, o); o += 4;

        players[id] = new PlayerState
        {
            id = id,
            name = name,
            pos = new Vector2(x, y),
            vel = Vector2.zero,
            rot = 0,
            ep = ep
        };

        Debug.Log($"[SERVER] CREATE {name} ({id})");

        BroadcastCreate(players[id]);
    }
    private void HandleDelete(byte[] buf, ref int o, IPEndPoint ep)
    {
        int id = BitConverter.ToInt32(buf, o); o += 4;

        if (players.ContainsKey(id))
        {
            players.Remove(id);
            Debug.Log($"[SERVER] DELETE {id}");
        }

        BroadcastDelete(id);
    }
    private void BroadcastDelete(int id)
    {
        byte[] data = BuildDeletePacket(id);

        foreach (var ep in endpoints.Values)
            socket.SendTo(data, ep);
    }
    private byte[] BuildDeletePacket(int id)
    {
        var ms = new System.IO.MemoryStream();
        var w = new System.IO.BinaryWriter(ms);

        w.Write((byte)4); // DELETE
        w.Write(id);

        return ms.ToArray();
    }
    


    private void HandleUpdate(byte[] buf, ref int o, IPEndPoint ep)
    {
        int id = BitConverter.ToInt32(buf, o); o += 4;
        if (!players.ContainsKey(id)) return;

        var p = players[id];
        p.pos.x = BitConverter.ToSingle(buf, o); o += 4;
        p.pos.y = BitConverter.ToSingle(buf, o); o += 4;
        p.vel.x = BitConverter.ToSingle(buf, o); o += 4;
        p.vel.y = BitConverter.ToSingle(buf, o); o += 4;
        p.rot = BitConverter.ToSingle(buf, o); o += 4;
    }

    private void HandleShoot(byte[] buf, ref int o, IPEndPoint ep)
    {
        int id = BitConverter.ToInt32(buf, o); o += 4;
        float ox = BitConverter.ToSingle(buf, o); o += 4;
        float oy = BitConverter.ToSingle(buf, o); o += 4;
        float dx = BitConverter.ToSingle(buf, o); o += 4;
        float dy = BitConverter.ToSingle(buf, o); o += 4;

        BroadcastShoot(id, ox, oy, dx, dy);
    }


    private void BroadcastSnapshot()
    {
        foreach (var kv in players)
            BroadcastCreate(kv.Value);
    }

    private void BroadcastCreate(PlayerState p)
    {
        foreach (var ep in endpoints.Values)
            socket.SendTo(BuildCreatePacket(p), ep);
    }

    private byte[] BuildCreatePacket(PlayerState p)
    {
        var ms = new System.IO.MemoryStream();
        var w = new System.IO.BinaryWriter(ms);

        w.Write((byte)0);
        w.Write(p.id);
        WriteString(w, p.name);
        w.Write(p.pos.x);
        w.Write(p.pos.y);
        w.Write(p.vel.x);
        w.Write(p.vel.y);
        w.Write(p.rot);

        return ms.ToArray();
    }

    private void BroadcastShoot(int id, float ox, float oy, float dx, float dy)
    {
        foreach (var ep in endpoints.Values)
            socket.SendTo(BuildShootPacket(id, ox, oy, dx, dy), ep);
    }

    private byte[] BuildShootPacket(int id, float ox, float oy, float dx, float dy)
    {
        var ms = new System.IO.MemoryStream();
        var w = new System.IO.BinaryWriter(ms);

        w.Write((byte)2);
        w.Write(id);
        w.Write(ox);
        w.Write(oy);
        w.Write(dx);
        w.Write(dy);

        return ms.ToArray();
    }


    private void ServerSpawnAsteroid()
    {
        float x = UnityEngine.Random.Range(-10f, 10f);
        float y = 6f;
        float dirX = UnityEngine.Random.Range(-0.5f, 0.5f);
        float dirY = -1f;
        float speed = UnityEngine.Random.Range(2f, 5f);

        BroadcastAsteroid(x, y, dirX, dirY, speed);
    }

    private void BroadcastAsteroid(float x, float y, float dirX, float dirY, float speed)
    {
        foreach (var ep in endpoints.Values)
            socket.SendTo(BuildAsteroidPacket(x, y, dirX, dirY, speed), ep);
    }

    private byte[] BuildAsteroidPacket(float x, float y, float dirX, float dirY, float speed)
    {
        var ms = new System.IO.MemoryStream();
        var w = new System.IO.BinaryWriter(ms);

        w.Write((byte)3);
        w.Write(x);
        w.Write(y);
        w.Write(dirX);
        w.Write(dirY);
        w.Write(speed);

        return ms.ToArray();
    }


    private void WriteString(System.IO.BinaryWriter w, string s)
    {
        w.Write((short)s.Length);
        w.Write(System.Text.Encoding.UTF8.GetBytes(s));
    }

    private string ReadString(byte[] buf, ref int o)
    {
        short len = BitConverter.ToInt16(buf, o); o += 2;
        string s = System.Text.Encoding.UTF8.GetString(buf, o, len);
        o += len;
        return s;
    }
}
