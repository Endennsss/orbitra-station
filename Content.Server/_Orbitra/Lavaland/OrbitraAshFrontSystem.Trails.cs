using System.Numerics;
using Content.Shared._Orbitra.Lavaland;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;

namespace Content.Server._Orbitra.Lavaland;

public sealed partial class OrbitraAshFrontSystem
{
    private const float TrailLifetime = 240f;

    /// <summary>Регистрирует прямой след от первой клетки до последней в направлении находки.</summary>
    public void RegisterTrail(EntityUid map, Direction sector, EntProtoId marker, Vector2i start, Vector2i end)
    {
        var delta = end - start;
        if (delta == Vector2i.Zero || delta.X != 0 && delta.Y != 0)
            throw new ArgumentException("A trail must follow one axis and have a heading.", nameof(end));

        var front = EnsureComp<OrbitraAshFrontComponent>(map);
        if (!front.Trails.TryGetValue(sector, out var tiles))
            front.Trails[sector] = tiles = new();

        var step = new Vector2i(Math.Sign(delta.X), Math.Sign(delta.Y));
        var heading = step.AsDirection();
        for (var tile = start; tile != end + step; tile += step)
        {
            if (!tiles.TryGetValue(tile, out var trace))
                tiles[tile] = trace = new();
            trace.Marker = marker;
            trace.Heading = heading;
        }
    }

    private void OnBandRemoved(Entity<OrbitraAshBandComponent> ent, ref StatusEffectRemovedEvent args)
    {
        // Погода может окончиться между секундными проверками или исчезнуть вместе с картой.
        if (TerminatingOrDeleted(args.Target) || !TryComp<OrbitraAshFrontComponent>(args.Target, out var front) ||
            front.WeatherEntity != ent.Owner || _timing.CurTime < front.NextChange)
            return;

        UncoverTrails(args.Target, front, ent.Comp, 1f);
    }

    private void OnTrackTerminating(Entity<OrbitraAshTrackComponent> ent, ref EntityTerminatingEvent args)
    {
        var xform = Transform(ent);
        if (TryComp<OrbitraAshFrontComponent>(xform.ParentUid, out var front) &&
            front.Trails.TryGetValue(ent.Comp.Sector, out var tiles) &&
            tiles.TryGetValue(xform.LocalPosition.Floored(), out var trace) && trace.Visible == ent.Owner)
            trace.Visible = null;
    }

    private void UncoverTrails(EntityUid map, OrbitraAshFrontComponent front, OrbitraAshBandComponent band, float progress)
    {
        if (progress <= front.TrailProgress || !front.Trails.TryGetValue(front.Sector, out var tiles))
            return;

        var previous = band.Footprint(front.TrailProgress);
        var current = band.Footprint(progress);
        front.TrailProgress = progress;
        // Когда полоса исчезает, открывается и земля под её последним положением.
        var edge = progress >= 1f ? OrbitraAshFootprint.HalfWidth : -OrbitraAshFootprint.HalfWidth;
        foreach (var (tile, trace) in tiles)
        {
            var position = (Vector2) tile + new Vector2(0.5f);
            var offset = position - current.Center;
            var across = offset.X * band.Travel.Y - offset.Y * band.Travel.X;
            if (MathF.Abs(across) >= OrbitraAshFootprint.HalfLength ||
                Vector2.Dot(position - previous.Center, band.Travel) < -OrbitraAshFootprint.HalfWidth ||
                Vector2.Dot(offset, band.Travel) >= edge)
                continue;

            if (trace.Visible is not {} uid || TerminatingOrDeleted(uid) || EntityManager.IsQueuedForDeletion(uid))
            {
                uid = SpawnAttachedTo(trace.Marker, new EntityCoordinates(map, position), rotation: trace.Heading.ToAngle());
                Comp<OrbitraAshTrackComponent>(uid).Sector = front.Sector;
                trace.Visible = uid;
            }
            EnsureComp<TimedDespawnComponent>(uid).Lifetime = TrailLifetime;
        }
    }
}
