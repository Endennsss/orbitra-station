using Content.Shared._Orbitra.Particles;
using Content.Shared.DoAfter;
using Content.Shared.Tag;
using Content.Shared.Tools.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared.Tools.Systems;

public abstract partial class SharedToolSystem
{
    [Dependency] private TagSystem _orbitraParticleTags = default!;
    private static readonly ProtoId<TagPrototype> OrbitraDrillTag = "Powerdrill";

    private void ConfigureOrbitraParticles(ToolDoAfterEvent ev, EntityUid tool,
        IEnumerable<ProtoId<ToolQualityPrototype>> qualities)
    {
        foreach (var quality in qualities)
        {
            // У инструментов для пола список содержит все возможности, а не только нужную операции.
            if (ev.WrappedEvent is TileToolDoAfterEvent tileWork)
            {
                var grid = GetEntity(tileWork.Grid);
                var tileRef = _maps.GetTileRef(grid, Comp<MapGridComponent>(grid), tileWork.GridTile);
                if (!((Content.Shared.Maps.ContentTileDefinition) _tileDefManager[tileRef.Tile.TypeId]).DeconstructTools.Contains(quality))
                    continue;
            }
            if (quality.Id == "Welding")
            {
                ev.orbitraParticleEffect = "OrbitraParticleWelding";
                ev.OrbitraParticleRate = 24f;
                break;
            }

            if (quality.Id is "Sawing" or "Cutting" or "Prying" or "Anchoring" or "Screwing")
            {
                ev.orbitraParticleEffect = "material";
                ev.OrbitraParticleRate = Math.Max(ev.OrbitraParticleRate,
                    quality.Id == "Sawing" || _orbitraParticleTags.HasTag(tool, OrbitraDrillTag) ? 12f : 3f);
            }
        }

        // Клетка пола не совпадает с Target: штатный TileTool передаёт туда сам инструмент.
        if (ev.WrappedEvent is TileToolDoAfterEvent tile)
        {
            var grid = GetEntity(tile.Grid);
            if (TryComp<MapGridComponent>(grid, out var mapGrid))
                ev.OrbitraParticleCoordinates = GetNetCoordinates(_maps.GridTileToLocal(grid, mapGrid, tile.GridTile));
        }
        else if (ev.WrappedEvent is LatticeCuttingCompleteEvent lattice)
            ev.OrbitraParticleCoordinates = lattice.Coordinates;
    }

    public bool TryGetOrbitraParticleWork(Content.Shared.DoAfter.DoAfter action, out string? effect, out float rate,
        out EntityCoordinates? coordinates)
    {
        effect = null;
        rate = 0;
        coordinates = null;
        if (action.Cancelled || action.Completed || action.Args.Event is not ToolDoAfterEvent ev || ev.orbitraParticleEffect == null)
            return false;
        if (action.Args.Used is { } used)
        {
            if (TerminatingOrDeleted(used))
                return false;
            // Остаток топлива проверяет серверный DoAfter; наблюдателю достаточно публичного состояния горелки.
            if (ev.orbitraParticleEffect == "OrbitraParticleWelding" && HasComp<WelderComponent>(used) && !ItemToggle.IsActivated(used))
                return false;
        }
        effect = ev.orbitraParticleEffect;
        rate = ev.OrbitraParticleRate;
        if (ev.OrbitraParticleCoordinates is { } point)
            coordinates = GetCoordinates(point);
        return true;
    }

    protected sealed partial class ToolDoAfterEvent
    {
        [DataField] public string? orbitraParticleEffect;
        [DataField] public float OrbitraParticleRate;
        [DataField] public NetCoordinates? OrbitraParticleCoordinates;
    }
}
