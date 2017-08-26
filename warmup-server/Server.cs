using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using LiteNetLib;
using LiteNetLib.Utils;
using LitJson;

namespace warmup_server
{
    public class Server
    {
        public delegate void OnEventRecieved(NetPeer peer , JsonData data);

        public delegate void OnPeerConnected(NetPeer peer);

        public delegate void OnPeerDisconnected(NetPeer peer);
        
        private Dictionary<string, OnEventRecieved> handlers;
        private OnPeerConnected onPeerConnected;
        private OnPeerDisconnected onPeerDisconnected;
        private bool running = true;
        private NetManager server;
        private EventBasedNetListener listener;
        private Thread pollEventThread;
        private NetDataWriter dataWriter;
        private int pollDifference;
        
        public Server( string key , int port , int pollDifference , int maxClient , int disconnectTimeOut)
        {
            handlers = new Dictionary<string, OnEventRecieved>();
            dataWriter = new NetDataWriter();
            listener = new EventBasedNetListener();
            server = new NetManager(listener , maxClient , key);
            server.Start(port);
            server.DisconnectTimeout = disconnectTimeOut;
            this.pollDifference = pollDifference;
            
            listener.PeerConnectedEvent += peer =>
            {
                Console.WriteLine("peer connectd ===>", peer.EndPoint);
                onPeerConnected?.Invoke(peer);
            };

            listener.NetworkReceiveEvent += (peer, dataReader) =>
            {
                string x = dataReader.GetString();
                Console.WriteLine(x);
                JsonData jsonData = JsonMapper.ToObject(x);
                if (handlers.ContainsKey(jsonData[0].ToString()))
                {
                    if (jsonData.Count > 1)
                        handlers[jsonData[0].ToString()](peer, jsonData[1]);
                    else
                        handlers[jsonData[0].ToString()](peer, null);
                }
            };

            listener.PeerDisconnectedEvent += (peer, disconnectInfo) =>
            {
                Console.WriteLine("peer disconnectd ===>", peer.EndPoint); // Show peer ip
                onPeerDisconnected?.Invoke(peer);
            };
            
            pollEventThread = new Thread(pollEvent);
            pollEventThread.Start();

        }

        private void pollEvent()
        {
            while (running)
            {
                server.PollEvents();
                Thread.Sleep(pollDifference);
            }
        }

        public void Emit(NetPeer peer , string data)
        {
            dataWriter.Reset();
            dataWriter.Put(data);
            peer.Send(dataWriter, SendOptions.ReliableOrdered);
        }

        public void On(string eventNum, OnEventRecieved onEventRecieved)
        {
            handlers.Add(eventNum,onEventRecieved); //todo dual or multiple event assignment
        }

        public void OnConnected(OnPeerConnected onPeerConnected)
        {
            this.onPeerConnected = onPeerConnected;
        }

        public void OnDisconnected(OnPeerDisconnected onPeerDisconnected)
        {
            this.onPeerDisconnected = onPeerDisconnected;
        }
        
    }
}