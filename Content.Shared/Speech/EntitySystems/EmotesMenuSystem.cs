using Content.Shared.Chat;

namespace Content.Shared.Speech.EntitySystems;

public sealed partial class EmotesMenuSystem : EntitySystem
{
    [Dependency] private SharedChatSystem _chat = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeAllEvent<PlayEmoteMessage>(OnPlayEmote);
    }

    private void OnPlayEmote(PlayEmoteMessage msg, EntitySessionEventArgs args)
    {
        var player = args.SenderSession.AttachedEntity;
        if (!player.HasValue)
            return;

        if (!ProtoMan.Resolve(msg.ProtoId, out var proto) || proto.ChatTriggers.Count == 0)
            return;

        // Orbitra added start - единый серверный кулдаун меню H и быстрых эмоций.
        if (!_chat.AllowedToUseEmote(player.Value, proto) || !_chat.TryConsumeOrbitraEmote(args.SenderSession))
            return;
        // Orbitra added end
        _chat.TryEmoteWithChat(player.Value, msg.ProtoId);
    }
}
