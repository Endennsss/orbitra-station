using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Orbitra.Ratvar;

/// <summary>Completes one explicitly selected APC operation.</summary>
[Serializable, NetSerializable]
public sealed partial class OrbitraRatvarCogEvent : DoAfterEvent
{
    public bool OpenPanel;
    public NetEntity Rule;
    public NetEntity Mind;
    public override DoAfterEvent Clone() => (OrbitraRatvarCogEvent) MemberwiseClone();
}

/// <summary>Removes the original installed cog, never a replacement.</summary>
[Serializable, NetSerializable]
public sealed partial class OrbitraRatvarCogRemoveEvent : DoAfterEvent
{
    public NetEntity Cog;
    public override DoAfterEvent Clone() => (OrbitraRatvarCogRemoveEvent) MemberwiseClone();
}
