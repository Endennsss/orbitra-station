using System.Numerics;
using System.Runtime.InteropServices;
using Content.Client.Graphics;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Orbitra.Particles;

/// <summary>Рисует пройденный пулей путь на пиксельной сетке её грида.</summary>
internal sealed class OrbitraBulletTrailOverlay : Overlay
{
    private readonly OrbitraParticleSystem _particles;
    private readonly SharedTransformSystem _transform;
    private readonly EntityQuery<TransformComponent> _xforms;
    private readonly EntityQuery<MapComponent> _maps;
    private readonly ILightManager _lights;
    private readonly IGameTiming _timing;
    private readonly OverlayResourceCache<ShaderInstance> _shaders = new();
    private readonly Func<IClydeViewport, ShaderInstance> _createShader;
    private readonly List<WorldRect> _pixels = new(96);
    private static readonly ProtoId<ShaderPrototype> TrailShader = "OrbitraBulletTrail";
    private const float Pixel = 1f / EyeManager.PixelsPerMeter;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public OrbitraBulletTrailOverlay(OrbitraParticleSystem particles, IEntityManager entities,
        IPrototypeManager prototypes, IGameTiming timing, ILightManager lights)
    {
        _particles = particles;
        _transform = entities.System<SharedTransformSystem>();
        _xforms = entities.GetEntityQuery<TransformComponent>();
        _maps = entities.GetEntityQuery<MapComponent>();
        _lights = lights;
        _timing = timing;
        _createShader = _ => prototypes.Index(TrailShader).InstanceUnique();
        ZIndex = 149;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args) =>
        args.Viewport.Eye != null && (_particles.Flying.Count != 0 || _particles.Fading.Count != 0);

    protected override void Draw(in OverlayDrawArgs args)
    {
        var eye = args.Viewport.Eye!;
        var shader = _shaders.GetForViewport(args.Viewport, _createShader);
        shader.SetParameter("FOV_TEXTURE", args.Viewport.FovRenderTarget.Texture);
        // Clyde обновляет текстуру глубины только для камер и карт с включённым светом.
        var useFov = eye.DrawFov && eye.DrawLight && _lights.Enabled &&
                     _maps.TryComp(args.MapUid, out var map) && map.LightingEnabled;
        shader.SetParameter("use_fov", useFov);
        shader.SetParameter("eye_position", eye.Position.Position);
        var handle = args.WorldHandle;
        var oldShader = handle.GetShader();
        var oldTransform = handle.GetTransform();
        var bounds = args.WorldAABB.Enlarged(1f);
        var now = _timing.RealTime;
        try
        {
            handle.UseShader(shader);
            foreach (var trail in _particles.Flying.Values)
                DrawTrail(handle, args.MapUid, bounds, trail, 1f);
            foreach (var trail in _particles.Fading)
            {
                var fade = 1f - (float) ((now - trail.Ended) / OrbitraParticleSystem.TrailFade);
                if (fade > 0f)
                    DrawTrail(handle, args.MapUid, bounds, trail, fade);
            }
        }
        finally
        {
            handle.SetTransform(oldTransform);
            handle.UseShader(oldShader);
        }
    }

    private void DrawTrail(DrawingHandleWorld handle, EntityUid map, Box2 bounds,
        OrbitraParticleSystem.BulletTrail trail, float fade)
    {
        var path = trail.Path;
        if (path.Count < 2 || !_xforms.TryComp(trail.Parent, out var xform) || xform.MapUid != map)
            return;

        var matrix = _transform.GetWorldMatrix(xform);
        if (!bounds.Contains(Vector2.Transform(path.GetPoint(path.Count - 1), matrix)))
            return;

        handle.SetTransform(matrix);
        _pixels.Clear();
        var travelled = 0f;
        for (var i = 1; i < path.Count; i++)
        {
            var start = path.GetPoint(i - 1);
            var end = path.GetPoint(i);
            var distance = Vector2.Distance(start, end);
            var from = (start * EyeManager.PixelsPerMeter).Floored();
            var to = (end * EyeManager.PixelsPerMeter).Floored();
            var delta = to - from;
            var steps = (int) Math.Max(Math.Abs(delta.X), Math.Abs(delta.Y));
            var edge = Math.Abs(delta.X) >= Math.Abs(delta.Y) ? Vector2.UnitY : Vector2.UnitX;
            // Общую точку соседних отрезков рисуем один раз, чтобы изгиб не становился ярче.
            for (var step = 1; step <= steps; step++)
            {
                var t = (float) step / steps;
                var along = (travelled + distance * t) / path.Length;
                var cell = from + delta * t;
                var corner = new Vector2(MathF.Round(cell.X), MathF.Round(cell.Y)) * Pixel;
                var tip = Math.Clamp((along - 0.60f) * 0.85f, 0f, 0.34f);
                var alpha = trail.Tint.A * fade * (0.14f + 0.84f * along);
                var color = Color.InterpolateBetween(trail.Tint, Color.White, tip).WithAlpha(alpha);
                var pixel = Box2.FromDimensions(corner, new Vector2(Pixel));
                _pixels.Add(new WorldRect(pixel, color));
                if (!trail.Thin && along > 0.66f)
                    _pixels.Add(new WorldRect(pixel.Translated(edge * Pixel), color.WithAlpha(alpha * 0.36f)));
            }
            travelled += distance;
        }
        handle.DrawRects(CollectionsMarshal.AsSpan(_pixels));
    }

    protected override void DisposeBehavior()
    {
        _shaders.Dispose();
        base.DisposeBehavior();
    }
}
