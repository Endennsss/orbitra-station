namespace Content.Server._Orbitra.Ratvar;

/// <summary>Restricts an Eminence-requested native station event to its original cult station.</summary>
[RegisterComponent]
public sealed partial class OrbitraRatvarEventTargetComponent : Component
{
    /// <summary>Server-assigned station, never a client-provided coordinate.</summary>
    public EntityUid Station;
}
