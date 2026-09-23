using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;
using Robust.Client.Input;
using Robust.Shared.Input;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>One tooltip presenter per UI root; mouse timing and specialized suppliers remain native.</summary>
internal sealed class OrbitraTooltips : Control
{
    private OrbitraTooltip? _active;
    private Control? _focusOwner;
    private float _delay;
    private bool _keyboard;
    private bool _hiding;
    private int _generation;
    private readonly OrbitraInputBlock _input = new();
    private OwnerLifetime? _ownerLifetime;

    internal static void Attach(Control owner, BoundKeyFunction? hotkey = null)
    {
        if (owner.HasStyleClass("OrbitraTooltipOwner")) return;
        owner.AddStyleClass("OrbitraTooltipOwner");
        var original = owner.TooltipSupplier;
        owner.TooltipSupplier = sender =>
        {
            var content = original?.Invoke(sender);
            if (original != null && content == null) return null;
            if (content == null && string.IsNullOrWhiteSpace(sender.ToolTip)) return null;
            var text = sender.ToolTip ?? "";
            var binding = hotkey ?? (sender as Content.Client.UserInterface.Controls.MenuButton)?.BoundKey;
            if (binding != null && content == null)
            {
                var input = IoCManager.Resolve<IInputManager>();
                if (input.TryGetKeyBinding(binding.Value, out var key))
                    text = Robust.Shared.Localization.Loc.GetString("orbitra-ui-tooltip-hotkey", ("text", text), ("key", key.GetKeyString()));
            }
            return Get(sender).Present(sender, content, text);
        };
        owner.OnShowTooltip += (_, _) => Get(owner).Shown(owner);
        owner.OnHideTooltip += (_, _) => Get(owner).Hide(owner);
    }

    private static OrbitraTooltips Get(Control owner)
    {
        var ui = owner.UserInterfaceManager;
        foreach (var child in ui.RootControl.Children)
            if (child is OrbitraTooltips presenter) return presenter;
        var result = new OrbitraTooltips { MouseFilter = MouseFilterMode.Ignore };
        ui.RootControl.AddChild(result);
        return result;
    }

    internal static void FocusChanged(Control owner)
    {
        var presenter = Get(owner);
        if (presenter._focusOwner == owner) return;
        if (presenter._keyboard && presenter._active?.Owner is { } previous)
            presenter.Hide(previous);
        presenter._focusOwner = owner;
        presenter._delay = owner.TooltipDelay ?? 0.25f;
    }

    internal static void ClearOwned(Control owner)
    {
        var ui = owner.UserInterfaceManager;
        foreach (var child in ui.RootControl.Children)
        {
            if (child is not OrbitraTooltips presenter) continue;
            if (Contains(owner, presenter._focusOwner)) presenter._focusOwner = null;
            if (Contains(owner, presenter._active?.Owner)) presenter.Clear();
        }
    }

    private OrbitraTooltip Present(Control owner, Control? content, string text)
    {
        Clear(false);
        _keyboard = false;
        _hiding = false;
        _active = content as OrbitraTooltip ?? (content == null ? new OrbitraTooltip(text) : new OrbitraTooltip(content) { SuppliedContent = content });
        _active.Owner = owner;
        _input.Block(_active);
        _ownerLifetime = new OwnerLifetime(this) { MouseFilter = MouseFilterMode.Ignore };
        owner.AddChild(_ownerLifetime);
        return _active;
    }

    private void Shown(Control owner)
    {
        if (_active?.Owner != owner) return;
        _active.Place(_keyboard);
        OrbitraMotion.Reveal(_active, OrbitraMotionPresets.PopupOpen);
    }

    private void Hide(Control owner)
    {
        if (_active?.Owner != owner || _hiding) return;
        if (!owner.VisibleInTree || owner.Disposed) { Clear(); return; }
        _hiding = true;
        var tip = _active;
        var color = tip.Modulate;
        OrbitraMotion.Finish(tip, UserInterfaceManager);
        var generation = ++_generation;
        UserInterfaceManager.DeferAction(() =>
        {
            if (generation != _generation || tip.Disposed || !owner.VisibleInTree) return;
            if (tip.Parent == null) UserInterfaceManager.PopupRoot.AddChild(tip);
            tip.Modulate = color;
            OrbitraMotion.Hide(tip, OrbitraMotionPresets.PopupClose, () =>
            {
                if (generation != _generation) return;
                tip.Visible = false;
                UserInterfaceManager.DeferAction(() =>
                {
                    if (generation == _generation) Clear();
                });
            });
        });
    }

    private void Clear(bool hideNative = true)
    {
        ++_generation;
        var tip = _active;
        _active = null;
        _hiding = false;
        var lifetime = _ownerLifetime;
        _ownerLifetime = null;
        lifetime?.Dispose();
        if (tip == null) return;
        if (hideNative) tip.Owner?.HideTooltip();
        OrbitraMotion.Finish(tip, UserInterfaceManager);
        _input.Restore();
        // Специализированное содержимое принадлежит исходному supplier, а не оболочке.
        tip.SuppliedContent?.Orphan();
        tip.Orphan();
        tip.Dispose();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (_active?.Owner is { } owner)
        {
            if (!owner.VisibleInTree || owner.Disposed) Clear();
            else if (_keyboard && UserInterfaceManager.KeyboardFocused != owner) Hide(owner);
            else if (!_hiding) _active.Place(_keyboard);
        }
        if (_focusOwner == null || UserInterfaceManager.KeyboardFocused != _focusOwner || !_focusOwner.VisibleInTree)
        {
            _focusOwner = null;
            return;
        }
        if (_delay < 0) return;
        _delay -= args.DeltaSeconds;
        if (_delay > 0) return;
        _delay = -1;
        if (!_focusOwner.HasStyleClass("OrbitraTooltipOwner") || _active?.Owner == _focusOwner) return;
        // Штатная мышиная подсказка уступает текущему клавиатурному действию.
        UserInterfaceManager.CurrentlyHovered?.HideTooltip();
        var tip = _focusOwner.TooltipSupplier?.Invoke(_focusOwner);
        if (tip == null) return;
        _keyboard = true;
        UserInterfaceManager.PopupRoot.AddChild(tip);
        Shown(_focusOwner);
    }

    private static bool Contains(Control root, Control? control)
    {
        for (; control != null; control = control.Parent)
            if (control == root) return true;
        return false;
    }

    private void LostOwner(OwnerLifetime lifetime)
    {
        if (_ownerLifetime != lifetime || _active == null) return;
        _active.Visible = false;
        var generation = _generation;
        // Не меняем дочернюю коллекцию владельца из его обхода ExitedTree.
        UserInterfaceManager.DeferAction(() =>
        {
            if (generation == _generation) Clear();
        });
    }

    private sealed class OwnerLifetime(OrbitraTooltips presenter) : Control
    {
        protected override Vector2 MeasureOverride(Vector2 availableSize) => Vector2.Zero;
        protected override void ExitedTree()
        {
            presenter.LostOwner(this);
            base.ExitedTree();
        }
        protected override void VisibilityChanged(bool newVisible)
        {
            if (!newVisible) presenter.LostOwner(this);
            base.VisibilityChanged(newVisible);
        }
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize) => Vector2.Zero;

    protected override void Dispose(bool disposing)
    {
        Clear();
        _focusOwner = null;
        base.Dispose(disposing);
    }
}
