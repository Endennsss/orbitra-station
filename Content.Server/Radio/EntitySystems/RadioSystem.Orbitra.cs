using System.Collections.Generic;
using Content.Server._Orbitra.VoiceChat;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.Radio.EntitySystems;

public sealed partial class RadioSystem
{
    /// <summary>
    /// Собирает сетевые каналы игроков, которые слышат голосовую передачу по выбранному каналу.
    /// События приёма сохраняют проверки глушилок, EMP и правил станции, но чат не создаётся.
    /// </summary>
    public bool TryCollectVoiceRecipients(EntityUid speaker, string channelId, List<INetChannel> recipients)
    {
        recipients.Clear();

        if (!ProtoMan.TryIndex(channelId, out RadioChannelPrototype? channel) || channel == null)
            return false;

        var canTransmit = false;
        if (TryComp<WearingHeadsetComponent>(speaker, out var wearing) &&
            TryComp<EncryptionKeyHolderComponent>(wearing.Headset, out var keys) &&
            keys.Channels.Contains(channel.ID))
        {
            canTransmit = true;
        }

        if (TryComp<IntrinsicRadioTransmitterComponent>(speaker, out var intrinsic) &&
            intrinsic.Channels.Contains(channel.ID))
        {
            canTransmit = true;
        }

        if (!canTransmit)
            return false;

        var sourceMapId = Transform(speaker).MapID;
        var radioSource = speaker;
        if (TryComp<WearingHeadsetComponent>(speaker, out var wearingHeadset))
            radioSource = wearingHeadset.Headset;

        var sourceServerExempt = _exemptQuery.HasComp(radioSource);
        var hasActiveServer = HasActiveServer(sourceMapId, channel.ID);

        var sendAttemptEv = new RadioSendAttemptEvent(channel, radioSource);
        RaiseLocalEvent(ref sendAttemptEv);
        RaiseLocalEvent(radioSource, ref sendAttemptEv);
        if (sendAttemptEv.Cancelled)
            return false;

        var radioQuery = EntityQueryEnumerator<ActiveRadioComponent, TransformComponent>();
        while (radioQuery.MoveNext(out var receiver, out var radio, out var transform))
        {
            if (!radio.ReceiveAllChannels && !radio.Channels.Contains(channel.ID))
                continue;

            if (TryComp<IntercomComponent>(receiver, out var intercom) &&
                !radio.ReceiveAllChannels && !intercom.SupportedChannels.Contains(channel.ID))
            {
                continue;
            }

            if (!channel.LongRange && transform.MapID != sourceMapId && !radio.GlobalReceive)
                continue;

            var needServer = !channel.LongRange && !sourceServerExempt;
            if (needServer && !hasActiveServer)
                continue;

            var attemptEv = new RadioReceiveAttemptEvent(channel, radioSource, receiver);
            RaiseLocalEvent(ref attemptEv);
            RaiseLocalEvent(receiver, ref attemptEv);
            if (attemptEv.Cancelled)
                continue;

            var actorEntity = receiver;
            if (TryComp<HeadsetComponent>(receiver, out _) && Transform(receiver).ParentUid.IsValid())
                actorEntity = Transform(receiver).ParentUid;

            if (!TryComp<ActorComponent>(actorEntity, out var actor) || !actor.PlayerSession.Channel.IsConnected)
                continue;

            if (TryComp<ActorComponent>(speaker, out var speakerActor) &&
                speakerActor.PlayerSession.UserId == actor.PlayerSession.UserId)
            {
                continue;
            }

            if (!recipients.Contains(actor.PlayerSession.Channel))
                recipients.Add(actor.PlayerSession.Channel);
        }

        return recipients.Count > 0;
    }
}
