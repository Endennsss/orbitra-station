using System.Diagnostics.CodeAnalysis;
using ChunkKey = (Robust.Shared.GameObjects.EntityUid Grid, Robust.Shared.Maths.Vector2i Index);

namespace Content.Client._Orbitra.Atmos;

/// <summary>Хранит до 128 текстурированных чанков с общим бюджетом для всех камер.</summary>
internal sealed class OrbitraGasCache<T> : IDisposable where T : IDisposable
{
    public const int Capacity = 128;
    private readonly Dictionary<ChunkKey, LinkedListNode<(ChunkKey Key, T Value)>> _entries = new(Capacity);
    private readonly LinkedList<(ChunkKey Key, T Value)> _usage = new();
    private readonly HashSet<ChunkKey> _usedThisFrame = new(Capacity);

    public T this[ChunkKey key] => _entries[key].Value.Value;

    public void BeginFrame() => _usedThisFrame.Clear();

    public bool TryGet(ChunkKey key, [MaybeNullWhen(false)] out T value)
    {
        if (!_entries.TryGetValue(key, out var entry))
        {
            value = default;
            return false;
        }

        _usage.Remove(entry);
        _usage.AddLast(entry);
        value = entry.Value.Value;
        return true;
    }

    public void Add(ChunkKey key, T value) => _entries.Add(key, _usage.AddLast((key, value)));

    /// <summary>Освобождает давние чанки только при нехватке мест для текущего вида.</summary>
    public bool Reserve(HashSet<ChunkKey> visible)
    {
        var missing = 0;
        var totalInFrame = _usedThisFrame.Count;
        foreach (var key in visible)
        {
            if (!_usedThisFrame.Contains(key))
                totalInFrame++;
            if (!_entries.ContainsKey(key))
                missing++;
        }

        // Иначе две камеры с непересекающимися областями вытесняли бы друг друга каждый кадр.
        if (totalInFrame > Capacity)
            return false;

        for (var node = _usage.First; _entries.Count + missing > Capacity && node != null;)
        {
            var next = node.Next;
            if (!visible.Contains(node.Value.Key) && !_usedThisFrame.Contains(node.Value.Key))
                Remove(node);
            node = next;
        }
        _usedThisFrame.UnionWith(visible);
        return true;
    }

    public void Retain(Func<ChunkKey, bool> keep)
    {
        foreach (var (key, node) in _entries)
        {
            if (!keep(key))
                Remove(node);
        }
    }

    private void Remove(LinkedListNode<(ChunkKey Key, T Value)> entry)
    {
        _usage.Remove(entry);
        _entries.Remove(entry.Value.Key);
        entry.Value.Value.Dispose();
    }

    public void Dispose()
    {
        foreach (var (_, value) in _usage)
            value.Dispose();

        _usage.Clear();
        _entries.Clear();
        _usedThisFrame.Clear();
    }
}
