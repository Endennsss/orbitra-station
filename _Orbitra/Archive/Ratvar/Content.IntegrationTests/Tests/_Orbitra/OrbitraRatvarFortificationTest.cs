using Content.IntegrationTests.Tests.Interaction;
using Content.Server.Atmos.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Stacks;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Orbitra;

public sealed class OrbitraRatvarFortificationTest : InteractionTest
{
    private const string Brass = "SheetBrass1";
    private const string BrassTile = "OrbitraRatvarFloorTile";
    private const string BrassFloor = "OrbitraRatvarFloor";
    private const string Barricade = "OrbitraRatvarBarricade";
    private static readonly ProtoId<DamageTypePrototype> StructuralDamage = "Structural";

    [Test]
    public async Task CraftOneTileFromOneBrassSheet()
    {
        await PlaceInHands(Brass);
        await CraftItem(BrassTile);
        var tile = await FindEntity((BrassTile, 1));
        await Server.WaitAssertion(() => Assert.That(SEntMan.GetComponent<StackComponent>(tile).Count, Is.EqualTo(1)));
        await FindEntity(Brass, shouldSucceed: false);
    }

    [Test]
    public async Task CannotCraftTileWithoutBrass()
    {
        await PlaceInHands(Steel);
        await CraftItem(BrassTile, shouldSucceed: false);
        await FindEntity(BrassTile, shouldSucceed: false);
        await FindEntity(Steel);
    }

    [TestCase("Plating")]
    [TestCase("PlatingSnow")]
    public async Task PlaceAndRemoveTilePreservesBase(string baseTile)
    {
        await SetTile(baseTile);
        await InteractUsing(BrassTile);
        await AssertTile(BrassFloor);
        Assert.That(HandSys.GetActiveItem((SPlayer, Hands)), Is.Null);
        await InteractUsing(Pry);
        await AssertTile(baseTile);
        await AssertEntityLookup((BrassTile, 1));
    }

    [Test]
    public async Task FloorDoesNotDeleteExistingItems()
    {
        var cable = await Spawn("CableHV");
        var pipe = await Spawn("GasPipeStraight");
        await InteractUsing(BrassTile);
        await AssertTile(BrassFloor);
        await InteractUsing(Pry);
        await AssertTile(Plating);
        AssertPrototype("CableHV", cable);
        AssertPrototype("GasPipeStraight", pipe);
        await AssertEntityLookup("CableHV", "GasPipeStraight", (BrassTile, 1));
    }

    [Test]
    public async Task ConstructBarricade()
    {
        await StartConstruction(Barricade);
        await InteractUsing(Brass, 4);
        AssertPrototype(Barricade);
        Assert.That(HandSys.GetActiveItem((SPlayer, Hands)), Is.Null);
        await AssertEntityLookup();
        await Server.WaitAssertion(() => Assert.That(SEntMan.HasComponent<AirtightComponent>(STarget!.Value), Is.False));
    }

    [Test]
    public async Task DeconstructBarricadeReturnsFourSheets()
    {
        await StartDeconstruction(Barricade);
        await InteractUsing(Cut);
        AssertDeleted();
        await AssertEntityLookup((Brass, 4));
    }

    [Test]
    public async Task DestructionReturnsLessThanConstructionCost()
    {
        await SpawnTarget(Barricade);
        var damage = new DamageSpecifier(SProtoMan.Index(StructuralDamage), 80);
        await Server.WaitPost(() =>
            SEntMan.System<DamageableSystem>().TryChangeDamage(STarget!.Value, damage, ignoreResistances: true));
        await RunTicks(5);
        AssertDeleted();
        var sheets = await FindEntity(Brass);
        await Server.WaitAssertion(() => Assert.That(SEntMan.GetComponent<StackComponent>(sheets).Count, Is.InRange(1, 2)));
        await AssertEntityLookup((Brass, SEntMan.GetComponent<StackComponent>(sheets).Count));
    }
}
