using Content.Server.Radio.EntitySystems;
using Content.Shared._Orbitra.Lavaland;
using Content.Shared.Radio;
using Robust.Shared.Prototypes;

namespace Content.Server._Orbitra.Lavaland;

/// <summary>Передаёт сводки терминалов шахтёрского шаттла по каналу снабжения.</summary>
public sealed partial class OrbitraLavalandRadioSystem : EntitySystem
{
    [Dependency] private RadioSystem _radio = default!;

    private static readonly ProtoId<RadioChannelPrototype> Supply = "Supply";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OrbitraLavalandTerminalComponent, RadioSendAttemptEvent>(OnRadioSendAttempt);
    }

    private void OnRadioSendAttempt(Entity<OrbitraLavalandTerminalComponent> terminal, ref RadioSendAttemptEvent args)
    {
        if (args.Channel.ID == Supply.Id && TryComp<TransformComponent>(terminal.Comp.RadioPeer, out var peer) && peer.Anchored)
            args.RelayMap = peer.MapID;
    }

    public void Broadcast(EntityUid source, string message)
    {
        if (!TerminatingOrDeleted(source))
            _radio.SendRadioMessage(source, message, Supply, source);
    }
}
