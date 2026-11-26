using System.Numerics;
using Content.Client._Orbitra.Stylesheets; // Orbitra-Edit - палитра корпуса КПК
using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Sheetlets;
using Content.Client.Stylesheets.SheetletConfigs;
using Content.Client.Stylesheets.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client.PDA;

[CommonSheetlet]
public sealed class PdaSheetlet : Sheetlet<NanotrasenStylesheet>
{
    public override StyleRule[] GetRules(NanotrasenStylesheet sheet, object config)
    {
        // Orbitra added start - общая скруглённая текстура с переменным цветом корпуса.
        IButtonConfig buttons = sheet;
        var caseBox = sheet.GetTextureOr(buttons.RoundedButtonBorderedPath, NanotrasenStylesheet.TextureRoot)
            .IntoPatch(StyleBox.Margin.All, 8);
        caseBox.TextureScale = new Vector2(2f);
        // Orbitra added end

        return
        [
            // Orbitra edit start - палитра экрана и корпуса КПК.
            E<PanelContainer>()
                .Class("PdaContentBackground")
                .Prop(PanelContainer.StylePropertyPanel, new StyleBoxFlat(OrbitraPalettes.PanelInset)
                {
                    BorderColor = Color.Black,
                    BorderThickness = new Thickness(2),
                }),
            // Orbitra edit end

            E<PanelContainer>()
                .Class("PdaBackground")
                .Prop(PanelContainer.StylePropertyPanel, StyleBoxHelpers.SquareStyleBox(sheet))
                .Prop(Control.StylePropertyModulateSelf, Color.FromHex("#000000")),

            // Orbitra edit start - корпус, вставки и цвета экрана.
            E<PanelContainer>()
                .Class("PdaBackgroundRect")
                .Prop(PanelContainer.StylePropertyPanel, caseBox)
                .Modulate(OrbitraPalettes.PanelHighlight),

            E<PanelContainer>()
                .Class("PdaInfoPanel")
                .Prop(PanelContainer.StylePropertyPanel, new StyleBoxFlat(OrbitraPalettes.PanelBackground)
                {
                    BorderColor = OrbitraPalettes.PanelBorder,
                    BorderThickness = new Thickness(1),
                }),

            E<PanelContainer>()
                .Class("PdaStatusBar")
                .Prop(PanelContainer.StylePropertyPanel, new StyleBoxFlat(OrbitraPalettes.PanelInset)
                {
                    BorderColor = OrbitraPalettes.PanelBorder,
                    BorderThickness = new Thickness(0, 1, 0, 0),
                }),

            // Orbitra edit end

            //PDA - Text
            E<Label>()
                .Class("PdaContentFooterText")
                .Prop(Label.StylePropertyFont, sheet.BaseFont.GetFont(11)) // Orbitra-Edit - читаемый адрес внизу экрана
                .Prop(Label.StylePropertyFontColor, OrbitraPalettes.IconNormal), // Orbitra-Edit - светлый текст строки состояния
        ];
    }
}

