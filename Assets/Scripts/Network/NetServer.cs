using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace Network
{
    public class NetServer : MonoBehaviour
    {
        public enum RunningMode
        {
            Server,
            Client
        }

        public static readonly RunningMode BuiltRunningMode = RunningMode.Server;

        public uint nextClientID;
        public ConcurrentDictionary<TcpClient, uint> clientToID;
        public ConcurrentDictionary<uint, TcpClient> idToClient;
        public ConcurrentDictionary<uint, ConcurrentQueue<byte[]>> messageRecvQueue;

        public ConcurrentDictionary<uint, ConcurrentQueue<byte[]>> messageSendQueue;
        public ConcurrentDictionary<uint, Guid> secretKeys;

        public ConcurrentQueue<uint> newClientsForGame;


        public void Start()
        {
            if (BuiltRunningMode != RunningMode.Server)
                return;
            StartServer();
        }


        public void StartServer()
        {
            nextClientID = 1;
            secretKeys = new ConcurrentDictionary<uint, Guid>();
            clientToID = new ConcurrentDictionary<TcpClient, uint>();
            idToClient = new ConcurrentDictionary<uint, TcpClient>();
            messageRecvQueue = new ConcurrentDictionary<uint, ConcurrentQueue<byte[]>>();
            messageSendQueue = new ConcurrentDictionary<uint, ConcurrentQueue<byte[]>>();
            newClientsForGame = new();

            var tcpListener = new TcpListener(IPAddress.Parse("10.119.200.30"), 2735);
            new Thread(() =>
            {
                tcpListener.Start();
                Debug.Log("Server started!");
                while (true)
                {
                    var newClient = tcpListener.AcceptTcpClient();
                    nextClientID++;
                    var newClientId = nextClientID;
                    new Thread(() =>
                    {
                        Debug.Log("New client! ID = " + newClientId);
                        OnNewClient(newClient, newClientId);
                    }).Start();
                }
            }).Start();
        }

        public void ProcessIncoming(TcpClient client, ConcurrentQueue<byte[]> myQueue, uint clientId)
        {
            try
            {
                var cliStream = client.GetStream();
                var readingHeader = true;
                uint headerLen = 0;
                byte headerType = 0xFF;
                var messageLengthBytes = new byte[4];
                uint messageLengthReal = 0;
                byte[] incomingMsgChunk = null;
                uint readIndex = 0;

                while (true)
                {
                    var messageByte = cliStream.ReadByte();
                    if (messageByte < 0 || messageByte > 255)
                        throw new Exception("Got invalid data. " + messageByte);

                    //Debug.Log("New byte from id" + clientId + ": " + messageByte);

                    if (readingHeader)
                    {
                        switch (headerLen)
                        {
                            case 0:
                                headerType = (byte)messageByte;
                                break;
                            case < 4:
                                messageLengthBytes[headerLen - 1] = (byte)messageByte;
                                break;
                            case 4:
                                messageLengthReal = BitConverter.ToUInt32(messageLengthBytes);
                                Debug.Log("Got full header! Type=" + (PacketTypes.PacketType)headerType + ", size of " +
                                          messageLengthReal + " bytes.");
                                incomingMsgChunk = new byte[messageLengthReal];
                                readingHeader = false;
                                readIndex = 0;
                                break;
                        }

                        headerLen++;
                    }
                    else
                    {
                        incomingMsgChunk[readIndex] = (byte)messageByte;
                        readIndex++;
                        if (readIndex < incomingMsgChunk.Length)
                            continue;
                        var fullMessage = new byte[5 + incomingMsgChunk.Length];
                        readingHeader = true;
                        headerLen = 0;
                        fullMessage[0] = headerType;
                        for (var i = 0; i < messageLengthBytes.Length; i++) fullMessage[i + 1] = messageLengthBytes[i];
                        for (var i = 0; i < incomingMsgChunk.Length; i++) fullMessage[i + 5] = incomingMsgChunk[i];
                        myQueue.Enqueue(fullMessage);
                        Debug.Log("Finished getting all bytes! queuing and resetting (total msg pak = " +
                                  BitConverter.ToString(fullMessage) + " len = " + fullMessage.Length + ")");
                    }
                }
            }
            catch (Exception error)
            {
                Debug.LogWarning("Client had a recv error! id: " + clientId + "\n" + error.Message);
            }
        }

        public void ProcessOutgoing(TcpClient client, ConcurrentQueue<byte[]> myQueue, uint clientId)
        {
            try
            {
                var cliStream = client.GetStream();
                while (true)
                {
                    byte[] nextByteArr = null;
                    while (!myQueue.TryDequeue(out nextByteArr))
                    {
                        /// burn the cpu on this thread
                    }

                    foreach (var next in nextByteArr) cliStream.WriteByte(next);
                    cliStream.Flush();
                }
            }
            catch (Exception error)
            {
                Debug.LogWarning("Client had a send error! id: " + clientId + "\n" + error.Message);
            }
        }


        public void OnNewClient(TcpClient client, uint clientId)
        {
            client.NoDelay = true;
            client.ReceiveTimeout = int.MaxValue;
            client.SendTimeout = int.MaxValue;
            secretKeys[clientId] = new Guid();
            clientToID[client] = clientId;
            idToClient[clientId] = client;
            messageRecvQueue[clientId] = new ConcurrentQueue<byte[]>();
            messageSendQueue[clientId] = new ConcurrentQueue<byte[]>();
            messageSendQueue[clientId].Enqueue(MessagePacker.PackSendClientIDMessage(clientId));
            new Thread(() => { ProcessIncoming(client, messageRecvQueue[clientId], clientId); }).Start();
            new Thread(() => { ProcessOutgoing(client, messageSendQueue[clientId], clientId); }).Start();
            newClientsForGame.Enqueue(clientId);
        }

        public void SendMessage(uint clientId, byte[] msgData)
        {
            if (!messageSendQueue.ContainsKey(clientId))
                return;
            messageSendQueue[clientId].Enqueue(msgData);
        }

        public void SendMessage(byte[] msgData)
        {
            for (uint index = 2; index < nextClientID+1; index++)
            {
                if (!messageSendQueue.ContainsKey(index))
                    continue;
                messageSendQueue[index].Enqueue(msgData);
            }
        }

        public void SendMessageToAllBut(uint clientId, byte[] msgData)
        {
            for (uint index = 2; index < nextClientID+1; index++)
            {
                if (clientId == index || !messageSendQueue.ContainsKey(index))
                    continue;
                messageSendQueue[index].Enqueue(msgData);
            }
        }
    }
}