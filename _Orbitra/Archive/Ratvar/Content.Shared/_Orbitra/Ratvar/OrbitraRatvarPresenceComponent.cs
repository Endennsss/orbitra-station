using Robust.Shared.GameStates;

namespace Content.Shared._Orbitra.Ratvar;

/// <summary>Only Ratvar's own sprite is drawn above FOV; surrounding entities remain hidden.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class OrbitraRatvarPresenceComponent : Component;
