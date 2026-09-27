using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.Serialization;

namespace Content.Shared._Orbitra.Ratvar;

/// <summary>A tablet can channel only one scripture at a time.</summary>
[RegisterComponent]
public sealed partial class OrbitraRatvarTabletComponent : Component
{
    [DataField] public bool RequireHeld = true;
    [DataField] public bool AllowScriptures = true;
    [DataField] public TimeSpan MessageCooldown = TimeSpan.FromSeconds(2);
    [DataField] public SoundSpecifier? ScriptureSound;
    public bool Busy;
    public EntityUid? RitualMind;
    /// <summary>Cult authorizing the currently channeled scripture.</summary>
    public EntityUid? ScriptureRule;
    public TimeSpan NextMessage;
}

[Serializable, NetSerializable]
public sealed partial class OrbitraRatvarConversionEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class OrbitraRatvarScriptureEvent : DoAfterEvent
{
    [DataField] public string Scripture = string.Empty;
    public override DoAfterEvent Clone() => (OrbitraRatvarScriptureEvent) MemberwiseClone();
}
