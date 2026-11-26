using System.Numerics;
using Content.Shared._Orbitra.DoAfter;
using Content.Shared.DoAfter;
using Robust.Client.GameObjects;
using Robust.Shared.Map;

#pragma warning disable IDE0130 // Пространство имён совпадает с расширяемым vanilla-классом.
namespace Content.Client.DoAfter;

public sealed partial class DoAfterOverlay
{
    private const float EntranceDuration = 0.2f;
    private const float EntranceDistance = 0.25f;

    private readonly Dictionary<DoAfterId, TimeSpan> _entranceStartTimes = new();
    private readonly HashSet<DoAfterId> _activeDoAfters = new();
    private readonly List<DoAfterId> _staleDoAfters = new();

    /// <summary>Сохраняет поворот и масштаб полосы, привязывая её к спрайту цели выбранного действия.</summary>
    private Entity<SpriteComponent> ResolveProgressAnchor(DoAfterArgs args, Entity<SpriteComponent> actor,
        MapId map, ref Matrix3x2 matrix)
    {
        if (args.Event is not IOrbitraTargetProgress || args.Target is not { } target ||
            !_entManager.TryGetComponent<TransformComponent>(target, out var xform) || xform.MapID != map ||
            !_entManager.TryGetComponent<SpriteComponent>(target, out var sprite) ||
            _container.IsEntityOrParentInContainer(target, xform: xform))
            return actor;

        matrix.Translation = _transform.GetWorldPosition(xform);
        return (target, sprite);
    }

    private void BeginEntranceFrame()
    {
        _activeDoAfters.Clear();
    }

    private void TrackActiveDoAfters(DoAfterComponent component)
    {
        foreach (var doAfter in component.DoAfters.Values)
        {
            _activeDoAfters.Add(doAfter.Id);
        }
    }

    private void EndEntranceFrame()
    {
        _staleDoAfters.Clear();
        foreach (var doAfter in _entranceStartTimes.Keys)
        {
            if (!_activeDoAfters.Contains(doAfter))
                _staleDoAfters.Add(doAfter);
        }

        foreach (var doAfter in _staleDoAfters)
        {
            _entranceStartTimes.Remove(doAfter);
        }
    }

    private float GetEntranceProgress(DoAfterId doAfter, TimeSpan currentTime)
    {
        if (!_entranceStartTimes.TryGetValue(doAfter, out var startTime))
        {
            _entranceStartTimes.Add(doAfter, currentTime);
            return 0f;
        }

        return GetEntranceProgress(currentTime - startTime);
    }

    private static float GetEntranceProgress(TimeSpan elapsed)
    {
        var elapsedSeconds = elapsed.TotalSeconds;
        if (elapsedSeconds >= EntranceDuration)
            return 1f;

        var entranceRatio = Math.Clamp((float) (elapsedSeconds / EntranceDuration), 0f, 1f);
        var inverseRatio = 1f - entranceRatio;
        return 1f - inverseRatio * inverseRatio * inverseRatio;
    }

    private static Vector2 GetEntrancePosition(Vector2 position, float entranceProgress)
    {
        position.Y -= EntranceDistance * (1f - entranceProgress);
        return position;
    }
}
