using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Movement;
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

        public readonly float tickRate = 15f;
        public Dictionary<uint, PlayerHandler> idToPlayers;
        private float timeUntilNextTick = 1f;

        private void Start()
        {
            clientMsgHandler = GetComponent<GameMsgHandlerClient>();
            serverMsgHandler = GetComponent<GameMsgHandlerServer>();

            netClient = GetComponent<NetClient>();
            netServer = GetComponent<NetServer>();

            idToPlayers = new Dictionary<uint, PlayerHandler>();

            timeUntilNextTick = 1f / tickRate;
        }

        private void Update()
        {
            timeUntilNextTick -= Time.deltaTime;
            if (timeUntilNextTick <= 0)
            {
                switch (NetServer.BuiltRunningMode)
                {
                    case NetServer.RunningMode.Client when NetClient.isReadyForTicking:
                        NetClient.SendMsg(CreatePlayerUpdateMessage(NetClient.clientId));
                        break;
                    case NetServer.RunningMode.Server:
                    {
                        if (!serverMsgHandler.gameManager.ready)
                            break;

                        foreach (var player in serverMsgHandler.gameManager.alivePlayerIds)
                            netServer.SendMessageToAllBut(player, CreatePlayerUpdateMessage(player));

                        break;
                    }
                }

                timeUntilNextTick = 1f / tickRate;
            }

            if (NetServer.BuiltRunningMode == NetServer.RunningMode.Server)
                //Debug.Log("nextClientID = " + netServer.nextClientID);
                for (uint index = 2; index < netServer.nextClientID + 1; index++)
                {
                    if (!netServer.messageRecvQueue.ContainsKey(index))
                        continue;
                    HandleMessageInQueue(index, netServer.messageRecvQueue[index]);
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

                //Debug.Log("handling new message from remote...");
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
                    case PacketTypes.PacketType.PlayerLoadedMessage:
                    {
                        if (NetServer.BuiltRunningMode == NetServer.RunningMode.Server)
                            serverMsgHandler.HandlePlayerLoadedMsg();
                        break;
                    }
                    case PacketTypes.PacketType.ChangeGameScene:
                    {
                        if (NetServer.BuiltRunningMode == NetServer.RunningMode.Client)
                            clientMsgHandler.HandleLevelChangeMsg(msg);
                        break;
                    }
                    case PacketTypes.PacketType.PlayerQualifiedMessage:
                    {
                        if (NetServer.BuiltRunningMode == NetServer.RunningMode.Client)
                            clientMsgHandler.HandleQualifiedPlayerMsg(msg);
                        break;
                    }
                    case PacketTypes.PacketType.PlayerEliminatedMessage:
                    {
                        if (NetServer.BuiltRunningMode == NetServer.RunningMode.Client)
                            clientMsgHandler.HandleEliminatedPlayerMsg(msg);
                        break;
                    }
                    case PacketTypes.PacketType.SpawnCeilSpike:
                    {
                        if (NetServer.BuiltRunningMode == NetServer.RunningMode.Client)
                            clientMsgHandler.HandleRainSpikeMsg(msg);

                        break;
                    }
                    case PacketTypes.PacketType.SpawnWaterWave:
                    {
                        if (NetServer.BuiltRunningMode == NetServer.RunningMode.Client)
                            clientMsgHandler.HandleSpawnWaveMsg(msg);
                        break;
                    }
                    case PacketTypes.PacketType.SpawnBubble:
                    {
                        if (NetServer.BuiltRunningMode == NetServer.RunningMode.Client)
                            clientMsgHandler.HandleAddBubbleMsg(msg);
                        break;
                    }
                    case PacketTypes.PacketType.RemoveBubble:
                    {
                        if (NetServer.BuiltRunningMode == NetServer.RunningMode.Client)
                            clientMsgHandler.HandleRemoveBubbleMsg(msg);
                        break;
                    }
                    case PacketTypes.PacketType.PlayerScoreUpdate:
                    {
                        if (NetServer.BuiltRunningMode == NetServer.RunningMode.Client)
                            clientMsgHandler.HandleScoreUpdate(msg);
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

        public PlayerHandler AddNewPlayer(uint clientId)
        {
            var newPlayerObj = Instantiate(playerPrefab, new Vector3(0f, 999999f, 0f), Quaternion.identity);
            var newPlayerCode = newPlayerObj.GetComponent<PlayerHandler>();
            newPlayerCode.skipTick = PlayerHandler.SkipTickReason.Loading;
            newPlayerCode.localPlayer = NetClient.clientId == clientId;
            idToPlayers[clientId] = newPlayerCode;
            newPlayerCode.playerId = clientId;

            if (newPlayerCode.localPlayer)
                NetClient.isReadyForTicking = true;
            return newPlayerCode;
        }


        public void HandlePlayerUpdateMessage(uint clientId, byte[] msg)
        {
            var playerUpdData = MessagePacker.UnpackPlayerUpdateMsg(msg);
            if (clientId == 0)
            {
                // This is the client code path.
                clientId = playerUpdData.playerID;
            }
            else
            {
                // This is the server code path.
                // CRITICAL FIX: We must first check if the player exists in the dictionary
                // before we try to do anything with it.
                if (!idToPlayers.ContainsKey(clientId))
                    // The message is from a player that was just destroyed during a level transition.
                    // We can safely ignore it.
                    return;

                // Now that we know the player exists, we can safely check if they are in the 'alive' list.
                if (!idToPlayers[clientId].parentGameManager.alivePlayerIds.Contains(clientId))
                    return;
            }

            try
            {
                // Since we've passed the safety checks, this lookup is now safe.
                var playerHandler = idToPlayers[clientId];

                // create vectors from the streamed data
                var newPos = new Vector3(playerUpdData.positionX, playerUpdData.positionY, playerUpdData.positionZ);
                var newRot = Quaternion.Euler(playerUpdData.rotationX, playerUpdData.rotationY,
                    playerUpdData.rotationZ);

                if (clientId != NetClient.clientId)
                {
                    playerHandler.ReceiveNetworkUpdate(newPos, newRot);
                }
                else
                {
                    playerHandler.transform.position = newPos;
                    playerHandler.transform.rotation = newRot;
                }

                playerHandler.currentAnim = playerUpdData.animId;
                playerHandler.skipTick = (PlayerHandler.SkipTickReason)playerUpdData.skipTickReason;
            }
            catch (Exception e)
            {
                // This will now catch other potential issues, but not the KeyNotFoundException.
                //Debug.LogWarning("Failed to unpack player update: "+e.Message);
            }
        }

        public byte[] CreatePlayerUpdateMessage(uint clientId)
        {
            try
            {
                if (!idToPlayers.TryGetValue(clientId, out var playerCode))
                    return new byte[] { (byte)PacketTypes.PacketType.InvalidPacket, 0x00, 0x00, 0x00, 0x00 };

                if (!playerCode.readyForUpdates)
                    return new byte[] { (byte)PacketTypes.PacketType.InvalidPacket, 0x00, 0x00, 0x00, 0x00 };

                var position = playerCode.transform.position;
                var rotation = playerCode.transform.eulerAngles;
                var updTemplate = new MessagePacker.PlayerUpdateMessage
                {
                    playerID = clientId,
                    positionX = position.x,
                    positionY = position.y,
                    positionZ = position.z,
                    rotationX = rotation.x,
                    rotationY = rotation.y,
                    rotationZ = rotation.z,
                    animId = playerCode.currentAnim,
                    skipTickReason = (byte)playerCode.skipTick // Add this line
                };


                return MessagePacker.PackPlayerUpdateMsg(updTemplate);
            }
            catch (Exception e)
            {
                ///how did we get here
                throw e;
                return new byte[] { (byte)PacketTypes.PacketType.InvalidPacket, 0x00, 0x00, 0x00, 0x00 };
            }
        }
    }
}