using Content.Server.Station.Components;
using Content.Shared._Orbitra.Ratvar;
using Content.Shared.Database;
using Robust.Shared.Prototypes;

namespace Content.Server._Orbitra.Ratvar;

public sealed partial class OrbitraRatvarEminenceSystem
{
    // Только проверенные штатные события с ограничением станции; произвольных ID из BUI нет.
    private static readonly EntProtoId AnomalyEvent = "OrbitraRatvarAnomalyEvent";
    private static readonly EntProtoId GridCheckEvent = "PowerGridCheck";

    /// <summary>Requests one whitelisted native event with a server-owned station restriction.</summary>
    public bool TryManipulate(Entity<OrbitraRatvarEminenceAvatarComponent> avatar, OrbitraRatvarEminenceReality effect)
    {
        if (!Enum.IsDefined(effect) || !CanManipulate(avatar, out var observer, out _)) return false;
        var cult = Comp<OrbitraRatvarRuleComponent>(observer.Comp.Rule);
        var prototype = effect == OrbitraRatvarEminenceReality.Anomaly ? AnomalyEvent : GridCheckEvent;
        // Резервируем стоимость и перезарядку до событий добавления правила, исключая повторный вход.
        var oldCooldown = observer.Comp.RealityReadyAt;
        cult.Energy -= avatar.Comp.RealityEnergy;
        observer.Comp.RealityReadyAt = _timing.CurTime + avatar.Comp.RealityCooldown;
        var eventRule = _ticker.AddGameRule(prototype);
        EnsureComp<OrbitraRatvarEventTargetComponent>(eventRule).Station = cult.Station!.Value;
        if (!_ticker.StartGameRule(eventRule))
        {
            cult.Energy += avatar.Comp.RealityEnergy;
            observer.Comp.RealityReadyAt = oldCooldown;
            _ticker.EndGameRule(eventRule);
            QueueDel(eventRule);
            return false;
        }
        _adminLog.Add(LogType.Action, LogImpact.High,
            $"Ratvar Eminence {ToPrettyString(avatar)} requested {prototype} for station {ToPrettyString(cult.Station)}, spent {avatar.Comp.RealityEnergy} for {ToPrettyString(observer.Comp.Rule)}.");
        return true;
    }

    /// <summary>Requires an authorized current view and a station eligible for native station events.</summary>
    public bool CanManipulate(Entity<OrbitraRatvarEminenceAvatarComponent> avatar,
        out Entity<OrbitraRatvarEminenceComponent> observer, out string reason)
    {
        observer = default;
        reason = "orbitra-ratvar-eminence-ability-select";
        if (!CanAccessMenu(avatar, avatar, out var mind)) return false;
        observer = (mind, Comp<OrbitraRatvarEminenceComponent>(mind));
        if (observer.Comp.Target is not { } target || !CanObserve(mind, target)) return false;
        reason = "orbitra-ratvar-eminence-ability-cooldown";
        if (observer.Comp.RealityReadyAt > _timing.CurTime) return false;
        reason = "orbitra-ratvar-eminence-ability-energy";
        var cult = Comp<OrbitraRatvarRuleComponent>(observer.Comp.Rule);
        if (avatar.Comp.RealityEnergy < 0 || cult.Energy < avatar.Comp.RealityEnergy) return false;
        reason = "orbitra-ratvar-eminence-reality-station";
        if (cult.Station is not { } station || TerminatingOrDeleted(station) || EntityManager.IsQueuedForDeletion(station) ||
            !HasComp<StationEventEligibleComponent>(station)) return false;
        reason = "orbitra-ratvar-eminence-ability-ready";
        return true;
    }
}
