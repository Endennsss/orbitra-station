using System;
using System.Linq;
using System.Numerics;
using Content.Client._Orbitra.Economy;
using Content.Client.PDA;
using Content.Client.UserInterface.Systems.Chat;
using Content.IntegrationTests.Fixtures;
using Content.Server._Orbitra.Economy;
using Content.Server.Cargo.Systems;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Station.Systems;
using Content.Shared._Orbitra.Economy;
using Content.Shared.CartridgeLoader;
using Content.Shared.Chat;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.PDA;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Stacks;
using Content.Shared.VendingMachines;
using Content.Shared.VendingMachines.Components;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Orbitra.Economy;

[TestFixture]
[NonParallelizable]
public sealed class OrbitraEconomyTest : GameTest
{
    private static readonly EntProtoId Cola = "DrinkColaCan";

    public override PoolSettings PoolSettings => new()
    {
        Connected = true,
        DummyTicker = false,
        Dirty = true,
        Map = "Dev",
    };

    [Test]
    public async Task DevPaydayBankingAndRetailUseTheSameMoney()
    {
        EntityUid actor = default, terminal = default, vendor = default, actorMind = default, station = default;
        EntityUid receiverMind = default, foreignCard = default, ownPda = default, ownCard = default;
        NetEntity netTerminal = default, netVendor = default;
        var bank = Server.System<OrbitraBankSystem>();
        var serverUi = Server.System<SharedUserInterfaceSystem>();
        var power = Server.System<SharedPowerReceiverSystem>();
        var slots = Server.System<ItemSlotsSystem>();
        var inventory = Server.System<InventorySystem>();
        var hands = Server.System<SharedHandsSystem>();
        int receiverNumber = 0, serviceBalance = 0, vendTicks = 0;
        int startingBalance = 0, receiverStartingBalance = 0, salary = 0;
        var interval = TimeSpan.FromMinutes(15);
        TimeSpan firstPayday = default, forcedPayday = default;
        string paydayTitle = string.Empty, paydayMessage = string.Empty;
        await Pair.RunTicksSync(20);
        await Server.WaitPost(() =>
        {
            station = Server.System<ServerStationSystem>().GetStations().Single();
            var session = ServerSession!;
            if (session.AttachedEntity is not { } body || !bank.TryGetAccount(body, out _))
                Server.System<ServerGameTicker>().MakeJoinGame(session, station, "Captain", silent: true);
            actor = session.AttachedEntity!.Value;

            terminal = FindEntity("OrbitraAtm");
            vendor = FindEntity("VendingMachineCola");
            var grid = SEntMan.GetComponent<TransformComponent>(terminal).ParentUid;
            Server.System<SharedTransformSystem>().SetCoordinates(actor, new EntityCoordinates(grid, 33.5f, 19.5f));
            netTerminal = SEntMan.GetNetEntity(terminal);
            netVendor = SEntMan.GetNetEntity(vendor);
            var vendDelay = SEntMan.GetComponent<VendingMachineEjectComponent>(vendor).EjectDelay;
            vendTicks = (int) Math.Ceiling(vendDelay.TotalSeconds * SGameTiming.TickRate) + 12;
            Assert.That(bank.TryGetAccount(actor, out var account), Is.True);
            actorMind = account.Owner;
            Assert.That(inventory.TryGetSlotEntity(actor, "id", out var equippedId), Is.True);
            ownPda = equippedId!.Value;
            var cardInPda = slots.GetItemOrNull(ownPda, PdaComponent.PdaIdSlotId);
            Assert.That(cardInPda, Is.Not.Null);
            ownCard = cardInPda!.Value;
            startingBalance = account.Comp.Balance;
            salary = account.Comp.Salary;
            firstPayday = account.Comp.NextPay;
            Assert.That(startingBalance, Is.InRange(110, 145));
            Assert.That(salary, Is.EqualTo(300));
            Assert.That(firstPayday - SGameTiming.CurTime, Is.EqualTo(interval));
            var payroll = SProtoMan.Index(OrbitraPayrollPrototype.Station);
            Assert.That(payroll.GetGrade("StationEngineer").Salary, Is.EqualTo(180));
            Assert.That(payroll.GetGrade("ChiefEngineer").Salary, Is.EqualTo(240));
            var loc = Server.ResolveDependency<ILocalizationManager>();
            paydayTitle = loc.GetString("orbitra-payroll-notification-title");
            paydayMessage = loc.GetString("orbitra-payroll-notification", ("amount", salary));
            Assert.That(paydayMessage, Does.Contain(salary.ToString()));

            var coordinates = SEntMan.GetComponent<TransformComponent>(actor).Coordinates.Offset(new Vector2(0, -2));
            var recipient = SEntMan.SpawnAttachedTo("MobHuman", coordinates);
            foreignCard = SEntMan.SpawnAttachedTo("PassengerIDCard", coordinates);
            Assert.That(inventory.TryEquip(recipient, foreignCard, "id", force: true), Is.True);
            var minds = Server.System<MindSystem>();
            var mind = minds.CreateMind(null, "Второй счёт");
            receiverMind = mind.Owner;
            minds.TransferTo(mind, recipient);
            bank.EnsureCrewAccount(recipient, "Passenger");
            Assert.That(bank.TryGetAccount(recipient, out var receiver), Is.True);
            receiverNumber = receiver.Comp.Number;
            receiverStartingBalance = receiver.Comp.Balance;
            Assert.That(receiverStartingBalance, Is.InRange(25, 40));
            Assert.That(receiver.Comp.Salary, Is.EqualTo(120));
            bank.EnsureCrewAccount(recipient, "Passenger");
            Assert.That(receiver.Comp.Balance, Is.EqualTo(receiverStartingBalance));

            var pricing = Server.System<PricingSystem>();
            var retail = Server.System<OrbitraRetailSystem>();
            Assert.That(pricing.GetEstimatedPrice(SProtoMan.Index(Cola)), Is.LessThanOrEqualTo(6));
            Assert.That(retail.GetPrice("SustenanceInventory", "FoodSnackNutribrick"), Is.Zero);
            Assert.That(receiver.Comp.Salary, Is.GreaterThanOrEqualTo(6 * retail.GetPrice("DonutInventory", "FoodDonutPink")));
            Assert.That(SProtoMan.EnumeratePrototypes<OrbitraRetailCatalogPrototype>().Count(), Is.GreaterThanOrEqualTo(15));
        });

        await Pair.RunTicksSync(12);
        await Server.WaitPost(() =>
        {
            Assert.That(power.IsPowered(terminal), Is.True, "Банкомат Девы должен быть запитан.");
            Assert.That(power.IsPowered(vendor), Is.True, "Автомат Девы должен быть запитан.");
            Assert.That(serverUi.TryOpenUi(terminal, OrbitraAtmUiKey.Key, actor), Is.True);
        });
        await Pair.RunTicksSync(12);

        await AssertAtmBalance(null);
        await AtmMessage(new OrbitraAtmWithdrawMessage(25));
        await Server.WaitAssertion(() =>
        {
            Assert.That(Account().Balance, Is.EqualTo(startingBalance), "ID в КПК не открывает банкомат.");
            Assert.That(slots.TryInsert(terminal, OrbitraAtmComponent.CardSlot, foreignCard, actor), Is.False);
            Assert.That(slots.GetItemOrNull(terminal, OrbitraAtmComponent.CardSlot), Is.Null);
        });

        await InsertOwnId();
        await AssertAtmBalance(startingBalance);
        await AtmMessage(new OrbitraAtmWithdrawMessage(25));
        await Server.WaitAssertion(() =>
        {
            Assert.That(Account().Balance, Is.EqualTo(startingBalance - 25));
            var cash = hands.GetActiveItem(actor);
            Assert.That(cash, Is.Not.Null);
            Assert.That(SEntMan.GetComponent<StackComponent>(cash!.Value).Count, Is.EqualTo(25));
        });

        await AtmMessage(new OrbitraAtmDepositMessage());
        await AtmMessage(new OrbitraAtmTransferMessage(receiverNumber, 20));
        await AtmMessage(new OrbitraAtmTransferMessage(receiverNumber, -20));
        await AtmMessage(new OrbitraAtmTransferMessage(receiverNumber, int.MaxValue));
        await AssertPaydayNotifications(0);
        await Server.WaitPost(() =>
        {
            var account = Account();
            Assert.That(account.Balance, Is.EqualTo(startingBalance - 20));
            Assert.That(account.NextPay, Is.EqualTo(firstPayday));
            Assert.That(SEntMan.GetComponent<OrbitraBankAccountComponent>(receiverMind).Balance, Is.EqualTo(receiverStartingBalance + 20));
            Assert.That(account.Entries.Any(entry => entry.Kind == OrbitraBankEntryKind.Deposit && entry.Amount == 25), Is.True);
            forcedPayday = SGameTiming.CurTime;
            account.NextPay = forcedPayday;
        });

        await Pair.RunTicksSync(75);
        await AssertPaydayNotifications(1);
        var paydayBalance = startingBalance - 20 + salary;
        await AtmMessage(new OrbitraAtmEjectCardMessage());
        await AssertAtmBalance(null);
        await AtmMessage(new OrbitraAtmWithdrawMessage(10));
        await Server.WaitPost(() =>
        {
            Assert.That(hands.GetActiveItem(actor), Is.EqualTo(ownCard));
            var account = Account();
            Assert.That(account.Balance, Is.EqualTo(paydayBalance), "ID в руке не открывает банкомат.");
            Assert.That(slots.TryGetSlot(ownPda, PdaComponent.PdaIdSlotId, out var pdaSlot), Is.True);
            Assert.That(slots.TryInsertFromHand(ownPda, pdaSlot!, actor), Is.True);
            Assert.That(account.Balance, Is.EqualTo(paydayBalance));
            Assert.That(account.Entries.Single(entry => entry.Kind == OrbitraBankEntryKind.Salary).Amount, Is.EqualTo(salary));
            Assert.That(account.NextPay, Is.InRange(forcedPayday + interval, forcedPayday + interval + TimeSpan.FromSeconds(2)));
            serviceBalance = Server.System<CargoSystem>().GetBalanceFromAccount(station, "Service");
            Assert.That(serverUi.TryOpenUi(vendor, VendingMachineUiKey.Key, actor), Is.True);
        });
        await Pair.RunTicksSync(12);

        await BuyCola();
        await Server.WaitAssertion(() =>
        {
            Assert.That(Account().Balance, Is.EqualTo(paydayBalance - 12));
            Assert.That(SEntMan.GetComponent<VendingMachineComponent>(vendor).Inventory["DrinkColaCan"].Amount, Is.EqualTo(3));
            Assert.That(Server.System<CargoSystem>().GetBalanceFromAccount(station, "Service"), Is.EqualTo(serviceBalance + 12));
        });

        await InsertOwnId();
        await AtmMessage(new OrbitraAtmWithdrawMessage(12));
        await BuyCola();
        await AtmMessage(new OrbitraAtmEjectCardMessage());
        await Server.WaitPost(() =>
        {
            Assert.That(Account().Balance, Is.EqualTo(paydayBalance - 24));
            Assert.That(hands.GetActiveItem(actor), Is.EqualTo(ownCard));
            Assert.That(SEntMan.GetComponent<VendingMachineComponent>(vendor).Inventory["DrinkColaCan"].Amount, Is.EqualTo(2));
            Assert.That(hands.TryDrop(actor, ownCard), Is.True);
            Assert.That(bank.TryGetAccount(actor, out _), Is.False);
        });

        await AtmMessage(new OrbitraAtmWithdrawMessage(10));
        await BuyCola();
        await Server.WaitAssertion(() =>
        {
            Assert.That(Account().Balance, Is.EqualTo(paydayBalance - 24), "Возврат карты закрывает доступ к счёту в банкомате.");
            Assert.That(SEntMan.GetComponent<VendingMachineComponent>(vendor).Inventory["DrinkColaCan"].Amount, Is.EqualTo(2));
            Assert.That(Server.System<CargoSystem>().GetBalanceFromAccount(station, "Service"), Is.EqualTo(serviceBalance + 24));
        });

        OrbitraBankAccountComponent Account() => SEntMan.GetComponent<OrbitraBankAccountComponent>(actorMind);

        async Task AssertPaydayNotifications(int expectedCount)
        {
            await Client.WaitAssertion(() =>
            {
                var chat = Client.ResolveDependency<IUserInterfaceManager>().GetUIController<ChatUIController>();
                var count = chat.History.Count(entry => entry.Msg.Channel == ChatChannel.Notifications &&
                    entry.Msg.Message == paydayMessage && entry.Msg.WrappedMessage.Contains(paydayTitle));
                Assert.That(count, Is.EqualTo(expectedCount));
            });
        }

        async Task InsertOwnId()
        {
            await Server.WaitPost(() =>
            {
                Assert.That(slots.TryGetSlot(ownPda, PdaComponent.PdaIdSlotId, out var pdaSlot), Is.True);
                Assert.That(slots.TryEjectToHands(ownPda, pdaSlot!, actor), Is.True);
                Assert.That(hands.GetActiveItem(actor), Is.EqualTo(ownCard));
            });
            await AtmMessage(new OrbitraAtmInsertCardMessage());
            await Server.WaitAssertion(() =>
                Assert.That(slots.GetItemOrNull(terminal, OrbitraAtmComponent.CardSlot), Is.EqualTo(ownCard)));
        }

        async Task AssertAtmBalance(int? expected)
        {
            await Client.WaitAssertion(() =>
            {
                var uid = CEntMan.GetEntity(netTerminal);
                var ui = Client.System<SharedUserInterfaceSystem>();
                Assert.That(ui.TryGetOpenUi<OrbitraAtmBoundUserInterface>(uid, OrbitraAtmUiKey.Key, out var bui), Is.True);
                var balance = bui!.Window!.FindControl<Label>("BalanceLabel").Text;
                if (expected is { } amount)
                    Assert.That(balance, Does.Contain(amount.ToString()));
                else
                    Assert.That(balance, Is.Empty);
            });
        }

        async Task BuyCola()
        {
            await Client.WaitPost(() => Client.System<SharedUserInterfaceSystem>().ClientSendUiMessage(
                CEntMan.GetEntity(netVendor), VendingMachineUiKey.Key, new VendingMachineEjectMessage(InventoryType.Regular, Cola.Id)));
            await Pair.RunTicksSync(vendTicks);
        }

        async Task AtmMessage(BoundUserInterfaceMessage message)
        {
            await Client.WaitPost(() => Client.System<SharedUserInterfaceSystem>().ClientSendUiMessage(
                CEntMan.GetEntity(netTerminal), OrbitraAtmUiKey.Key, message));
            await Pair.RunTicksSync(9);
        }
    }

