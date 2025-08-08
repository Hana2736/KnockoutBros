using System;
using System.Collections;
using System.Collections.Generic;
using Movement;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Util;

namespace Network
{
    public class GameMsgHandlerClient : MonoBehaviour
    {
        public GameMsgHandlerCommon msgHandlerCommon;
        public LevelLoader LevelLoader;
        public InGameGUIMgr guiMgr;
        public GameObject CeilingSpikePrefab;
    
        public RawImage loadingScreen;
        public Texture2D raceLoad, surviveLoad, ptsLoad, finalLoad, titleLoad;
        private PingSender pingSender;


        public GameObject wavePrefab; // <-- Add this field and assign it in the Inspector
        private List<Vector3[]> wavePaths;
        private void Start()
        {
        
            pingSender = GetComponent<PingSender>();
            msgHandlerCommon = GetComponent<GameMsgHandlerCommon>();
            LevelLoader = GetComponent<LevelLoader>();
            guiMgr = GetComponent<InGameGUIMgr>();
            InitializeWavePaths();
        }


        void InitializeWavePaths()
        {
            wavePaths = new List<Vector3[]>();
            wavePaths.Add(new Vector3[] { new Vector3(0, -0.88f, -19.5f), new Vector3(0, -0.21f, -17.87f), new Vector3(0, -0.21f, 17.87f), new Vector3(0, -0.88f, 19.5f) });
            wavePaths.Add(new Vector3[] { new Vector3(0, -0.88f, 19.5f), new Vector3(0, -0.21f, 17.87f), new Vector3(0, -0.21f, -17.87f), new Vector3(0, -0.88f, -19.5f) });
            wavePaths.Add(new Vector3[] { new Vector3(-19.5f, -0.88f, 0), new Vector3(-17.87f, -0.21f, 0), new Vector3(17.87f, -0.21f, 0), new Vector3(19.5f, -0.88f, 0) });
            wavePaths.Add(new Vector3[] { new Vector3(19.5f, -0.88f, 0), new Vector3(17.87f, -0.21f, 0), new Vector3(-17.87f, -0.21f, 0), new Vector3(-19.5f, -0.88f, 0) });
        }


        public void HandleSpawnWaveMsg(byte[] msg)
        {
            var waveData = MessagePacker.UnpackNewWaveMessage(msg);
            int pathIndex = waveData.pathIndex;
            float speed = waveData.speed;

            if (pathIndex < 0 || pathIndex >= wavePaths.Count) return;

            Vector3[] selectedPath = wavePaths[pathIndex];
            Quaternion waveRotation = Quaternion.identity;

            if (pathIndex == 2 || pathIndex == 3)
            {
                waveRotation = Quaternion.Euler(0, 90, 0);
            }

            GameObject waveInstance = Instantiate(wavePrefab, selectedPath[0], waveRotation, new InstantiateParameters()
            {
                parent = Util.LevelLoader.parentForItems.transform,
                worldSpace = true
            });

            WaveMove waveMoveScript = waveInstance.GetComponent<WaveMove>();
            if (waveMoveScript != null)
            {
                waveMoveScript.Initialize(selectedPath, speed);
            }
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
            var playerCode = msgHandlerCommon.idToPlayers[playerId];
            if (playerId == NetClient.clientId) guiMgr.UpdateGuiWeQualified();
            StartCoroutine(PlayOutQualAnim(playerCode));
            Debug.Log("Qualified: " + playerId);
        }

        public IEnumerator PlayOutQualAnim(PlayerHandler playerCode)
        {
            // The player object is valid when the coroutine starts.
            var animId = Animator.StringToHash("Qualify");
            playerCode.skipTick = PlayerHandler.SkipTickReason.Qualify;
            playerCode.currentAnim = animId;

            // Wait for the animation to play.
            yield return new WaitForSeconds(1.9166666f);

            // FIX: After the wait, check if the player object still exists before trying to use it.
            // It might have been destroyed by a level transition.
            if (playerCode != null && playerCode.gameObject != null)
            {
                playerCode.gameObject.SetActive(false);
            }
        }

        public IEnumerator PlayOutElimAnim(PlayerHandler playerCode)
        {
            // The player object is valid when the coroutine starts.
            var animId = Animator.StringToHash("Die");
            playerCode.skipTick = PlayerHandler.SkipTickReason.Dead;
            playerCode.currentAnim = animId;

            // Wait for the animation to play.
            yield return new WaitForSeconds(1.7833333f);

            // FIX: Add the same null check here.
            if (playerCode != null && playerCode.gameObject != null)
            {
                playerCode.gameObject.SetActive(false);
            }
        }

        public void HandleRainSpikeMsg(byte[] msg)
        {
            var spikeData = MessagePacker.UnpackRainSpikeMsg(msg);
            Instantiate(CeilingSpikePrefab, new Vector3(spikeData.locationX, -1.72f, spikeData.locationZ),
                Quaternion.identity,
                new InstantiateParameters
                {
                    parent = Util.LevelLoader.parentForItems.transform,
                    worldSpace = true
                });
        }


        public void HandleEliminatedPlayerMsg(byte[] msg)
        {
            var playerId = MessagePacker.UnpackPlayerEliminatedMessage(msg);
            var playerCode = msgHandlerCommon.idToPlayers[playerId];
            if (playerId == NetClient.clientId) guiMgr.UpdateGuiWeEliminated();
            StartCoroutine(PlayOutElimAnim(playerCode));
            Debug.Log("Eliminated: " + playerId);
        }

        public void HandleLevelChangeMsg(byte[] msg)
        {
            StartCoroutine(DoSceneChangeWork(msg));
        }

        private IEnumerator DoSceneChangeWork(byte[] msg)
        {
            var nextConfig = MessagePacker.UnpackChangeGameSceneMsg(msg);
            
            // 1. Set the correct loading screen texture and show it.
            switch (nextConfig.GameLevel)
            {
                case GameManager.GameLevel.RaceLevel:
                    loadingScreen.texture = raceLoad;
                    break;
                case GameManager.GameLevel.SurvivalLevel:
                    loadingScreen.texture = surviveLoad;
                    break;
                case GameManager.GameLevel.PointsLevel:
                    loadingScreen.texture = ptsLoad;
                    break;
                case GameManager.GameLevel.FinalLevel:
                    loadingScreen.texture = finalLoad;
                    break;
                case GameManager.GameLevel.MenuLevel:
                    loadingScreen.texture = titleLoad;
                    break;
            }
            loadingScreen.enabled = true;
            if (guiMgr != null) guiMgr.HideBanner();

            // 2. Eradicate all existing player objects on the client.
            foreach (var player in msgHandlerCommon.idToPlayers.Values)
            {
                if (player != null)
                {
                    Destroy(player.gameObject);
                }
            }
            msgHandlerCommon.idToPlayers.Clear();

            // 3. Load the new level scenery. The server will re-add players.
            LevelLoader.LoadLevel(nextConfig.GameLevel);
            
            // 4. Wait for 5 seconds for the splash screen to display.
            yield return new WaitForSeconds(5f);
            
            // 5. Tell the server we have finished loading the assets.
            NetClient.SendMsg(MessagePacker.PackPlayerLoadedMessage());
            Debug.Log("Client has loaded level and sent packet to server.");

            // 6. Hide the loading screen. The player is still frozen at this point.
            loadingScreen.enabled = false;
            
            // 7. The client's job is done. It now waits for the server to send an update
            // that changes the player's SkipTickReason from 'Loading' to 'None', which will
            // happen after all other players have loaded and the 5-second countdown ends.
        }
    }
}