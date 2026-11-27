using System.Numerics;
using Robust.Client.Graphics;

namespace Content.Client._Orbitra.Particles;

/// <summary>Короткая история наблюдаемых позиций пули в системе координат одного родителя.</summary>
internal sealed class OrbitraBulletTrailPath
{
    private const int Capacity = 16;
    private const float StraightTolerance = 0.25f / EyeManager.PixelsPerMeter;
    private readonly Vector2[] _points = new Vector2[Capacity];
    private int _head;

    public int Count { get; private set; }
    public float Length { get; private set; }

    public static int VisiblePixels(int lengthPixels, bool low, bool reducedMotion)
    {
        var pixels = Math.Clamp(lengthPixels, 0, 32);
        if (low)
            pixels = (pixels + 1) / 2;
        return reducedMotion ? Math.Min(pixels, 4) : pixels;
    }

    public void Reset(Vector2 position)
    {
        _head = 0;
        _points[0] = position;
        Count = 1;
        Length = 0;
    }

    /// <summary>Возвращает точки от хвоста к текущей позиции пули.</summary>
    public Vector2 GetPoint(int index)
    {
        if ((uint) index >= (uint) Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        return PointBehindHead(Count - 1 - index);
    }

    public void Observe(Vector2 position, float maxLength)
    {
        if (Count == 0 || maxLength <= 0)
        {
            Reset(position);
            return;
        }

        var head = _points[_head];
        if (position != head)
        {
            // Прямой участок хранится двумя точками независимо от частоты кадров.
            if (Count == 1 || !ContinuesStraight(head - PointBehindHead(1), position - head))
            {
                _head = (_head + 1) % Capacity;
                Count = Math.Min(Count + 1, Capacity);
            }
            _points[_head] = position;
        }
        else if (Length <= maxLength)
            return;

        var length = 0f;
        var end = position;
        for (var back = 1; back < Count; back++)
        {
            ref var start = ref PointBehindHead(back);
            var segment = start - end;
            var distance = segment.Length();
            var remaining = maxLength - length;
            if (distance >= remaining)
            {
                start = end + segment * (remaining / distance);
                Count = back + 1;
                Length = maxLength;
                return;
            }
            length += distance;
            end = start;
        }
        Length = length;
    }

    private static bool ContinuesStraight(Vector2 previous, Vector2 next)
    {
        var dot = Vector2.Dot(previous, next);
        var cross = MathF.Abs(previous.X * next.Y - previous.Y * next.X);
        // Сохраняем выраженный перелом или отклонение от нового направления более четверти пикселя.
        return dot > 0 && cross <= dot * 0.2f && cross * cross <= StraightTolerance * StraightTolerance * next.LengthSquared();
    }

    private ref Vector2 PointBehindHead(int back) => ref _points[(_head - back + Capacity) % Capacity];
}
