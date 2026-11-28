using System.Linq;
using System.Numerics;
using Content.Shared._Orbitra.Lavaland;
using Content.Shared.Light.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Content.Shared.Weather;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Orbitra.Lavaland;

/// <summary>Вышка предупреждает о фронте в одном из разведочных секторов до начала пеплопада.</summary>
public sealed partial class OrbitraAshFrontSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private OrbitraLavalandFerrySystem _ferry = default!;
    [Dependency] private OrbitraLavalandRadioSystem _radio = default!;
    [Dependency] private SharedWeatherSystem _weather = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedMapSystem _maps = default!;
    [Dependency] private MovementModStatusSystem _movement = default!;

    public static readonly EntProtoId Weather = "OrbitraWeatherAshFront";
    private static readonly EntProtoId AshSlowdown = "OrbitraAshSlowdown";
    private static readonly TimeSpan Warning = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan Passage = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan Sample = TimeSpan.FromSeconds(1);
    private TimeSpan _nextSample;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OrbitraAshFrontComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<OrbitraAshBandComponent, StatusEffectRemovedEvent>(OnBandRemoved);
        SubscribeLocalEvent<OrbitraAshTrackComponent, EntityTerminatingEvent>(OnTrackTerminating);
    }

    private void OnMapInit(Entity<OrbitraAshFrontComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.NextChange == default)
            Schedule(ent.Comp);
    }

    private void Schedule(OrbitraAshFrontComponent front)
    {
        front.Phase = OrbitraAshPhase.Clear;
        front.WeatherEntity = null;
        front.NextChange = _timing.CurTime + TimeSpan.FromMinutes(_random.Next(8, 13));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        if (now < _nextSample)
            return;

        _nextSample = now + Sample;
        var surfaces = EntityQueryEnumerator<OrbitraAshFrontComponent>();
        while (surfaces.MoveNext(out var map, out var front))
        {
            if (front.Phase == OrbitraAshPhase.Passing)
            {
                if (front.WeatherEntity is not {} activeWeather ||
                    !TryComp<OrbitraAshBandComponent>(activeWeather, out var band) ||
                    !TryComp<StatusEffectComponent>(activeWeather, out var status) ||
                    status.AppliedTo != map)
                {
                    Schedule(front);
                    continue;
                }

                var progress = Math.Clamp(OrbitraAshFootprint.Progress(now, status.StartEffectTime, status.Duration), 0f, 1f);
                var complete = now >= front.NextChange;
                if (status.EndEffectTime == front.NextChange && (complete || !EntityManager.IsQueuedForDeletion(activeWeather)))
                    UncoverTrails(map, front, band, progress);

                if (complete)
                    Schedule(front);
                else
                    ApplyExposure(map, band.Footprint(progress));
                continue;
            }

            if (now < front.NextChange || front.Sites.Count == 0)
                continue;

            if (front.Phase == OrbitraAshPhase.Clear)
            {
                if (front.Sites.ContainsKey(front.FirstSector))
                    front.Sector = front.FirstSector;
                else
                {
                    // Исключаем предыдущий сектор, если в этой смене есть другой маршрут.
                    var sites = front.Sites.Keys.Where(d => front.Sites.Count == 1 || d != front.Sector).ToArray();
                    front.Sector = sites[_random.Next(sites.Length)];
                }
                front.FirstSector = Direction.Invalid;
                front.Phase = OrbitraAshPhase.Forecast;
                front.NextChange = now + Warning;
                if (_ferry.TryGetRoute(out var route) && route.Planet == map && route.OutpostTerminal is {} terminal)
                    _radio.Broadcast(terminal, Loc.GetString(ForecastKey(front.Sector)));
                continue;
            }

            if (!_weather.TryAddWeather(map, Weather, out var effect, Passage))
            {
                Schedule(front);
                continue;
            }

            var ash = Comp<OrbitraAshBandComponent>(effect.Value);
            var radial = front.Sector.ToVec();
            ash.Site = front.Sites[front.Sector];
            ash.Travel = new Vector2(-radial.Y, radial.X);
            Dirty(effect.Value, ash);
            front.WeatherEntity = effect;
            front.TrailProgress = 0f;
            front.NextChange = now + Passage;
            front.Phase = OrbitraAshPhase.Passing;
        }
    }

    private static string ForecastKey(Direction direction) => direction switch
    {
        Direction.North => "orbitra-ash-warning-north",
        Direction.South => "orbitra-ash-warning-south",
        Direction.East => "orbitra-ash-warning-east",
        Direction.NorthWest => "orbitra-ash-warning-greenhouse",
        Direction.SouthWest => "orbitra-ash-warning-mine",
        _ => throw new ArgumentOutOfRangeException(nameof(direction)),
    };

    private void ApplyExposure(EntityUid map, OrbitraAshFootprint footprint)
    {
        var walkers = EntityQueryEnumerator<ActorComponent, MovementSpeedModifierComponent, TransformComponent>();
        while (walkers.MoveNext(out var uid, out _, out _, out var xform))
        {
            if (xform.MapUid != map || xform.GridUid is not {} grid || xform.ParentUid != grid ||
                footprint.Strength(_transform.GetWorldPosition(xform)) < 0.65f ||
                !TryComp<MapGridComponent>(grid, out var tiles) || !_maps.TryGetTileRef(grid, tiles, xform.Coordinates, out var tile))
                continue;

            TryComp<RoofComponent>(grid, out var roof);
            if (_weather.CanWeatherAffect((grid, tiles, roof), tile))
                _movement.TryUpdateMovementSpeedModDuration(uid, AshSlowdown, Sample + Sample, 0.85f);
        }
    }
}
