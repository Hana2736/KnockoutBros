using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Movement;
using Network;
using UnityEngine;
using Debug = UnityEngine.Debug;

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
        public bool ready;
        public HashSet<uint> alivePlayerIds;
        private uint lastPlayersQualSent = 9999;

        private uint lastSecondCountdown = 16;
        public ConcurrentQueue<uint> pendingPlayers = new();
        private float playerLoadStartTime = -1f;
        public Dictionary<uint, uint> playerPointsScores;
        public HashSet<uint> qualifiedPlayerIds;
        public HashSet<uint> readyPlayers;

        private Queue<MessagePacker.NewGameLevelMessage> roundOrder;
        private bool roundShouldEnd;


        private float sendUpdatesTimer;

        // Custom variables
        private float sessionStartTime = -1f;
        public Dictionary<RoundType, float> timeLimitsPerRound;

      
        public void ResetRoundQueue()
        {
            roundOrder = new Queue<MessagePacker.NewGameLevelMessage>();
            
            // For testing, we can use a shorter round order.
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
            
         
        }

       
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
            sessionStartTime = -1f;


            timeLimitsPerRound = new Dictionary<RoundType, float>();
            timeLimitsPerRound[RoundType.Free] = -100f;
            timeLimitsPerRound[RoundType.Race] = 180f;
            timeLimitsPerRound[RoundType.Survival] = 300f;
            timeLimitsPerRound[RoundType.Points] = 220f;

            // The round order is empty at first. It will be populated when a new game starts.
            roundOrder = new Queue<MessagePacker.NewGameLevelMessage>();

            levelTimeRemaining = 15f;
            startMatching = false;

            roundShouldEnd = false;
            playerPointsScores = new Dictionary<uint, uint>();

            while (pendingPlayers.Count > 0)
            {
                uint pendingPlayerId;
                if (pendingPlayers.TryDequeue(out pendingPlayerId))
                    serverHandler.netServer.newClientsForGame.Enqueue(pendingPlayerId);
            }
            
            serverHandler.netServer.SendMessage(MessagePacker.PackStringMsg("Step on the green pad to play!"));

            ready = true;
        }


        public void Update()
        {
            if (sessionStartTime > 0 && Time.time - sessionStartTime > 600f)
            {
                Debug.Log("Session timeout, exiting server.");
                Process.GetCurrentProcess().Kill();
            }

            if (playerLoadStartTime > 0 && Time.time - playerLoadStartTime > 30f)
            {
                Debug.Log("Player loading timeout, exiting server.");
                Process.GetCurrentProcess().Kill();
            }

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
                // A round has just ended.
                roundShouldEnd = false;
                
                MessagePacker.NewGameLevelMessage nextLevel;

                if (roundOrder.Count > 0)
                {
                    // If there are more rounds in the queue, proceed to the next one.
                    nextLevel = roundOrder.Dequeue();
                }
                else
                {
                    // If the queue is empty, the game session is over. Go back to the lobby.
                    nextLevel = new MessagePacker.NewGameLevelMessage
                    {
                        RoundType = RoundType.Free,
                        GameLevel = GameLevel.MenuLevel
                    };
                }
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

        public IEnumerator StartCountDown()
        {
            // This is the official start of a new game session from the lobby.
            // Reset the round queue for the new game.
            ResetRoundQueue();

            readyPlayers.Clear();

            for (var i = 15; i >= 0; i--)
            {
                yield return new WaitForSeconds(1);
                serverHandler.netServer.SendMessage(
                    MessagePacker.PackStringMsg("Starting in " + (uint)i));
            }
            
            var humanCnt = (uint)alivePlayerIds.Count;
            var botCnt = targetPlayerCount - humanCnt;
            for (var ind = 0; ind < botCnt; ind++)
            {
                nextBotId--;
                serverHandler.netServer.newClientsForGame.Enqueue(nextBotId);
            }

            AddNewPlayers();
            // Signal that the first round should begin.
            roundShouldEnd = true;
        }


        public void UpdateInFreeStage()
        {
            AddNewPlayers();
            // If any player is ready and a match isn't already starting, begin the countdown.
            if (readyPlayers.Count > 0 && !startMatching)
            {
                startMatching = true;
                StartCoroutine(StartCountDown());
            }
        }

        public void UpdateInRaceStage()
        {
            var messageString = "Level Over!";
            if (qualifiedPlayerIds.Count != playersPassExpectedThisRound)
                messageString = "Players Finished: " + qualifiedPlayerIds.Count + " / " + playersPassExpectedThisRound;
            SendPlayersCountMsg(messageString);
            CheckIfQualEnd();
        }

        public void UpdateInPointsStage()
        {
            var messageString = "Level Over!";
            if (qualifiedPlayerIds.Count != playersPassExpectedThisRound)
                messageString = "Players Finished: " + qualifiedPlayerIds.Count + " / " + playersPassExpectedThisRound;
            SendPlayersCountMsg(messageString);
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
            if (currentRoundType == RoundType.Survival)
            {
                foreach (var alivePlayer in alivePlayerIds.ToList()) OnPlayerQualify(alivePlayer);
                return;
            }
            
            foreach (var playerId in alivePlayerIds.ToList())
                OnPlayerEliminated(playerId);
        }

        public void UpdateInSurvivalStage()
        {
            var diffNum = (uint)(alivePlayerIds.Count - playersPassExpectedThisRound);
            var messageString = diffNum switch
            {
                1 => "Outlive " + diffNum + " player!",
                > 1 => "Outlive " + diffNum + " players!",
                _ => "Level Over!"
            };

            SendPlayersCountMsg(messageString);
            if (alivePlayerIds.Count > playersPassExpectedThisRound)
                return;
            foreach (var playerId in alivePlayerIds.ToList())
                OnPlayerQualify(playerId);
            roundShouldEnd = true;
        }

        public void SendPlayersCountMsg(string msg)
        {
            if (lastPlayersQualSent != alivePlayerIds.Count)
            {
                serverHandler.netServer.SendMessage(MessagePacker.PackStringMsg(msg));
                lastPlayersQualSent = (uint)alivePlayerIds.Count;
            }
        }

        public void CheckIfQualEnd()
        {
            if (qualifiedPlayerIds.Count < playersPassExpectedThisRound)
                return;
            foreach (var playerId in alivePlayerIds.ToList())
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
            playersPassExpectedThisRound = (uint)(alivePlayerIds.Count * playersQualifyPerRoundPercent);
        }

        public void SetupFinalRound()
        {
            playersPassExpectedThisRound = 1;
        }

        public void SpreadPlayers()
        {
            for (var index = 0; index < alivePlayerIds.Count; index++)
            {
                var playerId = alivePlayerIds.ToList()[index];
                var thisPlayer = serverHandler.msgHandlerCommon.idToPlayers[playerId];
                try
                {
                    var spawner = GameObject.Find("spawner (" + index + ")").transform;
                    thisPlayer.transform.position = spawner.position;
                    thisPlayer.transform.rotation = spawner.rotation;
                }
                catch (Exception e)
                {
                }
                thisPlayer.skipTick = PlayerHandler.SkipTickReason.None;

                serverHandler.netServer.SendMessage(serverHandler.msgHandlerCommon.CreatePlayerUpdateMessage(playerId));
                thisPlayer.haveWeStartedYet = true;
            }
        }


        public void HumanPlayerIsReady()
        {
            Debug.Log("Human is ready");
            if (ready || !waitingForLoadedPlayers)
                return;
            playersLoaded++;
            if (playersLoaded < playersLoadedTarget) return;

            waitingForLoadedPlayers = false;
            playerLoadStartTime = -1f;

            foreach (var player in alivePlayerIds)
                serverHandler.netServer.SendMessage(MessagePacker.PackAddPlayerMessage(player));


            SpreadPlayers();

            ready = true;
            lastPlayersQualSent = 999;
            switch (currentLevel)
            {
                case GameLevel.MenuLevel:
                    break;
                case GameLevel.RaceLevel:
                    SetupRaceRound();
                    break;
                case GameLevel.SurvivalLevel:
                    SetupSurvivalRound();
                    break;
                case GameLevel.PointsLevel:
                    SetupPointsRound();
                    break;
                case GameLevel.FinalLevel:
                    SetupFinalRound();
                    break;
            }
        }


        public void AddNewPlayers()
        {
            while (!serverHandler.netServer.newClientsForGame.IsEmpty)
            {
                uint nextClient = 0;
                while (!serverHandler.netServer.newClientsForGame.TryDequeue(out nextClient))
                {
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
            // When we go back to the lobby, allow a new match to be started.
            if (newType == RoundType.Free)
            {
                startMatching = false;
            }
            serverHandler.netServer.SendMessage(MessagePacker.PackChangeGameSceneMsg(
                new MessagePacker.NewGameLevelMessage
                {
                    RoundType = newType,
                    GameLevel = newLevel
                }));

            List<uint> playerIdsForNextRound;

            if (newLevel == GameLevel.MenuLevel)
            {
                // If transitioning TO the lobby, gather ALL recently active players to bring them back.
                playerIdsForNextRound = new List<uint>();
                foreach (var playerTime in serverHandler.timeOfLastPing)
                {
                    if (Time.time - playerTime.Value < 10f)
                    {
                        playerIdsForNextRound.Add(playerTime.Key);
                    }
                }
            }
            else
            {
                // Otherwise, use the standard logic for round progression.
                if (currentRoundType == RoundType.Free)
                    playerIdsForNextRound = alivePlayerIds.ToList(); // From lobby to first game
                else
                    playerIdsForNextRound = qualifiedPlayerIds.ToList(); // From game to game
            }
            
            qualifiedPlayerIds.Clear();
            alivePlayerIds.Clear();

            foreach (var existingPlayer in serverHandler.msgHandlerCommon.idToPlayers.Values)
                if (existingPlayer != null)
                    Destroy(existingPlayer.gameObject);
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
            }


            ready = false;
            waitingForLoadedPlayers = true;
            playersLoaded = 0;
            playersLoadedTarget = 0;
            playerLoadStartTime = Time.time;


            foreach (var playerId in alivePlayerIds)
            {
                var thisPlayer = serverHandler.msgHandlerCommon.idToPlayers[playerId];
                var playerRb = thisPlayer.GetComponent<Rigidbody>();

                thisPlayer.skipTick = PlayerHandler.SkipTickReason.Loading;
                if (playerRb != null) playerRb.isKinematic = true;

                // Only human players need to send a "Loaded" message.
                if (playerId < uint.MaxValue / 2) playersLoadedTarget++;
            }
        }
    }
}