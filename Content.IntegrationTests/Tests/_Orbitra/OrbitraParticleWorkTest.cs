using Content.IntegrationTests.Fixtures;
using Content.Shared._Orbitra.Particles;
using Content.Shared.DoAfter;
using Content.Shared.Tools;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;
using Content.Shared.Item.ItemToggle;
using Robust.Shared.Serialization;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Orbitra;

[TestFixture]
public sealed partial class OrbitraParticleWorkTest : GameTest
{
    [TestCase("Wrench", "Anchoring", "material", 3f)]
    [TestCase("PowerDrill", "Screwing", "material", 12f)]
    [TestCase("Welder", "Welding", "OrbitraParticleWelding", 24f)]
    public async Task ToolMetadataSurvivesCloningAndStopsWithAction(string prototype, string quality, string expected, float expectedRate)
    {
        var server = Pair.Server;
        var map = await Pair.CreateTestMap();
        EntityUid tool = default;
        EntityUid target = default;
        DoAfterId? id = null;
        var started = false;
        var qualities = new ProtoId<ToolQualityPrototype>[] { quality };
        await server.WaitPost(() =>
        {
            tool = server.EntMan.SpawnEntity(prototype, map.GridCoords);
            server.EntMan.AddComponent<DoAfterComponent>(tool);
            if (prototype == "Welder")
                server.System<ItemToggleSystem>().Toggle(tool, showPopup: false);
            target = server.EntMan.SpawnEntity("WallSolid", map.GridCoords);
            started = server.System<SharedToolSystem>().UseTool(tool, tool, target, TimeSpan.FromSeconds(5),
                qualities, new OrbitraParticleTestDoAfterEvent(), out id);
        });
        Assert.That(started, Is.True);
        Assert.That(id, Is.Not.Null);
        await server.WaitAssertion(() =>
        {
            var action = server.EntMan.GetComponent<DoAfterComponent>(tool).DoAfters[id!.Value.Index];
            var tools = server.System<SharedToolSystem>();
            Assert.That(tools.TryGetOrbitraParticleWork(action, out var effect, out var rate, out _), Is.True);
            Assert.That(effect, Is.EqualTo(expected));
            Assert.That(rate, Is.EqualTo(expectedRate));
            var clone = new Content.Shared.DoAfter.DoAfter(server.EntMan, action);
            Assert.That(tools.TryGetOrbitraParticleWork(clone, out var cloneEffect, out var cloneRate, out _), Is.True);
            Assert.That(cloneEffect, Is.EqualTo(effect));
            Assert.That(cloneRate, Is.EqualTo(rate));
            Assert.That(server.System<SharedOrbitraParticleSystem>().MaterialEffect(target), Is.EqualTo("OrbitraParticleMetal"));
        });
        if (prototype == "Welder")
        {
            await server.WaitPost(() => server.System<ItemToggleSystem>().Toggle(tool, showPopup: false));
            await server.WaitAssertion(() =>
            {
                var action = server.EntMan.GetComponent<DoAfterComponent>(tool).DoAfters[id!.Value.Index];
                Assert.That(server.System<SharedToolSystem>().TryGetOrbitraParticleWork(action, out _, out _, out _), Is.False);
            });
        }
        await server.WaitPost(() => server.System<SharedDoAfterSystem>().Cancel(id!.Value));
        await server.WaitAssertion(() =>
        {
            var actions = server.EntMan.GetComponent<DoAfterComponent>(tool).DoAfters;
            if (actions.TryGetValue(id!.Value.Index, out var action))
                Assert.That(server.System<SharedToolSystem>().TryGetOrbitraParticleWork(action, out _, out _, out _), Is.False);
        });
    }

    [Serializable, NetSerializable]
    private sealed partial class OrbitraParticleTestDoAfterEvent : DoAfterEvent
    {
        public override DoAfterEvent Clone() => new OrbitraParticleTestDoAfterEvent();
    }
}
