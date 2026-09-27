using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Orbitra.Ratvar;

/// <summary>Preserves the repairer's original mind and cult during the operation.</summary>
[Serializable, NetSerializable]
public sealed partial class OrbitraRatvarFabricatorEvent : DoAfterEvent
{
    public NetEntity Mind;
    public NetEntity Rule;
    public override DoAfterEvent Clone() => (OrbitraRatvarFabricatorEvent) MemberwiseClone();
}
