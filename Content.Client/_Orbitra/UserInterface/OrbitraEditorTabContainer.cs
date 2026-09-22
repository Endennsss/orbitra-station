using System.Numerics;
using Robust.Client.UserInterface.Controls;

using Content.Client._Orbitra.Lobby;

namespace Content.Client._Orbitra.UserInterface;

/// <summary>Arranges hidden-tab content without retaining the previous visible header height.</summary>
public sealed class OrbitraEditorTabContainer : TabContainer
{
    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        if (TabsVisible)
            return base.ArrangeOverride(finalSize);

        // Штатный контейнер сохраняет высоту старого заголовка после скрытия вкладок.
        if (CurrentTab >= 0 && CurrentTab < ChildCount)
        {
            var child = GetChild(CurrentTab);
            child.Visible = true;
            child.Arrange(UIBox2.FromDimensions(Vector2.Zero, finalSize));
        }
        return finalSize;
    }
}
