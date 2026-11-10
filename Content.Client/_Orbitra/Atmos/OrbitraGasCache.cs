using Robust.Shared.Map;

namespace Content.Client._Orbitra.Atmos;

internal sealed class OrbitraGasCache<T> : IDisposable where T : IDisposable
{
    public const int Capacity = 128;
    public readonly Dictionary<(EntityUid Grid, Vector2i Index), T> Entries = new(Capacity);
    private MapId _map = MapId.Nullspace;

    public void ChangeMap(MapId map)
    {
        if (_map == map)
            return;

        Clear();
        _map = map;
    }

    public void Retain(Func<(EntityUid Grid, Vector2i Index), bool> keep)
    {
        foreach (var (key, entry) in Entries)
        {
            if (keep(key))
                continue;

            entry.Dispose();
            Entries.Remove(key);
        }
    }

    public void Dispose() => Clear();

    private void Clear()
    {
        foreach (var entry in Entries.Values)
            entry.Dispose();

        Entries.Clear();
    }
}
