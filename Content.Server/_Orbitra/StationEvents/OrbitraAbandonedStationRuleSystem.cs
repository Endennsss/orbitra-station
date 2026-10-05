using Content.Server.Chat.Managers;
using Content.Server.Fax;
using Content.Server.GameTicking;
using Content.Server.RoundEnd;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Components;
using Content.Server.StationEvents.Components;
using Content.Server.StationEvents.Events;
using Content.Shared.Database;
using Content.Shared.AlertLevel;
using Content.Shared.Cargo.Components;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.Fax.Components;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Popups;
using Robust.Shared.Localization;
using Robust.Shared.Random;
using Robust.Shared.Audio;

namespace Content.Server._Orbitra.StationEvents;

/// <summary>
/// Controls a round-wide communications outage without changing persistent server settings.
/// </summary>
public sealed partial class OrbitraAbandonedStationRuleSystem : StationEventSystem<OrbitraAbandonedStationRuleComponent>
{
    [Dependency] private ILocalizationManager _loc = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private RoundEndSystem _roundEnd = default!;
    [Dependency] private EmergencyShuttleSystem _emergency = default!;
    [Dependency] private ArrivalsSystem _arrivals = default!;
    [Dependency] private FaxSystem _fax = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private AlertLevelSystem _alertLevel = default!;

    // Ссылка остаётся до следующего раунда, даже после ручной остановки события.
    private EntityUid? _acceptedRule;

