using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Orbitra.Surgery;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OrbitraFragmentEvidenceComponent : Component
{
    [DataField, AutoNetworkedField] public EntProtoId? Projectile;
}
