using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using Util;

namespace Network
{
    public class MessagePacker : MonoBehaviour
    {
        private static readonly int headerLen = 0x1 + sizeof(uint);


        //////// hello message
        public static byte[] PackStringMsg(string msg)
        {
            var msgBytes = Encoding.UTF8.GetBytes(msg);
            var messageID = PacketTypes.PacketType.StringMessage;
            var retBlock = WriteHeader(messageID, (uint)msgBytes.Length);
            msgBytes.CopyTo(retBlock, headerLen);
            return retBlock;
        }

        public static string UnpackStringMsg(byte[] msg)
        {
            return Encoding.UTF8.GetString(msg, headerLen, msg.Length - headerLen);
        }

        //////// ping message

        public static byte[] PackPingMsg(uint pingID)
        {
            var messageID = PacketTypes.PacketType.PingMessage;
            var retBlock = WriteHeader(messageID, sizeof(uint));
            BitConverter.TryWriteBytes(retBlock.AsSpan(headerLen, sizeof(uint)), pingID);
            return retBlock;
        }

        public static uint UnpackPingMsg(byte[] msg)
        {
            return BitConverter.ToUInt32(msg, headerLen);
        }

        //////// secret key message

        public static byte[] PackSecretKeyMsg(Guid secretKey)
        {
            var messageID = PacketTypes.PacketType.SecretKeyMessage;
            var secretKeyBytes = secretKey.ToByteArray();
            var retBlock = WriteHeader(messageID, (uint)secretKeyBytes.Length);
            secretKeyBytes.CopyTo(retBlock, headerLen);
            return retBlock;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct NewWaveMessage
        {
            public int pathIndex;
            public float speed;
        }

        public static byte[] PackNewWaveMessage(NewWaveMessage message)
        {
            var messageID = PacketTypes.PacketType.SpawnWaterWave;

            var messageSize = sizeof(int) + sizeof(float);
            var retBlock = WriteHeader(messageID, (uint)messageSize);

            var structPointer = Marshal.AllocHGlobal(messageSize);
            Marshal.StructureToPtr(message, structPointer, true);
            Marshal.Copy(structPointer, retBlock, headerLen, messageSize);
            Marshal.FreeHGlobal(structPointer);

            return retBlock;
        }

        public static NewWaveMessage UnpackNewWaveMessage(byte[] msg)
        {
            var messageSize = sizeof(int) + sizeof(float);
            NewWaveMessage returned;
            var structPointer = Marshal.AllocHGlobal(messageSize);
            Marshal.Copy(msg, headerLen, structPointer, messageSize);
            returned = Marshal.PtrToStructure<NewWaveMessage>(structPointer);
            Marshal.FreeHGlobal(structPointer);
            return returned;
        }


        public static Guid UnpackSecretKeyMsg(byte[] msg)
        {
            return new Guid(msg.AsSpan(headerLen, msg.Length - headerLen));
        }


        //////// player update message
        public static byte[] PackPlayerUpdateMsg(PlayerUpdateMessage message)
        {
            var messageID = PacketTypes.PacketType.PlayerUpdateMessage;


            var messageSize = sizeof(uint) + (sizeof(float) * 6) + sizeof(int) + sizeof(byte);

            var retBlock = WriteHeader(messageID, (uint)messageSize);

            var structPointer = Marshal.AllocHGlobal(messageSize);
            Marshal.StructureToPtr(message, structPointer, true);
            Marshal.Copy(structPointer, retBlock, headerLen, messageSize);
            Marshal.FreeHGlobal(structPointer);

            return retBlock;
        }

        public static PlayerUpdateMessage UnpackPlayerUpdateMsg(byte[] msg)
        {
            var messageSize = sizeof(uint) + (sizeof(float) * 6) + sizeof(int) + sizeof(byte);

            PlayerUpdateMessage returned;
            var structPointer = Marshal.AllocHGlobal(messageSize);
            Marshal.Copy(msg, headerLen, structPointer, messageSize);
            returned = Marshal.PtrToStructure<PlayerUpdateMessage>(structPointer);
            Marshal.FreeHGlobal(structPointer);
            return returned;
        }

        public static uint UnpackAddPlayerMessage(byte[] msg)
        {
            return BitConverter.ToUInt32(msg, headerLen);
        }

        public static byte[] PackAddPlayerMessage(uint playerId)
        {
            var messageID = PacketTypes.PacketType.AddPlayerMessage;
            var retBlock = WriteHeader(messageID, sizeof(uint));
            BitConverter.TryWriteBytes(retBlock.AsSpan(headerLen, sizeof(uint)), playerId);
            return retBlock;
        }


        public static byte[] PackPlayerLoadedMessage()
        {
            Debug.Log("Sending player loaded message-stage: packmsg called");
            var messageID = PacketTypes.PacketType.PlayerLoadedMessage;
            var retBlock = WriteHeader(messageID, 0);
            Debug.Log("Sending player loaded message-stage: retblock created");
            return retBlock;
        }

        public static byte[] PackStartRoundMessage()
        {
            var messageID = PacketTypes.PacketType.StartRound;
            var retBlock = WriteHeader(messageID, 0);
            return retBlock;
        }

        public static uint UnpackSendClientIDMessage(byte[] msg)
        {
            return BitConverter.ToUInt32(msg, headerLen);
        }

        public static byte[] PackSendClientIDMessage(uint clientId)
        {
            var messageID = PacketTypes.PacketType.SendClientIDMessage;
            var retBlock = WriteHeader(messageID, sizeof(uint));
            BitConverter.TryWriteBytes(retBlock.AsSpan(headerLen, sizeof(uint)), clientId);
            return retBlock;
        }

        // write message header: byte ID + 4 bytes msglength
        private static byte[] WriteHeader(PacketTypes.PacketType messageID, uint dataLen)
        {
            var retBlock = new byte[headerLen + dataLen];
            retBlock[0] = (byte)messageID;
            BitConverter.TryWriteBytes(retBlock.AsSpan(1, sizeof(uint)), dataLen);
            return retBlock;
        }

        public static uint UnpackPlayerEliminatedMessage(byte[] msg)
        {
            return BitConverter.ToUInt32(msg, headerLen);
        }

        public static byte[] PackPlayerEliminatedMessage(uint playerId)
        {
            var messageID = PacketTypes.PacketType.PlayerEliminatedMessage;
            var retBlock = WriteHeader(messageID, sizeof(uint));
            BitConverter.TryWriteBytes(retBlock.AsSpan(headerLen, sizeof(uint)), playerId);
            return retBlock;
        }

        public static uint UnpackPlayerQualifiedMessage(byte[] msg)
        {
            return BitConverter.ToUInt32(msg, headerLen);
        }

        public static byte[] PackPlayerQualifiedMessage(uint playerId)
        {
            var messageID = PacketTypes.PacketType.PlayerQualifiedMessage;
            var retBlock = WriteHeader(messageID, sizeof(uint));
            BitConverter.TryWriteBytes(retBlock.AsSpan(headerLen, sizeof(uint)), playerId);
            return retBlock;
        }

        public static uint UnpackPlayerScoreUpdateMessage(byte[] msg)
        {
            return BitConverter.ToUInt32(msg, headerLen);
        }

        public static byte[] PackPlayerScoreUpdateMessage(uint newScore)
        {
            var messageID = PacketTypes.PacketType.PlayerScoreUpdate;
            var retBlock = WriteHeader(messageID, sizeof(uint));
            BitConverter.TryWriteBytes(retBlock.AsSpan(headerLen, sizeof(uint)), newScore);
            return retBlock;
        }

        public static byte[] PackChangeGameSceneMsg(NewGameLevelMessage message)
        {
            var messageID = PacketTypes.PacketType.ChangeGameScene;

            var messageSize = 2;
            var retBlock = WriteHeader(messageID, (uint)messageSize);

            var structPointer = Marshal.AllocHGlobal(messageSize);
            Marshal.StructureToPtr(message, structPointer, true);
            Marshal.Copy(structPointer, retBlock, headerLen, messageSize);
            Marshal.FreeHGlobal(structPointer);

            return retBlock;
        }

        public static NewGameLevelMessage UnpackChangeGameSceneMsg(byte[] msg)
        {
            var messageSize = 2;
            NewGameLevelMessage returned;
            var structPointer = Marshal.AllocHGlobal(messageSize);
            Marshal.Copy(msg, headerLen, structPointer, messageSize);
            returned = Marshal.PtrToStructure<NewGameLevelMessage>(structPointer);
            Marshal.FreeHGlobal(structPointer);
            return returned;
        }


        public static byte[] PackNewBubbleMessage(NewBubbleMessage message)
        {
            var messageID = PacketTypes.PacketType.SpawnBubble;

            var messageSize = sizeof(uint) + sizeof(uint) + sizeof(float) * 3;
            var retBlock = WriteHeader(messageID, (uint)messageSize);

            var structPointer = Marshal.AllocHGlobal(messageSize);
            Marshal.StructureToPtr(message, structPointer, true);
            Marshal.Copy(structPointer, retBlock, headerLen, messageSize);
            Marshal.FreeHGlobal(structPointer);

            return retBlock;
        }

        public static NewBubbleMessage UnPackNewBubbleMessage(byte[] msg)
        {
            var messageSize = sizeof(uint) + sizeof(uint) + sizeof(float) * 3;
            NewBubbleMessage returned;
            var structPointer = Marshal.AllocHGlobal(messageSize);
            Marshal.Copy(msg, headerLen, structPointer, messageSize);
            returned = Marshal.PtrToStructure<NewBubbleMessage>(structPointer);
            Marshal.FreeHGlobal(structPointer);
            return returned;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PlayerUpdateMessage
        {
            public uint playerID;

            public float positionX,
                positionY,
                positionZ,
                rotationX,
                rotationY,
                rotationZ;

            public int animId;
            public byte skipTickReason; // Add this line
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct NewGameLevelMessage
        {
            public GameManager.RoundType RoundType;
            public GameManager.GameLevel GameLevel;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct NewBubbleMessage
        {
            public uint bubbleId;
            public uint bubbleScore;
            public float posX, posY, posZ;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RainSpikeMessage
        {
            public float locationX, locationZ;
        }

        public static byte[] PackRainSpikeMessage(RainSpikeMessage msg)
        {
            var messageID = PacketTypes.PacketType.SpawnCeilSpike;

            var messageSize = sizeof(float) * 2;
            var retBlock = WriteHeader(messageID, (uint)messageSize);

            var structPointer = Marshal.AllocHGlobal(messageSize);
            Marshal.StructureToPtr(msg, structPointer, true);
            Marshal.Copy(structPointer, retBlock, headerLen, messageSize);
            Marshal.FreeHGlobal(structPointer);

            return retBlock;
        }

        public static RainSpikeMessage UnpackRainSpikeMsg(byte[] msg)
        {
            var messageSize = sizeof(float) * 2;
            RainSpikeMessage returned;
            var structPointer = Marshal.AllocHGlobal(messageSize);
            Marshal.Copy(msg, headerLen, structPointer, messageSize);
            returned = Marshal.PtrToStructure<RainSpikeMessage>(structPointer);
            Marshal.FreeHGlobal(structPointer);
            return returned;
        }

        public static uint UnpackRemoveBubbleMessage(byte[] msg)
        {
            return BitConverter.ToUInt32(msg, headerLen);
        }

        public static byte[] PackRemoveBubbleMessage(uint bubbleId)
        {
            var messageID = PacketTypes.PacketType.RemoveBubble;
            var retBlock = WriteHeader(messageID, sizeof(uint));
            BitConverter.TryWriteBytes(retBlock.AsSpan(headerLen, sizeof(uint)), bubbleId);
            return retBlock;
        }


    }
}