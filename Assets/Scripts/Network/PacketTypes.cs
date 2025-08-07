public class PacketTypes
{
    public enum PacketType : byte
    {
        StringMessage,
        PingMessage,
        SecretKeyMessage,
        PlayerUpdateMessage,
        AddPlayerMessage,
        SendClientIDMessage,
        PlayerQualifiedMessage,
        PlayerEliminatedMessage,
        PlayerScoreUpdate,
        

        InvalidPacket = 0xFF
    }
}