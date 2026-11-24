// ReSharper disable CheckNamespace
namespace Content.Server.StationEvents.Components;

public sealed partial class StationEventComponent
{
    /// <summary>
    /// Исключает событие из случайного выбора, сохраняя ручной запуск администратора.
    /// </summary>
    [DataField]
    public bool ManualOnly;
}
