using System.Numerics;
using Content.Client.Markers;
using Content.Client.Resources;
using Content.Shared._Orbitra.Particles;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.GameObjects;
using Robust.Client.Placement;
using Robust.Shared.Enums;
using Robust.Shared.Map;

namespace Content.Client._Orbitra.Particles;

/// <summary>Controls local dust zone authoring visibility.</summary>
public sealed partial class OrbitraAmbientDustZoneSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IResourceCache _resources = default!;
    [Dependency] private IPlacementManager _placement = default!;
    [Dependency] private SpriteSystem _sprites = default!;
    [Dependency] private MarkerSystem _markers = default!;

    /// <summary>Local authoring visibility, independent of particle quality.</summary>
    public bool FieldsVisible { get; private set; }
    private bool _placingZone;

    public override void Initialize()
    {
        base.Initialize();
        _overlays.AddOverlay(new ZoneOverlay(EntityManager, _resources, this));
    }

    public override void Shutdown()
    {
        _overlays.RemoveOverlay<ZoneOverlay>();
        FieldsVisible = false;
        base.Shutdown();
    }

    public override void FrameUpdate(float frameTime)
    {
        var placing = _placement.IsActive && _placement.CurrentPermission?.EntityType == "OrbitraAmbientDustZone";
        if (placing && !_placingZone)
            SetFieldsVisible(true);
        _placingZone = placing;
    }

    /// <summary>Keeps the nullspace placement ghost visible even when map markers are hidden.</summary>
    public bool ShouldShowMarker(EntityUid uid) => HasComp<OrbitraAmbientDustZoneComponent>(uid) &&
        (FieldsVisible || Transform(uid).MapID == MapId.Nullspace);

    /// <summary>Updates zone icons without revealing unrelated map markers.</summary>
    public void SetFieldsVisible(bool visible)
    {
        FieldsVisible = visible;
        var query = EntityQueryEnumerator<OrbitraAmbientDustZoneComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out _, out var sprite))
            _sprites.SetVisible((uid, sprite), _markers.MarkersVisible || ShouldShowMarker(uid));
    }

    private sealed class ZoneOverlay : Overlay
    {
        private readonly IEntityManager _entities;
        private readonly SharedTransformSystem _transform;
        private readonly MarkerSystem _markers;
        private readonly Font _font;
        private readonly OrbitraAmbientDustZoneSystem _system;
        public override OverlaySpace Space => OverlaySpace.ScreenSpace;

        public ZoneOverlay(IEntityManager entities, IResourceCache resources, OrbitraAmbientDustZoneSystem system)
        {
            _system = system;
            _entities = entities;
            _transform = entities.System<SharedTransformSystem>();
            _markers = entities.System<MarkerSystem>();
            _font = resources.GetFont("/Fonts/NotoSans/NotoSans-Regular.ttf", 11);
        }

        protected override void Draw(in OverlayDrawArgs args)
        {
            if ((!_markers.MarkersVisible && !_system.FieldsVisible) || args.ViewportControl == null)
                return;
            var query = _entities.EntityQueryEnumerator<OrbitraAmbientDustGridComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var grid, out var xform))
            {
                if (xform.MapID != args.MapId)
                    continue;
                var matrix = _transform.GetWorldMatrix(xform);
                foreach (var box in grid.Regions)
                {
                    var a = args.ViewportControl.WorldToScreen(Vector2.Transform(box.BottomLeft, matrix));
                    var b = args.ViewportControl.WorldToScreen(Vector2.Transform(box.BottomRight, matrix));
                    var c = args.ViewportControl.WorldToScreen(Vector2.Transform(box.TopRight, matrix));
                    var d = args.ViewportControl.WorldToScreen(Vector2.Transform(box.TopLeft, matrix));
                    var handle = args.ScreenHandle;
                    handle.DrawLine(a, b, Color.Wheat);
                    handle.DrawLine(b, c, Color.Wheat);
                    handle.DrawLine(c, d, Color.Wheat);
                    handle.DrawLine(d, a, Color.Wheat);
                    var size = _entities.GetComponent<Robust.Shared.Map.Components.MapGridComponent>(uid).TileSize;
                    handle.DrawString(_font, d, $"{box.Width / size:0} × {box.Height / size:0}", Color.Wheat);
                }
            }
        }
    }
}
