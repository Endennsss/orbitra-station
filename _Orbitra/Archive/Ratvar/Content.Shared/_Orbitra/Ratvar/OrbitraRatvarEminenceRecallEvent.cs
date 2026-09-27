using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Orbitra.Ratvar;

/// <summary>A server-authorized recall tied to the original observer, target mind and destination.</summary>
[Serializable, NetSerializable]
public sealed partial class OrbitraRatvarEminenceRecallEvent : SimpleDoAfterEvent
{
    public NetEntity Observer;
    public NetEntity TargetMind;
    public NetEntity Destination;
    public uint Sequence;
}
