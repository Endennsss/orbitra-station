using Content.Shared.Roles.Components;

namespace Content.Shared._Orbitra.Ratvar;

/// <summary>Persistent Ratvar allegiance on a mind role, independent of the controlled body.</summary>
[RegisterComponent]
public sealed partial class OrbitraRatvarRoleComponent : BaseMindRoleComponent
{
    /// <summary>Owning cult rule. Assigned by the server, never supplied by clients.</summary>
    public EntityUid? Rule;
    /// <summary>Marauder allegiance cannot acquire scripture or conversion privileges after a body transfer.</summary>
    [DataField] public bool Marauder;
    /// <summary>Shared server-side communication cooldown across bodies and tablets.</summary>
    public TimeSpan NextMessage;
}
