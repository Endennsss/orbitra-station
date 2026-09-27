namespace Content.Server._Orbitra.Ratvar;

/// <summary>Passive submission sigil and the authority of its one current conversion.</summary>
[RegisterComponent]
public sealed partial class OrbitraRatvarSubmissionComponent : Component
{
    [DataField] public float Radius = 0.65f;
    public EntityUid? Target;
    public EntityUid? Mind;
    public EntityUid? Rule;
}
