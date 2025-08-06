using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Network
{
    public class MessagePacker
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

        public static Guid UnpackSecretKeyMsg(byte[] msg)
        {
            return new Guid(msg.AsSpan(headerLen, msg.Length - headerLen));
        }


        //////// player update message
        public static byte[] PackPlayerUpdateMsg(PlayerUpdateMessage message)
        {
            var messageID = PacketTypes.PacketType.PlayerUpdateMessage;


            var messageSize = sizeof(uint) + (sizeof(float) * (3 + 3 + 3 + 2));
            var retBlock = WriteHeader(messageID, (uint)messageSize);

            var structPointer = Marshal.AllocHGlobal(messageSize);
            Marshal.StructureToPtr(message, structPointer, true);
            Marshal.Copy(structPointer, retBlock, headerLen, messageSize);
            Marshal.FreeHGlobal(structPointer);

            return retBlock;
        }

        public static PlayerUpdateMessage UnpackPlayerUpdateMsg(byte[] msg)
        {
            var messageSize = sizeof(uint) + (sizeof(float) * (3 + 3 + 3 + 2));
            PlayerUpdateMessage returned;
            var structPointer = Marshal.AllocHGlobal(messageSize);
            Marshal.Copy(structPointer, msg, headerLen, messageSize);
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

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class PlayerUpdateMessage
        {
            public uint playerID;

            public float positionX,
                positionY,
                positionZ,
                rotationX,
                rotationY,
                rotationZ,
                velocityX,
                velocityY,
                velocityZ;

            public float inputX, inputZ;
        }
    }
}