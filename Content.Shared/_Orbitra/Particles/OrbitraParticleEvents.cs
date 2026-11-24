using Content.Shared.Damage;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Orbitra.Particles;

/// <summary>Подтверждённое попадание в ближнем бою после расчёта урона.</summary>
[ByRefEvent]
public readonly record struct OrbitraParticleMeleeHitEvent(EntityUid User, DamageSpecifier Damage);

/// <summary>Сохраняет электрическое состояние до отключения машины штатной поломкой.</summary>
[ByRefEvent]
public readonly record struct OrbitraParticleBeforeBreakEvent;

/// <summary>Одно событие короткого выброса для ближайших клиентов.</summary>
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
