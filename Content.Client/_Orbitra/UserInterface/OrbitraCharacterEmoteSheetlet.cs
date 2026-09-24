using Content.Client._Orbitra.Stylesheets;
using Content.Client.Lobby.UI;
using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Character cards and the explicitly styled emote wheel.</summary>
[CommonSheetlet]
public sealed class OrbitraCharacterEmoteSheetlet : Sheetlet<PalettedStylesheet>
{
    public override StyleRule[] GetRules(PalettedStylesheet sheet, object config)
    {
        var normal = Box(OrbitraPalettes.PanelInset, OrbitraPalettes.PanelBorder);
        var hover = Box(OrbitraPalettes.PanelHighlight, OrbitraPalettes.PanelBorder);
        var selected = Box(OrbitraPalettes.Primary.PressedElement, OrbitraPalettes.IconHovered);
        var rules = new List<StyleRule>
        {
            E<CharacterPickerButton>().Class("OrbitraCharacterCard", ContainerButton.StyleClassButton).Box(normal).Modulate(Color.White),
            E<CharacterPickerButton>().Class("OrbitraCharacterCard", ContainerButton.StyleClassButton).PseudoNormal().Box(normal).Modulate(Color.White),
            E<CharacterPickerButton>().Class("OrbitraCharacterCard", ContainerButton.StyleClassButton).PseudoHovered().Box(hover).Modulate(Color.White),
            E<CharacterPickerButton>().Class("OrbitraCharacterCard", ContainerButton.StyleClassButton).PseudoPressed().Box(selected).Modulate(Color.White),
            E<CharacterPickerButton>().Class("OrbitraCharacterCard", ContainerButton.StyleClassButton).PseudoDisabled().Box(normal).Modulate(Color.White.WithAlpha(0.55f)),
            E<OrbitraRadialSector>().Class("OrbitraRadialSector")
                .Prop("orbitra-radial-normal", OrbitraPalettes.PanelInset.WithAlpha(0.96f))
                .Prop("orbitra-radial-hover", OrbitraPalettes.PanelHighlight.WithAlpha(0.98f))
                .Prop("orbitra-radial-pressed", OrbitraPalettes.Primary.PressedElement)
                .Prop("orbitra-radial-disabled", OrbitraPalettes.Primary.DisabledElement)
                .Prop("orbitra-radial-border", OrbitraPalettes.PanelBorder),
        };
        foreach (var (style, icon) in new[] { ("OrbitraRadialClose", "close"), ("OrbitraRadialBack", "chevron_left") })
        {
            var texture = sheet.GetTextureOr(new ResPath($"_Orbitra/Interface/Icons/{icon}.svg.192dpi.png"), new ResPath("/Textures"));
            rules.Add(E<TextureButton>().Class(style).Prop(TextureButton.StylePropertyTexture, texture).Modulate(OrbitraPalettes.IconNormal));
            rules.Add(E<TextureButton>().Class(style).PseudoHovered().Modulate(OrbitraPalettes.IconHovered));
        }
        return rules.ToArray();
    }

    private static StyleBoxFlat Box(Color background, Color border) => new(background)
    {
        BorderColor = border,
        BorderThickness = new Thickness(1),
    };
}
