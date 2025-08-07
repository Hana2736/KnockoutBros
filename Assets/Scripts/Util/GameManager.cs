using System.Collections.Generic;
using Network;
using UnityEngine;

namespace Util
{
    public class GameManager : MonoBehaviour
    {
        public HashSet<uint> alivePlayerIds;
        public HashSet<uint> qualifiedPlayerIds;

        public GameLevel currentLevel;
        public RoundType currentRoundType;

        public uint pointsNeededForPointsLevel = 20;

        public float playersQualifyPerRoundPercent = 0.33f;
        public float timeRemaining = -100f;
        public Dictionary<RoundType, float> timeLimitsPerRound;

        public Dictionary<uint, uint> playerPointsScores;

        public GameMsgHandlerServer serverHandler;

        private bool ready;

        public void Reset()
        {
            alivePlayerIds = new();
            qualifiedPlayerIds = new();
            currentLevel = GameLevel.MenuLevel;
            currentRoundType = RoundType.Free;

            timeLimitsPerRound = new();
            timeLimitsPerRound[RoundType.Free] = -100f;
            timeLimitsPerRound[RoundType.Race] = 180f;
            timeLimitsPerRound[RoundType.Survival] = 300f;
            timeLimitsPerRound[RoundType.Points] = 220f;

            playerPointsScores = new();
            ready = true;
        }
        
        
        public enum RoundType
        {
            Free,
            Race,
            Survival,
            Points
        }

        public enum GameLevel
        {
            MenuLevel,
            RaceLevel,
            SurvivalLevel,
            PointsLevel,
            FinalLevel
        }

        public void OnPlayerEliminated(uint playerId)
        {
            alivePlayerIds.Remove(playerId);
            serverHandler.netServer.SendMessage(MessagePacker.PackPlayerEliminatedMessage(playerId));
        }
        
        public void OnPlayerQualify(uint playerId)
        {
            alivePlayerIds.Remove(playerId);
            qualifiedPlayerIds.Add(playerId);
            serverHandler.netServer.SendMessage(MessagePacker.PackPlayerQualifiedMessage(playerId));
        }
        
        public void OnPlayerScore(uint increaseBy, uint playerId)
        {
            playerPointsScores.TryAdd(playerId, 0);
            playerPointsScores[playerId] += increaseBy;
            serverHandler.netServer.SendMessage(playerId,MessagePacker.PackPlayerScoreUpdateMessage(playerPointsScores[playerId]));
        }


        public void Update()
        {
            if(!ready)
                return;
            // deal with this only when we are in lobby
            if(currentRoundType != RoundType.Free)
                return;
            while (!serverHandler.netServer.newClientsForGame.IsEmpty)
            {
                // get each new player ID
                uint nextClient = 0;
                while (!serverHandler.netServer.newClientsForGame.TryDequeue(out nextClient))
                {
                    // burn the cpu here
                }

                // keep them tracked so we can handle their game state
                alivePlayerIds.Add(nextClient);
                
                // tell the game world to add the player
                var newPlayer = serverHandler.msgHandlerCommon.AddNewPlayer(nextClient);
                newPlayer.parentGameManager = this;
                
                // tell everybody about the new player
                serverHandler.netServer.SendMessage(MessagePacker.PackAddPlayerMessage(nextClient));

                // we need to tell our new client about existing players  
                foreach (var playerId in alivePlayerIds)
                {
                    // we already told them about themself so skip that
                    if(playerId == nextClient)
                        continue;
                    
                    // tell just the new clients about every other player
                    serverHandler.netServer.SendMessage(nextClient, MessagePacker.PackAddPlayerMessage(playerId));
                }
                
            }
        }
    }
}