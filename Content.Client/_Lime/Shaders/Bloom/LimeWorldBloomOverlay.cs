using System.Numerics;
using Content.Client.Graphics;
using Content.Shared._Lime.Graphics;
using Content.Shared.Light.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Utility;
using Robust.Shared.Enums;
using Robust.Shared.Graphics;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._Lime.Shaders.Bloom;

/// <summary>
/// Добавляет изолированное аддитивное размытие (Bloom) светящихся unshaded-слоев спрайтов поверх сцены.
/// </summary>
internal sealed partial class LimeWorldBloomOverlay : Overlay
{
    private const float BrightnessThreshold = 0.10f;
    private const float SoftKnee = 0.05f;
    internal const int TargetPadding = 18;

    private static readonly ProtoId<ShaderPrototype> ExtractShader = "LimeWorldBloomExtract";
    private static readonly ProtoId<ShaderPrototype> BlurShader = "LimeBloomBlur";
    private static readonly ProtoId<ShaderPrototype> DownsampleShader = "LimeBloomDownsample";
    private static readonly ProtoId<ShaderPrototype> CompositeShader = "LimeBloomComposite";

    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;

    private readonly EntityLookupSystem _lookup;
    private readonly TransformSystem _transform;
    private readonly SharedMapSystem _map;
    private readonly EntityQuery<TransformComponent> _transformQuery;
    private readonly EntityQuery<MapGridComponent> _gridQuery;
    private readonly HashSet<Entity<SpriteComponent>> _sprites = new();
    private readonly HashSet<Entity<TileEmissionComponent>> _tiles = new();
    private readonly OverlayResourceCache<CachedResources> _resources = new();

    private readonly ShaderInstance _extract;
    private readonly ShaderInstance _downsample;
    private readonly ShaderInstance _blurHorizontal;
    private readonly ShaderInstance _blurVertical;
    private readonly ShaderInstance _composite;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public bool Enabled = true;
    public float Strength = 0.4f;
    public LimeBloomQuality Quality = LimeBloomQuality.Medium;

