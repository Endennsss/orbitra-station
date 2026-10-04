using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Fonts;
using Content.Client._Orbitra.UserInterface;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client._Orbitra.Stylesheets;

/// <summary>
/// Typography for Orbitra chat and speech bubbles.
/// </summary>
[CommonSheetlet]
public sealed class OrbitraChatSheetlet : Sheetlet<PalettedStylesheet>
{
    public const string ChatText = "OrbitraChatText";
    public const string SpeechText = "OrbitraSpeechText";
    public const string SpeechBox = "OrbitraSpeechBox";

    public static string FormatChatMarkup(string message)
    {
        // Стандартный italic-тег выбирает старый шрифт независимо от стиля чата.
        return message
            .Replace("[italic]", "[font=\"NotoSansDisplayItalic\"]", StringComparison.Ordinal)
            .Replace("[/italic]", "[/font]", StringComparison.Ordinal);
    }

    public static string FormatSpeechMarkup(string message)
    {
        // Внешний курсив эмоций задаётся стилем пузыря, а не старым шрифтом тега.
        const string start = "[italic]";
        const string end = "[/italic]";
        return message.StartsWith(start, StringComparison.Ordinal) && message.EndsWith(end, StringComparison.Ordinal)
            ? message[start.Length..^end.Length]
            : message;
    }

    public override StyleRule[] GetRules(PalettedStylesheet sheet, object config)
    {
        var fonts = new NotoFontFamilyStack(ResCache, "Display");
        var speechBox = new StyleBoxEmpty(); // Orbitra-Edit - речь и эмоции без задней подложки
        var briefingPanel = new StyleBoxFlat(OrbitraPalettes.PanelInset.WithAlpha(0.98f))
        {
            BorderColor = sheet.NegativePalette.Element.WithAlpha(0.75f),
            BorderThickness = new Thickness(1),
        };
        briefingPanel.SetContentMarginOverride(StyleBox.Margin.All, OrbitraUiMetrics.Small);
        return
        [
            E<OutputPanel>().Class(ChatText).Font(fonts.GetFont(13)),
            E<PanelContainer>().Class("OrbitraAntagBriefing").Panel(briefingPanel),
            E<PanelContainer>().Class("OrbitraAntagBriefingAccent").Panel(new StyleBoxFlat(sheet.NegativePalette.Element)),
            E<Label>().Class("OrbitraAntagBriefingTitle").Font(fonts.GetFont(13, FontKind.Bold)).FontColor(sheet.NegativePalette.Text),
            E<RichTextLabel>().Class("OrbitraAntagBriefingText").Font(fonts.GetFont(13)).FontColor(OrbitraPalettes.Primary.Text),
            E<LineEdit>().Class(ChatText).Font(fonts.GetFont(13)),
            E<RichTextLabel>().Class(SpeechText)
                .Font(fonts.GetFont(13))
                .Prop(Label.StylePropertyFontOutlineThickness, 0f),
            E<PanelContainer>().Class("speechBox", SpeechBox)
                .Panel(speechBox),
            E<PanelContainer>().Class("speechBox", "emoteBox")
                .ParentOf(E<RichTextLabel>().Class(SpeechText))
                .Font(fonts.GetFont(13, FontKind.Italic)),
            E<PanelContainer>().Class("speechBox", "whisperBox")
                .ParentOf(E<RichTextLabel>().Class(SpeechText))
                .Font(fonts.GetFont(13, FontKind.Italic)),
        ];
    }
}
