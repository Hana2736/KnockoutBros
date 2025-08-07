using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Movement;
using TMPro;
using UnityEngine;
using Util;

namespace Network
{
    public class GameMsgHandlerServer : MonoBehaviour
    {
        public NetServer netServer;
        public GameMsgHandlerCommon msgHandlerCommon;
        public GameManager gameManager;
        
        
        
        public void Start()
        {
            netServer = GetComponent<NetServer>();
            if (NetServer.BuiltRunningMode != NetServer.RunningMode.Server)
                return;
            gameManager = gameObject.AddComponent<GameManager>();
            gameManager.serverHandler = this;
            msgHandlerCommon = GetComponent<GameMsgHandlerCommon>();
            
            gameManager.Reset();
        }

        public void Update()
        {
            // do nothing here if we arent the controlling server
            if (NetServer.BuiltRunningMode != NetServer.RunningMode.Server)
                return;
            
            // the server accepted a new client so we need to add them to the world
            
        }

        public void HandlePingMsg(uint clientId, byte[] msg)
        {
            netServer.SendMessage(clientId, msg);
        }

        public void HandlePlayerLoadedMsg()
        {
            gameManager.HumanPlayerIsReady();
        }
        
    }
}