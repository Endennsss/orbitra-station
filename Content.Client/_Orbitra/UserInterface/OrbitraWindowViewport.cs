// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Ende (Orbitra)

using System.Numerics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Fits an ordinary window into its current logical viewport.</summary>
internal sealed class OrbitraWindowViewport(BaseWindow window)
{
    private Dimension _minWidth;
    private Dimension _minHeight;
    private Dimension _setWidth;
    private Dimension _setHeight;

    internal void Fit()
    {
        if (window.Parent is not LayoutContainer parent || parent.Width <= 0 || parent.Height <= 0)
            return;

        var margins = new Vector2(window.Margin.SumHorizontal, window.Margin.SumVertical);
        if (UpdateLimits(Vector2.Max(Vector2.Zero, parent.Size - margins)))
        {
            // Пересчитываем прямоугольник в родителе после штатного смещения окна.
            // Новый размер нельзя раскладывать по сохранённому прямоугольнику старого размера.
            parent.InvalidateArrange();
        }

        var size = Vector2.Min(window.Size, parent.Size);
        var position = Vector2.Clamp(window.Position, Vector2.Zero, parent.Size - size);
        if (position != window.Position)
            LayoutContainer.SetPosition(window, position);
    }

    internal void Restore() => UpdateLimits(new Vector2(float.PositiveInfinity));

    private bool UpdateLimits(Vector2 viewport)
    {
        var minimum = window.MinSize;
        var requested = window.SetSize;
        var fittedMinimum = new Vector2(_minWidth.Resolve(minimum.X, viewport.X, window.MaxWidth),
            _minHeight.Resolve(minimum.Y, viewport.Y, window.MaxHeight));
        var fittedRequest = new Vector2(_setWidth.Resolve(requested.X, viewport.X, window.MaxWidth),
            _setHeight.Resolve(requested.Y, viewport.Y));
        if (!minimum.X.Equals(fittedMinimum.X))
            window.MinWidth = fittedMinimum.X;
        if (!minimum.Y.Equals(fittedMinimum.Y))
            window.MinHeight = fittedMinimum.Y;
        if (!requested.X.Equals(fittedRequest.X))
            window.SetWidth = fittedRequest.X;
        if (!requested.Y.Equals(fittedRequest.Y))
            window.SetHeight = fittedRequest.Y;
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
                limit = Math.Min(limit, maximum);
            }
            _applied = preferred > limit ? limit : preferred;
            _preferred = !restoring && preferred > limit ? preferred : null;
            return _applied;
        }
    }
}
