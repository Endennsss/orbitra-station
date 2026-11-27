using System.Numerics;
using Content.Client._Orbitra.UserInterface;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Client._Orbitra.Lobby.Roadmap;

/// <summary>Кнопка лобби однократно прочерчивает созвездие при наведении или выборе клавиатурой.</summary>
public sealed partial class OrbitraAtlasButton : OrbitraContainerButton
{
    [Dependency] private IConfigurationManager _configuration = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly Vector2[] Points = [new(0, 11), new(11, 2), new(25, 7)];
    private readonly Label _caption;
    private TimeSpan? _start;
    private bool _engaged;

    public bool Compact
    {
        set => _caption.Visible = !value;
    }

    public OrbitraAtlasButton()
    {
        IoCManager.InjectDependencies(this);
        AddStyleClass(StyleClassButton);
        AddStyleClass("OrbitraLobbyButton");
        ToolTip = Loc.GetString("orbitra-atlas-title");
        _caption = new Label
        {
            Text = Loc.GetString("orbitra-atlas-button"),
            Margin = new Thickness(34, 0, 0, 0),
            VerticalAlignment = VAlignment.Center,
        };
        AddChild(_caption);
        AddChild(new Robust.Client.UserInterface.Control { MinWidth = 26, MouseFilter = MouseFilterMode.Ignore });
        OrbitraTooltips.Attach(this);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var engaged = IsHovered || HasKeyboardFocus();
        if (_engaged != engaged)
        {
            _engaged = engaged;
            _start = engaged ? _timing.RealTime : null;
        }
        if (_start != null && (_configuration.GetCVar(CCVars.ReducedMotion) || !OrbitraMotion.CanAnimate(this)))
            _start = null;

        var progress = _start is { } start ? (float) (_timing.RealTime - start).TotalSeconds / 0.42f : 1;
        if (progress >= 1)
            _start = null;
        var origin = new Vector2((_caption.Visible ? 12 * UIScale : PixelWidth / 2f - 13 * UIScale),
            PixelHeight / 2f - 6 * UIScale);
        var ink = engaged ? OrbitraAtlasArt.Amber : OrbitraAtlasArt.Mist;
        for (var i = 1; i < Points.Length; i++)
        {
            var from = origin + Points[i - 1] * UIScale;
            var to = origin + Points[i] * UIScale;
            handle.DrawLine(from, to, ink.WithAlpha(0.40f));
            var t = Math.Clamp(progress * (Points.Length - 1) - i + 1, 0, 1);
            var tip = Vector2.Lerp(from, to, t);
            if (t > 0)
                handle.DrawLine(from, tip, ink);
            if (t is > 0 and < 1)
                handle.DrawCircle(tip, 2.6f * UIScale, Color.White);
        }
        for (var i = 0; i < Points.Length; i++)
        {
            var center = origin + Points[i] * UIScale;
            handle.DrawCircle(center, (i == 1 ? 2.5f : 1.8f) * UIScale, ink);
        }
    }
}
