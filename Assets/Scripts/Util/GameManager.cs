using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Movement;
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
            FinalLevel,
            NullLevel
        }


        public enum RoundType : byte
        {
            Free,
            Race,
            Survival,
            Points
        }

        public static uint targetPlayerCount = 30;

        public static float playersQualifyPerRoundPercent = 0.60f;

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
        public bool ready;
        public HashSet<uint> readyPlayers;

        private Queue<MessagePacker.NewGameLevelMessage> roundOrder;
        private bool roundShouldEnd;



        private float sendUpdatesTimer;
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
                RoundType = RoundType.Free,
                GameLevel = GameLevel.MenuLevel
            });
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


            // playersPassExpectedThisRound = 999; // REMOVE this line from here

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
                if (roundOrder.Count == 0)
                {
                    Reset();
                    ///re-add all recently pinged players
                    foreach (var playerTime in serverHandler.timeOfLastPing)
                    {
                        if (Time.time - playerTime.Value < 10f)
                        {
                            serverHandler.netServer.newClientsForGame.Enqueue(playerTime.Key);
                        }
                    }
                }
                    
                var nextLevel = roundOrder.Dequeue();
                ChangeGameType(nextLevel.RoundType, nextLevel.GameLevel);
            }
        }

        public void OnPlayerEliminated(uint playerId)
        {
            alivePlayerIds.Remove(playerId);
            serverHandler.msgHandlerCommon.idToPlayers[playerId].gameObject.SetActive(false);
            serverHandler.netServer.SendMessage(MessagePacker.PackPlayerEliminatedMessage(playerId));
        }

        public void OnPlayerQualify(uint playerId)
        {
            alivePlayerIds.Remove(playerId);
            qualifiedPlayerIds.Add(playerId);
            serverHandler.msgHandlerCommon.idToPlayers[playerId].gameObject.SetActive(false);
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
            //Debug.Log("Alive: " + alivePlayerIds.Count+", Wanted Remaining: "+playersPassExpectedThisRound);
            if (alivePlayerIds.Count > playersPassExpectedThisRound)
                return;
            foreach (var playerId in alivePlayerIds.ToList())
                // qualify everyone left behind
                OnPlayerQualify(playerId);
            roundShouldEnd = true;
        }

        public void CheckIfQualEnd()
        {
            if (qualifiedPlayerIds.Count < playersPassExpectedThisRound)
                return;
            foreach (var playerId in alivePlayerIds.ToList())
                // kill everyone left behind
                OnPlayerEliminated(playerId);
            roundShouldEnd = true;
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
            playersPassExpectedThisRound = (uint)(alivePlayerIds.Count * playersQualifyPerRoundPercent);
        }

        public void SetupFinalRound()
        {
            //kill all but one
            playersPassExpectedThisRound = 1;
        }

        public void SpreadPlayers()
        {
            for (var index = 0; index < alivePlayerIds.Count; index++)
            {
                var spawner = GameObject.Find("spawner (" + index + ")").transform;
                var playerId = alivePlayerIds.ToList()[index];
                var thisPlayer = serverHandler.msgHandlerCommon.idToPlayers[playerId];
                thisPlayer.transform.position = spawner.position;
                thisPlayer.transform.rotation = spawner.rotation;

                serverHandler.netServer.SendMessage(serverHandler.msgHandlerCommon.CreatePlayerUpdateMessage(playerId));
                thisPlayer.haveWeStartedYet = true;
            }
        }


        public void HumanPlayerIsReady()
        {
            if (ready || !waitingForLoadedPlayers)
                return;
            playersLoaded++;
            if (playersLoaded < playersLoadedTarget) return;

            Debug.Log("All players have loaded. Spreading them to spawners and starting final countdown.");
            waitingForLoadedPlayers = false;

            SpreadPlayers();

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

            StartCoroutine(UnlockPlayersIn5());
        }


        public IEnumerator UnlockPlayersIn5()
        {
            yield return new WaitForSeconds(5f);
            Debug.Log("Unlocking all players now.");
            for (var index = 0; index < alivePlayerIds.Count; index++)
            {
                var playerId = alivePlayerIds.ToList()[index];
                var thisPlayer = serverHandler.msgHandlerCommon.idToPlayers[playerId];
                thisPlayer.skipTick = PlayerHandler.SkipTickReason.None;
                thisPlayer.myRb.isKinematic = false;
            }
        }


        public void AddNewPlayers()
        {
            while (!serverHandler.netServer.newClientsForGame.IsEmpty)
            {
                uint nextClient = 0;
                while (!serverHandler.netServer.newClientsForGame.TryDequeue(out nextClient))
                {
                    // burn the cpu here
                }

                alivePlayerIds.Add(nextClient);

                var newPlayer = serverHandler.msgHandlerCommon.AddNewPlayer(nextClient);
                newPlayer.parentGameManager = this;

                if (nextClient > uint.MaxValue / 2) newPlayer.isBotPlayer = true;

                serverHandler.netServer.SendMessage(MessagePacker.PackAddPlayerMessage(nextClient));

                foreach (var playerId in alivePlayerIds)
                {
                    if (playerId == nextClient)
                        continue;

                    serverHandler.netServer.SendMessage(nextClient, MessagePacker.PackAddPlayerMessage(playerId));
                }
            }
        }
        private void ChangeGameType(RoundType newType, GameLevel newLevel)
        {
            serverHandler.netServer.SendMessage(MessagePacker.PackChangeGameSceneMsg(
                new MessagePacker.NewGameLevelMessage
                {
                    RoundType = newType,
                    GameLevel = newLevel
                }));

            List<uint> playerIdsForNextRound;

            if (currentRoundType == RoundType.Free)
            {
                playerIdsForNextRound = alivePlayerIds.ToList();
            }
            else
            {
                playerIdsForNextRound = qualifiedPlayerIds.ToList();
            }

            qualifiedPlayerIds.Clear();
            alivePlayerIds.Clear();

            foreach (var existingPlayer in serverHandler.msgHandlerCommon.idToPlayers.Values)
            {
                if (existingPlayer != null) Destroy(existingPlayer.gameObject);
            }
            serverHandler.msgHandlerCommon.idToPlayers.Clear();

            levelTimeRemaining = timeLimitsPerRound[newType];
            currentRoundType = newType;
            currentLevel = newLevel;
            levelLoader.LoadLevel(newLevel);

            foreach (var playerId in playerIdsForNextRound)
            {
                var newPlayer = serverHandler.msgHandlerCommon.AddNewPlayer(playerId);
                newPlayer.parentGameManager = this;
                if (playerId > uint.MaxValue / 2) newPlayer.isBotPlayer = true;

                alivePlayerIds.Add(playerId);

                serverHandler.netServer.SendMessage(MessagePacker.PackAddPlayerMessage(playerId));
            }

            // Only freeze players if the next level is NOT the menu/lobby.
            if (newLevel != GameLevel.MenuLevel)
            {
                ready = false;
                waitingForLoadedPlayers = true;
                playersLoaded = 0;
                playersLoadedTarget = 0;

                foreach (var playerId in alivePlayerIds)
                {
                    var thisPlayer = serverHandler.msgHandlerCommon.idToPlayers[playerId];
                    Rigidbody playerRb = thisPlayer.GetComponent<Rigidbody>();

                    thisPlayer.skipTick = PlayerHandler.SkipTickReason.Loading;
                    if (playerRb != null) playerRb.isKinematic = true;

                    if (playerId < uint.MaxValue / 2)
                    {
                        playersLoadedTarget++;
                    }
                }
            }
            else
            {
                // If we are loading the menu, players are free to move immediately.
                // Reset relevant state machines.
                ready = true;
                waitingForLoadedPlayers = false;
            }
        }
    }
}