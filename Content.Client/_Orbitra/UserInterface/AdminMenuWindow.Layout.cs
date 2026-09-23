using Content.Client._Orbitra.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Administration.UI;

public sealed partial class AdminMenuWindow
{
    /// <summary>Reuses the original pages behind a fixed scrollable navigation row.</summary>
    private void InitializeOrbitraMenu()
    {
        AddStyleClass("OrbitraPreserveHorizontalScroll");
        SetSize = new System.Numerics.Vector2(900, 600);
        // Таблицы игроков и объектов сохраняют собственные области прокрутки.
        for (var i = 0; i < 6; i++)
        {
            var page = MasterTabContainer.GetChild(i);
            var content = page.GetChild(0);
            content.Orphan();
            var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
            scroll.AddChild(content);
            page.AddChild(scroll);
        }
        MasterTabContainer.Orphan();
        ContentsContainer.AddChild(new OrbitraMenuTabs(MasterTabContainer));
    }
}