    public LimeWorldBloomOverlay(IEntityManager entityManager)
    {
        IoCManager.InjectDependencies(this);
        _lookup = entityManager.System<EntityLookupSystem>();
        _transform = entityManager.System<TransformSystem>();
        _map = entityManager.System<SharedMapSystem>();
        _transformQuery = entityManager.GetEntityQuery<TransformComponent>();
        _gridQuery = entityManager.GetEntityQuery<MapGridComponent>();

        _extract = _prototypeManager.Index(ExtractShader).InstanceUnique();
        _downsample = _prototypeManager.Index(DownsampleShader).InstanceUnique();
        _blurHorizontal = _prototypeManager.Index(BlurShader).InstanceUnique();
        _blurVertical = _prototypeManager.Index(BlurShader).InstanceUnique();
        _composite = _prototypeManager.Index(CompositeShader).InstanceUnique();

        ZIndex = 100;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (!Enabled || Strength <= 0f || args.Viewport.Eye == null || args.MapId == MapId.Nullspace)
            return false;

        _sprites.Clear();
        _tiles.Clear();
        var bounds = args.WorldAABB.Enlarged(3f);
        _lookup.GetEntitiesIntersecting(args.MapId, bounds, _sprites);
        _lookup.GetEntitiesIntersecting(args.MapId, bounds, _tiles);
        _sprites.RemoveWhere(static source => !CanDrawSprite(source.Comp) || !HasBloomLayer(source.Comp));
        _tiles.RemoveWhere(static source => source.Comp.Deleted || source.Comp.Color.A <= 0f);
        return _sprites.Count != 0 || _tiles.Count != 0;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var viewport = args.Viewport;
        var eye = viewport.Eye;
        if (eye == null)
            return;

        var resources = _resources.GetForViewport(viewport, static _ => new CachedResources());
        var divisor = Quality == LimeBloomQuality.High ? 2 : 4;
        var contentSize = Vector2i.ComponentMax(Vector2i.One, viewport.Size / divisor);
        var targetSize = contentSize + new Vector2i(TargetPadding * 2, TargetPadding * 2);
        EnsureTargets(resources, targetSize, targetSize * divisor);

        var ping = resources.Ping!;
        var pong = resources.Pong!;
        var handle = args.WorldHandle;

        var worldToTarget = GetWorldToTargetMatrix(viewport.GetWorldToLocalMatrix(), viewport.Size, contentSize);
        var worldToSource = worldToTarget * Matrix3x2.CreateScale(divisor);
        if (!Matrix3x2.Invert(worldToTarget, out var targetToWorld))
            return;
        var screen = args.RenderHandle.DrawingHandleScreen;
        var oldTransform = handle.GetTransform();
        var oldShader = handle.GetShader();

        try
        {
            // RGB хранит энергию, alpha всегда единица: Clyde Add использует SrcAlpha/DstAlpha.
            handle.RenderInRenderTarget(resources.Source!, () =>
            {
                foreach (var sprite in _sprites)
                    DrawBloomLayers(handle, sprite, eye, viewport, worldToSource);
                DrawTileEmission(handle, eye, viewport, worldToSource);
            }, Color.Black);

            // Сначала собираем тонкие пиксели, затем усредняем всю площадь, а не один случайный texel.
            _downsample.SetParameter("sample_step", new Vector2(divisor * 0.25f) / (Vector2) resources.Source!.Size);
            screen.RenderInRenderTarget(ping, () =>
            {
                screen.SetTransform(Matrix3x2.Identity);
                screen.UseShader(_downsample);
                screen.DrawTextureRect(resources.Source.Texture, UIBox2.FromDimensions(Vector2.Zero, targetSize));
            }, Color.Black);

            var pixelsPerMeter = EyeManager.PixelsPerMeter * viewport.RenderScale / eye.Zoom *
                                 ((Vector2) contentSize / (Vector2) viewport.Size);
            var texelSize = Vector2.One / (Vector2) targetSize;
            DrawBlurPass(screen, ping.Texture, pong, _blurHorizontal, texelSize, Vector2.UnitX,
                CalculateBlurRadius(Quality, pixelsPerMeter.X));
            DrawBlurPass(screen, pong.Texture, ping, _blurVertical, texelSize, Vector2.UnitY,
                CalculateBlurRadius(Quality, pixelsPerMeter.Y));

            // Экранный quad преобразуется точной обратной матрицей маски, включая padding и поворот.
            screen.SetTransform(targetToWorld);
            _composite.SetParameter("bloom_strength", GetBloomStrength(Strength));
            screen.UseShader(_composite);
            screen.DrawTextureRect(ping.Texture, UIBox2.FromDimensions(Vector2.Zero, targetSize));
        }
        finally
        {
            handle.UseShader(oldShader);
            handle.SetTransform(oldTransform);
        }
    }

    private static void DrawBlurPass(
        DrawingHandleScreen handle,
        Texture source,
        IRenderTarget destination,
        ShaderInstance shader,
        Vector2 texelSize,
        Vector2 direction,
        float radius)
    {
        shader.SetParameter("texel_size", texelSize);
        shader.SetParameter("blur_direction", direction);
        shader.SetParameter("blur_radius", radius);

        handle.RenderInRenderTarget(destination, () =>
        {
            try
            {
                handle.SetTransform(Matrix3x2.Identity);
                handle.UseShader(shader);
                handle.DrawTextureRect(source, UIBox2.FromDimensions(Vector2.Zero, destination.Size), Color.White);
            }
            finally
            {
                handle.UseShader(null);
                handle.SetTransform(Matrix3x2.Identity);
            }
        }, Color.Transparent);
    }

    private static bool CanDrawSprite(SpriteComponent sprite)
    {
        return !sprite.Deleted && sprite.Visible && !sprite.ContainerOccluded && sprite.Color.A > 0f;
    }

    private static bool HasBloomLayer(SpriteComponent sprite)
    {
        foreach (var layer in sprite.AllLayers)
        {
            if (layer is SpriteComponent.Layer
                {
                    Visible: true,
                    Blank: false,
                    ShaderPrototype: var shader,
                    CopyToShaderParameters: null,
                } && shader == SpriteSystem.UnshadedId)
                return true;
        }

        return false;
    }

