using Content.Shared.Damage;
using Robust.Shared.Audio;

namespace Content.Server._Orbitra.Ratvar;

/// <summary>Cult-owned trap wiring and prototype-tunable activation parameters.</summary>
[RegisterComponent]
public sealed partial class OrbitraRatvarTrapComponent : Component
{
    /// <summary>Native effect performed when a valid signal arrives.</summary>
    [DataField] public OrbitraRatvarTrapKind Kind;
    /// <summary>Whether the device can have outgoing connections.</summary>
    [DataField] public bool Sender;
    /// <summary>Whether the device accepts incoming connections.</summary>
    [DataField] public bool Receiver;
    /// <summary>Maximum outgoing connections; duplicate links toggle removal.</summary>
    [DataField] public int MaxOutputs = 8;
    /// <summary>Maximum same-grid connection length, in tiles.</summary>
    [DataField] public float LinkRange = 16;
    /// <summary>Minimum interval between activations.</summary>
    [DataField] public TimeSpan Cooldown = TimeSpan.FromSeconds(0.5);
    /// <summary>Signal delay, or the duration of the flipper's active visual.</summary>
    [DataField] public TimeSpan Delay = TimeSpan.FromSeconds(0.5);
    /// <summary>Uninterrupted time needed to free an impaled creature.</summary>
    [DataField] public TimeSpan EscapeTime = TimeSpan.FromSeconds(5);
    /// <summary>Target distance for the native throwing system.</summary>
    [DataField] public float ThrowRange = 6;
    /// <summary>Throw velocity before native friction compensation.</summary>
    [DataField] public float ThrowSpeed = 4;
    /// <summary>Skewer damage, subject to normal damage resistance.</summary>
    [DataField] public DamageSpecifier Damage = new();
    /// <summary>Positional activation feedback.</summary>
    [DataField] public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/Machines/button.ogg");
    public readonly HashSet<EntityUid> Outputs = new();
    public TimeSpan NextUse;
    public bool Extended;
}

public enum OrbitraRatvarTrapKind : byte { Lever, Plate, Delay, Skewer, Flipper }

/// <summary>One bounded signal shared by all branches, including delayed branches.</summary>
public sealed class OrbitraRatvarTrapPulse(EntityUid rule, EntityUid grid)
{
    public readonly EntityUid Rule = rule;
    public readonly EntityUid Grid = grid;
    public readonly HashSet<EntityUid> Visited = new();
}

/// <summary>Only traps awaiting a delayed signal or visual reset are polled.</summary>
[RegisterComponent]
public sealed partial class ActiveOrbitraRatvarTrapComponent : Component
{
    public OrbitraRatvarTrapPulse? Pulse;
    public TimeSpan FinishAt;
    public EntityUid Rule;
    public EntityUid Grid;
}

/// <summary>Private tablet selection; cannot be inherited by another user or cult.</summary>
[RegisterComponent]
public sealed partial class OrbitraRatvarTrapLinkComponent : Component
{
    public EntityUid Source;
    public EntityUid User;
    public EntityUid Rule;
}
