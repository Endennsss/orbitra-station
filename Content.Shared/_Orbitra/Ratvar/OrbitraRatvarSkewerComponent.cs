using Robust.Shared.GameStates;

namespace Content.Shared._Orbitra.Ratvar;

/// <summary>Prevents raised skewers from physically ejecting their own buckled occupants.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class OrbitraRatvarSkewerComponent : Component;
