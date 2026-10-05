// ReSharper disable CheckNamespace
namespace Content.Server.StationEvents.Components;

public sealed partial class StationEventComponent
{
    /// <summary>
    /// Excludes this event from automatic selection without disabling administrative starts.
    /// </summary>
    [DataField]
    public bool ManualOnly;
}
