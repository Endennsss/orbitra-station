using Robust.Shared.GameStates;

namespace Content.Shared._Orbitra.Lavaland;

/// <summary>Сторона маршрута, с которой пассажир вызывает или отправляет шахтёрский шаттл.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OrbitraLavalandTerminalComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public LavalandSide Side;

    /// <summary>Парный терминал, через который сервер передаёт сводки на другую сторону маршрута.</summary>
    public EntityUid? RadioPeer;
}
