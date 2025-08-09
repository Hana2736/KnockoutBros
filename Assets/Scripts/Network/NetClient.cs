using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using NativeWebSocket;
using UnityEngine;

namespace Network
{
    public class NetClient : MonoBehaviour
    {
        public static WebSocket sock;
        public static uint clientId = 0;
        public ConcurrentQueue<byte[]> inMessageQueue;
        public static ConcurrentQueue<byte[]> outMessageQueue;
        private static byte[] readBuf;
        private static bool resetting;
        private static uint readBufPos;
        public static bool keyRecovered;
        public static bool isReadyForTicking;

        private void Start()
        {
            if (NetServer.BuiltRunningMode != NetServer.RunningMode.Client)
                return;
            inMessageQueue = new ();
            outMessageQueue = new();
#if !UNITY_WEBGL || UNITY_EDITOR
            SetupMT();
#endif
            ResetConnection();
        }


        private void Update()
        {
            if (NetServer.BuiltRunningMode != NetServer.RunningMode.Client)
                return;
#if !UNITY_WEBGL || UNITY_EDITOR
            sock.DispatchMessageQueue();
#endif
            sendMsgs();
        }


        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void SetupMT()
        {
            MainThreadUtil.Setup();
        }


        public async void ResetConnection()
        {
            keyRecovered = false;
            if (resetting)
                return;
            resetting = true;
            readBuf = new byte[32768];
            readBufPos = 0;
            sock = new WebSocket("wss://direct.hana.lol:443");
            sock.OnOpen += () => { Debug.Log("Net Connected"); };
            sock.OnClose += code =>
            {
                Debug.LogWarning("Net Closed: " + code);
                resetting = false;
                NetReconnector.HandleDisconnect(code.ToString());
            };
            sock.OnError += msg =>
            {
                Debug.LogWarning("Net Error: " + msg);
                resetting = false;
                NetReconnector.HandleDisconnect(msg);
            };
            sock.OnMessage += data =>
            {
                // Copy incoming data to the position of the read buffer
                if (data.Length + readBufPos > readBuf.Length)
                {
                    Debug.LogError("Read buffer overflow. Data size exceeds buffer capacity.");
                    return;
                }

                Array.Copy(data, 0, readBuf, readBufPos, data.Length);
                readBufPos += (uint)data.Length;

                while (true)
                {
                    // Ensure we have at least the minimum header size (5 bytes)
                    if (readBufPos < 5) break;

                    var dataSize = BitConverter.ToUInt32(readBuf, 1);
                    if (readBufPos < 5 + dataSize) break;

                    // Process the complete message
                    var msg = new byte[5 + dataSize];
                    Array.Copy(readBuf, 0, msg, 0, msg.Length);
                    inMessageQueue.Enqueue(msg);

                    // Shift the remaining buffer
                    var remainingDataSize = readBufPos - (5 + dataSize);
                    Array.Copy(readBuf, 5 + dataSize, readBuf, 0, remainingDataSize);
                    readBufPos = remainingDataSize;
                }
            };
            await sock.Connect();
            resetting = false;
        }

        // Start is called once before the first execution of Update after the MonoBehaviour is created

        public static void SendMsg(byte[] msg)
        {
            outMessageQueue.Enqueue(msg);
        }

        private async void sendMsgs()
        {
            if (sock.State != WebSocketState.Open) return;
            while (outMessageQueue.Count > 0)
            {
                
                //if (!keyRecovered && msg[0] != (byte)PacketTypes.PacketType.SecretKeyMessage)
                //     return;
                byte[] msg = null;
                while (!outMessageQueue.TryDequeue(out msg))
                {
                    // burn the cpu here
                }
                // if (keyRecovered && msg[0] == (byte)PacketTypes.PacketType.SecretKeyMessage)
                //     continue;
                await sock.Send(msg);
                Console.WriteLine("Sent: " + BitConverter.ToString(msg));
            }
        }
    }
}