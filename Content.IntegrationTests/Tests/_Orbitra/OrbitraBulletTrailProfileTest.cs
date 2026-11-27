using Content.IntegrationTests.Fixtures;
using Content.Shared._Orbitra.Particles;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Orbitra;

[TestFixture]
public sealed class OrbitraBulletTrailProfileTest : GameTest
{
    [Test]
    public async Task ClientLoadsProfilesForLiveBallisticRounds()
    {
        await Client.WaitAssertion(() =>
        {
            CheckProfile(12, "#C8AC78", "BulletPistol", "BulletHivebot");
            CheckProfile(12, "#BF8059", "BulletPistolIncendiary");
            CheckProfile(12, "#929B70", "BulletPistolUranium");
            CheckProfile(20, "#C8AC78", "BulletMagnum", "BulletMagnumAP", "BulletRifle", "BulletLightRifle",
                "BulletHeavyRifle", "BulletMinigun", "BulletCaselessRifle", "BulletAntiMateriel", "PelletShotgunSlug");
            CheckProfile(20, "#BF8059", "BulletMagnumIncendiary", "BulletRifleIncendiary", "BulletLightRifleIncendiary");
            CheckProfile(20, "#929B70", "BulletMagnumUranium", "BulletRifleUranium", "BulletLightRifleUranium");
            CheckProfile(6, "#C8AC78A8", "PelletShotgun", "PelletShotgunSpread", "PelletShotgunImprovised",
                "PelletShotgunImprovisedSpread", "PelletGrapeshot", "PelletGrapeshotSpread", "PelletClusterLethal");
            CheckProfile(6, "#BF8059A8", "PelletShotgunIncendiary", "PelletShotgunIncendiarySpread");
            CheckProfile(6, "#929B70A8", "PelletShotgunUranium", "PelletShotgunUraniumSpread");

            string[] excluded =
            [
                "BulletPistolPractice", "BulletMagnumPractice", "BulletRiflePractice", "PelletShotgunPracticeSpread",
                "PelletShotgunBeanbag", "PelletShotgunTranquilizer", "PelletShotgunFlare", "RedLightLaser",
                "BulletDisabler", "BulletInstakillMagic", "BulletRocket", "BulletFoam", "BulletNanite",
            ];
            foreach (var id in excluded)
            {
                var projectile = CProtoMan.Index<EntityPrototype>(id);
                Assert.That(projectile.HasComp<OrbitraBulletTrailComponent>(CEntMan.ComponentFactory), Is.False, id);
            }
        });
    }

    private void CheckProfile(int length, string tint, params string[] projectiles)
    {
        foreach (var id in projectiles)
        {
            var projectile = CProtoMan.Index<EntityPrototype>(id);
            Assert.That(projectile.TryComp<OrbitraBulletTrailComponent>(out var trail, CEntMan.ComponentFactory), Is.True, id);
            Assert.That(trail!.LengthPixels, Is.EqualTo(length), id);
            Assert.That(trail.Tint, Is.EqualTo(Color.FromHex(tint)), id);
        }
    }
}