    private void DrawBloomLayers(
        DrawingHandleWorld handle,
        Entity<SpriteComponent> source,
        IEye eye,
        IClydeViewport viewport,
        Matrix3x2 worldToTarget)
    {
        var (worldPosition, worldRotation) = _transform.GetWorldPositionRotation(source);
        worldPosition += GetGridPixelSnapOffset(source, eye, viewport);

        foreach (var spriteLayer in source.Comp.AllLayers)
        {
            if (spriteLayer is not SpriteComponent.Layer
                {
                    Visible: true,
                    Blank: false,
                    ShaderPrototype: var shader,
                    CopyToShaderParameters: null,
                } layer || shader != SpriteSystem.UnshadedId)
                continue;

            DrawLayer(handle, source.Comp, layer, worldPosition, worldRotation, eye.Rotation, worldToTarget);
        }
    }

    private void DrawLayer(
        DrawingHandleWorld handle,
        SpriteComponent sprite,
        SpriteComponent.Layer layer,
        Vector2 worldPosition,
        Angle worldRotation,
        Angle eyeRotation,
        Matrix3x2 worldToTarget)
    {
        var angle = (worldRotation + eyeRotation).Reduced().FlipPositive();
        var state = layer.ActualState;
        var direction = state == null
            ? RsiDirection.South
            : SpriteComponent.Layer.GetDirection(state.RsiDirections, angle);
        layer.GetLayerDrawMatrix(direction, out var layerMatrix);

        if (sprite.EnableDirectionOverride && state != null)
            direction = sprite.DirectionOverride.Convert(state.RsiDirections);

        direction = direction.OffsetRsiDir(layer.DirOffset);
        var texture = state?.GetFrame(direction, layer.AnimationFrame) ?? layer.Texture;
        if (texture == null)
            return;

        var cardinal = !sprite.NoRotation && sprite.SnapCardinals
            ? angle.RoundToCardinalAngle()
            : Angle.Zero;
        var renderRotation = sprite.NoRotation ? -eyeRotation : worldRotation - cardinal;
        if (sprite.GranularLayersRendering)
        {
            renderRotation = layer.RenderingStrategy switch
            {
                LayerRenderingStrategy.Default => worldRotation,
                LayerRenderingStrategy.NoRotation => -eyeRotation,
                LayerRenderingStrategy.SnapToCardinals => worldRotation - angle.RoundToCardinalAngle(),
                _ => renderRotation,
            };
        }

        var color = sprite.Color * layer.Color;
        if (color.A <= 0f || GetBrightness(color) <= 0f)
            return;

        _extract.SetParameter("bloom_color", color);
        _extract.SetParameter("threshold", BrightnessThreshold);
        _extract.SetParameter("soft_knee", SoftKnee);
        handle.UseShader(_extract);
        handle.SetTransform(GetSourceToTargetMatrix(
            layerMatrix,
            sprite.LocalMatrix,
            worldPosition,
            renderRotation,
            worldToTarget));
        handle.DrawTextureRect(
            texture,
            Box2.CenteredAround(Vector2.Zero, (Vector2) texture.Size / EyeManager.PixelsPerMeter),
            Color.White);
    }

    private Vector2 GetGridPixelSnapOffset(EntityUid source, IEye eye, IClydeViewport viewport)
    {
        if (!_transformQuery.TryComp(source, out var xform) ||
            xform.GridUid is not { } gridUid ||
            !_gridQuery.HasComp(gridUid))
            return Vector2.Zero;

        var gridPosition = _transform.GetWorldPosition(gridUid);
        var viewPosition = eye.Position.Position + eye.Offset;
        var viewScale = eye.Scale * viewport.RenderScale *
                        new Vector2(EyeManager.PixelsPerMeter, -EyeManager.PixelsPerMeter);
        return CalculatePixelSnapOffset(gridPosition, viewPosition, eye.Rotation, viewScale, viewport.Size);
    }

    internal static Matrix3x2 GetSourceToTargetMatrix(
        Matrix3x2 layerMatrix,
        Matrix3x2 spriteMatrix,
        Vector2 worldPosition,
        Angle worldRotation,
        Matrix3x2 worldToTarget)
    {
        return layerMatrix * spriteMatrix * Matrix3Helpers.CreateTransform(worldPosition, worldRotation) * worldToTarget;
    }

