using Content.Client._Orbitra.Stylesheets;
using Content.Client.ContextMenu.UI;
using Content.Client.Stylesheets;
using Content.Client.UserInterface.Controls;
using Content.Client.Verbs.UI;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Scoped HUD and context-menu surfaces; gameplay sprites and chat markup remain untouched.</summary>
[CommonSheetlet]
public sealed class OrbitraHudMenuSheetlet : Sheetlet<PalettedStylesheet>
{
    public override StyleRule[] GetRules(PalettedStylesheet sheet, object config)
    {
        var normal = Box(OrbitraPalettes.PanelBackground);
        var hover = Box(OrbitraPalettes.PanelHighlight);
        var pressed = Box(OrbitraPalettes.Primary.PressedElement);
        var selected = new StyleBoxFlat(pressed) { BorderColor = OrbitraPalettes.IconHovered };
        var row = new StyleBoxFlat(OrbitraPalettes.PanelInset);
        row.SetContentMarginOverride(StyleBox.Margin.Horizontal, 4);
        var rowHover = new StyleBoxFlat(row) { BackgroundColor = OrbitraPalettes.PanelHighlight };
        var rowPressed = new StyleBoxFlat(row) { BackgroundColor = OrbitraPalettes.Primary.PressedElement };
        var panel = Box(OrbitraPalettes.PanelInset);
        panel.SetContentMarginOverride(StyleBox.Margin.All, 2);
        var input = Box(OrbitraPalettes.PanelInset);
        input.SetContentMarginOverride(StyleBox.Margin.Horizontal, 8);
        var rules = new List<StyleRule>
        {
            E<PanelContainer>().Class("OrbitraChatFrame").Panel(Box(Color.Transparent)),
            E<ContainerButton>().Class("OrbitraEmoteButton").Box(normal).Modulate(Color.White),
            E<ContainerButton>().Class("OrbitraEmoteButton").PseudoNormal().Box(normal).Modulate(Color.White),
            E<ContainerButton>().Class("OrbitraEmoteButton").PseudoHovered().Box(hover).Modulate(Color.White),
            E<ContainerButton>().Class("OrbitraEmoteButton").PseudoPressed().Box(pressed).Modulate(Color.White),
            E<PanelContainer>().Class(ContextMenuPopup.StyleClassContextMenuPopup, "OrbitraContextPanel").Panel(panel),
            E<MenuButton>().Class("OrbitraHudButton").Box(normal).Modulate(Color.White),
            E<MenuButton>().Class("OrbitraHudButton").PseudoNormal().Box(normal).Modulate(Color.White),
            E<MenuButton>().Class("OrbitraHudButton").PseudoHovered().Box(hover).Modulate(Color.White),
            E<MenuButton>().Class("OrbitraHudButton").PseudoPressed().Box(selected).Modulate(Color.White),
            E<MenuButton>().Class("OrbitraHudButton").PseudoDisabled().Box(normal).Modulate(Color.White.WithAlpha(0.55f)),
            E<MenuButton>().Class("OrbitraHudButton", StyleClass.Negative).Box(Box(sheet.NegativePalette.Element)).Modulate(Color.White),
            E<MenuButton>().Class("OrbitraHudButton", StyleClass.Negative).PseudoNormal().Box(Box(sheet.NegativePalette.Element)),
            E<MenuButton>().Class("OrbitraHudButton", StyleClass.Negative).PseudoHovered().Box(Box(sheet.NegativePalette.HoveredElement)),
            E<MenuButton>().Class("OrbitraHudButton", StyleClass.Negative).PseudoPressed().Box(Box(sheet.NegativePalette.PressedElement)),
            E<MenuButton>().Class("OrbitraHudButton", StyleClass.Negative).PseudoDisabled().Box(normal).Modulate(Color.White.WithAlpha(0.55f)),
            E<PanelContainer>().Class("OrbitraChatSurface").Panel(new StyleBoxFlat(OrbitraPalettes.PanelBackground)),
            E<PanelContainer>().Class("ChatPanel", "OrbitraChatInput").Panel(new StyleBoxEmpty()),
            E<LineEdit>().Class("ChatLineEdit", "OrbitraChatInput").Prop(LineEdit.StylePropertyStyleBox, input),
        };
        foreach (var native in new[] { ContextMenuElement.StyleClassContextMenuButton, ConfirmationMenuElement.StyleClassConfirmationContextMenuButton })
        {
            var danger = native == ConfirmationMenuElement.StyleClassConfirmationContextMenuButton;
            var active = danger ? new StyleBoxFlat(rowHover) { BackgroundColor = sheet.NegativePalette.PressedElement } : rowPressed;
            rules.AddRange([
                E<ContextMenuElement>().Class(native, "OrbitraContextRow").Box(row).Modulate(Color.White),
                E<ContextMenuElement>().Class(native, "OrbitraContextRow").PseudoNormal().Box(row).Modulate(Color.White),
                E<ContextMenuElement>().Class(native, "OrbitraContextRow").PseudoHovered().Box(danger ? active : rowHover).Modulate(Color.White),
                E<ContextMenuElement>().Class(native, "OrbitraContextRow").PseudoPressed().Box(active).Modulate(Color.White),
                E<ContextMenuElement>().Class(native, "OrbitraContextRow").PseudoDisabled().Box(row).Modulate(Color.White.WithAlpha(0.55f)),
            ]);
        }
        foreach (var native in new[] { "ChatSelectorOptionButton", "ChatFilterOptionButton" })
        {
            rules.AddRange([
                E<ContainerButton>().Class(native, "OrbitraChatButton").Box(normal).Modulate(Color.White),
                E<ContainerButton>().Class(native, "OrbitraChatButton").PseudoNormal().Box(normal).Modulate(Color.White),
                E<ContainerButton>().Class(native, "OrbitraChatButton").PseudoHovered().Box(hover).Modulate(Color.White),
                E<ContainerButton>().Class(native, "OrbitraChatButton").PseudoPressed().Box(pressed).Modulate(Color.White),
                E<ContainerButton>().Class(native, "OrbitraChatButton").PseudoDisabled().Box(normal).Modulate(Color.White.WithAlpha(0.55f)),
            ]);
        }
        foreach (var native in new[] { ContextMenuElement.StyleClassContextMenuExpansionTexture, VerbMenuElement.StyleClassVerbMenuConfirmationTexture })
            rules.Add(E<ContextMenuElement>().Class("OrbitraContextRow").ParentOf(E<BoxContainer>()).ParentOf(E<TextureRect>().Class(native))
                .Prop(TextureRect.StylePropertyTexture, sheet.GetTextureOr(new ResPath("_Orbitra/Interface/Icons/chevron_right.svg.192dpi.png"), new ResPath("/Textures"))));
        return rules.ToArray();
    }

    private static StyleBoxFlat Box(Color background) => new(background)
    {
        BorderColor = OrbitraPalettes.PanelBorder,
        BorderThickness = new Thickness(1),
    };
}
