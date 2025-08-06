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

        public HashSet<uint> syncedPlayerIds;
        
        public void Start()
        {
            netServer = GetComponent<NetServer>();
            syncedPlayerIds = new();
            msgHandlerCommon = GetComponent<GameMsgHandlerCommon>();
        }

        public void Update()
        {
            // do nothing here if we arent the controlling server
            if (NetServer.BuiltRunningMode != NetServer.RunningMode.Server)
                return;
            
            // the server accepted a new client so we need to add them to the world
            while (!netServer.newClientsForGame.IsEmpty)
            {
                // get each new player ID
                uint nextClient = 0;
                while (!netServer.newClientsForGame.TryDequeue(out nextClient))
                {
                    // burn the cpu here
                }

                // keep them tracked so we can handle their game state
                syncedPlayerIds.Add(nextClient);
                
                // tell the game world to add the player
                msgHandlerCommon.AddNewPlayer(nextClient);
                
                // tell everybody about the new player
                netServer.SendMessage(MessagePacker.PackAddPlayerMessage(nextClient));

                // we need to tell our new client about existing players  
                foreach (var playerId in syncedPlayerIds)
                {
                    // we already told them about themself so skip that
                    if(playerId == nextClient)
                        continue;
                    
                    // tell just the new clients about every other player
                    netServer.SendMessage(nextClient, MessagePacker.PackAddPlayerMessage(playerId));
                }
                
            }
        }

        public void HandlePingMsg(uint clientId, byte[] msg)
        {
            netServer.SendMessage(clientId, msg);
        }
        
    }
}