using Content.Shared._Orbitra.Surgery;
using Content.Shared.Inventory;
using Content.Shared.Medical.HealthAnalyzer;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Storage;
using Content.Shared.Temperature.Components;
using Robust.Shared.Containers;

namespace Content.Server._Orbitra.Surgery;

public sealed partial class OrbitraSurgerySystem
{
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedTransformSystem _monitorTransform = default!;

    private readonly Dictionary<(EntityUid Table, EntityUid Doctor), MonitorView> _monitorViews = new();
    private static readonly TimeSpan MonitorInterval = TimeSpan.FromSeconds(1);

    private sealed class MonitorView(NetEntity table, EntityUid patient)
    {
        public readonly NetEntity Table = table;
        public readonly EntityUid Patient = patient;
        public EntityUid? Analyzer;
        public TimeSpan NextSample;
    }

    private void InitializeMonitor()
    {
        SubscribeLocalEvent<EntRemovedFromContainerMessage>(OnMonitorItemRemoved);
        SubscribeLocalEvent<HealthAnalyzerComponent, ComponentShutdown>(OnMonitorAnalyzerShutdown);
    }

    private void UpdateMonitorPatients(EntityUid table, EntityUid? patient)
    {
        foreach (var doctor in _ui.GetActors(table, OrbitraSurgeryUiKey.Key))
        {
            var key = (table, doctor);
            if (_monitorViews.TryGetValue(key, out var view) && view.Patient == patient)
                continue;

            CloseMonitor(table, doctor);
            if (patient is { } occupant)
                _monitorViews.Add(key, new MonitorView(GetNetEntity(table), occupant));
        }
    }

    private void CloseMonitor(EntityUid table, EntityUid doctor)
    {
        if (_monitorViews.Remove((table, doctor), out var view))
            ClearMonitor(doctor, view);
    }

    private void ClearMonitor(EntityUid doctor, MonitorView view)
    {
        view.Analyzer = null;
        if (!TryComp<OrbitraSurgeryMonitorComponent>(doctor, out var monitor) ||
            !monitor.Readings.Remove(view.Table))
            return;

        if (monitor.Readings.Count == 0)
            RemComp<OrbitraSurgeryMonitorComponent>(doctor);
        else
            Dirty(doctor, monitor);
    }

    private void OnMonitorItemRemoved(EntRemovedFromContainerMessage args)
    {
        foreach (var ((_, doctor), view) in _monitorViews)
        {
            if (view.Analyzer is not { } analyzer ||
                (args.Entity != analyzer && (!HasComp<StorageComponent>(args.Entity) || !_monitorTransform.ContainsEntity(args.Entity, analyzer))))
                continue;

            // Из рук мог выпасть как сам анализатор, так и весь рюкзак с аптечкой.
            if (FindCarriedAnalyzer(doctor) is { } other)
                view.Analyzer = other;
            else
                ClearMonitor(doctor, view);
        }
    }

    private void OnMonitorAnalyzerShutdown(Entity<HealthAnalyzerComponent> analyzer, ref ComponentShutdown args)
    {
        foreach (var ((_, doctor), view) in _monitorViews)
        {
            if (view.Analyzer != analyzer.Owner)
                continue;

            if (FindCarriedAnalyzer(doctor, analyzer.Owner) is { } other)
                view.Analyzer = other;
            else
                ClearMonitor(doctor, view);
        }
    }

    private EntityUid? FindCarriedAnalyzer(EntityUid doctor, EntityUid? excluded = null)
    {
        foreach (var item in _inventory.GetHandOrInventoryEntities(doctor))
        {
            if (FindAnalyzerInItem(item, excluded) is { } analyzer)
                return analyzer;
        }

        return null;
    }

    private EntityUid? FindAnalyzerInItem(EntityUid item, EntityUid? excluded)
    {
        if (TerminatingOrDeleted(item))
            return null;

        if (item != excluded && HasComp<HealthAnalyzerComponent>(item))
            return item;

        if (!TryComp<StorageComponent>(item, out var storage))
            return null;

        // Только содержимое хранилища. Слоты КПК, органы и чужая экипировка сюда не входят.
        foreach (var child in storage.Container.ContainedEntities)
        {
            if (FindAnalyzerInItem(child, excluded) is { } analyzer)
                return analyzer;
        }

        return null;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        foreach (var ((table, doctor), view) in _monitorViews)
        {
            if (now < view.NextSample)
                continue;

            view.NextSample = now + MonitorInterval;
            if (!_ui.IsUiOpen(table, OrbitraSurgeryUiKey.Key, doctor) || TerminatingOrDeleted(view.Patient))
            {
                CloseMonitor(table, doctor);
                continue;
            }

            if (!CanOperate(table, doctor, view.Patient) || FindCarriedAnalyzer(doctor) is not { } analyzer)
            {
                ClearMonitor(doctor, view);
                continue;
            }

            view.Analyzer = analyzer;
            SampleMonitor(view.Table, doctor, view.Patient);
        }
    }

    private void SampleMonitor(NetEntity table, EntityUid doctor, EntityUid patient)
    {
        var monitor = EnsureComp<OrbitraSurgeryMonitorComponent>(doctor);
        var state = CompOrNull<MobStateComponent>(patient)?.CurrentState ?? MobState.Invalid;
        var temperature = CompOrNull<TemperatureComponent>(patient)?.Temperature;
        var blood = _bloodstream.GetOrbitraMonitoredBloodLevel(patient);
        var trend = OrbitraBloodTrend.Unknown;
        var hadPrevious = monitor.Readings.TryGetValue(table, out var previous);
        if (hadPrevious && blood is { } current && previous.BloodLevel is { } last)
        {
            trend = current.CompareTo(last) switch
            {
                < 0 => OrbitraBloodTrend.Falling,
                > 0 => OrbitraBloodTrend.Rising,
                _ => OrbitraBloodTrend.Stable,
            };
        }

        var reading = new OrbitraSurgeryReading(GetNetEntity(patient), state, temperature, blood, trend);
        if (hadPrevious && previous == reading)
            return;

        monitor.Readings[table] = reading;
        Dirty(doctor, monitor);
    }
}