    internal static Vector2 CalculatePixelSnapOffset(
        Vector2 worldPosition,
        Vector2 viewPosition,
        Angle viewRotation,
        Vector2 viewScale,
        Vector2 viewportSize)
    {
        var relativePosition = viewRotation.RotateVec(worldPosition - viewPosition);
        var screenPosition = relativePosition * viewScale + viewportSize / 2f;
        var screenOffset = screenPosition.Rounded() - screenPosition;
        var viewOffset = screenOffset / viewScale;
        return (-viewRotation).RotateVec(viewOffset);
    }

    internal static float GetBloomStrength(float strength)
    {
        if (!float.IsFinite(strength) || strength <= 0f)
            return 0f;

        var value = Math.Clamp(strength, 0f, 1f);
        return value * (2.5f + 2.5f * value);
    }

    internal static Matrix3x2 GetWorldToTargetMatrix(Matrix3x2 worldToViewport, Vector2 viewportSize, Vector2 contentSize)
    {
        return worldToViewport * Matrix3x2.CreateScale(contentSize / viewportSize) *
               Matrix3x2.CreateTranslation(TargetPadding, TargetPadding);
    }

    internal static float CalculateBlurRadius(LimeBloomQuality quality, float pixelsPerMeter)
    {
        var baseWorldRadius = quality switch
        {
            LimeBloomQuality.Low => 0.25f,
            LimeBloomQuality.High => 0.5f,
            _ => 0.375f,
        };

        return Math.Clamp(baseWorldRadius * pixelsPerMeter, 2f, 16f);
    }

    private void DrawTileEmission(DrawingHandleWorld handle, IEye eye, IClydeViewport viewport, Matrix3x2 worldToTarget)
    {
        foreach (var source in _tiles)
        {
            if (!_transformQuery.TryComp(source, out var xform) ||
                xform.GridUid is not { } gridUid || !_gridQuery.TryComp(gridUid, out var grid))
                continue;

            var tile = _map.LocalToTile(gridUid, grid, xform.Coordinates);
            var matrix = _transform.GetWorldMatrix(gridUid);
            matrix.Translation += GetGridPixelSnapOffset(source, eye, viewport);
            handle.SetTransform(matrix * worldToTarget);
            _extract.SetParameter("bloom_color", source.Comp.Color);
            _extract.SetParameter("threshold", BrightnessThreshold);
            _extract.SetParameter("soft_knee", SoftKnee);
            handle.UseShader(_extract);
            handle.DrawRect(_lookup.GetLocalBounds(tile, grid.TileSize), Color.White);
        }
    }

    private static float GetBrightness(Color color)
    {
        return Math.Max(color.R, Math.Max(color.G, color.B));
    }

    private void EnsureTargets(CachedResources resources, Vector2i size, Vector2i sourceSize)
    {
        if (resources.Ping?.Texture.Size == size && resources.Pong?.Texture.Size == size && resources.Source?.Size == sourceSize)
            return;

        resources.Dispose();
        var format = new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb);
        var samples = new TextureSampleParameters { Filter = true };
        resources.Source = _clyde.CreateRenderTarget(sourceSize, format, samples, "lime-bloom-source");
        resources.Ping = _clyde.CreateRenderTarget(size, format, samples, "lime-bloom-ping");
        resources.Pong = _clyde.CreateRenderTarget(size, format, samples, "lime-bloom-pong");
    }

    protected override void DisposeBehavior()
    {
        _resources.Dispose();
        _extract.Dispose();
        _downsample.Dispose();
        _blurHorizontal.Dispose();
        _blurVertical.Dispose();
        _composite.Dispose();
        base.DisposeBehavior();
    }

    private sealed class CachedResources : IDisposable
    {
        public IRenderTexture? Source;
        public IRenderTexture? Ping;
        public IRenderTexture? Pong;

        public void Dispose()
        {
            Source?.Dispose();
            Source = null;
            Ping?.Dispose();
            Pong?.Dispose();
            Ping = null;
            Pong = null;
        }
    }
}

internal enum LimeBloomQuality : byte
{
    Low,
    Medium,
    High,
}

internal static class LimeBloomQualityExtensions
{
    public static LimeBloomQuality Parse(string quality)
    {
        return quality switch
        {
            LimeBloomCVars.QualityLow => LimeBloomQuality.Low,
            LimeBloomCVars.QualityHigh => LimeBloomQuality.High,
            _ => LimeBloomQuality.Medium,
        };
    }
}
