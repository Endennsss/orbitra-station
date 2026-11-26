using Robust.Shared.GameStates;

namespace Content.Shared._Orbitra.Surgery;

/// <summary>Хирургическая роль инструмента, общая для обычной и улучшенных моделей.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OrbitraSurgicalToolComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public string Kind = default!;
}
