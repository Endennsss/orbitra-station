using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;

namespace Content.Client._Orbitra.UserInterface;

[CVarDefs]
public static class OrbitraMenuCVars
{
    public static readonly CVarDef<bool> StandardBackground = CVarDef.Create(
        "orbitra.ui.standard_background", false, CVar.CLIENTONLY | CVar.ARCHIVE);
}

/// <summary>Optional static background, retaining each screen's original background when disabled.</summary>
public sealed class OrbitraMenuBackground : TextureRect
{
    private readonly IConfigurationManager _configuration = IoCManager.Resolve<IConfigurationManager>();
    private readonly Control _fallback;
    private readonly Control? _credits;

    private OrbitraMenuBackground(Control fallback, Control? credits)
    {
        _fallback = fallback;
        _credits = credits;
        MouseFilter = MouseFilterMode.Ignore;
        CanShrink = true;
        RectClipContent = true;
        Stretch = StretchMode.KeepAspectCovered;
        TexturePath = "/Textures/_Orbitra/Interface/Backgrounds/standard.png";
    }

    public static void Attach(Control owner, Control fallback, Control? credits = null)
    {
        var background = new OrbitraMenuBackground(fallback, credits);
        owner.AddChild(background);
        background.SetPositionInParent(1);
        LayoutContainer.SetAnchorPreset(background, LayoutContainer.LayoutPreset.Wide);
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        _configuration.OnValueChanged(OrbitraMenuCVars.StandardBackground, UpdateBackground, true);
    }

    protected override void ExitedTree()
    {
        _configuration.UnsubValueChanged(OrbitraMenuCVars.StandardBackground, UpdateBackground);
        base.ExitedTree();
    }

    private void UpdateBackground(bool enabled)
    {
        Visible = enabled;
        _fallback.Visible = !enabled;
        if (_credits != null)
            _credits.Visible = !enabled;
    }
}
