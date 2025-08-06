using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Movement;
using TMPro;
using UnityEngine;

namespace Network
{
    public class GameMsgHandlerServer : MonoBehaviour
    {
        public NetServer netServer;
        public GameMsgHandlerCommon msgHandlerCommon;
        
        public void Start()
        {
            netServer = GetComponent<NetServer>();
            msgHandlerCommon = GetComponent<GameMsgHandlerCommon>();
        }

        public void Update()
        {
            if(NetServer.BuiltRunningMode == NetServer.RunningMode.SocketServer)
            while (!netServer.newClientsForGame.IsEmpty)
            {
                uint nextClient = 0;
                while (!netServer.newClientsForGame.TryDequeue(out nextClient))
                {
                    // burn the cpu here
                }
                msgHandlerCommon.AddNewPlayer(nextClient);
                netServer.SendMessage(MessagePacker.PackAddPlayerMessage(nextClient));
            }
        }

        public void HandlePingMsg(uint clientId, byte[] msg)
        {
            netServer.SendMessage(clientId, msg);
        }
        
    }
}