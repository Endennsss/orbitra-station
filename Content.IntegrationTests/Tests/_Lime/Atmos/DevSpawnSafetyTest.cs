using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Tests.Helpers;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Temperature.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using System.Linq;

namespace Content.IntegrationTests.Tests._Lime.Atmos;

[TestFixture]
public sealed partial class DevSpawnSafetyTest : GameTest
{
#pragma warning disable CS0618 // План проверки требует фиксировать DamageChangedEvent.
    private sealed class DevSpawnDamageListenerSystem : TestListenerSystem<DamageChangedEvent>;
#pragma warning restore CS0618

    public override PoolSettings PoolSettings => new()
    {
        Connected = false,
        DummyTicker = false,
        Dirty = true,
        Map = "Dev",
    };

    [TestCase("SpawnPointCaptain", "MobHuman")]
    [TestCase("SpawnPointCaptain", "MobDwarf")]
    [TestCase("SpawnPointCaptain", "MobReptilian")]
    [TestCase("SpawnPointLatejoin", "MobHuman")]
    [TestCase("SpawnPointLatejoin", "MobDwarf")]
    [TestCase("SpawnPointLatejoin", "MobReptilian")]
    public async Task DevSpawnRoomDoesNotDamagePlayableSpecies(string spawnPrototype, string species)
    {
        EntityCoordinates spawnCoordinates = default;
        EntityUid mob = default;

        await Server.WaitPost(() =>
        {
            var query = SEntMan.AllEntityQueryEnumerator<MetaDataComponent, TransformComponent>();
            while (query.MoveNext(out _, out var metadata, out var transform))
            {
                if (metadata.EntityPrototype?.ID != spawnPrototype)
                    continue;

                spawnCoordinates = transform.Coordinates;
                break;
            }

            Assert.That(spawnCoordinates.IsValid(SEntMan), Is.True,
                $"Dev map has no valid {spawnPrototype} spawn point.");
            mob = SEntMan.SpawnEntity(species, spawnCoordinates);
            SEntMan.EnsureComponent<TestListenerComponent>(mob);
        });

        await Server.WaitRunTicks((int) (TimeSpan.FromMinutes(5).TotalSeconds * SGameTiming.TickRate));

        await Server.WaitAssertion(() =>
        {
            var atmosphere = Server.System<AtmosphereSystem>();
            var damageable = Server.System<DamageableSystem>();

            var mixture = atmosphere.GetContainingMixture(mob, excite: false);
            var oxygenPartialPressure = mixture == null || mixture.TotalMoles <= 0f
                ? 0f
                : mixture.Pressure * mixture.GetMoles(Gas.Oxygen) / mixture.TotalMoles;
            var mixtureInfo = mixture == null
                ? "no atmosphere"
                : $"P={mixture.Pressure:F2} kPa, T={mixture.Temperature:F2} K, " +
                  $"O2={mixture.GetMoles(Gas.Oxygen):F3} ({oxygenPartialPressure:F2} kPa), " +
                  $"N2={mixture.GetMoles(Gas.Nitrogen):F3}";
            var allDamage = damageable.GetAllDamage(mob);
            var positiveDamage = allDamage.DamageDict.Where(static pair => pair.Value > 0).ToArray();
            var damageInfo = positiveDamage.Length == 0
                ? "none"
                : string.Join(", ", positiveDamage.Select(static pair => $"{pair.Key}={pair.Value}"));
            var temperature = SEntMan.GetComponent<TemperatureComponent>(mob);
            var thresholds = SEntMan.GetComponent<TemperatureDamageComponent>(mob);
            var bodyInfo = $"body T={temperature.Temperature:F2} K, " +
                           $"safe=({thresholds.ColdDamageThreshold:F2}, {thresholds.HeatDamageThreshold:F2}) K";
#pragma warning disable CS0618 // План проверки требует фиксировать DamageChangedEvent.
            var damageEvents = Server.System<DevSpawnDamageListenerSystem>()
                .GetEvents(mob, static ev => ev.DamageIncreased)
                .ToArray();
#pragma warning restore CS0618
            var eventInfo = string.Join("; ", damageEvents.Select(static ev => ev.DamageDelta == null
                ? "unknown damage"
                : string.Join(", ", ev.DamageDelta.DamageDict.Select(static pair => $"{pair.Key}={pair.Value}"))));

            Assert.That(mixture, Is.Not.Null, $"{SEntMan.ToPrettyString(mob)} spawned without atmosphere.");
            Assert.Multiple(() =>
            {
                Assert.That(mixture!.Pressure, Is.InRange(80f, 120f),
                    $"Unsafe pressure for {SEntMan.ToPrettyString(mob)}: {mixtureInfo}; {bodyInfo}; damage: {damageInfo}");
                Assert.That(oxygenPartialPressure, Is.InRange(16f, 30f),
                    $"Unsafe oxygen for {SEntMan.ToPrettyString(mob)}: {mixtureInfo}; {bodyInfo}; damage: {damageInfo}");
                Assert.That(temperature.Temperature,
                    Is.GreaterThan(thresholds.ColdDamageThreshold).And.LessThan(thresholds.HeatDamageThreshold),
                    $"Unsafe body temperature for {SEntMan.ToPrettyString(mob)}: " +
                    $"{bodyInfo}; {mixtureInfo}; damage: {damageInfo}");
                Assert.That(positiveDamage, Is.Empty,
                    $"Unexpected damage on {SEntMan.ToPrettyString(mob)} after five minutes: " +
                    $"{damageInfo}; {bodyInfo}; {mixtureInfo}");
                Assert.That(damageEvents, Is.Empty,
                    $"Positive DamageChangedEvent on {SEntMan.ToPrettyString(mob)}: " +
                    $"{eventInfo}; {bodyInfo}; {mixtureInfo}");
            });
        });
    }
}
