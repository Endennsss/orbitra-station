using Content.Shared.Damage;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Orbitra.Particles;

/// <summary>A confirmed melee hit, raised only after damage resolution.</summary>
[ByRefEvent]
public readonly record struct OrbitraParticleMeleeHitEvent(EntityUid User, DamageSpecifier Damage);

/// <summary>Captures electrical state before ordinary breakage handlers disable the machine.</summary>
[ByRefEvent]
public readonly record struct OrbitraParticleBeforeBreakEvent;

/// <summary>One transient burst for nearby clients, not one message per particle.</summary>
[Serializable, NetSerializable]
public sealed class OrbitraParticleBurstEvent(NetEntity source, NetCoordinates coordinates, string effect, float direction, Color? tint = null, bool impactMark = false) : EntityEventArgs
{
    public readonly NetEntity Source = source;
    public readonly NetCoordinates Coordinates = coordinates;
    public readonly string Effect = effect;
    public readonly float Direction = direction;
    public readonly Color? Tint = tint;
    public readonly bool ImpactMark = impactMark;
}
