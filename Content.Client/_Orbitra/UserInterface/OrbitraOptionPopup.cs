using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

using Content.Client._Orbitra.Lobby;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Presentation adapter for the stock option popup; selection and modal input remain native.</summary>
internal static class OrbitraOptionPopup
{
    private const int MaximumHeight = 288;

    public static void Attach(OptionButton option)
    {
        if (option.HasStyleClass("OrbitraOptionPopupAttached"))
            return;
        option.AddStyleClass("OrbitraOptionPopupAttached");
        StyleCaption(option);
        StyleRows(option.OptionsScroll);
        option.OptionStyleClasses.Remove("OrbitraLobbyButton");
        option.OptionStyleClasses.Add("OrbitraOptionRow");
        option.OptionsScroll.HScrollEnabled = false;
        option.OptionsScroll.MaxHeight = MaximumHeight;
        option.OnPressed += _ => Place(option);
    }

    private static void Place(OptionButton option)
    {
        if (option.OptionsScroll.Parent is not Popup { Visible: true } popup || option.Root == null)
            return;
        var screen = option.Root.Size;
        var margin = Vector2.Min(new Vector2(OrbitraUiMetrics.ScreenMargin), screen / 4);
        var maximumWidth = Math.Max(0, screen.X - 2 * margin.X);
        var rowWidth = MeasureRows(option.OptionsScroll, maximumWidth);
        var width = Math.Min(maximumWidth, Math.Max(option.Width, rowWidth + OrbitraUiMetrics.Medium));
        var below = Math.Max(0, screen.Y - margin.Y - option.GlobalPosition.Y - option.Height);
        var above = Math.Max(0, option.GlobalPosition.Y - margin.Y);
        option.OptionsScroll.SetWidth = width;
        option.OptionsScroll.MaxHeight = MaximumHeight;
        option.OptionsScroll.Measure(new Vector2(width, MaximumHeight));
        var wanted = Math.Min(MaximumHeight, option.OptionsScroll.DesiredSize.Y);
        var openAbove = below < wanted && above > below;
        var height = Math.Min(wanted, openAbove ? above : below);
        option.OptionsScroll.MaxHeight = height;
        popup.MinSize = Vector2.Zero;
        popup.SetSize = popup.MaxSize = new Vector2(width, height);
        var x = Math.Clamp(option.GlobalPosition.X, margin.X, Math.Max(margin.X, screen.X - width - margin.X));
        var y = openAbove ? option.GlobalPosition.Y - height : option.GlobalPosition.Y + option.Height;
        // Повторный Open закрывает штатную модальность и отвязывает список от дерева.
        PopupContainer.SetPopupOrigin(popup, new Vector2(x, y));
        OrbitraMotion.BindPopup(popup, option);
        OrbitraMotion.Reveal(popup, OrbitraMotion.MenuDuration);
    }

    private static void StyleCaption(OptionButton option)
    {
        foreach (var child in option.Children)
        {
            if (child is not BoxContainer contents)
                continue;
            contents.SeparationOverride ??= OrbitraUiMetrics.Small;
            foreach (var item in contents.Children)
            {
                if (item is Label caption && caption.HasStyleClass(OptionButton.StyleClassOptionButton))
                {
                    caption.RectClipContent = true;
                    if (float.IsFinite(option.SetWidth))
                        caption.ClipText = true;
                    option.TooltipSupplier ??= _ =>
                    {
                        var text = option.ToolTip ?? caption.Text;
                        return string.IsNullOrWhiteSpace(text) ? null : new OrbitraTooltip(text);
                    };
                }
                else if (item is TextureRect icon && icon.HasStyleClass(OptionButton.StyleClassOptionTriangle))
                {
                    icon.AddStyleClass("OrbitraIcon-chevron_down");
                    icon.SetSize = new Vector2(OrbitraUiMetrics.IconSize);
                    icon.Stretch = TextureRect.StretchMode.KeepAspectCentered;
                }
            }
        }
    }

    private static float MeasureRows(Control control, float maximumWidth)
    {
        if (!control.Visible)
            return 0;
        if (control is Button button)
        {
            // Обрезанный Label не запрашивает ширину. Измеряем полную подпись при открытии.
            var clips = button.ClipText;
            button.ClipText = false;
            button.Measure(new Vector2(maximumWidth, MaximumHeight));
            var width = button.DesiredSize.X;
            button.ClipText = clips;
            return width;
        }

        var widest = 0f;
        foreach (var child in control.Children)
        {
            widest = Math.Max(widest, MeasureRows(child, maximumWidth));
            if (widest >= maximumWidth)
                break;
        }
        return widest;
    }

    private static void StyleRows(Control control)
    {
        if (control.HasStyleClass("OrbitraOptionRowsStyled"))
            return;
        control.AddStyleClass("OrbitraOptionRowsStyled");
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
