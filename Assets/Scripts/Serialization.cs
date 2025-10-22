//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;
//using System;
//using System.Net;
//using System.Net.Sockets;
//using System.Text;
//using System.Threading;
//using System.Diagnostics;
//using System.IO;

//class Player
//{
//    public string name;
//}

//public class Serialization
//{
//    struct DTO
//    {
//        string playerpos;
//        List<Player> playernumber;
//    }

//    void serialize()
//    {
//        double myfloat = 100.1f;
//        int myint = 15;
//        string mystring = "test";
//        int[] mylist = new int[3] { 1, 2, 4 };
//        stream = new MemoryStream();
//        BinaryWriter writer = new BinaryWriter(stream);
//        writer.Write(myfloat);
//        writer.Write(myint);
//        writer.Write(mystring);
//        foreach (var i in mylist)
//        {
//            writer.Write(i);
//        }
//        Debug.Log("serialized!");
//    }

//    void deserialize()
//    {
//        BinaryReader reader = new BinaryReader(stream);
//        stream.Seek(0, SeekOrigin.Begin);
//        double newfloat = reader.ReadDouble();
//        Debug.Log("float " + newfloat.ToString());
//        int newint = reader.ReadInt32();
//        Debug.Log("int " + newint.ToString());
//        string newstring = reader.ReadString();
//        Debug.Log("string " + newstring.ToString());
//        int[] newlist = new int[3];
//        for (int i = 0; i < newlist.Length; i++)
//        {
//            newlist[i] = reader.ReadInt32();
//        }
//        Debug.Log(newlist.ToString());
//    }

//}
