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

        private readonly float tickRate = 10f;
        private float timeUntilNextTick = 1f;
        private void Start()
        {
            clientMsgHandler = GetComponent<GameMsgHandlerClient>();
            serverMsgHandler = GetComponent<GameMsgHandlerServer>();

            netClient = GetComponent<NetClient>();
            netServer = GetComponent<NetServer>();

            idToPlayers = new();
            
            timeUntilNextTick = 1f / tickRate;
        }

        private void Update()
        {
            timeUntilNextTick -= Time.deltaTime;
            //Debug.Log("time until next tick = "+timeUntilNextTick);
            if (timeUntilNextTick <= 0)
            {
                if (NetServer.BuiltRunningMode == NetServer.RunningMode.Client && NetClient.isReadyForTicking)
                {
                    NetClient.SendMsg(CreatePlayerUpdateMessage(NetClient.clientId));
                }
                else
                {
                    foreach (var player in serverMsgHandler.syncedPlayerIds)
                    {
                        netServer.SendMessageToAllBut(player, CreatePlayerUpdateMessage(player));
                    }
                }
                timeUntilNextTick = 1f / tickRate;
            }
            if (NetServer.BuiltRunningMode == NetServer.RunningMode.Server)
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
                        if (NetServer.BuiltRunningMode == NetServer.RunningMode.Client)
                            clientMsgHandler.HandleClientIDMsg(msg);
                        break;
                    }
                    case PacketTypes.PacketType.AddPlayerMessage:
                    {
                        if (NetServer.BuiltRunningMode == NetServer.RunningMode.Client)
                            clientMsgHandler.HandleAddPlayerMsg(msg);
                        break;
                    }
                    case PacketTypes.PacketType.StringMessage:
                    {
                        if (NetServer.BuiltRunningMode == NetServer.RunningMode.Client)
                            clientMsgHandler.HandleStringMsg(msg);
                        break;
                    }
                    case PacketTypes.PacketType.PingMessage:
                    {
                        if (NetServer.BuiltRunningMode == NetServer.RunningMode.Client)
                            clientMsgHandler.HandlePingMsg(msg);
                        else
                            serverMsgHandler.HandlePingMsg(clientId, msg);
                        break;
                    }
                    case PacketTypes.PacketType.PlayerUpdateMessage:
                    {
                        HandlePlayerUpdateMessage(
                            NetServer.BuiltRunningMode == NetServer.RunningMode.Client ? 0 : clientId,
                            msg);
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

            if (newPlayerCode.localPlayer)
                NetClient.isReadyForTicking = true;
        }

        public void HandlePlayerUpdateMessage(uint clientId, byte[] msg)
        {
            var playerUpdData = MessagePacker.UnpackPlayerUpdateMsg(msg);
            if (clientId == 0)
                clientId = playerUpdData.playerID;
            try
            {
                var playerHandler = idToPlayers[clientId];

                // create vectors from the streamed data
                Vector3 newPos = new Vector3(playerUpdData.positionX, playerUpdData.positionY, playerUpdData.positionZ);
                Quaternion newRot = Quaternion.Euler(playerUpdData.rotationX, playerUpdData.rotationY, playerUpdData.rotationZ);
                Vector3 newVel = new Vector3(playerUpdData.velocityX, playerUpdData.velocityY, playerUpdData.velocityZ);

                // apply it to the player TODO lerp
                playerHandler.transform.position = newPos;
                playerHandler.transform.rotation = newRot;
                playerHandler.myRb.linearVelocity = newVel;
                
                // apply input so they will move accurately
                playerHandler.inputX = playerUpdData.inputX;
                playerHandler.inputZ = playerUpdData.inputZ;
            }
            catch (Exception e)
            {
                //oh well, they sent us garbage
            }
        }

        public byte[] CreatePlayerUpdateMessage(uint clientId)
        {
            Debug.Log("Trying to send an update for "+clientId);
            var playerCode = idToPlayers[clientId];
            Vector3 position = playerCode.transform.position;
            Vector3 rotation = playerCode.transform.eulerAngles;
            Vector3 velocity = playerCode.myRb.linearVelocity;
            var updTemplate = new MessagePacker.PlayerUpdateMessage
            {
                playerID = clientId,
                positionX = position.x,
                positionY = position.y,
                positionZ = position.z,
                rotationX = rotation.x,
                rotationY = rotation.y,
                rotationZ = rotation.z,
                velocityX = velocity.x,
                velocityY = velocity.y,
                velocityZ = velocity.z,
                inputX = playerCode.inputX,
                inputZ = playerCode.inputZ
            };


            return MessagePacker.PackPlayerUpdateMsg(updTemplate);
        }
    }
}