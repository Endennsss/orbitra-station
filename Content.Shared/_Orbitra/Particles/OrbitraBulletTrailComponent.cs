namespace Content.Shared._Orbitra.Particles;

/// <summary>Внешний вид следа летящей пули задаётся её прототипом.</summary>
[RegisterComponent]
public sealed partial class OrbitraBulletTrailComponent : Component
{
    /// <summary>Максимальная длина следа в пикселях спрайта.</summary>
    [DataField] public int LengthPixels = 12;

    /// <summary>Цвет и непрозрачность следа у пули.</summary>
    [DataField] public Color Tint = Color.FromHex("#C8AC78");
}
