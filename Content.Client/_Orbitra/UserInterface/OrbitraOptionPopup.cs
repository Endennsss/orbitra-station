using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

using Content.Client._Orbitra.Lobby;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Presentation adapter for the stock option popup; selection and modal input remain native.</summary>
internal static class OrbitraOptionPopup
{
    public static void Attach(OptionButton option)
    {
        StyleRows(option.OptionsScroll);
        option.OptionStyleClasses.Remove("OrbitraLobbyButton");
        option.OptionStyleClasses.Add("OrbitraOptionRow");
        option.OptionsScroll.HScrollEnabled = false;
        option.OptionsScroll.MaxHeight = 288;
        option.OnPressed += _ => Place(option);
    }

    private static void Place(OptionButton option)
    {
        if (option.OptionsScroll.Parent is not Popup { Visible: true } popup || option.Root == null)
            return;
        var screen = option.Root.Size;
        var width = Math.Max(0, Math.Min(option.Width, screen.X - 32));
        var below = Math.Max(0, screen.Y - 16 - option.GlobalPosition.Y - option.Height);
        var above = Math.Max(0, option.GlobalPosition.Y - 16);
        option.OptionsScroll.SetWidth = width;
        option.OptionsScroll.MaxHeight = 288;
        option.OptionsScroll.Measure(new Vector2(width, 288));
        var wanted = Math.Min(288, option.OptionsScroll.DesiredSize.Y);
        var openAbove = below < wanted && above > below;
        var height = Math.Min(wanted, openAbove ? above : below);
        option.OptionsScroll.MaxHeight = height;
        popup.MinSize = Vector2.Zero;
        popup.SetSize = popup.MaxSize = new Vector2(width, height);
        var x = Math.Clamp(option.GlobalPosition.X, 16, Math.Max(16, screen.X - width - 16));
        var y = openAbove ? option.GlobalPosition.Y - height : option.GlobalPosition.Y + option.Height;
        // Повторный Open закрывает штатную модальность и отвязывает список от дерева.
        PopupContainer.SetPopupOrigin(popup, new Vector2(x, y));
        OrbitraMotion.BindPopup(popup, option);
        OrbitraMotion.Reveal(popup, OrbitraMotion.MenuDuration);
    }

    private static void StyleRows(Control control)
    {
        if (control is BoxContainer box)
            box.SeparationOverride = 0;
        if (control is Button button)
        {
            OrbitraMotion.AttachButton(button);
            button.RemoveStyleClass("OrbitraLobbyButton");
            button.AddStyleClass("OrbitraOptionRow");
            button.MinHeight = button.SetHeight = 32;
            button.MinWidth = 0;
            button.ClipText = true;
            button.Label.RemoveStyleClass(ContainerButton.StyleClassButton);
            button.TextAlign = Label.AlignMode.Left;
            button.Label.Margin = new Thickness(0, 0, 20, 0);
            button.ToolTip ??= button.Text;
            OrbitraTooltips.Attach(button);
            button.AddChild(new SelectionMark { MouseFilter = Control.MouseFilterMode.Ignore });
            return;
        }
        foreach (var child in control.Children)
            StyleRows(child);
        control.OnChildAdded += StyleRows;
    }

    private sealed class SelectionMark : Control
    {
        private readonly OrbitraIcon _icon = new() { Icon = "check", HorizontalAlignment = HAlignment.Right, Margin = new Thickness(0, 0, 4, 0) };

        public SelectionMark() => AddChild(_icon);

        protected override void Draw(DrawingHandleScreen handle)
        {
            _icon.Modulate = Color.White.WithAlpha(Parent is Button { Pressed: true } ? 1 : 0);
        }

        protected override Vector2 MeasureOverride(Vector2 availableSize)
        {
            base.MeasureOverride(availableSize);
            return Vector2.Zero;
        }
    }
}
