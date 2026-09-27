using Content.IntegrationTests.Tests.Interaction;
using Content.Server.Atmos.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Doors.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Orbitra;

public sealed class OrbitraRatvarTerritoryTest : InteractionTest
{
    private const string Brass = "SheetBrass1";
    private static readonly ProtoId<DamageTypePrototype> StructuralDamage = "Structural";

    [Test]
    public async Task ConstructBrassWall()
    {
        await StartConstruction("OrbitraRatvarWall");
        await InteractUsing(Steel, 2);
        AssertPrototype("Girder");
        await InteractUsing(Brass, 2);
        await InteractUsing(Weld);
        AssertPrototype("WallBrass");
        await AssertEntityLookup();
    }

    [Test]
    public async Task DeconstructBrassWall()
    {
        await StartDeconstruction("WallBrass");
        await Interact(Weld, Pry);
        AssertPrototype("Girder");
        await Interact(Wrench, Screw);
        AssertDeleted();
        await AssertEntityLookup((Brass, 2), (Steel, 2));
    }

    [Test]
    public async Task ConstructBrassWindow()
    {
        await StartConstruction("OrbitraRatvarWindow");
        await InteractUsing(Brass, 2);
        await InteractUsing(RGlass, 2);
        AssertPrototype("OrbitraRatvarWindow");
        Assert.That(HandSys.GetActiveItem((SPlayer, Hands)), Is.Null);
        await AssertEntityLookup();
    }

    [Test]
    public async Task DeconstructBrassWindow()
    {
        await StartDeconstruction("OrbitraRatvarWindow");
        await Interact(Weld, Screw, Pry, Weld, Screw, Wrench);
        AssertDeleted();
        await AssertEntityLookup((Brass, 2), (RGlass, 2));
    }

    [Test]
    public async Task ConstructBrassDoor()
    {
        await StartConstruction("OrbitraRatvarDoor");
        await InteractUsing(Brass, 10);
        AssertPrototype("OrbitraRatvarDoor");
        Assert.That(HandSys.GetActiveItem((SPlayer, Hands)), Is.Null);
        await AssertEntityLookup();
    }

    [Test]
    public async Task DeconstructBrassDoor()
    {
        await StartDeconstruction("OrbitraRatvarDoor");
        await InteractUsing(Wrench);
        AssertDeleted();
        await AssertEntityLookup((Brass, 10));
    }

    [Test]
    public async Task BrassDoorOpensWithoutPowerOrCultMembership()
    {
        await SpawnTarget("OrbitraRatvarDoor");
        await AssertDoorState(DoorState.Closed, true);
        await Activate();
        await RunSeconds(2);
        await AssertDoorState(DoorState.Open, false);
        await Activate();
        await RunSeconds(2);
        await AssertDoorState(DoorState.Closed, true);
    }

    [TestCase("OrbitraRatvarWall")]
    [TestCase("OrbitraRatvarWindow")]
    [TestCase("OrbitraRatvarDoor")]
    public async Task CannotBuildOverExistingWall(string recipe)
    {
        await SpawnTarget("WallSolid");
        await StartConstruction(recipe, shouldSucceed: false);
    }

    [TestCase("OrbitraRatvarWindow")]
    [TestCase("OrbitraRatvarDoor")]
    public async Task BrassStructureCanBeDestroyed(string prototype)
    {
        await SpawnTarget(prototype);
        var damage = new DamageSpecifier(SProtoMan.Index(StructuralDamage), 1000);
        await Server.WaitPost(() =>
            SEntMan.System<DamageableSystem>().TryChangeDamage(STarget!.Value, damage, ignoreResistances: true));
        await RunTicks(5);
        AssertDeleted();
    }

    private async Task AssertDoorState(DoorState state, bool airtight)
    {
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<DoorComponent>(STarget!.Value).State, Is.EqualTo(state));
            Assert.That(SEntMan.GetComponent<AirtightComponent>(STarget.Value).AirBlocked, Is.EqualTo(airtight));
        });
        await Client.WaitAssertion(() =>
            Assert.That(CEntMan.GetComponent<DoorComponent>(CTarget!.Value).State, Is.EqualTo(state)));
    }
}
