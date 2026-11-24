using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

using Content.Client._Orbitra.Lobby;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Измеряет строки журнала по мере прокрутки и держит в дереве только ближайшие к экрану.</summary>
public sealed class OrbitraVirtualList : Control
{
    private const float RowSpacing = 8f;
    private const float EstimatedRowHeight = 32f;
    private readonly ScrollContainer _scroll;
    private readonly List<Func<Control>> _rows;
    private readonly Dictionary<int, Control> _live = new();
    private readonly List<int> _remove = new();
    private readonly float[] _heights;
    private readonly float[] _offsets;
    private float _measuredWidth = -1;
    private float _measuredScale;
    private bool _queued;
    private (int Index, float Within, float Scroll)? _anchor;

    public int LiveRows => _live.Count;
    public int RowCount => _rows.Count;

    public OrbitraVirtualList(ScrollContainer scroll, List<Func<Control>> rows)
    {
        _scroll = scroll;
        _rows = rows;
        _heights = new float[rows.Count];
        _offsets = new float[rows.Count + 1];
        for (var i = 0; i < rows.Count; i++)
        {
            _heights[i] = EstimatedRowHeight;
            _offsets[i + 1] = _offsets[i] + EstimatedRowHeight + RowSpacing;
        }
        _scroll.OnScrolled += QueueRefresh;
        OnResized += QueueRefresh;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var width = Math.Max(1, availableSize.X);
        if (Math.Abs(width - _measuredWidth) > 0.1f || _measuredScale != UIScale)
        {
            _measuredWidth = width;
            _measuredScale = UIScale;
            QueueRefresh();
        }
        MeasureLiveRows(width);
        return new Vector2(width, _offsets[^1]);
    }

    private void MeasureLiveRows(float width)
    {
        var firstChanged = _rows.Count;
        foreach (var (index, row) in _live)
        {
            row.Measure(new Vector2(width, float.PositiveInfinity));
            var height = Math.Max(1, row.DesiredSize.Y);
            if (_heights[index].Equals(height))
                continue;

            _heights[index] = height;
            firstChanged = Math.Min(firstChanged, index);
        }
        if (firstChanged == _rows.Count)
            return;

        // Старые измерения за экраном служат оценкой до следующего показа строки.
        // Точку прокрутки привязываем к той же строке, пока пользователь сам её не сдвинет.
        if (_anchor == null && _scroll.VScroll > 0)
        {
            var index = FindRow(_scroll.VScroll);
            _anchor = (index, _scroll.VScroll - _offsets[index], _scroll.VScroll);
            UserInterfaceManager.DeferAction(RestoreAnchor);
        }
        for (var i = firstChanged; i < _rows.Count; i++)
            _offsets[i + 1] = _offsets[i] + _heights[i] + RowSpacing;
        QueueRefresh();
    }

    private void RestoreAnchor()
    {
        var anchor = _anchor;
        _anchor = null;
        if (Disposed || anchor is not { } saved || !_scroll.VScroll.Equals(saved.Scroll))
            return;

        var scroll = _offsets[saved.Index] + Math.Clamp(saved.Within, 0, _heights[saved.Index]);
        _scroll.VScroll = _scroll.VScrollTarget = scroll;
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
        var changed = _remove.Count != 0;
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
            changed = true;
        }
        if (changed)
            InvalidateMeasure();
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        foreach (var (index, row) in _live)
            row.Arrange(UIBox2.FromDimensions(new Vector2(0, _offsets[index]), new Vector2(finalSize.X, _heights[index])));
        return finalSize;
    }

    protected override void Dispose(bool disposing)
    {
        _scroll.OnScrolled -= QueueRefresh;
        base.Dispose(disposing);
    }
}
