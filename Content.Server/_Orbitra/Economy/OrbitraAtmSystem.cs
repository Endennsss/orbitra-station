using Content.Shared._Orbitra.Economy;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Containers;

namespace Content.Server._Orbitra.Economy;

/// <summary>Проверяет операции банкомата и отправляет баланс только владельцу открытого окна.</summary>
public sealed partial class OrbitraAtmSystem : EntitySystem
{
    [Dependency] private OrbitraBankSystem _bank = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private ItemSlotsSystem _slots = default!;

    private readonly HashSet<(EntityUid Terminal, EntityUid Actor)> _views = new();

    public override void Initialize()
    {
        base.Initialize();
        Subs.BuiEvents<OrbitraAtmComponent>(OrbitraAtmUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnOpened);
            subs.Event<BoundUIClosedEvent>(OnClosed);
            subs.Event<OrbitraAtmRefreshMessage>(OnRefresh);
            subs.Event<OrbitraAtmInsertCardMessage>(OnInsertCard);
            subs.Event<OrbitraAtmEjectCardMessage>(OnEjectCard);
            subs.Event<OrbitraAtmDepositMessage>(OnDeposit);
            subs.Event<OrbitraAtmWithdrawMessage>(OnWithdraw);
            subs.Event<OrbitraAtmTransferMessage>(OnTransfer);
        });
        SubscribeLocalEvent<OrbitraAtmComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<OrbitraAtmComponent, ItemSlotInsertAttemptEvent>(OnCardInsertAttempt);
        SubscribeLocalEvent<OrbitraAtmComponent, EntInsertedIntoContainerMessage>(OnCardInserted);
        SubscribeLocalEvent<OrbitraAtmComponent, EntRemovedFromContainerMessage>(OnCardRemoved);
        SubscribeLocalEvent<OrbitraBankBalanceChangedEvent>(OnBalanceChanged);
    }

    private void OnOpened(Entity<OrbitraAtmComponent> ent, ref BoundUIOpenedEvent args)
    {
        _views.Add((ent, args.Actor));
        SendSnapshot(ent, args.Actor);
    }

    private void OnClosed(Entity<OrbitraAtmComponent> ent, ref BoundUIClosedEvent args) =>
        _views.Remove((ent, args.Actor));

    private void OnShutdown(Entity<OrbitraAtmComponent> ent, ref ComponentShutdown args) =>
        _views.RemoveWhere(view => view.Terminal == ent.Owner);

    private void OnBalanceChanged(ref OrbitraBankBalanceChangedEvent args)
    {
        foreach (var (terminal, actor) in _views)
        {
            if (_mind.GetMind(actor) == args.Account)
                SendSnapshot(terminal, actor);
        }
    }

    private void OnRefresh(Entity<OrbitraAtmComponent> ent, ref OrbitraAtmRefreshMessage args) =>
        SendSnapshot(ent, args.Actor);

    private void OnCardInsertAttempt(Entity<OrbitraAtmComponent> ent, ref ItemSlotInsertAttemptEvent args)
    {
        if (args.Slot.ContainerSlot?.ID == OrbitraAtmComponent.CardSlot &&
            (args.User is not { } actor || !_bank.TryGetAccount(actor, args.Item, out _)))
            args.Cancelled = true;
    }

    private void OnCardInserted(Entity<OrbitraAtmComponent> ent, ref EntInsertedIntoContainerMessage args) =>
        RefreshCard(ent, args.Container.ID);

    private void OnCardRemoved(Entity<OrbitraAtmComponent> ent, ref EntRemovedFromContainerMessage args) =>
        RefreshCard(ent, args.Container.ID);

    private void RefreshCard(EntityUid terminal, string slot)
    {
        if (slot != OrbitraAtmComponent.CardSlot)
            return;

        foreach (var (viewTerminal, actor) in _views)
        {
            if (viewTerminal == terminal)
                SendSnapshot(terminal, actor);
        }
    }

    private void OnInsertCard(Entity<OrbitraAtmComponent> ent, ref OrbitraAtmInsertCardMessage args) =>
        Finish(ent, args.Actor, _slots.TryGetSlot(ent.Owner, OrbitraAtmComponent.CardSlot, out var slot) &&
            _slots.TryInsertFromHand(ent, slot, args.Actor), "orbitra-atm-card-insert-denied");

    private void OnEjectCard(Entity<OrbitraAtmComponent> ent, ref OrbitraAtmEjectCardMessage args) =>
        Finish(ent, args.Actor, TryGetAccount(ent, args.Actor, out _) &&
            _slots.TryGetSlot(ent.Owner, OrbitraAtmComponent.CardSlot, out var slot) &&
            _slots.TryEjectToHands(ent, slot, args.Actor), "orbitra-atm-card-eject-denied");

    private void OnDeposit(Entity<OrbitraAtmComponent> ent, ref OrbitraAtmDepositMessage args) =>
        Finish(ent, args.Actor, TryGetAccount(ent, args.Actor, out var account) && _bank.TryDeposit(args.Actor, account),
            "orbitra-atm-deposit-denied");

    private void OnWithdraw(Entity<OrbitraAtmComponent> ent, ref OrbitraAtmWithdrawMessage args) =>
        Finish(ent, args.Actor, TryGetAccount(ent, args.Actor, out var account) &&
            _bank.TryWithdraw(args.Actor, account, args.Amount),
            "orbitra-atm-withdraw-denied");

    private void OnTransfer(Entity<OrbitraAtmComponent> ent, ref OrbitraAtmTransferMessage args) =>
        Finish(ent, args.Actor, TryGetAccount(ent, args.Actor, out var account) &&
            _bank.TryTransfer(account, args.Recipient, args.Amount),
            "orbitra-atm-transfer-denied");

    private void Finish(EntityUid terminal, EntityUid actor, bool succeeded, LocId failure)
    {
        if (succeeded)
            return;

        SendSnapshot(terminal, actor);
        _popup.PopupEntity(Loc.GetString(failure), terminal, actor);
    }

    private bool TryGetAccount(EntityUid terminal, EntityUid actor, out Entity<OrbitraBankAccountComponent> account)
    {
        account = default;
        return _power.IsPowered(terminal) && _slots.GetItemOrNull(terminal, OrbitraAtmComponent.CardSlot) is { } card &&
            _bank.TryGetAccount(actor, card, out account);
    }

    private void SendSnapshot(EntityUid terminal, EntityUid actor)
    {
        var inserted = _slots.GetItemOrNull(terminal, OrbitraAtmComponent.CardSlot) != null;
        var snapshot = TryGetAccount(terminal, actor, out var account)
            ? new OrbitraAtmSnapshotMessage(true, account.Comp.Number, account.Comp.Balance, account.Comp.Entries.ToArray())
            : new OrbitraAtmSnapshotMessage(inserted, 0, 0, []);
        _ui.ServerSendUiMessage(terminal, OrbitraAtmUiKey.Key, snapshot, actor);
    }
}
