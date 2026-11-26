using Content.Shared._Orbitra.Economy;
using Content.Shared.CartridgeLoader;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Mind;
using Content.Shared.PDA;
using Content.Shared.Popups;

namespace Content.Server._Orbitra.Economy;

/// <summary>Обслуживает личное банковское приложение в открытом интерфейсе КПК.</summary>
public sealed partial class OrbitraBankPdaSystem : EntitySystem
{
    [Dependency] private OrbitraBankSystem _bank = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private ItemSlotsSystem _slots = default!;

    private readonly HashSet<(EntityUid Pda, EntityUid Actor)> _views = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OrbitraBankCartridgeComponent, CartridgeDeactivatedEvent>(OnDeactivated);
        SubscribeLocalEvent<OrbitraBankCartridgeComponent, CartridgeMessageEvent>(OnMessage);
        SubscribeLocalEvent<PdaComponent, BoundUIClosedEvent>(OnClosed);
        SubscribeLocalEvent<PdaComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<OrbitraBankBalanceChangedEvent>(OnBalanceChanged);
    }

    private void OnDeactivated(Entity<OrbitraBankCartridgeComponent> ent, ref CartridgeDeactivatedEvent args)
    {
        var pda = args.Loader.Owner;
        _views.RemoveWhere(view => view.Pda == pda);
    }

    private void OnClosed(Entity<PdaComponent> ent, ref BoundUIClosedEvent args)
    {
        if (PdaUiKey.Key.Equals(args.UiKey))
            _views.Remove((ent, args.Actor));
    }

    private void OnShutdown(Entity<PdaComponent> ent, ref ComponentShutdown args) =>
        _views.RemoveWhere(view => view.Pda == ent.Owner);

    internal void RefreshCard(EntityUid pda, string slot)
    {
        if (slot != PdaComponent.PdaIdSlotId)
            return;

        foreach (var actor in _ui.GetActors(pda, PdaUiKey.Key))
        {
            if (_views.Contains((pda, actor)))
                SendSnapshot(pda, actor);
        }
    }

    private void OnBalanceChanged(ref OrbitraBankBalanceChangedEvent args)
    {
        foreach (var (pda, actor) in _views)
        {
            if (_mind.GetMind(actor) == args.Account)
                SendSnapshot(pda, actor);
        }
    }

    private void OnMessage(Entity<OrbitraBankCartridgeComponent> ent, ref CartridgeMessageEvent args)
    {
        if (!TryGetEntity(args.LoaderUid, out var pda) ||
            !HasComp<PdaComponent>(pda) || !_ui.IsUiOpen(pda.Value, PdaUiKey.Key, args.Actor))
            return;

        if (args is OrbitraBankPdaRefreshMessage)
        {
            _views.Add((pda.Value, args.Actor));
            SendSnapshot(pda.Value, args.Actor);
            return;
        }

        if (args is not OrbitraBankPdaTransferMessage transfer)
            return;

        if (TryGetAccount(pda.Value, args.Actor, out var account) &&
            _bank.TryTransfer(account, transfer.Recipient, transfer.Amount))
            return;

        SendSnapshot(pda.Value, args.Actor);
        _popup.PopupEntity(Loc.GetString("orbitra-bank-pda-transfer-denied"), pda.Value, args.Actor);
    }

    private bool TryGetAccount(EntityUid pda, EntityUid actor, out Entity<OrbitraBankAccountComponent> account)
    {
        account = default;
        return _slots.GetItemOrNull(pda, PdaComponent.PdaIdSlotId) is { } card &&
            _bank.TryGetAccount(actor, card, out account);
    }

    private void SendSnapshot(EntityUid pda, EntityUid actor)
    {
        var state = TryGetAccount(pda, actor, out var account)
            ? new OrbitraBankPdaState(true, account.Comp.Number, account.Comp.Balance, account.Comp.Entries.ToArray())
            : new OrbitraBankPdaState(_slots.GetItemOrNull(pda, PdaComponent.PdaIdSlotId) != null, 0, 0, []);
        _ui.ServerSendUiMessage(pda, PdaUiKey.Key, new OrbitraBankPdaSnapshotMessage(state), actor);
    }
}
