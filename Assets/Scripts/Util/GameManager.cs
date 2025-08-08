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
                RoundType = RoundType.Race,
                GameLevel = GameLevel.RaceLevel
            });
            roundOrder.Enqueue(new MessagePacker.NewGameLevelMessage
            {
                RoundType = RoundType.Survival,
                GameLevel = GameLevel.SurvivalLevel
            });
            //       roundOrder.Enqueue(new MessagePacker.NewGameLevelMessage
            //        {
            //            RoundType = RoundType.Points,
            //            GameLevel = GameLevel.PointsLevel
            //        });
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
                serverHandler.netServer.SendMessage(serverHandler.msgHandlerCommon.CreatePlayerUpdateMessage(playerId));

            }
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

            SpreadPlayers();
            StartCoroutine(UnlockPlayersIn5());
        }


        public IEnumerator UnlockPlayersIn5()
        {
            yield return new WaitForSeconds(5f);
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

            // 1. Tell all clients to start loading the new scene.
            serverHandler.netServer.SendMessage(MessagePacker.PackChangeGameSceneMsg(
                new MessagePacker.NewGameLevelMessage
                {
                    RoundType = newType,
                    GameLevel = newLevel
                }));

            // 2. Determine the list of players advancing to the next round.
            List<uint> playerIdsForNextRound;

            // If we are transitioning from the lobby, all 'alive' players should move to the next round.
            if (currentRoundType == RoundType.Free)
            {
                playerIdsForNextRound = alivePlayerIds.ToList();
            }
            else // For all other rounds, only players who have explicitly qualified will advance.
            {
                playerIdsForNextRound = qualifiedPlayerIds.ToList();
            }

            // Clear the old lists to prepare for the new round.
            qualifiedPlayerIds.Clear();
            alivePlayerIds.Clear();

            // 3. Destroy all old player GameObjects on the server to ensure a clean slate.
            foreach (var existingPlayer in serverHandler.msgHandlerCommon.idToPlayers.Values)
            {
                if (existingPlayer != null) Destroy(existingPlayer.gameObject);
            }
            serverHandler.msgHandlerCommon.idToPlayers.Clear();

            // 4. Update the server's game state and load the level scenery.
            levelTimeRemaining = timeLimitsPerRound[newType];
            currentRoundType = newType;
            currentLevel = newLevel;
            levelLoader.LoadLevel(newLevel);

            // 5. Re-create fresh player instances for the new round.
            foreach (var playerId in playerIdsForNextRound)
            {
                // AddNewPlayer instantiates the prefab.
                var newPlayer = serverHandler.msgHandlerCommon.AddNewPlayer(playerId);
                newPlayer.parentGameManager = this;
                if (playerId > uint.MaxValue / 2) newPlayer.isBotPlayer = true;

                alivePlayerIds.Add(playerId);

                // Tell all clients to create this new player in their world.
                serverHandler.netServer.SendMessage(MessagePacker.PackAddPlayerMessage(playerId));
            }

            // 6. Reset the server's state machine to wait for players to finish loading.
            ready = false;
            waitingForLoadedPlayers = true;
            playersLoaded = 0;
            playersLoadedTarget = 0;

            // 7. Initialize the state of the newly created players for the "loading" phase.
            foreach (var playerId in alivePlayerIds)
            {
                var thisPlayer = serverHandler.msgHandlerCommon.idToPlayers[playerId];

                // Safely get the Rigidbody component to avoid null reference errors.
                Rigidbody playerRb = thisPlayer.GetComponent<Rigidbody>();

                // Set the player to a "frozen" state until the round officially starts.
                thisPlayer.skipTick = PlayerHandler.SkipTickReason.Loading;
                if (playerRb != null) playerRb.isKinematic = true;

                // Count how many human players we need to wait for.
                if (playerId < uint.MaxValue / 2)
                {
                    playersLoadedTarget++;
                }
            }
        }
    }
}