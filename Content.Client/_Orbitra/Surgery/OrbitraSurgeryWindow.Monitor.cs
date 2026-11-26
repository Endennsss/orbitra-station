using Content.Shared._Orbitra.Surgery;
using Content.Shared.Atmos;
using Content.Shared.Mobs;
using Robust.Client.Player;

namespace Content.Client._Orbitra.Surgery;

public sealed partial class OrbitraSurgeryWindow
{
    [Dependency] private IPlayerManager _players = default!;

    internal NetEntity? MonitorTable;
    private OrbitraSurgeryReading? _reading;

    private void UpdateMonitor()
    {
        OrbitraSurgeryReading? reading = null;
        if (MonitorTable is { } table && _state?.Patient is { } patient &&
            _entity.TryGetComponent<OrbitraSurgeryMonitorComponent>(_players.LocalEntity, out var monitor) &&
            monitor.Readings.TryGetValue(table, out var sample) && sample.Patient == patient)
            reading = sample;

        if (_reading == reading)
            return;

        _reading = reading;
        PatientMonitor.Visible = reading.HasValue;
        if (reading is not { } current)
        {
            PatientMonitor.SetMessage(string.Empty);
            return;
        }

        var status = current.State switch
        {
            MobState.Alive => "health-analyzer-window-entity-alive-text",
            MobState.Critical => "health-analyzer-window-entity-critical-text",
            MobState.Dead => "health-analyzer-window-entity-dead-text",
            _ => "health-analyzer-window-entity-unknown-text",
        };
        var trend = current.Trend switch
        {
            OrbitraBloodTrend.Falling => "orbitra-surgery-monitor-falling",
            OrbitraBloodTrend.Rising => "orbitra-surgery-monitor-rising",
            OrbitraBloodTrend.Stable => "orbitra-surgery-monitor-stable",
            _ => "orbitra-surgery-monitor-first",
        };
        var unknown = _loc.GetString("health-analyzer-window-entity-unknown-value-text");
        var temperature = current.Temperature is { } kelvin ? $"{kelvin - Atmospherics.T0C:F1} °C" : unknown;
        var blood = current.BloodLevel is { } level ? $"{level * 100:F1} %" : unknown;
        PatientMonitor.SetMessage(_loc.GetString("orbitra-surgery-monitor",
            ("state", _loc.GetString(status)), ("temperature", temperature), ("blood", blood), ("trend", _loc.GetString(trend))));
    }
}
