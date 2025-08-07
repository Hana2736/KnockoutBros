using System.Collections.Generic;
using System.Linq;
using Network;
using UnityEngine;

namespace Util
{
    public class GameManager : MonoBehaviour
    {
        public enum GameLevel : byte
        {
            MenuLevel,
            RaceLevel,
            SurvivalLevel,
            PointsLevel,
            FinalLevel
        }


        public enum RoundType : byte
        {
            Free,
            Race,
            Survival,
            Points
        }

        public static uint targetPlayerCount = 30;

        public static float playersQualifyPerRoundPercent = 0.66f;

        public GameLevel currentLevel;
        public RoundType currentRoundType;


        public uint pointsNeededForPointsLevel = 20;
        public uint playersPassExpectedThisRound = 999;


        public float timeRemaining = -100f;

        public LevelLoader levelLoader;

        public float levelTimeRemaining;
        public bool startMatching;

        public bool waitingForLoadedPlayers;

        public GameMsgHandlerServer serverHandler;
        public uint nextBotId = uint.MaxValue;
        public uint playersLoaded;
        public uint playersLoadedTarget = 999;
        public HashSet<uint> alivePlayerIds;
        public Dictionary<uint, uint> playerPointsScores;
        public HashSet<uint> qualifiedPlayerIds;
        private bool ready;
        public HashSet<uint> readyPlayers;

        private Queue<MessagePacker.NewGameLevelMessage> roundOrder;
        private bool roundShouldEnd;
        public Dictionary<RoundType, float> timeLimitsPerRound;

        public void Reset()
        {
            levelLoader = GetComponent<LevelLoader>();
            alivePlayerIds = new HashSet<uint>();
            qualifiedPlayerIds = new HashSet<uint>();
            readyPlayers = new HashSet<uint>();
            currentLevel = GameLevel.MenuLevel;
            currentRoundType = RoundType.Free;
            waitingForLoadedPlayers = false;
            playersLoaded = 0;
            playersLoadedTarget = 999;

            timeLimitsPerRound = new Dictionary<RoundType, float>();
            timeLimitsPerRound[RoundType.Free] = -100f;
            timeLimitsPerRound[RoundType.Race] = 180f;
            timeLimitsPerRound[RoundType.Survival] = 300f;
            timeLimitsPerRound[RoundType.Points] = 220f;

            roundOrder = new Queue<MessagePacker.NewGameLevelMessage>();
            roundOrder.Enqueue(new MessagePacker.NewGameLevelMessage
            {
                RoundType = RoundType.Race,
                GameLevel = GameLevel.RaceLevel
            });
            roundOrder.Enqueue(new MessagePacker.NewGameLevelMessage
            {
                RoundType = RoundType.Survival,
                GameLevel = GameLevel.SurvivalLevel
            });
            roundOrder.Enqueue(new MessagePacker.NewGameLevelMessage
            {
                RoundType = RoundType.Points,
                GameLevel = GameLevel.PointsLevel
            });
            roundOrder.Enqueue(new MessagePacker.NewGameLevelMessage
            {
                RoundType = RoundType.Survival,
                GameLevel = GameLevel.FinalLevel
            });
            roundOrder.Enqueue(new MessagePacker.NewGameLevelMessage
            {
                RoundType = RoundType.Free,
                GameLevel = GameLevel.MenuLevel
            });


            playersPassExpectedThisRound = 999;

            levelTimeRemaining = 15f;
            startMatching = false;

            roundShouldEnd = false;
            playerPointsScores = new Dictionary<uint, uint>();
            ready = true;
        }


        public void Update()
        {
            if (!ready)
                return;

            if (currentRoundType != RoundType.Free)
            {
                levelTimeRemaining -= Time.deltaTime;
                if (levelTimeRemaining <= 0f)
                {
                    Debug.Log("level time is out");
                    HandleTimeout();
                    roundShouldEnd = true;
                }
            }

            if (!roundShouldEnd)
            {
                switch (currentRoundType)
                {
                    case RoundType.Free:
                    {
                        UpdateInFreeStage();
                        break;
                    }
                    case RoundType.Race:
                    {
                        UpdateInRaceStage();
                        break;
                    }
                    case RoundType.Survival:
                    {
                        UpdateInSurvivalStage();
                        break;
                    }
                    case RoundType.Points:
                    {
                        UpdateInPointsStage();
                        break;
                    }
                }
            }
            else
            {
                roundShouldEnd = false;
                var nextLevel = roundOrder.Dequeue();
                ChangeGameType(nextLevel.RoundType, nextLevel.GameLevel);
            }
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
            serverHandler.netServer.SendMessage(playerId,
                MessagePacker.PackPlayerScoreUpdateMessage(playerPointsScores[playerId]));
        }

        public void UpdateInFreeStage()
        {
            AddNewPlayers();
            if (readyPlayers.Count > 0 && !startMatching) startMatching = true;

            if (startMatching)
            {
                levelTimeRemaining -= Time.deltaTime;
                if (levelTimeRemaining > 0f)
                    return;
                startMatching = false;
                var humanCnt = (uint)alivePlayerIds.Count;
                //get how many bots to fill based on humans
                var botCnt = targetPlayerCount - humanCnt;
                for (var ind = 0; ind < botCnt; ind++)
                {
                    //tick bot ID backwards, player ID forwards. hopefully they never meet in the middle :)
                    nextBotId--;
                    //pretend we have a new real client. hopefully the server code knows not to deal with it
                    serverHandler.netServer.newClientsForGame.Enqueue(nextBotId);
                }

                //create the bots and blow up everyone's inboxes
                AddNewPlayers();
                //start game!
                roundShouldEnd = true;
            }
        }

