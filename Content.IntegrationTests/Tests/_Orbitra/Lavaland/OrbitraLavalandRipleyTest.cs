#nullable enable
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._Orbitra.Lavaland;
using Content.Shared.Mech;
using Content.Shared.Mech.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Stacks;
using Content.Shared.Vehicle.Systems;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;

namespace Content.IntegrationTests.Tests._Orbitra.Lavaland;

public sealed class OrbitraLavalandRipleyTest : InteractionTest
{
    protected override string PlayerPrototype => "MobHuman";
    private const string Wreck = "OrbitraMechRipleyAbandoned";

    [Test]
    public async Task StripDrillFromUnpoweredWreck()
    {
        var mech = ToServer(await SpawnTarget(Wreck));
        var equipment = SEntMan.GetComponent<MechComponent>(mech);
        var drill = equipment.EquipmentContainer.ContainedEntities.Single();

        await Server.WaitAssertion(() =>
        {
            Assert.That(equipment.BatterySlot.ContainedEntity, Is.Null);
            Assert.That(SEntMan.GetComponent<PhysicsComponent>(mech).BodyType, Is.EqualTo(BodyType.Static));
            Assert.That(SEntMan.System<PullingSystem>().TryStartPull(SPlayer, mech), Is.False);
            Assert.That(SEntMan.System<VehicleSystem>().TryEnter(mech, SPlayer), Is.False);
            var verbs = SEntMan.System<SharedVerbSystem>();
            var panelName = Server.ResolveDependency<ILocalizationManager>().GetString("mech-ui-open-verb");
            var open = verbs.GetLocalVerbs(mech, SPlayer, typeof(AlternativeVerb)).Single(v => v.Text == panelName);
            verbs.ExecuteVerb(open, SPlayer, mech);
        });
        await RunTicks(10);
        await SendBui(MechUiKey.Key, new MechEquipmentRemoveMessage(SEntMan.GetNetEntity(drill)));
        await Server.WaitAssertion(() =>
        {
            Assert.That(equipment.EquipmentContainer.Count, Is.Zero);
            Assert.That(HandSys.TryPickup(SPlayer, drill), Is.True);
            Assert.That(SMelee.TryGetWeapon(SPlayer, out var handTool, out _), Is.True);
            Assert.That(handTool, Is.EqualTo(drill));
            Assert.That(SEntMan.GetComponent<InputMoverComponent>(mech).CanMove, Is.False);
        });
    }

    [Test]
    public async Task RepairThenDriveWithPilotControlsUntilCellIsLost()
    {
        await AddAtmosphere();
        await AddGravity();
        for (var i = 1; i <= 5; i++)
            await SetTile(Plating, new NetCoordinates(TargetCoords.NetEntity, TargetCoords.Position + new Vector2(i, 0)), MapData.Grid);

        var mech = ToServer(await SpawnTarget(Wreck));
        var wreck = SEntMan.GetComponent<OrbitraSalvageMechComponent>(mech);
        var mover = SEntMan.GetComponent<InputMoverComponent>(mech);
        var equipment = SEntMan.GetComponent<MechComponent>(mech);
        var transform = SEntMan.System<SharedTransformSystem>();

        var spare = ToServer(await PlaceInHands("CableApcStack", 2));
        await Interact();
        Assert.That(wreck.DriveRepaired, Is.False);
        Assert.That(SEntMan.GetComponent<StackComponent>(spare).Count, Is.EqualTo(2));

        var cell = ToServer(await PlaceInHands("PowerCellSmall"));
        await Interact();
        Assert.That(equipment.BatterySlot.ContainedEntity, Is.EqualTo(cell));
        Assert.That(mover.CanMove, Is.False);

        var cable = ToServer(await PlaceInHands("CableApcStack", 2));
        var stack = SEntMan.GetComponent<StackComponent>(cable);
        await Interact(awaitDoAfters: false);
        Assert.That(ActiveDoAfters.Any(a => a.Args.Event is OrbitraMechDriveRepairEvent), Is.True);
        await Server.WaitAssertion(() => Assert.That(HandSys.TryDrop(SPlayer, cable), Is.True));
        await RunSeconds(6);
        Assert.That(wreck.DriveRepaired, Is.False);
        Assert.That(stack.Count, Is.EqualTo(2));

        await Pickup(SEntMan.GetNetEntity(cable));
        await Interact();
        Assert.That(wreck.DriveRepaired, Is.True);
        Assert.That(stack.Count, Is.EqualTo(1));
        Assert.That(SEntMan.GetComponent<PhysicsComponent>(mech).BodyType, Is.EqualTo(BodyType.KinematicController));

        await Server.WaitAssertion(() => Assert.That(SEntMan.System<VehicleSystem>().TryEnter(mech, SPlayer), Is.True));
        await RunTicks(10);
        Assert.That(SEntMan.GetComponent<RelayInputMoverComponent>(SPlayer).RelayEntity, Is.EqualTo(mech));
        var start = transform.GetWorldPosition(mech);
        var battery = SEntMan.System<SharedBatterySystem>();
        var charge = SEntMan.GetComponent<BatteryComponent>(cell);
        var initialCharge = battery.GetCharge(cell).Charge;
        await Move(DirectionFlag.East, 0.75f);
        Assert.That(transform.GetWorldPosition(mech).X, Is.GreaterThan(start.X + 0.5f));
        Assert.That(battery.GetCharge(cell).Charge, Is.LessThan(initialCharge));

        await Server.WaitPost(() => battery.SetCharge((cell, charge), 0));
        await RunTicks(10);
        Assert.That(mover.CanMove, Is.False);
        var stopped = transform.GetWorldPosition(mech);
        await Move(DirectionFlag.East, 0.5f);
        Assert.That(Vector2.Distance(transform.GetWorldPosition(mech), stopped), Is.LessThan(0.1f));
        await Server.WaitAssertion(() =>
        {
            battery.SetCharge((cell, charge), 100);
            Assert.That(mover.CanMove, Is.True);
            Assert.That(SEntMan.System<SharedContainerSystem>().Remove(cell, equipment.BatterySlot), Is.True);
            Assert.That(mover.CanMove, Is.False);
        });
    }
}
