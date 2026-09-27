using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Orbitra.Ratvar;

[Serializable, NetSerializable]
public sealed partial class OrbitraRatvarTargetSpellEvent : SimpleDoAfterEvent;
