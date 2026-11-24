using Content.Shared.GameTicking;

namespace Content.Client.Ghost;

public sealed partial class GhostSystem
{
    /// <summary>Локальный номер поколения для сброса настроек отображения прошлого раунда.</summary>
    internal uint OrbitraRoundGeneration { get; private set; }

    private void InitializeOrbitraRoundState()
    {
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(_ => OrbitraRoundGeneration++);
    }
}
