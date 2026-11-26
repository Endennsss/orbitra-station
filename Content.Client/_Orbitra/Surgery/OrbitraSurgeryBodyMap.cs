using System.Numerics;
using Content.Client._Orbitra.Stylesheets;
using Content.Client.Clickable;
using Content.Shared._Orbitra.Surgery;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Input;
using Robust.Shared.Prototypes;

namespace Content.Client._Orbitra.Surgery;

/// <summary>Фронтальная схема человека. Стороны принадлежат пациенту, как и слои тела в игре.</summary>
public sealed partial class OrbitraSurgeryBodyMap : Control
{
    [Dependency] private IResourceCache _cache = default!;
    [Dependency] private IClickMapManager _clickMap = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    private readonly RSI _parts;
    private readonly ShaderInstance _grayscale;
    private static readonly (SurgeryZone Zone, UIBox2 Bounds, string[] States)[] Regions =
    [
        (SurgeryZone.Head, new(11, 3, 20, 10), ["head_m"]),
        (SurgeryZone.RightArm, new(7, 11, 11, 22), ["r_arm", "r_hand"]),
        (SurgeryZone.LeftArm, new(20, 11, 24, 22), ["l_arm", "l_hand"]),
        (SurgeryZone.RightLeg, new(9, 23, 15, 32), ["r_leg", "r_foot"]),
        (SurgeryZone.LeftLeg, new(16, 23, 22, 32), ["l_leg", "l_foot"]),
        (SurgeryZone.Torso, new(10, 10, 21, 24), ["torso_m"]),
    ];
    private static readonly Vector2[] OutlineOffsets = [ -Vector2.UnitX, Vector2.UnitX, -Vector2.UnitY, Vector2.UnitY ];
    internal SurgeryZone? Selected;
    internal SurgeryZone? Hovered;
    internal OrbitraSurgeryState? State;
    internal event Action<SurgeryZone>? ZonePressed;
    internal Func<SurgeryZone, string>? DescribeZone;

    public OrbitraSurgeryBodyMap()
    {
        IoCManager.InjectDependencies(this);
        _parts = _cache.GetResource<RSIResource>("/Textures/Mobs/Species/Human/parts.rsi").RSI;
        _grayscale = _prototype.Index(OrbitraHudSheetlet.GreyscaleShader).Instance();
        MouseFilter = MouseFilterMode.Stop;
    }

    private float SpriteScale => Math.Max(1, MathF.Floor(Math.Min(PixelWidth - 16 * UIScale,
        PixelHeight - 12 * UIScale) / _parts.Size.Y));

    private UIBox2 SpriteRect()
    {
        var size = (Vector2) _parts.Size * SpriteScale;
        return UIBox2.FromDimensions(((PixelSize - size) / 2).Floored(), size);
    }

    private SurgeryZone? HitTest(Vector2 pixelPosition)
    {
        var position = ((pixelPosition - SpriteRect().TopLeft) / SpriteScale).Floored();
        for (var index = Regions.Length - 1; index >= 0; index--)
        {
            var (zone, bounds, states) = Regions[index];
            // Слой туловища перекрывает плечи и таз. Сужаем его со стороны рук и ног по южному кадру.
            if (zone == SurgeryZone.Torso)
            {
                var inset = position.Y switch { 23 => 2, >= 15 => 1, _ => 0 };
                bounds = new UIBox2(bounds.Left + inset, bounds.Top, bounds.Right - inset, bounds.Bottom);
            }
            // Ограничиваем допуск штатной карты попаданий на границе соседних частей.
            if (State?.Injuries.ContainsKey(zone) != true || position.X < bounds.Left || position.X >= bounds.Right || position.Y < bounds.Top || position.Y >= bounds.Bottom)
                continue;
            foreach (var state in states)
            {
                if (_clickMap.IsOccluding(_parts, state, RsiDirection.South, 0, position))
                    return zone;
            }
        }
        return null;
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        var hovered = HitTest(args.RelativePixelPosition);
        if (Hovered == hovered)
            return;
        Hovered = hovered;
        ToolTip = hovered is { } zone ? DescribeZone?.Invoke(zone) : null;
    }

    protected override void MouseExited()
    {
        base.MouseExited();
        Hovered = null;
        ToolTip = null;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function != EngineKeyFunctions.UIClick || HitTest(args.RelativePixelPosition) is not { } zone)
            return;
        ZonePressed?.Invoke(zone);
        args.Handle();
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        handle.DrawRect(PixelSizeBox, OrbitraPalettes.PanelInset);
        var rect = SpriteRect();
        foreach (var (zone, _, states) in Regions)
        {
            if (State == null || !State.Injuries.TryGetValue(zone, out var injury))
            {
                DrawStates(handle, states, rect, OrbitraPalettes.ButtonDisabled);
                continue;
            }
            var missing = (injury & SurgeryInjury.Missing) != 0;
            var marked = State.HasFindings(zone);
            var tinted = missing || Selected == zone || Hovered == zone || marked;
            var tint = Hovered == zone ? Color.PaleTurquoise : Selected == zone ? Color.Turquoise :
                marked ? Color.LightSalmon : Color.White;
            if (missing)
            {
                foreach (var offset in OutlineOffsets)
                    DrawStates(handle, states, rect.Translated(offset * MathF.Max(1, MathF.Round(SpriteScale / 2))), tint);
                DrawStates(handle, states, rect, OrbitraPalettes.PanelInset);
                continue;
            }
            if (!tinted)
                handle.UseShader(_grayscale);
            DrawStates(handle, states, rect, tint);
            if (!tinted)
                handle.UseShader(null);
        }

        foreach (var (zone, bounds, _) in Regions)
        {
            if (State?.HasFindings(zone) != true)
                continue;
            // Выносим знак за пределы фигуры, чтобы выбранный цвет не скрывал травму.
            var right = zone is SurgeryZone.LeftArm or SurgeryZone.LeftLeg or SurgeryZone.Torso;
            var markerY = zone == SurgeryZone.Torso ? 13 : bounds.Center.Y;
            var point = rect.TopLeft + new Vector2(right ? bounds.Right : bounds.Left, markerY) * SpriteScale;
            var sign = new Vector2(rect.Center.X + (right ? 1 : -1) * 13 * SpriteScale, point.Y);
            handle.DrawLine(point, sign, Color.LightSalmon);
            handle.DrawCircle(sign, 6 * UIScale, Color.LightSalmon);
            handle.DrawRect(new UIBox2(sign.X - UIScale, sign.Y - 4 * UIScale, sign.X + UIScale, sign.Y + UIScale), OrbitraPalettes.PanelInset);
            handle.DrawRect(new UIBox2(sign.X - UIScale, sign.Y + 2 * UIScale, sign.X + UIScale, sign.Y + 4 * UIScale), OrbitraPalettes.PanelInset);
        }
    }

    private void DrawStates(DrawingHandleScreen handle, string[] states, UIBox2 rect, Color tint)
    {
        foreach (var state in states)
            handle.DrawTextureRect(_parts[state].GetFrame(RsiDirection.South, 0), rect, tint);
    }
}
