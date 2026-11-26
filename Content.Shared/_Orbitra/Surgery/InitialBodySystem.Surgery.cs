using Robust.Shared.Prototypes;

namespace Content.Shared.Body;

public sealed partial class InitialBodySystem
{
    /// <summary>Предусмотрен ли орган анатомией этого вида, даже если он уже утрачен.</summary>
    public bool HasInitialOrgan(EntityUid patient, ProtoId<OrganCategoryPrototype> category) =>
        TryComp<InitialBodyComponent>(patient, out var initial) && initial.Organs.ContainsKey(category);
}
