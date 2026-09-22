using Content.Shared.GameTicking;

namespace Content.Client.Ghost;

public sealed partial class GhostSystem
{
    /// <summary>Local generation used to discard view preferences from a previous round.</summary>
    internal uint OrbitraRoundGeneration { get; private set; }

    private void InitializeOrbitraRoundState()
    {
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(_ => OrbitraRoundGeneration++);
    }
}
