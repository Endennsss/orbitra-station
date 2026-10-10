using System;
using System.Numerics;
using Lidgren.Network;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._Orbitra.VoiceChat;

/// <summary>
/// Opus voice frame. The client-to-server form contains only sequence and payload;
/// the server-to-client form additionally contains the authoritative speaker and position.
/// </summary>
public sealed class MsgOrbitraVoiceFrame : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Entity;

    // Orbitra-Edit: sequenced-транспорт сохраняет порядок отправки кадров;
    // это важно, потому что обычный Unreliable шифруется и отправляется в фоне.
    public override NetDeliveryMethod DeliveryMethod => NetDeliveryMethod.UnreliableSequenced;

    // Канал 1 отделяет голос от стандартного entity-канала 0.
    public override int SequenceChannel => 1;

    public ushort Sequence;
    public NetEntity Speaker = NetEntity.Invalid;
    public Vector2 Position;
    public OrbitraVoiceTransmissionMode TransmissionMode;
    public string RadioChannelId = string.Empty;
    public byte[] Data = Array.Empty<byte>();

    public override int EstimateBufferSize() => 1 + sizeof(ushort) + sizeof(int) + sizeof(float) * 2 +
        sizeof(byte) + RadioChannelId.Length + Data.Length;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        Sequence = buffer.ReadUInt16();
        Speaker = buffer.ReadNetEntity();
        Position = buffer.ReadVector2();
        TransmissionMode = (OrbitraVoiceTransmissionMode) buffer.ReadByte();
        RadioChannelId = TransmissionMode == OrbitraVoiceTransmissionMode.Radio
            ? buffer.ReadString()
            : string.Empty;

        if (!OrbitraVoiceChatPolicy.IsValidTransmission(TransmissionMode, RadioChannelId))
            throw new InvalidOperationException("Invalid Orbitra voice transmission mode or channel.");

        var length = buffer.ReadVariableInt32();
        if (!OrbitraVoiceChatPolicy.IsValidPayloadLength(length))
            throw new InvalidOperationException($"Invalid Orbitra voice payload length: {length}.");

        Data = buffer.ReadBytes(length);
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        if (!OrbitraVoiceChatPolicy.IsValidPayloadLength(Data.Length))
            throw new InvalidOperationException($"Invalid Orbitra voice payload length: {Data.Length}.");

        buffer.Write(Sequence);
        buffer.Write(Speaker);
        buffer.Write(Position);
        buffer.Write((byte) TransmissionMode);
        if (TransmissionMode == OrbitraVoiceTransmissionMode.Radio)
            buffer.Write(RadioChannelId);

        if (!OrbitraVoiceChatPolicy.IsValidTransmission(TransmissionMode, RadioChannelId))
            throw new InvalidOperationException("Invalid Orbitra voice transmission mode or channel.");

        buffer.WriteVariableInt32(Data.Length);
        buffer.Write(Data);
    }
}
