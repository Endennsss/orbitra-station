using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Orbitra.Ratvar;

/// <summary>Original authority and grid for an interruptible window conversion.</summary>
[Serializable, NetSerializable]
public sealed partial class OrbitraRatvarWindowEvent : DoAfterEvent
{
    public NetEntity Mind;
    public NetEntity Rule;
    public NetEntity Grid;
    public NetEntity Map;
    public override DoAfterEvent Clone() => (OrbitraRatvarWindowEvent) MemberwiseClone();
}
