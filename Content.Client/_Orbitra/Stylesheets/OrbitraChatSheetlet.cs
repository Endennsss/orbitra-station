using Content.Client.Stylesheets;
using Content.Client.Stylesheets.Fonts;
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
        return
        [
            E<OutputPanel>().Class(ChatText).Font(fonts.GetFont(13)),
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
