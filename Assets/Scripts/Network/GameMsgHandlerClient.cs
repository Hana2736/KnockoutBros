using System;
using TMPro;
using UnityEngine;
using Util;

namespace Network
{
    public class GameMsgHandlerClient : MonoBehaviour
    {
        private PingSender pingSender;
        public GameMsgHandlerCommon msgHandlerCommon;
        public LevelLoader LevelLoader;

        private void Start()
        {
            pingSender = GetComponent<PingSender>();
            msgHandlerCommon = GetComponent<GameMsgHandlerCommon>();
            LevelLoader = GetComponent<LevelLoader>();
        }


        public void HandleStringMsg(byte[] msg)
        {
            // Hello Msg
            if (msg.Length != 5)
            {
                var text = GameObject.Find("TestText").GetComponent<TMP_Text>();
                text.text += '\n' + MessagePacker.UnpackStringMsg(msg);
                return;
            }

            Console.WriteLine("key is recovered!");
            NetClient.keyRecovered = true;
        }

        public void HandlePingMsg(byte[] msg)
        {
            if (NetServer.BuiltRunningMode != NetServer.RunningMode.Client)
                return;
            // Ping Msg
            var pingID = MessagePacker.UnpackPingMsg(msg);
            pingSender.UpdatePing(pingID);
        }

        /*private void HandleSecretKeyMsg(byte[] msg)
        {
            // Secret Key Msg
            var newKey = MessagePacker.UnpackSecretKeyMsg(msg);
            Debug.Log("Secret Key Msg: " + newKey);
            if (NetReconnector.secretKey == Guid.Empty)
            {
                NetReconnector.secretKey = newKey;
                NetClient.keyRecovered = true;
                return;
            }

            NetReconnector.HandleReconnect();
        }*/

        public void HandleClientIDMsg(byte[] msg)
        {
            NetClient.clientId = MessagePacker.UnpackSendClientIDMessage(msg);
        }
        
        public void HandleAddPlayerMsg(byte[] msg)
        {
            var playerId = MessagePacker.UnpackAddPlayerMessage(msg);
            msgHandlerCommon.AddNewPlayer(playerId);
        }

        public void HandleQualifiedPlayerMsg(byte[] msg)
        {
            var playerId = MessagePacker.UnpackPlayerQualifiedMessage(msg);
            Debug.Log("Qualified: "+playerId);
        }
        
        public void HandleEliminatedPlayerMsg(byte[] msg)
        {
            var playerId = MessagePacker.UnpackPlayerEliminatedMessage(msg);
            Debug.Log("Eliminated: "+playerId);
        }

        public void HandleLevelChangeMsg(byte[] msg)
        {
            var nextConfig = MessagePacker.UnpackChangeGameSceneMsg(msg);
            LevelLoader.LoadLevel(nextConfig.GameLevel);
            Debug.Log("Sending player loaded message-stage: game conf recieve");
            NetClient.SendMsg(MessagePacker.PackPlayerLoadedMessage());
        }
    }
}