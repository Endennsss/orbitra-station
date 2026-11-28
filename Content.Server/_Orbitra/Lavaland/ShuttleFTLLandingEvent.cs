namespace Content.Server.Shuttles.Events;

/// <summary>Маршрут может отложить выход из БСС либо выбрать точную пару портов перед стыковкой.</summary>
[ByRefEvent]
public record struct ShuttleFTLLandingEvent
{
    public bool Hold;
    public DockingConfig? Dock;
}