    /// <summary>
    /// Whether purchases, external faxes, arrivals and ordinary evacuation are suspended.
    /// </summary>
    public bool IsIsolationActive => GameTicker.RunLevel == GameRunLevel.InRound &&
        TryComp<OrbitraAbandonedStationRuleComponent>(_acceptedRule, out var rule) &&
        rule.Phase == OrbitraAbandonedStationPhase.Isolated && GameTicker.IsGameRuleActive(_acceptedRule.Value);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
        SubscribeLocalEvent<OrbitraAbandonedStationRuleComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _acceptedRule = null;
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent args)
    {
        if (GameTicker.RunLevel != GameRunLevel.InRound && _acceptedRule is { } rule && Exists(rule))
            GameTicker.EndGameRule(rule);
    }

    private void OnShutdown(Entity<OrbitraAbandonedStationRuleComponent> ent, ref ComponentShutdown args)
    {
        Finish(ent);
    }

    protected override void Added(EntityUid uid, OrbitraAbandonedStationRuleComponent component,
        GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        // Проверка до старта не расходует лимит успешных запусков в истории правил.
        if (!CanBegin((uid, component), out var reason))
        {
            _chat.SendAdminAlert(_loc.GetString(reason));
            GameTicker.EndGameRule(uid, gameRule);
        }
    }

    protected override void Started(EntityUid uid, OrbitraAbandonedStationRuleComponent component,
        GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        if (TryBegin((uid, component)))
            return;

        GameTicker.EndGameRule(uid, gameRule);
    }

    /// <summary>
    /// Checks the round age, occurrence limit and evacuation status before accepting an outage.
    /// </summary>
    public bool CanBegin(Entity<OrbitraAbandonedStationRuleComponent> ent, out LocId reason)
    {
        reason = "orbitra-abandoned-station-start-unavailable";
        if (GameTicker.RunLevel != GameRunLevel.InRound ||
            !TryComp<StationEventComponent>(ent, out var stationEvent) ||
            GameTicker.RoundDuration() < TimeSpan.FromMinutes(stationEvent.EarliestStart))
            return false;

        reason = "orbitra-abandoned-station-already-started";
        if (_acceptedRule != null)
            return false;

        reason = "orbitra-abandoned-station-evacuation-active";
        return !_roundEnd.IsRoundEndRequested() && !_emergency.EmergencyShuttleArrived;
    }

    /// <summary>
    /// Accepts the event and begins its warning period exactly once per round.
    /// </summary>
    public bool TryBegin(Entity<OrbitraAbandonedStationRuleComponent> ent)
    {
        if (!CanBegin(ent, out var reason))
        {
            _chat.SendAdminAlert(_loc.GetString(reason));
            return false;
        }

        _acceptedRule = ent;
        ent.Comp.Phase = OrbitraAbandonedStationPhase.Warning;
        ent.Comp.IsolationAt = GameTicker.RoundDuration() + ent.Comp.WarningDuration;
        ent.Comp.RecoveryAt = TimeSpan.FromSeconds(RobustRandom.NextDouble(
            ent.Comp.RecoveryMinimum.TotalSeconds, ent.Comp.RecoveryMaximum.TotalSeconds));
        Announce("orbitra-abandoned-station-warning", ent.Comp.WarningSound);
        AdminLogManager.Add(LogType.EventStarted, LogImpact.High, $"Abandoned station warning started.");
        return true;
    }

    protected override void ActiveTick(EntityUid uid, OrbitraAbandonedStationRuleComponent component,
        GameRuleComponent gameRule, float frameTime)
    {
        if (GameTicker.RunLevel != GameRunLevel.InRound)
            return;

        var now = GameTicker.RoundDuration();
        if (component.Phase == OrbitraAbandonedStationPhase.Warning && now >= component.IsolationAt)
        {
            if (_roundEnd.IsRoundEndRequested() || _emergency.EmergencyShuttleArrived)
            {
                Announce("orbitra-abandoned-station-interrupted");
                GameTicker.EndGameRule(uid, gameRule);
                return;
            }

            component.Phase = OrbitraAbandonedStationPhase.Isolated;
            SetIsolationAlerts(component);
            if (component.RecoveryAt <= now)
                component.RecoveryAt = now + component.RetryInterval;
            RefreshServices();
            Announce("orbitra-abandoned-station-isolated", component.IsolationSound);
            AdminLogManager.Add(LogType.EventStarted, LogImpact.High, $"Abandoned station isolation activated.");
        }

        if (component.Phase != OrbitraAbandonedStationPhase.Isolated || now < component.RecoveryAt)
            return;

        component.RecoveryAttempts++;
        var recovered = RobustRandom.Prob(component.RecoveryChance);
        AdminLogManager.Add(LogType.EventAnnounced,
            $"Abandoned station recovery attempt {component.RecoveryAttempts}: {recovered}.");
        if (!recovered)
        {
            component.RecoveryAt = now + component.RetryInterval;
            return;
        }

        GameTicker.EndGameRule(uid, gameRule);
        Announce("orbitra-abandoned-station-recovered");
        _roundEnd.RequestRoundEnd(checkCooldown: false, cantRecall: true);
    }

    protected override void Ended(EntityUid uid, OrbitraAbandonedStationRuleComponent component,
        GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        Finish((uid, component));
        base.Ended(uid, component, gameRule, args);
    }

    private void Finish(Entity<OrbitraAbandonedStationRuleComponent> ent)
    {
        var isolated = ent.Comp.Phase == OrbitraAbandonedStationPhase.Isolated;
        ent.Comp.Phase = OrbitraAbandonedStationPhase.Finished;
        ent.Comp.IsolationAt = TimeSpan.Zero;
        ent.Comp.RecoveryAt = TimeSpan.Zero;
        RestoreIsolationAlerts(ent.Comp);
        if (!isolated || GameTicker.RunLevel != GameRunLevel.InRound)
            return;

        _arrivals.OrbitraResumeArrivals();
        RefreshServices();
    }

    private void RefreshServices()
    {
        _fax.OrbitraRefreshFaxes();
        RaiseLocalEvent(RoundEndSystemChangedEvent.Default);
    }

    private void SetIsolationAlerts(OrbitraAbandonedStationRuleComponent rule)
    {
        var stations = EntityQueryEnumerator<StationEventEligibleComponent, AlertLevelComponent>();
        while (stations.MoveNext(out var station, out _, out var alert))
        {
            // Дельта и другие заблокированные аварийные уровни имеют приоритет.
            if (alert.IsLevelLocked || alert.CurrentAlertLevel == rule.IsolationAlert)
                continue;

            rule.PreviousAlerts[station] = alert.CurrentAlertLevel;
            _alertLevel.SetLevel((station, alert), rule.IsolationAlert,
                playSound: false, announce: false, force: true);
        }
    }

    private void RestoreIsolationAlerts(OrbitraAbandonedStationRuleComponent rule)
    {
        foreach (var (station, previous) in rule.PreviousAlerts)
        {
            // Не отменяем новый аварийный уровень или административную смену кода.
            if (!TryComp<AlertLevelComponent>(station, out var alert) || alert.CurrentAlertLevel != rule.IsolationAlert)
                continue;

            _alertLevel.SetLevel((station, alert), previous,
                playSound: false, announce: false, force: true);
        }

        rule.PreviousAlerts.Clear();
    }

    private void Announce(LocId message, SoundSpecifier? sound = null)
    {
        var stations = EntityQueryEnumerator<StationEventEligibleComponent>();
        while (stations.MoveNext(out var uid, out _))
            ChatSystem.DispatchStationAnnouncement(uid, _loc.GetString(message),
                _loc.GetString("orbitra-abandoned-station-announcer"), announcementSound: sound);
    }

    /// <summary>
    /// Reports an unavailable service to the acting player, without spending resources.
    /// </summary>
    public bool CanUseService(EntityUid? user = null)
    {
        if (!IsIsolationActive)
            return true;
        if (user is { } actor && Exists(actor))
            _popup.PopupEntity(_loc.GetString("orbitra-abandoned-station-service-unavailable"), user.Value, user.Value);
        return false;
    }

    /// <summary>
    /// Gets the main station containing a fax, excluding remote grids owned by that station.
    /// </summary>
    private EntityUid? GetLocalStation(EntityUid uid)
    {
        if (!TryComp(uid, out TransformComponent? xform))
            return null;

        var station = StationSystem.GetOwningStation(uid, xform);
        if (!HasComp<StationEventEligibleComponent>(station) ||
            StationSystem.GetLargestGrid(station.Value) is not { } mainGrid ||
            HasComp<TradeStationComponent>(xform.GridUid) ||
            xform.MapUid != Transform(mainGrid).MapUid)
            return null;

        return station;
    }

    /// <summary>
    /// During isolation, fax traffic must remain within the same main station.
    /// Unknown senders, including direct external administrative deliveries, are rejected.
    /// </summary>
    public bool CanFax(EntityUid endpoint, string? otherAddress)
    {
        if (!IsIsolationActive)
            return true;
        if (string.IsNullOrEmpty(otherAddress) || GetLocalStation(endpoint) is not { } station)
            return false;

        var query = EntityQueryEnumerator<FaxMachineComponent, DeviceNetworkComponent>();
        while (query.MoveNext(out var uid, out _, out var network))
        {
            if (network.Address == otherAddress)
                return GetLocalStation(uid) == station;
        }
        return false;
    }
}
