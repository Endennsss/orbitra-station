using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Fonts;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Orbitra.Lobby.Roadmap;

[CommonSheetlet]
public sealed class OrbitraAtlasSheetlet : Sheetlet<PalettedStylesheet>
{
    public override StyleRule[] GetRules(PalettedStylesheet sheet, object config) =>
    [
        E<RichTextLabel>().Class("OrbitraAtlasTitle").Font(sheet.BaseFont.GetFont(20, FontKind.Bold)),
    ];
}
