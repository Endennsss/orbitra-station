using Robust.Shared.Player;

namespace Content.Shared.Chat;

public abstract partial class SharedChatSystem
{
    /// <summary>Reserves one player emote; the server owns the cooldown shared by all input paths.</summary>
    public virtual bool TryConsumeOrbitraEmote(ICommonSession player) => true;
}
