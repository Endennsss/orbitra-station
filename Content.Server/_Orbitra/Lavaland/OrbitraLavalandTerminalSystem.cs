using Content.Shared._Orbitra.Lavaland;
using Content.Shared.ActionBlocker;
using Content.Shared.Ghost.Components;
using Robust.Shared.Timing;

namespace Content.Server._Orbitra.Lavaland;

/// <summary>Публикует состояние рейса только у терминалов с открытым окном.</summary>
public sealed partial class OrbitraLavalandTerminalSystem : EntitySystem
{
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private ActionBlockerSystem _action = default!;
    [Dependency] private OrbitraLavalandFerrySystem _ferry = default!;
    [Dependency] private IGameTiming _timing = default!;

    private TimeSpan _nextRefresh;

    public override void Initialize()
    {
        base.Initialize();
        Subs.BuiEvents<OrbitraLavalandTerminalComponent>(OrbitraLavalandUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnOpened);
            subs.Event<OrbitraLavalandRequestMessage>(OnRequest);
        });
    }

    private void OnOpened(Entity<OrbitraLavalandTerminalComponent> ent, ref BoundUIOpenedEvent args)
    {
        Publish(ent);
    }

    private void OnRequest(Entity<OrbitraLavalandTerminalComponent> ent, ref OrbitraLavalandRequestMessage args)
    {
        if (HasComp<GhostComponent>(args.Actor) || !_action.CanInteract(args.Actor, ent))
            return;

        _ferry.Request(ent, args.Actor);
        Publish(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_timing.CurTime < _nextRefresh)
            return;

        _nextRefresh = _timing.CurTime + TimeSpan.FromSeconds(0.5);
        var terminals = EntityQueryEnumerator<OrbitraLavalandTerminalComponent>();
        while (terminals.MoveNext(out var uid, out _))
        {
            if (_ui.IsUiOpen(uid, OrbitraLavalandUiKey.Key))
                Publish(uid);
        }
    }

    private void Publish(EntityUid uid)
    {
        _ui.SetUiState(uid, OrbitraLavalandUiKey.Key, _ferry.GetState(uid));
    }
}
