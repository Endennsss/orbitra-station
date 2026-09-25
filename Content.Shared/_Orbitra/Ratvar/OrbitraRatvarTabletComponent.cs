using Content.Shared.DoAfter;
using Robust.Shared.Serialization;
using Robust.Shared.Map;

namespace Content.Shared._Orbitra.Ratvar;

/// <summary>A tablet can channel only one scripture or conversion at a time.</summary>
[RegisterComponent]
public sealed partial class OrbitraRatvarTabletComponent : Component
{
    [DataField] public bool RequireHeld = true;
    [DataField] public bool AllowScriptures = true;
    [DataField] public TimeSpan MessageCooldown = TimeSpan.FromSeconds(2);
    public bool Busy;
    public EntityUid? PendingTarget;
    public EntityUid? RitualMind;
    public EntityUid? TargetMind;
    public TimeSpan NextMessage;
    /// <summary>Caster position at ritual start; target movement is checked against its sigil instead.</summary>
    public EntityCoordinates? RitualOrigin;
    /// <summary>The original submission sigil must remain valid for the entire conversion.</summary>
    public EntityUid? RitualSigil;
}

[Serializable, NetSerializable]
public sealed partial class OrbitraRatvarConversionEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class OrbitraRatvarScriptureEvent : DoAfterEvent
{
    [DataField] public string Scripture = string.Empty;
    public override DoAfterEvent Clone() => (OrbitraRatvarScriptureEvent) MemberwiseClone();
}
