using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Orbitra.Particles;

[Serializable, NetSerializable]
public enum OrbitraParticleMaterial : byte { Generic, Metal, Stone, Wood, Glass }

/// <summary>Inherited visual material for tool and impact particles.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OrbitraParticleMaterialComponent : Component
{
    [DataField, AutoNetworkedField] public OrbitraParticleMaterial Material;
}
