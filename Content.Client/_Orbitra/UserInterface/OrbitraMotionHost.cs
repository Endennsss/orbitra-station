using System.Numerics;
using Robust.Client.UserInterface;

using Content.Client._Orbitra.Lobby;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Смещает содержимое при анимации, сохраняя размер выделенного ему места.</summary>
public sealed class OrbitraMotionHost : Control
{
    private Vector2 _offset;
    private OrbitraMotion.Transition? _transition;
    private bool _requestedVisible;
    private readonly OrbitraInputBlock _input = new();

    internal void Reveal(float duration, Vector2 offset)
    {
        _requestedVisible = true;
        _input.Restore();
        Visible = true;
        (_transition ??= new OrbitraMotion.Transition(this)).Reveal(duration, offset);
    }

    internal void SetShown(bool shown, Vector2 offset, bool immediate = false)
    {
        if (_requestedVisible == shown && !immediate)
            return;
        _requestedVisible = shown;
        if (immediate)
        {
            _transition?.Finish();
            _input.Restore();
            Visible = shown;
            return;
        }
        if (shown)
            Reveal(0.18f, offset);
        else if (Visible)
        {
            _input.Block(this);
            (_transition ??= new OrbitraMotion.Transition(this)).Hide(0.12f, offset);
        }
    }

    internal void CompleteMotion(bool hidden)
    {
        if (hidden)
            Visible = false;
        _input.Restore();
    }

    internal void SetVisualOffset(Vector2 offset)
    {
        if (_offset == offset)
            return;
        _offset = offset;
        InvalidateArrange();
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        foreach (var child in Children)
            child.Arrange(UIBox2.FromDimensions(_offset, finalSize));
        return finalSize;
    }

    protected override void ExitedTree()
    {
        _transition?.Finish();
        _input.Restore();
        base.ExitedTree();
    }
}
