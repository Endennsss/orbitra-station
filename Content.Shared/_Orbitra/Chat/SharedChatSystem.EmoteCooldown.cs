using Robust.Shared.Player;

namespace Content.Shared.Chat;

public abstract partial class SharedChatSystem
{
    /// <summary>Резервирует одну эмоцию игрока. Сервер хранит общий таймер для всех способов ввода.</summary>
    public virtual bool TryConsumeOrbitraEmote(ICommonSession player) => true;
}
