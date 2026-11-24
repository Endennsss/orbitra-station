// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using System.Numerics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Подгоняет обычное окно под текущую логическую область просмотра.</summary>
internal sealed class OrbitraWindowViewport(BaseWindow window)
{
    private Dimension _minWidth;
    private Dimension _minHeight;
    private Dimension _setWidth;
    private Dimension _setHeight;

    internal void Fit(float inset = 0, bool center = false)
    {
        if (window.Parent is not LayoutContainer parent || parent.Width <= 0 || parent.Height <= 0)
            return;

        var margin = Vector2.Min(new Vector2(inset), parent.Size / 4);
        var innerSize = parent.Size - 2 * margin;
        var windowMargins = new Vector2(window.Margin.SumHorizontal, window.Margin.SumVertical);
        var changed = UpdateLimits(Vector2.Max(Vector2.Zero, innerSize - windowMargins));

        // Положение пересчитываем по новому размеру, в том числе сразу после сжатия окна.
        window.Measure(innerSize);
        var size = Vector2.Min(window.DesiredSize, innerSize);
        var requestedPosition = center ? (parent.Size - size) / 2 : window.Position;
        var position = Vector2.Clamp(requestedPosition, margin, Vector2.Max(margin, parent.Size - size - margin));
        if (position != window.Position)
            LayoutContainer.SetPosition(window, position);
        if (changed)
            parent.InvalidateArrange();
    }

    internal void Restore() => UpdateLimits(new Vector2(float.PositiveInfinity));

    private bool UpdateLimits(Vector2 viewport)
    {
        var minimum = window.MinSize;
        var maximum = window.MaxSize;
        var requested = window.SetSize;
        var fittedMinimum = new Vector2(_minWidth.Resolve(minimum.X, viewport.X, maximum.X),
            _minHeight.Resolve(minimum.Y, viewport.Y, maximum.Y));
        var fittedRequest = new Vector2(_setWidth.Resolve(requested.X, viewport.X, maximum.X),
            _setHeight.Resolve(requested.Y, viewport.Y, maximum.Y));

        if (!minimum.Equals(fittedMinimum))
            window.MinSize = fittedMinimum;
        if (!requested.Equals(fittedRequest))
            window.SetSize = fittedRequest;
        return !minimum.Equals(fittedMinimum) || !requested.Equals(fittedRequest);
    }

    private struct Dimension
    {
        private float? _preferred;
        private float _applied;

        public float Resolve(float current, float limit, float maximum = float.PositiveInfinity)
        {
            var restoring = float.IsPositiveInfinity(limit);
            var preferred = current;
            // Изменение отдельной оси извне становится новым предпочтением окна.
            if (_preferred is { } saved && current.Equals(_applied))
            {
                preferred = saved;
                if (!restoring)
                    limit = Math.Min(limit, maximum);
            }
            _applied = preferred > limit ? limit : preferred;
            _preferred = !restoring && preferred > limit ? preferred : null;
            return _applied;
        }
    }
}
