using System.Numerics;
using Content.Client._Orbitra.UserInterface;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;
using RoundEndPlayerInfo = Content.Shared.GameTicking.RoundEndMessageEvent.RoundEndPlayerInfo;

namespace Content.Client.RoundEnd;

public sealed partial class RoundEndSummaryWindow
{
    private void InitializeOrbitraSummary(TabContainer tabs)
    {
        var manifest = tabs.GetChild(1);
        var search = manifest.GetChild(0);
        search.GetChild(0).Dispose(); // Английский дубль подписи не нужен: поле уже содержит локализованную подсказку.
        var oldHeader = manifest.GetChild(1);
        var header = new OrbitraManifestRow();
        foreach (var button in _sortButtons)
        {
            // У SortButton подпись вложенная, а штатный Button.ClipText меняет пустую базовую Label.
            if (button.GetChild(1).GetChild(0) is Label caption)
            {
                caption.ClipText = true;
                button.ToolTip = caption.Text;
            }
            button.Orphan();
            header.AddChild(button);
        }
        oldHeader.Dispose();
        var scroll = (ScrollContainer) manifest.GetChild(1);
        _playerGrid.Orphan();
        var table = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
        var headerPanel = new PanelContainer();
        headerPanel.AddStyleClass("OrbitraManifestPanel");
        headerPanel.AddChild(header);
        table.AddChild(headerPanel);
        table.AddChild(_playerGrid);
        scroll.AddChild(table);
        OrbitraEntryWindow.Attach(this);
    }

    private void AddOrbitraPlayerRow(RoundEndPlayerInfo player)
    {
        var row = new OrbitraManifestRow { MinHeight = 40 };
        row.AddStyleClass("OrbitraManifestRow");
        var nameCell = new BoxContainer { SeparationOverride = 8 };
        if (player.PlayerNetEntity is { } entity)
            nameCell.AddChild(new SpriteView(entity, _entityManager)
            {
                OverrideDirection = Direction.South,
                SetSize = new Vector2(32),
                VerticalAlignment = VAlignment.Center,
            });
        var name = string.IsNullOrWhiteSpace(player.PlayerICName) ? player.PlayerOOCName : player.PlayerICName;
        nameCell.AddChild(Cell(name, player.Antag));
        row.AddChild(nameCell);
        row.AddChild(Cell(player.Observer ? "-" : Loc.GetString(player.Role)));
        row.AddChild(Cell(GetPlayerTypeText(player), player.Antag));
        row.AddChild(Cell(player.PlayerOOCName));
        var panel = new PanelContainer { HorizontalExpand = true };
        panel.AddStyleClass("OrbitraManifestPanel");
        panel.AddChild(row);
        _playerGrid.AddChild(panel);
        OrbitraEditorStyles.Apply(panel);
    }

    private static RichTextLabel Cell(string text, bool antagonist = false)
    {
        var label = new RichTextLabel { HorizontalExpand = true, VerticalAlignment = VAlignment.Center };
        label.SetMessage(text);
        if (antagonist)
            label.AddStyleClass("OrbitraManifestAntagonist");
        return label;
    }
}
