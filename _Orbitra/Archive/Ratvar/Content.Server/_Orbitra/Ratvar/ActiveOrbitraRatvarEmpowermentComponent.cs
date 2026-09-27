using Robust.Shared.Prototypes;
using Content.Shared._Orbitra.Ratvar;

namespace Content.Server._Orbitra.Ratvar;

/// <summary>One prepared, unpaid tablet spell bound to its original caster and cult.</summary>
[RegisterComponent]
public sealed partial class ActiveOrbitraRatvarEmpowermentComponent : Component
{
    public EntityUid User;
    public EntityUid Mind;
    public EntityUid Rule;
    public ProtoId<OrbitraRatvarScripturePrototype> Scripture;
    public TimeSpan Expires;
    public EntityUid? Target;
}

/// <summary>Temporary defence of a particular controlled body, never transferable with its mind.</summary>
[RegisterComponent]
public sealed partial class ActiveOrbitraRatvarVanguardComponent : Component
{
    public EntityUid Mind;
    public EntityUid Rule;
    public TimeSpan Expires;
}
