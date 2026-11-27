using Content.Client._Orbitra.Parallax;

#pragma warning disable IDE0130
namespace Content.Client.Parallax.Data;

public sealed partial class ParallaxLayerConfig
{
    /// <summary>
    /// Одинаковые звёздные слои заменяются одним проходом. Distant и Near разделяют звёзды по сторонам туманности.
    /// Accents сохраняет исходную текстуру и добавляет поверх неё редкие звёзды переднего плана.
    /// </summary>
    [DataField]
    internal OrbitraSkyLayer? OrbitraSky { get; private set; }
}
