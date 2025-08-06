using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Movement;
using TMPro;
using UnityEngine;

namespace Network
{
    public class GameMsgHandlerCommon : MonoBehaviour
    {
        public GameMsgHandlerClient clientMsgHandler;
        public GameMsgHandlerServer serverMsgHandler;

        public NetClient netClient;
        public NetServer netServer;

        public GameObject playerPrefab;
        public Dictionary<uint, PlayerHandler> idToPlayers;
        
        private void Start()
        {
            clientMsgHandler = GetComponent<GameMsgHandlerClient>();
            serverMsgHandler = GetComponent<GameMsgHandlerServer>();

            netClient = GetComponent<NetClient>();
            netServer = GetComponent<NetServer>();

            idToPlayers = new();
        }

        private void Update()
        {
            if (NetServer.BuiltRunningMode == NetServer.RunningMode.SocketServer)
            {
                //Debug.Log("nextClientID = " + netServer.nextClientID);
                for (uint index = 2; index < netServer.nextClientID+1; index++)
                {
                    if (!netServer.messageRecvQueue.ContainsKey(index))
                        continue;
                    HandleMessageInQueue(index, netServer.messageRecvQueue[index]);
                }
            }
            else
                HandleMessageInQueue(0, netClient.inMessageQueue);
        }

        public void HandleMessageInQueue(uint clientId, ConcurrentQueue<byte[]> msgs)
        {
            while (msgs.Count > 0)
            {
                byte[] msg = null;
                while (!msgs.TryDequeue(out msg))
                {
                    // burn the cpu here
                }
                Debug.Log("handling new message from remote...");
                switch ((PacketTypes.PacketType)msg[0])
                {
                    case PacketTypes.PacketType.SendClientIDMessage:
                    {
                        if (NetServer.BuiltRunningMode == NetServer.RunningMode.WebSocketClient)
                            clientMsgHandler.HandleClientIDMsg(msg);
                        break;
                    }
                    case PacketTypes.PacketType.AddPlayerMessage:
                    {
                        if (NetServer.BuiltRunningMode == NetServer.RunningMode.WebSocketClient)
                            clientMsgHandler.HandleAddPlayerMsg(msg);
                        break;
                    }
                    case PacketTypes.PacketType.StringMessage:
                    {
                        if (NetServer.BuiltRunningMode == NetServer.RunningMode.WebSocketClient)
                            clientMsgHandler.HandleStringMsg(msg);
                        break;
                    }
                    case PacketTypes.PacketType.PingMessage:
                    {
                        if (NetServer.BuiltRunningMode == NetServer.RunningMode.WebSocketClient)
                            clientMsgHandler.HandlePingMsg(msg);
                        else
                            serverMsgHandler.HandlePingMsg(clientId, msg);
                        break;
                    }
                    /* case PacketTypes.PacketType.SecretKeyMessage:
                    {
                        HandleSecretKeyMsg(msg);
                        break;
                    } */
                }
            }
        }

        public void AddNewPlayer(uint clientId)
        {
            var newPlayerObj = Instantiate(playerPrefab, new Vector3(0f, 3f, 0f), Quaternion.identity);
            var newPlayerCode = newPlayerObj.GetComponent<PlayerHandler>();
            newPlayerCode.localPlayer = NetClient.clientId == clientId;
            idToPlayers[clientId] = newPlayerCode;
            newPlayerCode.playerId = clientId;
        }
    }
}