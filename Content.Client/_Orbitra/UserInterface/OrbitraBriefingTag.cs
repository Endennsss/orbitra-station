using System.Diagnostics.CodeAnalysis;
using Content.Client._Orbitra.Stylesheets;
using Content.Shared.Chat;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Рисует приветствие антагониста как компактный блок внутри общей ленты чата.</summary>
[UsedImplicitly]
public sealed class OrbitraBriefingTag : IMarkupTagHandler
{
    public string Name => "orbitra-briefing";

    public bool TryCreateControl(MarkupNode node, [NotNullWhen(true)] out Control? control)
    {
        // Самозакрывающийся rich-text тег получает пару узлов. Создаём блок только на открывающем.
        if (node.Closing || !node.Value.TryGetString(out var message))
        {
            control = null;
            return false;
        }

        control = new OrbitraBriefingControl(message, ChatChannel.Server.TextColor());
        return true;
    }
}