        public void UpdateInRaceStage()
        {
            CheckIfQualEnd();
        }

        public void UpdateInPointsStage()
        {
            foreach (var playerScores in playerPointsScores)
            {
                if (!alivePlayerIds.Contains(playerScores.Key))
                    continue;
                if (playerScores.Value >= pointsNeededForPointsLevel)
                    OnPlayerQualify(playerScores.Key);
            }

            CheckIfQualEnd();
        }

        public void HandleTimeout()
        {
            //timeout on survival = pass everyone
            if (currentRoundType == RoundType.Survival)
            {
                foreach (var alivePlayer in alivePlayerIds.ToList()) OnPlayerQualify(alivePlayer);
                return;
            }

            //timeout on race and points = kill stragglers
            foreach (var playerId in alivePlayerIds.ToList())
                // kill everyone left behind
                OnPlayerEliminated(playerId);
        }

        public void UpdateInSurvivalStage()
        {
            if (alivePlayerIds.Count == playersPassExpectedThisRound)
                foreach (var playerId in alivePlayerIds.ToList())
                    // qualify everyone left behind
                    OnPlayerQualify(playerId);
        }

        public void CheckIfQualEnd()
        {
            if (qualifiedPlayerIds.Count == playersPassExpectedThisRound)
                foreach (var playerId in alivePlayerIds.ToList())
                    // kill everyone left behind
                    OnPlayerEliminated(playerId);
        }

        public void SetupRaceRound()
        {
            playersPassExpectedThisRound = (uint)(alivePlayerIds.Count * playersQualifyPerRoundPercent);
        }

        public void SetupPointsRound()
        {
            playersPassExpectedThisRound = (uint)(alivePlayerIds.Count * playersQualifyPerRoundPercent);
            playerPointsScores = new Dictionary<uint, uint>();
        }

        public void SetupSurvivalRound()
        {
            //kill 33% instead of pass 66%
            playersPassExpectedThisRound = (uint)(alivePlayerIds.Count * (1f - playersQualifyPerRoundPercent));
        }

        public void SetupFinalRound()
        {
            //kill all but one
            playersPassExpectedThisRound = (uint)(alivePlayerIds.Count - 1);
        }

        public void HumanPlayerIsReady()
        {
            if (ready || !waitingForLoadedPlayers)
                return;
            playersLoaded++;
            if (playersLoaded < playersLoadedTarget) return;
            Debug.Log("Humans are ready, starting");
            waitingForLoadedPlayers = false;
            ready = true;

            switch (currentLevel)
            {
                case GameLevel.MenuLevel:
                {
                    Reset();
                    break;
                }
                case GameLevel.RaceLevel:
                {
                    SetupRaceRound();
                    break;
                }
                case GameLevel.SurvivalLevel:
                {
                    SetupSurvivalRound();
                    break;
                }
                case GameLevel.PointsLevel:
                {
                    SetupPointsRound();
                    break;
                }
                case GameLevel.FinalLevel:
                {
                    SetupFinalRound();
                    break;
                }
            }
        }


        public void AddNewPlayers()
        {
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

                //check if this is a bot ID
                if (nextClient > uint.MaxValue / 2) newPlayer.isBotPlayer = true;

                // tell everybody about the new player
                serverHandler.netServer.SendMessage(MessagePacker.PackAddPlayerMessage(nextClient));

                // we need to tell our new client about existing players  
                foreach (var playerId in alivePlayerIds)
                {
                    // we already told them about themself so skip that
                    if (playerId == nextClient)
                        continue;

                    // tell just the new clients about every other player
                    serverHandler.netServer.SendMessage(nextClient, MessagePacker.PackAddPlayerMessage(playerId));
                }
            }
        }

        private void ChangeGameType(RoundType newType, GameLevel newLevel)
        {
            levelTimeRemaining = timeLimitsPerRound[newType];
            currentRoundType = newType;
            currentLevel = newLevel;
            levelLoader.LoadLevel(newLevel);

            // "un-qualify" everyone who made it from last round
            foreach (var playerId in qualifiedPlayerIds)
            {
                //turn the character back on, they are disabled on qualification
                serverHandler.msgHandlerCommon.idToPlayers[playerId].gameObject.SetActive(true);
                alivePlayerIds.Add(playerId);
            }

            //reset the list
            qualifiedPlayerIds.Clear();

            ready = false;
            waitingForLoadedPlayers = true;
            playersLoadedTarget = 0;
            playersLoaded = 0;
            //hacky way to get the number of humans
            foreach (var playerId in alivePlayerIds)
                //bots start from maxvalue, humans start from 0
                if (playerId < uint.MaxValue / 2)
                    playersLoadedTarget++;

            serverHandler.netServer.SendMessage(MessagePacker.PackChangeGameSceneMsg(
                new MessagePacker.NewGameLevelMessage
                {
                    RoundType = newType,
                    GameLevel = newLevel
                }));
        }
    }
}