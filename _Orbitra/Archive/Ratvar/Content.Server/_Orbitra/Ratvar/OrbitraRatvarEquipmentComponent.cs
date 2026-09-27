namespace Content.Server._Orbitra.Ratvar;

/// <summary>Cache equipment authorization and native-effect configuration.</summary>
[RegisterComponent]
public sealed partial class OrbitraRatvarEquipmentComponent : Component
{
    [DataField] public string Slot = "outerClothing";
    [DataField] public bool Cloak;
    [DataField] public bool Spectacles;
    [DataField] public float Visibility = 0.55f;
    [DataField] public TimeSpan FadeTime = TimeSpan.FromSeconds(3);
    [DataField] public TimeSpan EyeDamageInterval = TimeSpan.FromSeconds(20);
    [DataField] public int EyeDamageCap = 7;
    [DataField] public TimeSpan RecoveryDelay = TimeSpan.FromSeconds(60);
    public EntityUid? Wearer;
    public bool Active;
    public bool OwnsStealth;
    public TimeSpan EquippedAt;
}

/// <summary>Only this source's temporary ghost visibility, not observer hearing or admin sight.</summary>
[RegisterComponent]
public sealed partial class ActiveOrbitraRatvarSpectaclesComponent : Component
{
    public EntityUid Source;
}

/// <summary>Eye strain belongs to the body and survives rapid equipment swapping.</summary>
[RegisterComponent]
public sealed partial class OrbitraRatvarEyeStrainComponent : Component
{
    public int Applied;
    public TimeSpan NextDamage;
    public TimeSpan? RecoverAt;
}
