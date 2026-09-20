using Content.Client.Humanoid;
using Content.IntegrationTests.Pair;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Commands;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests.Markings;

[AttributeUsage(AttributeTargets.Method)]
public sealed class MarkingTestAttribute : TestAttribute, IWrapTestMethod
{
    private sealed class MarkingTestCommand(TestCommand inner) : DelegatingTestCommand(inner)
    {
        public override TestResult Execute(TestExecutionContext context)
        {
            var fixture = innerCommand.Test.Fixture as MarkingsViewModelTests;
            fixture!.Client.WaitAssertion(() =>
                {
                    context.CurrentResult = innerCommand.Execute(context);
                })
                .Wait();
            return context.CurrentResult;
        }
    }

    public TestCommand Wrap(TestCommand command)
    {
        return new MarkingTestCommand(command);
    }
}

[TestFixture]
[TestOf(typeof(MarkingsViewModel))]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public sealed class MarkingsViewModelTests
{
    public ProtoId<SpeciesPrototype> TestSpecies = "Reptilian";
    public ProtoId<OrganCategoryPrototype> Head = "Head";
    public ProtoId<OrganCategoryPrototype> Torso = "Torso";
    public ProtoId<MarkingPrototype> LizardSnoutRound = "LizardSnoutRound";
    public ProtoId<MarkingPrototype> LizardSnoutSharp = "LizardSnoutSharp";
    public ProtoId<MarkingPrototype> LizardChestTiger = "LizardChestTiger";
    public ProtoId<MarkingPrototype> LizardChestUnderbelly = "LizardChestUnderbelly";
    public ProtoId<MarkingPrototype> LizardChestBackspikes = "LizardChestBackspikes";
    public ProtoId<MarkingPrototype> LizardChestFin = "LizardChestFin";

    public TestPair Pair = default!;
    public RobustIntegrationTest.ClientIntegrationInstance Client => Pair.Client;
    public MarkingsViewModel Model = default!;
    public MarkingManager Manager = default!;

    [SetUp]
    public async Task SetUp()
    {
        Pair = await PoolManager.GetServerClient(testContext: new NUnitTestContextWrap(TestContext.CurrentContext, TestContext.Out));
        await Client.WaitPost(() =>
        {
            Model = new MarkingsViewModel();
            Manager = Client.ResolveDependency<MarkingManager>();

            Model.OrganData = Manager.GetMarkingData(TestSpecies);
            Model.OrganProfileData = Manager.GetProfileData(TestSpecies, Sex.Male, Color.White, Color.White);
            Model.ValidateMarkings();
        });
    }

    [TearDown]
    public async Task TearDown()
    {
        await Pair.CleanReturnAsync();
    }

    [MarkingTest]
    public void MarkingSelection()
    {
        Assert.That(Model.TrySelectMarking(Head, HumanoidVisualLayers.Snout, LizardSnoutSharp), Is.True, "You should be able to select a marking in a limit-1 category if another marking is selected");
        Assert.That(Model.SelectedMarkings(Head, HumanoidVisualLayers.Snout)!, Has.Count.EqualTo(1), "The markings model should respect the limits when selecting markings");
        Assert.That(Model.SelectedMarkings(Head, HumanoidVisualLayers.Snout)![0].MarkingId, Is.EqualTo(LizardSnoutSharp), "The markings model should replace the default snout");

        Assert.That(Model.TrySelectMarking(Torso, HumanoidVisualLayers.Chest, LizardChestTiger), Is.True);
        Assert.That(Model.SelectedMarkings(Torso, HumanoidVisualLayers.Chest)!, Has.Count.EqualTo(1));
        Assert.That(Model.SelectedMarkings(Torso, HumanoidVisualLayers.Chest)![0].MarkingId, Is.EqualTo(LizardChestTiger));

        Assert.That(Model.TrySelectMarking(Torso, HumanoidVisualLayers.Chest, LizardChestUnderbelly), Is.True);
        Assert.That(Model.SelectedMarkings(Torso, HumanoidVisualLayers.Chest)!, Has.Count.EqualTo(2));
        Assert.That(Model.SelectedMarkings(Torso, HumanoidVisualLayers.Chest)![1].MarkingId, Is.EqualTo(LizardChestUnderbelly));

        Assert.That(Model.TrySelectMarking(Torso, HumanoidVisualLayers.Chest, LizardChestBackspikes), Is.True);
        Assert.That(Model.TrySelectMarking(Torso, HumanoidVisualLayers.Chest, LizardChestFin), Is.False);
        Assert.That(Model.TrySelectMarking(Torso, HumanoidVisualLayers.Chest, LizardSnoutSharp), Is.False);

        Model.EnforceLimits = false;
        Assert.That(Model.TrySelectMarking(Torso, HumanoidVisualLayers.Chest, LizardChestFin), Is.True);
        Assert.That(Model.SelectedMarkings(Torso, HumanoidVisualLayers.Chest)!, Has.Count.EqualTo(4));
        Assert.That(Model.SelectedMarkings(Torso, HumanoidVisualLayers.Chest)![3].MarkingId, Is.EqualTo(LizardChestFin));
    }

    [MarkingTest]
    public void MarkingDeselection()
    {
        Assert.That(Model.TryDeselectMarking(Head, HumanoidVisualLayers.Snout, LizardSnoutRound), Is.False);
        Assert.That(Model.SelectedMarkings(Head, HumanoidVisualLayers.Snout)!, Has.Count.EqualTo(1));
        Assert.That(Model.SelectedMarkings(Head, HumanoidVisualLayers.Snout)![0].MarkingId, Is.EqualTo(LizardSnoutRound));

        Model.EnforceLimits = false;

        Assert.That(Model.TryDeselectMarking(Head, HumanoidVisualLayers.Snout, LizardSnoutRound), Is.True);
        Assert.That(Model.SelectedMarkings(Head, HumanoidVisualLayers.Snout)!, Has.Count.EqualTo(0));
    }

    [MarkingTest]
    public void MarkingColors()
    {
        Model.TrySetMarkingColor(Head, HumanoidVisualLayers.Snout, LizardSnoutRound, 0, Color.AliceBlue);
        Assert.That(Model.SelectedMarkings(Head, HumanoidVisualLayers.Snout)![0].MarkingColors[0], Is.EqualTo(Color.AliceBlue));
    }

    [MarkingTest]
    public void MarkingColorRestoration()
    {
        Model.EnforceLimits = false;
        Model.TrySetMarkingColor(Head, HumanoidVisualLayers.Snout, LizardSnoutRound, 0, Color.AliceBlue);
        Assert.That(Model.SelectedMarkings(Head, HumanoidVisualLayers.Snout)![0].MarkingColors[0], Is.EqualTo(Color.AliceBlue));

        Assert.That(Model.TryDeselectMarking(Head, HumanoidVisualLayers.Snout, LizardSnoutRound), Is.True);
        Assert.That(Model.TrySelectMarking(Head, HumanoidVisualLayers.Snout, LizardSnoutRound), Is.True);

        Assert.That(Model.SelectedMarkings(Head, HumanoidVisualLayers.Snout)![0].MarkingColors[0], Is.EqualTo(Color.AliceBlue));
    }
}
