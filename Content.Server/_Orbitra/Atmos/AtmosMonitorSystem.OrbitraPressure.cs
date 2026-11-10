using Content.Shared.Atmos.Monitor;
using Robust.Shared.Timing;

namespace Content.Server.Atmos.Monitor.Systems;

public sealed partial class AtmosMonitorSystem
{
    [Dependency] private IGameTiming _orbitraTiming = default!;

    private void UpdateOrbitraPressureTrend(AtmosMonitorComponent monitor, float pressure,
        ref AtmosAlarmType state, ref AtmosMonitorThresholdTypeFlags alarms)
    {
        if (monitor.MonitorsPipeNet || monitor.PressureThreshold is not { Ignore: false })
        {
            monitor.OrbitraPressureFalling = false;
            monitor.OrbitraPressureSampleTime = TimeSpan.Zero;
            return;
        }

        var now = _orbitraTiming.CurTime;
        var elapsed = (now - monitor.OrbitraPressureSampleTime).TotalSeconds;
        if (elapsed >= 0.5)
        {
            monitor.OrbitraPressureFalling = monitor.OrbitraPressureSampleTime > TimeSpan.Zero &&
                elapsed <= 6 && monitor.OrbitraPressureSample - pressure >= 0.25 * elapsed;
            monitor.OrbitraPressureSample = pressure;
            monitor.OrbitraPressureSampleTime = now;
        }

        if (monitor.OrbitraPressureFalling && state == AtmosAlarmType.Normal)
        {
            state = AtmosAlarmType.Warning;
            alarms |= AtmosMonitorThresholdTypeFlags.Pressure;
        }
    }
}
