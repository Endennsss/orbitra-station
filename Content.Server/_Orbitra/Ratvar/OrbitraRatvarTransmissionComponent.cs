namespace Content.Server._Orbitra.Ratvar;

/// <summary>Local access to shared cult energy without an independent energy reserve.</summary>
[RegisterComponent]
public sealed partial class OrbitraRatvarTransmissionComponent : Component
{
    /// <summary>Coverage radius in metres on the same grid.</summary>
    [DataField] public float Radius = 6f;

    /// <summary>Index owner used for cleanup during rebinding and removal.</summary>
    public EntityUid? IndexedRule;
}
