using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Orbitra.Ratvar;

/// <summary>Original authority for one weapon ritual.</summary>
[Serializable, NetSerializable]
public sealed partial class OrbitraRatvarEnchantEvent : DoAfterEvent
{
    public NetEntity Rule;
    public NetEntity Mind;
    public override DoAfterEvent Clone() => (OrbitraRatvarEnchantEvent) MemberwiseClone();
}
