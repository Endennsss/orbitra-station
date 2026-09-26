using Content.Shared.DoAfter;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;

namespace Content.Shared._Orbitra.Ratvar;

/// <summary>Server-selected floor and authority snapshot for an interruptible fabrication.</summary>
[Serializable, NetSerializable]
public sealed partial class OrbitraRatvarFloorEvent : DoAfterEvent
{
    public NetEntity Mind;
    public NetEntity Rule;
    public NetEntity Grid;
    public NetEntity Map;
    public Vector2i Indices;
    public int SourceTile;
    public override DoAfterEvent Clone() => (OrbitraRatvarFloorEvent) MemberwiseClone();
}
