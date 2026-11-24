using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Orbitra.Particles;

[Serializable, NetSerializable]
public enum OrbitraParticleMaterial : byte { Generic, Metal, Stone, Wood, Glass, Ice }

/// <summary>Наследуемый материал для частиц от инструментов и попаданий.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OrbitraParticleMaterialComponent : Component
{
    [DataField, AutoNetworkedField] public OrbitraParticleMaterial Material;
}
