using System.Numerics;
using Content.Client._Orbitra.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.UserInterface.Controls.FancyTree;

public sealed partial class TreeItem
{
    /// <summary>Uses Lucide disclosure icons only for explicitly styled tree items.</summary>
    internal bool ApplyOrbitraIcon()
    {
        if (Tree == null || !HasStyleClass("OrbitraEditorControl"))
            return false;
        Icon.RemoveStyleClass("OrbitraIcon-chevron_down");
        Icon.RemoveStyleClass("OrbitraIcon-chevron_right");
        Icon.SetSize = new Vector2(OrbitraUiMetrics.IconSize);
        Icon.Stretch = TextureRect.StretchMode.KeepAspectCentered;
        Icon.Texture = null;
        if (Body.ChildCount > 0)
            Icon.AddStyleClass(Expanded ? "OrbitraIcon-chevron_down" : "OrbitraIcon-chevron_right");
        Icon.Modulate = Tree.IconColor;
        // Пустая ячейка сохраняет отступ, но статья без детей не выглядит сворачиваемой.
        Icon.Visible = true;
        return true;
    }
}
