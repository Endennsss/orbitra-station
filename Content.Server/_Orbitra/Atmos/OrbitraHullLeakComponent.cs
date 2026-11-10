using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Orbitra.Atmos;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class OrbitraHullLeakComponent : Component
{
    [DataField] public float DamageThreshold = 150f;
    [DataField] public float MolesPerSecond = 4f;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan PatchedUntil;
}

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class OrbitraActiveHullLeakComponent : Component
{
    [AutoPausedField] public TimeSpan NextUpdate;
    [AutoPausedField] public TimeSpan NextMist;
    public EntityUid? Hiss;
    public bool Flowing;
}
