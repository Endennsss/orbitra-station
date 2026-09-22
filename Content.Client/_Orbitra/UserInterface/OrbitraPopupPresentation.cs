using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

using Content.Client._Orbitra.Lobby;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Fades the existing popup content after native modality has already been released.</summary>
internal sealed class OrbitraPopupPresentation : IDisposable
{
    private readonly Popup _popup;
    private readonly Control _owner;
    private readonly Control _marker;
    private readonly Visual _visual;
    private readonly OrbitraInputBlock _input = new();
    private readonly List<Control> _contents = new();
    private readonly List<Control> _watched = new();
    private readonly IUserInterfaceManager _ui = IoCManager.Resolve<IUserInterfaceManager>();
    private int _generation;
    private bool _disposed;
    private bool _hideRequested;
    private readonly Action? _hidden;

    internal OrbitraPopupPresentation(Popup popup, Control owner, Control marker, Action? hidden)
    {
        _popup = popup;
        _owner = owner;
        _marker = marker;
        _hidden = hidden;
        _visual = new Visual(this) { MouseFilter = Control.MouseFilterMode.Ignore, Visible = false };
        _visual.AddStyleClass("OrbitraEntryWindow");
        popup.OnPopupOpen += OnOpen;
        popup.OnPopupHide += OnHide;
        owner.AddChild(new OwnerLifetime(this) { MouseFilter = Control.MouseFilterMode.Ignore });
    }

    private void OnHide()
    {
        _hideRequested = true;
        var position = _popup.GlobalPosition;
        var size = _popup.Size;
        var color = _popup.Modulate;
        OrbitraMotion.Finish(_popup);
        var generation = ++_generation;
        foreach (var child in _popup.Children)
        {
            if (child != _marker)
                Watch(child);
        }
        _ui.DeferAction(() =>
        {
            if (_disposed || generation != _generation || _popup.Visible || !_owner.VisibleInTree || _owner.Disposed)
                return;
            foreach (var child in _popup.Children)
            {
                if (child != _marker)
                    _contents.Add(child);
            }
            if (_contents.Count == 0)
                return;
            foreach (var child in _contents)
            {
                child.Orphan();
                _visual.AddChild(child);
            }
            _visual.SetSize = _visual.MaxSize = size;
            _visual.Modulate = color;
            _ui.ModalRoot.AddChild(_visual);
            PopupContainer.SetPopupOrigin(_visual, position);
            _visual.Visible = true;
            _input.Block(_visual);
            OrbitraMotion.Hide(_visual, OrbitraMotion.MenuCloseDuration, EndVisual);
        });
    }

    private void Watch(Control control)
    {
        _watched.Add(control);
        control.OnChildAdded += OnContentsChanged;
        control.OnChildRemoved += OnContentsChanged;
        foreach (var child in control.Children)
            Watch(child);
    }

    private void OnContentsChanged(Control _)
    {
        _generation++;
        EndVisual();
    }

    private void EndVisual()
    {
        _visual.Visible = false;
        var generation = _generation;
        _ui.DeferAction(() =>
        {
            if (!_disposed && generation == _generation)
                Restore();
        });
    }

    private void OnOpen()
    {
        _hideRequested = false;
        Restore();
    }

    internal void Restore()
    {
        var notify = _hideRequested;
        _hideRequested = false;
        _generation++;
        _visual.Visible = false;
        OrbitraMotion.Finish(_visual, _ui);
        _input.Restore();
        foreach (var control in _watched)
        {
            control.OnChildAdded -= OnContentsChanged;
            control.OnChildRemoved -= OnContentsChanged;
        }
        _watched.Clear();
        foreach (var child in _contents)
        {
            if (child.Disposed)
                continue;
            child.Orphan();
            if (!_disposed && !_popup.Disposed)
                _popup.AddChild(child);
            else
                child.Dispose();
        }
        _contents.Clear();
        _visual.Orphan();
        if (notify && !_disposed)
            _hidden?.Invoke();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _popup.OnPopupOpen -= OnOpen;
        _popup.OnPopupHide -= OnHide;
        Restore();
        _visual.Dispose();
    }

    private sealed class Visual(OrbitraPopupPresentation presentation) : Control
    {
        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);
            if (Visible && (presentation._owner.Disposed || !presentation._owner.VisibleInTree))
                presentation.EndVisual();
        }
    }

    private sealed class OwnerLifetime(OrbitraPopupPresentation presentation) : Control
    {
        protected override Vector2 MeasureOverride(Vector2 availableSize) => Vector2.Zero;

        protected override void VisibilityChanged(bool newVisible)
        {
            base.VisibilityChanged(newVisible);
            if (!VisibleInTree && !presentation._disposed)
            {
                presentation._popup.Close();
                presentation.Restore();
            }
        }

        protected override void ExitedTree()
        {
            if (!presentation._disposed)
            {
                presentation._popup.Close();
                presentation.Restore();
            }
            base.ExitedTree();
        }

        protected override void Dispose(bool disposing)
        {
            presentation.Dispose();
            base.Dispose(disposing);
        }
    }
}
