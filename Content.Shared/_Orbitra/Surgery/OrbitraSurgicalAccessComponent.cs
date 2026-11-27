using Robust.Shared.GameStates;

namespace Content.Shared._Orbitra.Surgery;

/// <summary>Открытая зона и временная повязка сохраняются на пациенте при эвакуации.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OrbitraSurgicalAccessComponent : Component
{
    [DataField, AutoNetworkedField] public Dictionary<SurgeryZone, SurgeryAccess> Zones = new();
    // Клиенту нужен только факт перевязки для применения бинта. Отсчёт и снятие ведёт сервер.
    [DataField, AutoNetworkedField] public Dictionary<SurgeryZone, float> DressingRemaining = new();
    [DataField] public float Elapsed;
}
