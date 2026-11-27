using System.Numerics;
using Content.Client.Parallax;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client._Orbitra.UserInterface;

[CVarDefs]
public static class OrbitraMenuCVars
{
    public const string Artwork = "art";
    public const string RandomScene = "random";

    public static readonly CVarDef<string> Background = CVarDef.Create(
        "orbitra.ui.menu_background", Artwork, CVar.CLIENTONLY | CVar.ARCHIVE);

    // Порядок соответствует ветвям шейдера; в настройках сохраняются строковые имена.
    internal static readonly string[] Scenes = ["rings", "aurora", "eclipse", "remnant", "comet"];
}

public sealed class OrbitraMenuBackground : Control
{
    private static readonly ProtoId<ShaderPrototype> ShaderId = "OrbitraMenuParallax";
    private readonly IConfigurationManager _configuration = IoCManager.Resolve<IConfigurationManager>();
    private readonly Control _fallback;
    private readonly Control? _credits;
    private readonly Control? _scrim;
    private ShaderInstance? _shader;
    private int? _randomScene;
    private float _time;
    private Vector2 _camera;

    private OrbitraMenuBackground(Control fallback, Control? credits, Control? scrim)
    {
        _fallback = fallback;
        _credits = credits;
        _scrim = scrim;
        MouseFilter = MouseFilterMode.Ignore;
        RectClipContent = true;
    }

    public static void Attach(Control owner, Control fallback, Control? credits = null, Control? scrim = null)
    {
        if (fallback is ParallaxControl parallax)
            parallax.UseOrbitraSky = true;

        var background = new OrbitraMenuBackground(fallback, credits, scrim);
        owner.AddChild(background);
        background.SetPositionInParent(1);
        LayoutContainer.SetAnchorPreset(background, LayoutContainer.LayoutPreset.Wide);
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        _configuration.OnValueChanged(OrbitraMenuCVars.Background, UpdateBackground, true);
    }

    protected override void ExitedTree()
    {
        _configuration.UnsubValueChanged(OrbitraMenuCVars.Background, UpdateBackground);
        _shader?.Dispose();
        _shader = null;
        base.ExitedTree();
    }

    private void UpdateBackground(string choice)
    {
        var scene = choice == OrbitraMenuCVars.RandomScene
            ? (_randomScene ??= IoCManager.Resolve<IRobustRandom>().Next(OrbitraMenuCVars.Scenes.Length))
            : Array.IndexOf(OrbitraMenuCVars.Scenes, choice);

        if (scene >= 0 && _shader == null &&
            IoCManager.Resolve<IPrototypeManager>().TryIndex(ShaderId, out var prototype))
        {
            try
            {
                _shader = prototype.InstanceUnique();
            }
            catch (Exception error)
            {
                Logger.GetSawmill("orbitra.parallax").Warning($"Не удалось загрузить фон меню: {error.Message}");
            }
        }

        var showScene = scene >= 0 && _shader != null;
        if (showScene)
            _shader!.SetParameter("scene", scene);
        Visible = showScene;
        _fallback.Visible = !showScene;
        if (_credits != null)
            _credits.Visible = !showScene;
        if (_scrim != null)
            _scrim.ModulateSelfOverride = showScene ? Color.White.WithAlpha(0.36f) : null;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (!VisibleInTree || Width < 1 || Height < 1 || _configuration.GetCVar(CCVars.ReducedMotion))
            return;

        _time += args.DeltaSeconds;
        var pointer = (UserInterfaceManager.MousePositionScaled.Position - GlobalPosition) / Size - new Vector2(0.5f);
        var target = Vector2.Clamp(pointer, new Vector2(-0.5f), new Vector2(0.5f));
        _camera = Vector2.Lerp(_camera, target, 1f - MathF.Exp(-3f * args.DeltaSeconds));
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        if (_shader == null || PixelWidth < 1 || PixelHeight < 1)
            return;

        _shader.SetParameter("resolution", (Vector2) PixelSize);
        _shader.SetParameter("phase", _time);
        _shader.SetParameter("camera", new Vector2(_camera.X, -_camera.Y));
        _shader.SetParameter("low_quality", _configuration.GetCVar(CCVars.ParallaxLowQuality));
        handle.UseShader(_shader);
        handle.DrawRect(UIBox2.FromDimensions(Vector2.Zero, PixelSize), Color.White);
        handle.UseShader(null);
    }
}
