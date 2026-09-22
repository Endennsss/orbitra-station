namespace Content.Shared._Orbitra.Movement;

using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

[ByRefEvent]
public record struct OrbitraRollAttemptEvent(float StaminaCost, bool Cancelled = false);

[ByRefEvent]
public record struct OrbitraJumpAttemptEvent(float StaminaCost, bool Cancelled = false);

[Serializable, NetSerializable]
public sealed partial class OrbitraStandDoAfterEvent : SimpleDoAfterEvent;
