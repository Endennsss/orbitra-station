using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Orbitra.Lavaland;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Content.Shared.Weather;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Spawners;

namespace Content.IntegrationTests.Tests._Orbitra.Lavaland;

public sealed class OrbitraAshTrailTest : GameTest
{
    [Test]
    public async Task PassageUncoversOnlyItsFootprintAndRenewsExistingTracks()
    {
        var map = (await Pair.CreateTestMap()).MapUid;
        var ash = SEntMan.System<OrbitraAshFrontSystem>();
        var weather = SEntMan.System<SharedWeatherSystem>();
        var tracks = Array.Empty<EntityUid>();
        OrbitraAshFrontComponent front = default!;
        await Server.WaitAssertion(() =>
        {
            ash.RegisterTrail(map, Direction.North, "OrbitraAshMechTrack", new(30, 1), new(28, 1));
            ash.RegisterTrail(map, Direction.North, "OrbitraAshCable", new(-65, 0), new(-66, 0));
            ash.RegisterTrail(map, Direction.North, "OrbitraAshCable", new(0, 49), new(1, 49));
            ash.RegisterTrail(map, Direction.East, "OrbitraAshStretcherTrack", new(30, 2), new(31, 2));
            front = SEntMan.GetComponent<OrbitraAshFrontComponent>(map);
            front.Sites.Add(Direction.North, Vector2.Zero);
            front.Sites.Add(Direction.East, Vector2.Zero);
            front.FirstSector = Direction.North;
            front.Sector = Direction.North;
            front.NextChange = SGameTiming.CurTime;
        });
        await Pair.RunSeconds(1.25f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(front.Phase, Is.EqualTo(OrbitraAshPhase.Forecast));
            Assert.That(front.Sector, Is.EqualTo(Direction.North));
            Assert.That(front.FirstSector, Is.EqualTo(Direction.Invalid));
            Assert.That(VisibleTracks(map), Is.Empty);
            front.NextChange = SGameTiming.CurTime;
        });
        await Pair.RunSeconds(1.25f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(front.Phase, Is.EqualTo(OrbitraAshPhase.Passing));
            Assert.That(VisibleTracks(map), Is.Empty);
            AcceleratePassage();
        });
        await Pair.RunSeconds(3.4f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(front.Phase, Is.EqualTo(OrbitraAshPhase.Passing));
            Assert.That(VisibleTracks(map), Has.Length.EqualTo(3));
        });
        await Pair.RunSeconds(2.6f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(front.Phase, Is.EqualTo(OrbitraAshPhase.Clear));
            tracks = VisibleTracks(map);
            Assert.That(tracks.Select(uid => SEntMan.GetComponent<TransformComponent>(uid).LocalPosition.Floored()),
                Is.EquivalentTo(new Vector2i[] { new(30, 1), new(29, 1), new(28, 1), new(-65, 0), new(-66, 0) }));
            foreach (var uid in tracks)
            {
                var xform = SEntMan.GetComponent<TransformComponent>(uid);
                var tile = xform.LocalPosition.Floored();
                var expected = tile.Y == 1 ? "OrbitraAshMechTrack" : "OrbitraAshCable";
                Assert.That(SEntMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID, Is.EqualTo(expected));
                Assert.That(xform.LocalRotation, Is.EqualTo(Direction.West.ToAngle()));
                var timer = SEntMan.GetComponent<TimedDespawnComponent>(uid);
                Assert.That(timer.Lifetime, Is.InRange(233f, 240f));
                timer.Lifetime = 60f;
            }
            front.NextChange = SGameTiming.CurTime;
        });
        await Pair.RunSeconds(1.25f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(front.Sector, Is.EqualTo(Direction.East));
            front.NextChange = SGameTiming.CurTime;
        });
        await Pair.RunSeconds(1.25f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.System<StatusEffectsSystem>().TryRemoveStatusEffect(map, OrbitraAshFrontSystem.Weather), Is.True);
        });
        await Pair.RunSeconds(1.25f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(front.Phase, Is.EqualTo(OrbitraAshPhase.Clear));
            Assert.That(VisibleTracks(map), Is.EquivalentTo(tracks));
            front.NextChange = SGameTiming.CurTime;
        });
        await Pair.RunSeconds(1.25f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(front.Sector, Is.EqualTo(Direction.North));
            front.NextChange = SGameTiming.CurTime;
        });
        await Pair.RunSeconds(1.25f);
        await Server.WaitAssertion(AcceleratePassage);
        await Pair.RunSeconds(6);
        await Server.WaitAssertion(() =>
        {
            Assert.That(VisibleTracks(map), Is.EquivalentTo(tracks));
            foreach (var uid in tracks)
            {
                var timer = SEntMan.GetComponent<TimedDespawnComponent>(uid);
                Assert.That(timer.Lifetime, Is.InRange(233f, 240f));
                timer.Lifetime = 0f;
            }
        });
        await Pair.RunTicksSync(3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(VisibleTracks(map), Is.Empty);
            Assert.That(front.Trails.Values.SelectMany(tiles => tiles.Values).All(tile => tile.Visible == null), Is.True);
        });

        void AcceleratePassage()
        {
            Assert.That(weather.TryAddWeather(map, OrbitraAshFrontSystem.Weather, out var effect, TimeSpan.FromSeconds(4)), Is.True);
            front.NextChange = SEntMan.GetComponent<StatusEffectComponent>(effect!.Value).EndEffectTime!.Value;
        }
    }

    private EntityUid[] VisibleTracks(EntityUid map)
    {
        var children = SEntMan.GetComponent<TransformComponent>(map).ChildEnumerator;
        var tracks = new List<EntityUid>();
        while (children.MoveNext(out var child))
        {
            if (SEntMan.HasComponent<OrbitraAshTrackComponent>(child))
                tracks.Add(child);
        }
        return tracks.ToArray();
    }
}
