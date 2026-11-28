using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Orbitra;

public sealed class OrbitraWallLightTest : GameTest
{
    [Test]
    public async Task WallFixturesDrawOnTheWallAndIlluminateTheRoom()
    {
        await Client.WaitAssertion(() =>
        {
            var factory = Client.ResolveDependency<IComponentFactory>();
            foreach (var id in new[] { "PoweredSmallLight", "SmallLight", "AlwaysPoweredWallLight", "Poweredlight", "EmergencyLight" })
            {
                var prototype = CProtoMan.Index<EntityPrototype>(id);
                Assert.That(prototype.TryComp<SpriteComponent>(out var sprite, factory), Is.True, id);
                Assert.That(sprite!.Offset, Is.EqualTo(Vector2.UnitY), id);
                Assert.That(prototype.TryComp<PointLightComponent>(out var source, factory), Is.True, id);
                Assert.That(Math.Abs(source!.Offset.X), Is.LessThan(0.5f), id);
                Assert.That(Math.Abs(source.Offset.Y), Is.LessThan(0.5f), id);
            }
        });
    }
}
