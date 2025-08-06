public class PacketTypes
{
    public enum PacketType : byte
    {
        StringMessage = 0x01,
        PingMessage = 0x02,
        SecretKeyMessage = 0x03,
        PlayerUpdateMessage = 0x04,
        AddPlayerMessage = 0x05,
        SendClientIDMessage = 0x06,
        

        InvalidPacket = 0xFF
    }
}