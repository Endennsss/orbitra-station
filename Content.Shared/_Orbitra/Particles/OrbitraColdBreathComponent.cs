using Robust.Shared.GameStates;

namespace Content.Shared._Orbitra.Particles;

/// <summary>Quantized ambient condensation potential. Zero disables cosmetic exhalation.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OrbitraColdBreathComponent : Component
{
    [AutoNetworkedField] public byte Intensity;
}
