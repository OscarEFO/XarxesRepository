// Packet.cs
using System;
using System.IO;
using System.Net;
using System.Numerics;

namespace Packet
{
    public class ShipDataPacket
    {
        public int id;
        public string name;
        public Vector2 pos;
        public Vector2 vel;
        public float rotation;
        public int lives;

        public ShipDataPacket(int id, string name, Vector2 pos, Vector2 vel, float rotation, int lives)
        {
            this.id = id;
            this.name = name;
            this.pos = pos;
            this.vel = vel;
            this.rotation = rotation;
            this.lives = lives;
        }
    }

    public class ActionShootDataPacket
    {
        public int id;
        public Vector2 origin;
        public Vector2 direction;

        public ActionShootDataPacket(int id, Vector2 origin, Vector2 direction)
        {
            this.id = id;
            this.origin = origin;
            this.direction = direction;
        }
    }

    public class DeleteDataPacket
    {
        public int id;
        public DeleteDataPacket(int id) { this.id = id; }
    }

    public class TextDataPacket
    {
        public int id;
        public string name;
        public string text;
        public TextDataPacket(int id, string name, string text) { this.id = id; this.name = name; this.text = text; }
    }

    public class MSCheckerDataPacket
    {
        public int id;
        public Int16 ms;
        public MSCheckerDataPacket(int id, Int16 ms) { this.id = id; this.ms = ms; }
    }

    public class Packet
    {
        public enum PacketType : Int16
        {
            CREATE = 0,
            UPDATE = 1,
            ACTION_SHOOT = 2,
            DELETE = 3,
            TEXT = 4,
            MSCHECKER = 5
        }

        private MemoryStream ms;
        private BinaryReader reader;
        private BinaryWriter writer;
        private Int16 goNumber = 0;

        public Packet()
        {
            this.ms = new MemoryStream();
            this.writer = new BinaryWriter(ms);
            this.goNumber = 0;
        }

        public Packet(byte[] data)
        {
            this.ms = new MemoryStream(data);
            this.reader = new BinaryReader(ms);
            this.goNumber = 0;
        }

        public void Start()
        {
            ms.Seek(0, SeekOrigin.Begin);
        }

        public void Init()
        {
            this.ms = new MemoryStream();
            this.writer = new BinaryWriter(ms);
            this.goNumber = 0;
        }

        public void Init(byte[] data)
        {
            this.ms = new MemoryStream(data);
            this.reader = new BinaryReader(ms);
            this.goNumber = 0;
        }

        public void Close()
        {
            reader?.Close();
            writer?.Close();
            ms?.Close();
            goNumber = 0;
        }

        public void Restart()
        {
            Close();
            Init();
        }

        public void Restart(byte[] data)
        {
            Close();
            Init(data);
        }

        // DESERIALIZING
        public int DeserializeGetGameObjectsAmount()
        {
            return (int)reader.ReadInt16();
        }

        public PacketType DeserializeGetType()
        {
            return (PacketType)reader.ReadInt16();
        }

        public ShipDataPacket DeserializeShipDataPacket()
        {
            int id = reader.ReadInt32();
            string name = reader.ReadString();
            Vector2 pos = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            Vector2 vel = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            float rotation = reader.ReadSingle();
            int lives = reader.ReadInt32();

            return new ShipDataPacket(id, name, pos, vel, rotation, lives);
        }

        public ActionShootDataPacket DeserializeActionShootDataPacket()
        {
            int id = reader.ReadInt32();
            Vector2 origin = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            Vector2 direction = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            return new ActionShootDataPacket(id, origin, direction);
        }

        public DeleteDataPacket DeserializeDeleteDataPacket()
        {
            int id = reader.ReadInt32();
            return new DeleteDataPacket(id);
        }

        public TextDataPacket DeserializeTextDataPacket()
        {
            int id = reader.ReadInt32();
            string name = reader.ReadString();
            string text = reader.ReadString();
            return new TextDataPacket(id, name, text);
        }

        public MSCheckerDataPacket DeserializeMSCheckerDataPacket()
        {
            int id = reader.ReadInt32();
            Int16 ms = reader.ReadInt16();
            return new MSCheckerDataPacket(id, ms);
        }

        // SERIALIZING
        public void Serialize(PacketType type, ShipDataPacket data)
        {
            if (ms.Position == 0) writer.Write((Int16)goNumber);
            writer.Write((Int16)type);

            writer.Write(data.id);
            writer.Write(data.name);
            writer.Write(data.pos.X);
            writer.Write(data.pos.Y);
            writer.Write(data.vel.X);
            writer.Write(data.vel.Y);
            writer.Write(data.rotation);
            writer.Write(data.lives);

            this.goNumber++;
        }

        public void Serialize(PacketType type, ActionShootDataPacket data)
        {
            if (ms.Position == 0) writer.Write((Int16)goNumber);
            writer.Write((Int16)type);

            writer.Write(data.id);
            writer.Write(data.origin.X);
            writer.Write(data.origin.Y);
            writer.Write(data.direction.X);
            writer.Write(data.direction.Y);

            this.goNumber++;
        }

        public void Serialize(PacketType type, DeleteDataPacket data)
        {
            if (ms.Position == 0) writer.Write((Int16)goNumber);
            writer.Write((Int16)type);

            writer.Write(data.id);

            this.goNumber++;
        }

        public void Serialize(PacketType type, TextDataPacket data)
        {
            if (ms.Position == 0) writer.Write((Int16)goNumber);
            writer.Write((Int16)type);

            writer.Write(data.id);
            writer.Write(data.name);
            writer.Write(data.text);

            this.goNumber++;
        }

        public void Serialize(PacketType type, MSCheckerDataPacket data)
        {
            if (ms.Position == 0) writer.Write((Int16)goNumber);
            writer.Write((Int16)type);

            writer.Write(data.id);
            writer.Write((Int16)data.ms);

            this.goNumber++;
        }

        // SEND
        // Writes the total goNumber at the beginning and sends the buffer
        public void Send(ref System.Net.Sockets.Socket socket, System.Net.IPEndPoint ipep)
        {
            Start();
            writer.Write((Int16)this.goNumber);
            socket.SendTo(this.ms.GetBuffer(), ipep);
        }

        // GETTERS
        public MemoryStream GetMemoryStream() { return this.ms; }
        public BinaryReader GetBinaryReader() { return this.reader; }
        public BinaryWriter GetBinaryWriter() { return this.writer; }
        public int GetGameObjectsAmount() { return (int)this.goNumber; }
    }
}
