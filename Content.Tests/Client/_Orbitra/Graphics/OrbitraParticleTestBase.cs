using Content.Shared._Orbitra.Particles;
using NUnit.Framework;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;

namespace Content.Tests.Client._Orbitra.Graphics;

/// <summary>Loads isolated particle presets through the same serialization path as game prototypes.</summary>
public abstract class OrbitraParticleTestBase : ContentUnitTest
{
    private IPrototypeManager _prototypes = default!;

    [OneTimeSetUp]
    public void LoadParticlePresets()
    {
        IoCManager.Resolve<ISerializationManager>().Initialize();
        _prototypes = IoCManager.Resolve<IPrototypeManager>();
        _prototypes.Initialize();
        _prototypes.LoadString("""
            - type: orbitraParticleEffect
              id: OrbitraTestChip
            - type: orbitraParticleEffect
              id: OrbitraTestSmoke
              smoke: true
            - type: orbitraParticleEffect
              id: OrbitraTestSpark
              emissive: true
            - type: orbitraParticleEffect
              id: OrbitraTestRising
              drag: 0.7
            - type: orbitraParticleEffect
              id: OrbitraTestAmbient
              ambient: true
            - type: orbitraParticleEffect
              id: OrbitraTestMark
              impactMark: true
            """);
        _prototypes.ResolveResults();
    }

    protected OrbitraParticleEffectPrototype Effect(ProtoId<OrbitraParticleEffectPrototype> id) => _prototypes.Index(id);
}
