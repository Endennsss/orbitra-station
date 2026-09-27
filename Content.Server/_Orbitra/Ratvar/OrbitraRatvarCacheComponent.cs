using Robust.Shared.Prototypes;

namespace Content.Server._Orbitra.Ratvar;

/// <summary>One shared issuance cooldown for all visitors of a particular Cache.</summary>
[RegisterComponent]
public sealed partial class OrbitraRatvarCacheComponent : Component
{
    [DataField] public TimeSpan Cooldown = TimeSpan.FromMinutes(4);
    [DataField] public List<EntProtoId> Choices = [];
    public TimeSpan NextUse;
}
