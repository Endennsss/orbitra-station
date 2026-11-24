using Content.Server.Hands.Systems;
using Content.Server.Stack;
using Content.Shared._Orbitra.Economy;
using Content.Shared.Access.Systems;
using Content.Shared.CartridgeLoader;
using Content.Shared.GameTicking;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared.PDA;
using Content.Shared.Roles;
using Content.Shared.Stacks;
using Content.Shared.StationRecords.Components;
using Content.Shared.StationRecords.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Orbitra.Economy;

/// <summary>Ведёт личные счета экипажа и начисляет зарплату в течение смены.</summary>
public sealed partial class OrbitraBankSystem : EntitySystem
{
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedIdCardSystem _idCard = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private HandsSystem _hands = default!;
    [Dependency] private StackSystem _stack = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private CartridgeLoaderSystem _cartridgeLoader = default!;

    private const int HistoryLimit = 12;
    private const int MaxBalance = 1_000_000;
    private static readonly ProtoId<StackPrototype> Currency = "Credit";
    private static readonly TimeSpan PayrollCheckInterval = TimeSpan.FromSeconds(2);
    private readonly Dictionary<int, EntityUid> _accounts = new();
    private TimeSpan _nextPayrollCheck;
    private int _nextNumber = 1000;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawn, after: [typeof(StationRecordsSystem)]);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundCleanup);
        SubscribeLocalEvent<OrbitraBankAccountComponent, ComponentShutdown>(OnAccountShutdown);
    }

    private void OnPlayerSpawn(PlayerSpawnCompleteEvent args)
    {
        if (args.JobId is { } job)
            EnsureCrewAccount(args.Mob, job);
    }

    /// <summary>Открывает один счёт на персонажа и связывает его с текущей служебной картой.</summary>
    internal void EnsureCrewAccount(EntityUid body, ProtoId<JobPrototype> job)
    {
        if (!_mind.TryGetMind(body, out var mind, out _) ||
            TryComp<OrbitraBankAccountComponent>(mind, out _))
            return;

        var payroll = _prototype.Index(OrbitraPayrollPrototype.Station);
        var grade = payroll.GetGrade(job);
        var startingBalance = _random.Next(grade.StartingMinimum, grade.StartingMaximum + 1);
        var account = AddComp<OrbitraBankAccountComponent>(mind);
        account.Number = ++_nextNumber;
        account.Salary = grade.Salary;
        account.NextPay = _timing.CurTime + payroll.Interval;
        if (_idCard.TryFindIdCard(body, out var card))
        {
            account.Card = card.Owner;
            if (TryComp<StationRecordKeyStorageComponent>(card.Owner, out var record))
                account.StationRecord = record.Key;
        }
        _accounts.Add(account.Number, mind);
        TryChangeBalance((mind, account), startingBalance, OrbitraBankEntryKind.StartingBalance);
    }

    private void OnAccountShutdown(Entity<OrbitraBankAccountComponent> entity, ref ComponentShutdown args)
    {
        if (_accounts.GetValueOrDefault(entity.Comp.Number) == entity.Owner)
            _accounts.Remove(entity.Comp.Number);
    }

    private void OnRoundCleanup(RoundRestartCleanupEvent args)
    {
        _accounts.Clear();
        _nextNumber = 1000;
        _nextPayrollCheck = default;
    }

    /// <summary>Начисляет только одну выплату за прошедший интервал живому экипажу в игре.</summary>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        if (_accounts.Count == 0 || _ticker.RunLevel != GameRunLevel.InRound || now < _nextPayrollCheck)
            return;

        _nextPayrollCheck = now + PayrollCheckInterval;
        var interval = _prototype.Index(OrbitraPayrollPrototype.Station).Interval;
        var query = EntityQueryEnumerator<OrbitraBankAccountComponent, MindComponent>();
        while (query.MoveNext(out var uid, out var account, out var mind))
        {
            if (now < account.NextPay)
                continue;

            account.NextPay = now + interval;
            var salary = account.Salary;
            if (salary > 0 && mind.OwnedEntity is { } body && mind.CurrentEntity == body &&
                HasComp<ActorComponent>(body) && _mobState.IsAlive(body) &&
                TryChangeBalance((uid, account), salary, OrbitraBankEntryKind.Salary))
                NotifySalary(body, salary);
        }
    }

    private void NotifySalary(EntityUid body, int salary)
    {
        if (!_inventory.TryGetSlotEntity(body, "id", out var pda) || !HasComp<PdaComponent>(pda))
        {
            pda = null;
            foreach (var held in _hands.EnumerateHeld(body))
            {
                if (!HasComp<PdaComponent>(held))
                    continue;

                pda = held;
                break;
            }
        }

        if (pda is { } device && TryComp<CartridgeLoaderComponent>(device, out var loader))
            _cartridgeLoader.SendNotification(device, Loc.GetString("orbitra-payroll-notification-title"),
                Loc.GetString("orbitra-payroll-notification", ("amount", salary)), loader);
    }

    /// <summary>Проверяет разум владельца и подлинную ID-карту, назначенную при выходе на смену.</summary>
    internal bool TryGetAccount(EntityUid actor, out Entity<OrbitraBankAccountComponent> account)
    {
        account = default;
        return _idCard.TryFindIdCard(actor, out var card) && TryGetAccount(actor, card.Owner, out account);
    }

    /// <summary>Проверяет, что предъявленная карта принадлежит счёту текущего персонажа.</summary>
    internal bool TryGetAccount(EntityUid actor, EntityUid card, out Entity<OrbitraBankAccountComponent> account)
    {
        account = default;
        if (!_mind.TryGetMind(actor, out var mindId, out var mind) || mind.CurrentEntity != actor ||
            !_mobState.IsAlive(actor) ||
            !TryComp<OrbitraBankAccountComponent>(mindId, out var component) ||
            !IsAccountCard(card, component))
            return false;

        account = (mindId, component);
        return true;
    }

    private bool IsAccountCard(EntityUid card, OrbitraBankAccountComponent account) =>
        card == account.Card || account.StationRecord is { } original &&
        TryComp<StationRecordKeyStorageComponent>(card, out var record) && record.Key is { } key && original.Equals(key);

    private static bool CanChangeBalance(OrbitraBankAccountComponent account, int amount) =>
        amount != 0 && (long) account.Balance + amount is >= 0 and <= MaxBalance;

    private bool TryChangeBalance(Entity<OrbitraBankAccountComponent> account, int amount, OrbitraBankEntryKind kind, int other = 0)
    {
        if (!CanChangeBalance(account.Comp, amount))
            return false;

        ChangeBalance(account.Comp, amount, kind, other);
        NotifyChanged(account);
        return true;
    }

    private static void ChangeBalance(OrbitraBankAccountComponent account, int amount, OrbitraBankEntryKind kind, int other = 0)
    {
        account.Balance += amount;
        if (account.Entries.Count == HistoryLimit)
            account.Entries.Dequeue();

        account.Entries.Enqueue(new OrbitraBankEntry(kind, amount, other));
    }

    private void NotifyChanged(EntityUid account)
    {
        var changed = new OrbitraBankBalanceChangedEvent(account);
        RaiseLocalEvent(ref changed);
    }

    internal bool TryTransfer(Entity<OrbitraBankAccountComponent> sender, int recipient, int amount)
    {
        if (amount <= 0 || sender.Comp.Number == recipient ||
            !_accounts.TryGetValue(recipient, out var target) ||
            !TryComp<OrbitraBankAccountComponent>(target, out var receiver) ||
            !CanChangeBalance(sender.Comp, -amount) || !CanChangeBalance(receiver, amount))
            return false;

        // Оба баланса обновляются до уведомлений, чтобы интерфейс увидел завершённый перевод.
        ChangeBalance(sender.Comp, -amount, OrbitraBankEntryKind.Transfer, recipient);
        ChangeBalance(receiver, amount, OrbitraBankEntryKind.Transfer, sender.Comp.Number);
        NotifyChanged(sender);
        NotifyChanged(target);
        return true;
    }

    internal bool TryDeposit(EntityUid actor, Entity<OrbitraBankAccountComponent> account)
    {
        if (!TryGetHeldCash(actor, out var cash, out var amount) ||
            !CanChangeBalance(account.Comp, amount) || !_stack.TryUse(cash.AsNullable(), amount))
            return false;

        return TryChangeBalance(account, amount, OrbitraBankEntryKind.Deposit);
    }

    internal bool TryWithdraw(EntityUid actor, Entity<OrbitraBankAccountComponent> account, int amount)
    {
        if (amount <= 0 ||
            !TryChangeBalance(account, -amount, OrbitraBankEntryKind.Withdrawal))
            return false;

        var cash = _stack.SpawnAtPosition(amount, Currency, Transform(actor).Coordinates);
        _hands.TryPickupAnyHand(actor, cash);
        return true;
    }

    /// <summary>Оплачивает заказ наличными в активной руке или с личного счёта.</summary>
    internal bool TryPurchase(EntityUid actor, int price)
    {
        if (price <= 0)
            return false;

        if (TryGetHeldCash(actor, out var cash, out var amount) && amount >= price)
            return _stack.TryUse(cash.AsNullable(), price);

        return TryGetAccount(actor, out var account) && TryChangeBalance(account, -price, OrbitraBankEntryKind.Purchase);
    }

    private bool TryGetHeldCash(EntityUid actor, out Entity<StackComponent> cash, out int amount)
    {
        cash = default;
        amount = 0;
        if (_hands.GetActiveItem(actor) is not { } item || TerminatingOrDeleted(item) || EntityManager.IsQueuedForDeletion(item) ||
            !TryComp<StackComponent>(item, out var stack) || stack.StackTypeId != Currency || stack.Unlimited || stack.Count <= 0)
            return false;

        cash = (item, stack);
        amount = stack.Count;
        return true;
    }
}

[ByRefEvent]
internal readonly record struct OrbitraBankBalanceChangedEvent(EntityUid Account);