    [Test]
    public async Task PdaBankRequiresPersonalCardAndRefreshesAfterTransfer()
    {
        var bank = Server.System<OrbitraBankSystem>();
        var serverUi = Server.System<SharedUserInterfaceSystem>();
        var slots = Server.System<ItemSlotsSystem>();
        var inventory = Server.System<InventorySystem>();
        var loader = Server.System<CartridgeLoaderSystem>();
        NetEntity netPda = default, netProgram = default;
        EntityUid actor = default, pda = default, ownCard = default, foreignCard = default;
        Entity<OrbitraBankAccountComponent> ownerAccount = default, recipientAccount = default;
        var start = 0;
        var recipientStart = 0;

        await Pair.RunTicksSync(20);
        await Server.WaitPost(() =>
        {
            var session = ServerSession!;
            if (session.AttachedEntity is not { } body || !bank.TryGetAccount(body, out _))
            {
                var station = Server.System<ServerStationSystem>().GetStations().Single();
                Server.System<ServerGameTicker>().MakeJoinGame(session, station, "Captain", silent: true);
            }

            actor = session.AttachedEntity!.Value;
            Assert.That(bank.TryGetAccount(actor, out ownerAccount), Is.True);
            start = ownerAccount.Comp.Balance;
            Assert.That(inventory.TryGetSlotEntity(actor, "id", out var equipped), Is.True);
            pda = equipped!.Value;
            ownCard = slots.GetItemOrNull(pda, PdaComponent.PdaIdSlotId)!.Value;
            netPda = SEntMan.GetNetEntity(pda);
            var program = loader.TryGetProgram<OrbitraBankCartridgeComponent>(pda);
            Assert.That(program, Is.Not.Null);
            netProgram = SEntMan.GetNetEntity(program!.Value.Owner);

            var coordinates = SEntMan.GetComponent<TransformComponent>(actor).Coordinates;
            var recipient = SEntMan.SpawnAttachedTo("MobHuman", coordinates);
            foreignCard = SEntMan.SpawnAttachedTo("PassengerIDCard", coordinates);
            Assert.That(inventory.TryEquip(recipient, foreignCard, "id", force: true), Is.True);
            var minds = Server.System<MindSystem>();
            minds.TransferTo(minds.CreateMind(null, "Получатель"), recipient);
            bank.EnsureCrewAccount(recipient, "Passenger");
            Assert.That(bank.TryGetAccount(recipient, out recipientAccount), Is.True);
            recipientStart = recipientAccount.Comp.Balance;
            Assert.That(inventory.TryUnequip(recipient, "id", force: true), Is.True);
            Assert.That(serverUi.TryOpenUi(pda, PdaUiKey.Key, actor), Is.True);
        });

        await Pair.RunTicksSync(15);
        await Send(new CartridgeLoaderUiMessage(netProgram, CartridgeUiMessageAction.Activate));
        await AssertBalance(start);
        await Client.WaitAssertion(() =>
        {
            var clientUi = Client.System<SharedUserInterfaceSystem>();
            Assert.That(clientUi.TryGetOpenUi<PdaBoundUserInterface>(CEntMan.GetEntity(netPda), PdaUiKey.Key, out var bui), Is.True);
            var menu = bui!.Menu!;
            Assert.That(menu.CaseTrim.Frame, Is.Not.Null);
            Assert.That(menu.ProgramListButton.VisibleInTree, Is.True);
            Assert.That(menu.FindControl<PdaNavigationButton>("SettingsButton").VisibleInTree, Is.True);
            Assert.That(menu.ProgramTitle.VisibleInTree, Is.True);
            Assert.That(menu.ProgramCloseButton.VisibleInTree, Is.True);
        });

        await Server.WaitPost(() =>
        {
            Assert.That(slots.TryEject(pda, PdaComponent.PdaIdSlotId, actor, out var card), Is.True);
            Assert.That(card, Is.EqualTo(ownCard));
        });
        await Pair.RunTicksSync(12);
        await AssertBalance(null);
        await Server.WaitPost(() => Assert.That(slots.TryInsert(pda, PdaComponent.PdaIdSlotId, foreignCard, actor), Is.True));
        await Pair.RunTicksSync(12);
        await AssertBalance(null);
        await Send(new CartridgeUiMessage(new OrbitraBankPdaTransferMessage(recipientAccount.Comp.Number, 10)));
        await Server.WaitAssertion(() => Assert.That(ownerAccount.Comp.Balance, Is.EqualTo(start)));

        await Server.WaitPost(() =>
        {
            Assert.That(slots.TryEject(pda, PdaComponent.PdaIdSlotId, actor, out _), Is.True);
            Assert.That(slots.TryInsert(pda, PdaComponent.PdaIdSlotId, ownCard, actor), Is.True);
        });
        await Pair.RunTicksSync(12);
        await AssertBalance(start);
        await Send(new CartridgeUiMessage(new OrbitraBankPdaTransferMessage(recipientAccount.Comp.Number, 10)));
        await AssertBalance(start - 10);
        await Server.WaitAssertion(() => Assert.That(recipientAccount.Comp.Balance, Is.EqualTo(recipientStart + 10)));

        await Server.WaitPost(() => Assert.That(bank.TryTransfer(recipientAccount, ownerAccount.Comp.Number, 4), Is.True));
        await Pair.RunTicksSync(12);
        await AssertBalance(start - 6);

        await Server.WaitPost(() => serverUi.CloseUi(pda, PdaUiKey.Key, actor));
        await Pair.RunTicksSync(12);
        await Server.WaitAssertion(() => Assert.That(serverUi.IsUiOpen(pda, PdaUiKey.Key, actor), Is.False));

        await Server.WaitPost(() => Assert.That(serverUi.TryOpenUi(pda, PdaUiKey.Key, actor), Is.True));
        await Pair.RunTicksSync(15);
        await AssertBalance(start - 6);

        async Task Send(BoundUserInterfaceMessage message)
        {
            await Client.WaitPost(() => Client.System<SharedUserInterfaceSystem>()
                .ClientSendUiMessage(CEntMan.GetEntity(netPda), PdaUiKey.Key, message));
            await Pair.RunTicksSync(15);
        }

        async Task AssertBalance(int? expected)
        {
            await Client.WaitAssertion(() =>
            {
                var clientUi = Client.System<SharedUserInterfaceSystem>();
                Assert.That(clientUi.TryGetOpenUi<PdaBoundUserInterface>(CEntMan.GetEntity(netPda), PdaUiKey.Key, out var bui), Is.True);
                Assert.That(bui!.Menu!.ProgramView.ChildCount, Is.EqualTo(1));
                var fragment = (OrbitraBankPdaFragment)bui.Menu.ProgramView.GetChild(0);
                var balance = fragment.FindControl<Label>("BalanceLabel").Text;
                if (expected is { } amount)
                    Assert.That(balance, Does.Contain(amount.ToString()));
                else
                    Assert.That(balance, Is.Empty);
            });
        }
    }

    private EntityUid FindEntity(string prototype)
    {
        var query = SEntMan.AllEntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out var uid, out var metadata))
        {
            if (metadata.EntityPrototype?.ID == prototype)
                return uid;
        }

        throw new InvalidOperationException($"На Деве отсутствует {prototype}.");
    }
}
