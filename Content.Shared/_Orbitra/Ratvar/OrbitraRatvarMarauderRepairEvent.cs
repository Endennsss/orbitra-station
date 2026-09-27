using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Orbitra.Ratvar;

/// <summary>Native tool operation completing one marauder repair.</summary>
[Serializable, NetSerializable]
public sealed partial class OrbitraRatvarMarauderRepairEvent : SimpleDoAfterEvent;
