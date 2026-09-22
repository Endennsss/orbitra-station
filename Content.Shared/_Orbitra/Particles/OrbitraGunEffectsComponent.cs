namespace Content.Shared._Orbitra.Particles;

/// <summary>Prototype-defined cosmetic muzzle parameters; does not affect weapon mechanics.</summary>
[RegisterComponent]
public sealed partial class OrbitraGunEffectsComponent : Component
{
    [DataField] public Color Color = Color.FromHex("#FFE3A0");
    [DataField] public float Radius = 1.5f;
    [DataField] public float Energy = 3f;
    [DataField] public bool Smoke;
}
