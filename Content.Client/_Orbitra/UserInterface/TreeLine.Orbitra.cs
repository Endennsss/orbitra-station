using Robust.Client.Graphics;
using Robust.Shared.Maths;

namespace Content.Client.UserInterface.Controls.FancyTree;

public sealed partial class TreeLine
{
    private bool DrawOrbitraLines(DrawingHandleScreen handle)
    {
        if (Parent is not TreeItem parent || !parent.HasStyleClass("OrbitraEditorControl"))
            return false;
        if (!parent.Expanded || !parent.Tree.DrawLines)
            return true;
        var origin = GlobalPixelPosition;
        var x = parent.Icon.GlobalPixelPosition.X - origin.X + parent.Icon.PixelWidth / 2;
        var top = parent.Button.GlobalPixelPosition.Y - origin.Y + parent.Button.PixelHeight;
        var bottom = top;
        var thickness = Math.Max(1, (int) UIScale);
        foreach (var child in parent.Body.Children)
        {
            if (child is not TreeItem item || !item.VisibleInTree)
                continue;
            var y = item.Button.GlobalPixelPosition.Y - origin.Y + item.Button.PixelHeight / 2;
            var end = item.Icon.GlobalPixelPosition.X - origin.X - (int) (3 * UIScale);
            bottom = Math.Max(bottom, y);
            if (end > x)
                handle.DrawRect(new UIBox2i(x, y, end, y + thickness), parent.Tree.LineColor);
        }
        if (bottom > top)
            handle.DrawRect(new UIBox2i(x, top, x + thickness, bottom + thickness), parent.Tree.LineColor);
        return true;
    }
}
