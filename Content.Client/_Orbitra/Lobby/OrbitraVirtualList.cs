using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Orbitra.Lobby;

/// <summary>Variable-height journal rows with cached measurements and a viewport-sized live window.</summary>
public sealed class OrbitraVirtualList : Control
{
    private readonly ScrollContainer _scroll;
    private readonly List<Func<Control>> _rows;
    private readonly Dictionary<int, Control> _live = new();
    private readonly List<int> _remove = new();
    private float[] _offsets;
    private float _measuredWidth = -1;
    private float _measuredScale;
    private bool _queued;

    public int LiveRows => _live.Count;
    public int RowCount => _rows.Count;

    public OrbitraVirtualList(ScrollContainer scroll, List<Func<Control>> rows)
    {
        _scroll = scroll;
        _rows = rows;
        _offsets = new float[rows.Count + 1];
        _scroll.OnScrolled += QueueRefresh;
        OnResized += QueueRefresh;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var width = Math.Max(1, availableSize.X);
        if (Math.Abs(width - _measuredWidth) > 0.1f || _measuredScale != UIScale)
        {
            var anchor = FindRow(_scroll.VScroll);
            var within = _scroll.VScroll - _offsets[anchor];
            var hadLayout = _measuredWidth > 0;
            _measuredWidth = width;
            _measuredScale = UIScale;
            _offsets[0] = 0;
            // Полная разметка нужна только при смене ширины/шрифта, не при прокрутке.
            for (var i = 0; i < _rows.Count; i++)
            {
                using var row = _rows[i]();
                // Шрифт и UIScale берутся из реального дерева окна, иначе высота ещё не оформленной строки неверна.
                AddChild(row);
                OrbitraEditorStyles.Apply(row);
                row.Measure(new Vector2(width, float.PositiveInfinity));
                _offsets[i + 1] = _offsets[i] + Math.Max(1, row.DesiredSize.Y) + 8;
            }
            if (hadLayout)
            {
                var restored = _offsets[anchor] + within;
                UserInterfaceManager.DeferAction(() =>
                {
                    if (!Disposed)
                        _scroll.VScroll = _scroll.VScrollTarget = restored;
                });
            }
            QueueRefresh();
        }
        return new Vector2(width, _offsets[^1]);
    }

    private int FindRow(float y)
    {
        if (_rows.Count == 0)
            return 0;
        // Array.BinarySearch<T> недоступен в песочнице клиента; сохраняем поиск за O(log n).
        var left = 0;
        var right = _offsets.Length;
        while (left < right)
        {
            var middle = left + (right - left) / 2;
            if (_offsets[middle] <= y)
                left = middle + 1;
            else
                right = middle;
        }
        return Math.Clamp(left - 1, 0, _rows.Count - 1);
    }

    private void QueueRefresh()
    {
        if (_queued || Disposed)
            return;
        _queued = true;
        UserInterfaceManager.DeferAction(RefreshRows);
    }

    private void RefreshRows()
    {
        _queued = false;
        if (Disposed || _rows.Count == 0 || _measuredWidth <= 0)
            return;
        var first = FindRow(Math.Max(0, _scroll.VScroll - _scroll.Height));
        var last = FindRow(_scroll.VScroll + _scroll.Height * 2);
        _remove.Clear();
        foreach (var (index, _) in _live)
        {
            if (index < first || index > last)
                _remove.Add(index);
        }
        foreach (var index in _remove)
        {
            _live[index].Dispose();
            _live.Remove(index);
        }
        for (var i = first; i <= last; i++)
        {
            if (_live.ContainsKey(i))
                continue;
            var row = _rows[i]();
            OrbitraEditorStyles.Apply(row);
            _live.Add(i, row);
            AddChild(row);
        }
        InvalidateArrange();
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        foreach (var (index, row) in _live)
        {
            row.Measure(new Vector2(finalSize.X, float.PositiveInfinity));
            row.Arrange(UIBox2.FromDimensions(new Vector2(0, _offsets[index]), new Vector2(finalSize.X, _offsets[index + 1] - _offsets[index] - 8)));
        }
        return finalSize;
    }

    protected override void Dispose(bool disposing)
    {
        _scroll.OnScrolled -= QueueRefresh;
        base.Dispose(disposing);
    }
}
