using System.Numerics;
using Content.Client.Graphics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._Orbitra.Particles;

/// <summary>Draws pooled particles in world space, with a separate FOV check at their origin.</summary>
internal sealed class OrbitraParticleOverlay : Overlay
{
    private readonly IEntityManager _entities;
    private readonly OrbitraParticleSystem _system;
    private readonly EntityLookupSystem _lookup;
    private readonly SharedTransformSystem _transform;
    private readonly HashSet<Entity<SpriteComponent>> _sources = new();
    private readonly ShaderInstance _lit;
    private readonly ShaderInstance _glow;
    private readonly OverlayResourceCache<ParticleShaders> _resources = new();
    private static readonly ProtoId<ShaderPrototype> LitShader = "OrbitraParticleLit";
    private static readonly ProtoId<ShaderPrototype> GlowShader = "OrbitraParticleGlow";
    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public OrbitraParticleOverlay(IEntityManager entities, OrbitraParticleSystem system)
    {
        _entities = entities;
        _system = system;
        _lookup = entities.System<EntityLookupSystem>();
        _transform = entities.System<SharedTransformSystem>();
        var prototypes = IoCManager.Resolve<IPrototypeManager>();
        _lit = prototypes.Index(LitShader).InstanceUnique();
        _glow = prototypes.Index(GlowShader).InstanceUnique();
        ZIndex = 150;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_system.Pool.Capacity == 0 || args.Viewport.Eye is not { } eye)
            return;
        _system.CollectAmbient(args);
        _sources.Clear();
        _lookup.GetEntitiesIntersecting(args.MapId, args.WorldAABB, _sources);
        foreach (var source in _sources)
        {
            if (source.Comp.Deleted || !source.Comp.Visible || source.Comp.ContainerOccluded || source.Comp.Color.A <= 0)
                continue;
            if ((_entities.GetComponent<MetaDataComponent>(source.Owner).Flags & MetaDataFlags.Detached) != 0)
                continue;
            _system.Collect(source.Owner);
            _system.CollectBreath(source, eye.Rotation);
        }

        if (_system.Pool.Count == 0)
            return;

        var handle = args.WorldHandle;
        var oldShader = handle.GetShader();
        var oldTransform = handle.GetTransform();
        var shaders = _resources.GetForViewport(args.Viewport, static _ => new ParticleShaders());
        var visibleBounds = args.WorldAABB.Enlarged(0.5f);
        try
        {
            for (var i = 0; i < _system.Pool.Count; i++)
            {
                ref var particle = ref _system.Pool.Particles[i];
                if (!_entities.TryGetComponent<TransformComponent>(particle.Parent, out var parent) || parent.MapID != args.MapId)
                    continue;
                var matrix = _transform.GetWorldMatrix(parent);
                var position = Vector2.Transform(particle.Position, matrix);
                if (!visibleBounds.Contains(position))
                    continue;
                var origin = Vector2.Transform(particle.Origin, matrix);
                var progress = Math.Clamp(particle.Age / particle.Lifetime, 0f, 1f);
                var effect = particle.Effect;
                // Команды Clyde исполняются отложенно: один mutable shader на все частицы теряет параметры.
                var instances = effect.Emissive ? shaders.Glow : shaders.Lit;
                var shader = instances[i] ??= (effect.Emissive ? _glow : _lit).Duplicate();
                SetFov(shader, args.Viewport);
                var tint = Color.InterpolateBetween(particle.Tint ?? effect.Color,
                    particle.Tint is { } blood ? new Color(blood.R * 0.45f, blood.G * 0.45f, blood.B * 0.45f, blood.A) : effect.EndColor, progress);
                tint.A *= (particle.Opacity ?? 1f) * (1f - progress) * Math.Min(1f, particle.Age * (effect.Ambient ? 1.5f : 40f));
                shader.SetParameter("particle_color", tint);
                shader.SetParameter("source_eye", origin - eye.Position.Position);
                shader.SetParameter("smoke", effect.Smoke);
                shader.SetParameter("shape", effect.Shape);
                handle.UseShader(shader);
                var size = particle.Size * (effect.Smoke ? 1f + progress * 2 : 1f);
                var rotation = MathF.Atan2(particle.Velocity.Y, particle.Velocity.X);
                handle.SetTransform(Matrix3x2.CreateRotation(rotation) * Matrix3x2.CreateTranslation(particle.Position) * matrix);
                handle.DrawTextureRect(Texture.White,
                    Box2.CenteredAround(Vector2.Zero, new Vector2(size * 2)), Color.White);
            }
        }
        finally
        {
            handle.UseShader(oldShader);
            handle.SetTransform(oldTransform);
        }
    }

    private static void SetFov(ShaderInstance shader, IClydeViewport viewport)
    {
        shader.SetParameter("FOV_TEXTURE", viewport.FovRenderTarget.Texture);
        shader.SetParameter("use_fov", viewport.Eye!.DrawFov);
    }

    protected override void DisposeBehavior()
    {
        _resources.Dispose();
        _lit.Dispose();
        _glow.Dispose();
        base.DisposeBehavior();
    }

    internal void ClearCache() => _resources.Dispose();

    private sealed class ParticleShaders : IDisposable
    {
        public readonly ShaderInstance?[] Glow = new ShaderInstance?[768];
        public readonly ShaderInstance?[] Lit = new ShaderInstance?[768];

        public void Dispose()
        {
            foreach (var shader in Glow)
                shader?.Dispose();
            foreach (var shader in Lit)
                shader?.Dispose();
        }
    }
}
