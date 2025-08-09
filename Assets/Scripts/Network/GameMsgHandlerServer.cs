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

        public Dictionary<uint, float> timeOfLastPing;
        
        public void Start()
        {
            netServer = GetComponent<NetServer>();
            timeOfLastPing = new();
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
            
            
            
        }

        public void HandlePingMsg(uint clientId, byte[] msg)
        {
            timeOfLastPing[clientId] = Time.time;
            netServer.SendMessage(clientId, msg);
        }

        public void HandlePlayerLoadedMsg()
        {
            gameManager.HumanPlayerIsReady();
        }
        
    }
}