using System.IO;
using System.Text;
using Content.Shared._Orbitra.Compatibility;
using Content.Shared._Orbitra.Graphics;
using Content.Shared._Orbitra.Particles;
using Moq;
using NUnit.Framework;
using Robust.Shared.Configuration;
using Robust.Shared.Log;
using Robust.Shared.Timing;
using Robust.UnitTesting;

namespace Content.Tests.Client._Orbitra;

[TestFixture]
public sealed class OrbitraSettingsMigrationTest
{
    private static IConfigurationManager CreateConfig(string text)
    {
        var config = MockInterfaces.MakeConfigurationManager(new Mock<IGameTiming>().Object, new LogManager(), isServer: false,
            loadCvarsFromTypes: [typeof(OrbitraLegacySettings), typeof(OrbitraBloomCVars), typeof(OrbitraParticleCVars)]);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        config.LoadFromTomlStream(stream);
        return config;
    }

    [Test]
    public void ImportsLegacyAndKeepsNewValuesOnRepeat()
    {
        var config = CreateConfig("[lime]\nbloom_enabled = false\nbloom_strength = 0.8\nbloom_quality = 'High'\nparticles_quality = 'Low'\n");
        OrbitraLegacySettings.Import(config);
        Assert.That(config.GetCVar(OrbitraBloomCVars.Enabled), Is.False);
        Assert.That(config.GetCVar(OrbitraBloomCVars.Strength), Is.EqualTo(0.8f));
        Assert.That(config.GetCVar(OrbitraBloomCVars.Quality), Is.EqualTo("High"));
        Assert.That(config.GetCVar(OrbitraParticleCVars.Quality), Is.EqualTo("Low"));
        config.SetCVar(OrbitraBloomCVars.Strength, 0.2f);
        OrbitraLegacySettings.Import(config);
        Assert.That(config.GetCVar(OrbitraBloomCVars.Strength), Is.EqualTo(0.2f));
    }

    [Test]
    public void ExplicitNewDefaultWinsOverLegacy()
    {
        var config = CreateConfig("[lime]\nbloom_strength = 0.9\n[orbitra]\nbloom_strength = 0.4\n");
        OrbitraLegacySettings.Import(config);
        Assert.That(config.GetCVar(OrbitraBloomCVars.Strength), Is.EqualTo(0.4f));
        using var stream = new MemoryStream();
        config.SaveToTomlStream(stream, [OrbitraBloomCVars.Strength.Name]);
        Assert.That(Encoding.UTF8.GetString(stream.ToArray()), Does.Contain("orbitra"));
    }
}
