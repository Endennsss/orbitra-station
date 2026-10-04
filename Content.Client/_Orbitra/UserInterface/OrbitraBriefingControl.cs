using System;
using System.Collections.Generic;
using Content.Client._Orbitra.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using Robust.Shared.Utility;
using Robust.Shared.Timing;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Адаптивная карточка приветствия антагониста вне однострочного OutputPanel.</summary>
public sealed class OrbitraBriefingControl : PanelContainer
{
    private readonly List<Label> _titleLetters = new();
    private readonly Color _gradientStart;
    private readonly Color _gradientEnd;
    private float _gradientPhase;

    public OrbitraBriefingControl(string wrappedMessage, Color bodyColor)
    {
        var theme = ResolveTheme(wrappedMessage);
        var message = FormattedMessage.FromMarkupOrThrow(OrbitraChatSheetlet.FormatChatMarkup(wrappedMessage));
        var lineCount = EstimateLineCount(message);
        var bodyHeight = lineCount * 20;

        HorizontalExpand = true;
        HorizontalAlignment = HAlignment.Center;
        MinWidth = 300;
        MaxWidth = 560;
        // RichTextEntry измеряет inline-контролы до их раскладки. Явная высота не даёт
        // многострочному содержимому вывалиться из рамки карточки.
        MinHeight = bodyHeight + 64;
        Margin = new Thickness(OrbitraUiMetrics.Small, 0);
        StyleClasses.Add("OrbitraAntagBriefing");
        // Нейтральный фон карточки не должен наследовать коричневый оттенок панели чата.
        // Внешняя рамка только ухудшает читаемость в узком окне, поэтому её нет.
        PanelOverride = new StyleBoxFlat(OrbitraPalettes.PanelInset)
        {
            BorderThickness = new Thickness(0),
        };

        var column = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = OrbitraUiMetrics.Small,
            HorizontalExpand = true,
            Margin = new Thickness(OrbitraUiMetrics.Medium),
        };

        var title = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 0,
            HorizontalExpand = true,
            MinHeight = 24,
        };
        var gradient = AddGradientTitle(title, Loc.GetString(theme.Title), theme.Color);
        _gradientStart = gradient.Start;
        _gradientEnd = gradient.End;
        var divider = new PanelContainer
        {
            MinHeight = 2,
            HorizontalExpand = true,
            PanelOverride = new StyleBoxFlat(theme.Color),
        };
        var body = new RichTextLabel
        {
            HorizontalExpand = true,
            MinHeight = bodyHeight,
            StyleClasses = { "OrbitraAntagBriefingText" },
        };
        body.SetMessage(message, tagsAllowed: null, defaultColor: theme.Color);

        column.AddChild(title);
        column.AddChild(divider);
        column.AddChild(body);
        AddChild(column);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (_titleLetters.Count == 0)
            return;

        // Медленный бесконечный цикл даёт живой перелив, но не превращает заголовок в мигание.
        _gradientPhase = (_gradientPhase + args.DeltaSeconds / 5f) % 1f;
        for (var i = 0; i < _titleLetters.Count; i++)
        {
            var position = (float)i / Math.Max(1, _titleLetters.Count - 1);
            var wave = 0.5f - 0.5f * MathF.Cos((position + _gradientPhase) * MathF.Tau);
            _titleLetters[i].FontColorOverride = Color.InterpolateBetween(_gradientStart, _gradientEnd, wave);
        }
    }

    private (Color Start, Color End) AddGradientTitle(BoxContainer title, string text, Color accent)
    {
        // Color хранит каналы в диапазоне 0..1: преобразование в byte обнуляло цвет.
        var start = Color.InterpolateBetween(accent, Color.Black, 0.25f);
        var end = Color.InterpolateBetween(accent, Color.White, 0.3f);
        for (var i = 0; i < text.Length; i++)
        {
            var progress = text.Length <= 1 ? 0f : (float)i / (text.Length - 1);
            var color = Color.InterpolateBetween(start, end, progress * progress * (3f - 2f * progress));
            var label = new Label
            {
                Text = text[i].ToString(),
                FontColorOverride = color,
                OutlineThicknessOverride = 0,
                StyleClasses = { "OrbitraAntagBriefingTitle" },
            };
            _titleLetters.Add(label);
            title.AddChild(label);
        }

        return (start, end);
    }

    private static (LocId Title, Color Color) ResolveTheme(string message)
    {
        var text = message.ToLowerInvariant();
        if (text.Contains("синдикат") || text.Contains("syndicate"))
            return ("orbitra-antag-briefing-syndicate", new Color(235, 30, 40));
        if (text.Contains("революц") || text.Contains("revolution"))
            return ("orbitra-antag-briefing-revolutionary", new Color(75, 145, 235));
        if (text.Contains("чейнджлинг") || text.Contains("генокрад") || text.Contains("changeling"))
            return ("orbitra-antag-briefing-changeling", new Color(80, 205, 145));
        if (text.Contains("волшебник") || text.Contains("wizard"))
            return ("orbitra-antag-briefing-wizard", new Color(175, 105, 235));
        if (text.Contains("ниндзя") || text.Contains("ninja"))
            return ("orbitra-antag-briefing-ninja", new Color(80, 180, 205));
        if (text.Contains("вор") || text.Contains("thief"))
            return ("orbitra-antag-briefing-thief", new Color(235, 175, 70));
        if (text.Contains("зомби") || text.Contains("инфицирован") || text.Contains("infected"))
            return ("orbitra-antag-briefing-infected", new Color(125, 205, 85));
        if (text.Contains("выживш") || text.Contains("survivor"))
            return ("orbitra-antag-briefing-survivor", new Color(190, 185, 105));

        return ("orbitra-antag-briefing-title", new Color(220, 75, 75));
    }

    public static int EstimateLineCount(FormattedMessage message)
    {
        var characters = 0;
        var lineBreaks = 0;
        foreach (var node in message.Nodes)
        {
            if (node.Name != null || !node.Value.TryGetString(out var text))
                continue;

            characters += text.Length;
            foreach (var character in text)
            {
                if (character == '\n')
                    lineBreaks++;
            }
        }

        // Это консервативная оценка для ширины карточки: лишнее место безопаснее,
        // чем обрезанный briefing при первом измерении inline-контрола.
        var wrappedLines = (characters + 39) / 40;
        return Math.Max(1, wrappedLines + lineBreaks);
    }
}
