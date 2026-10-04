using System.Numerics;
using System.Linq;
using Content.Client._Orbitra.Lobby;
using Content.Client._Orbitra.UserInterface;
using Content.Client.Resources;
using Content.Client.Stylesheets;
using Content.Client.Voting;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Utility;

namespace Content.Client._Orbitra.Voting;

/// <summary>Полноэкранный выбор карты в лобби, оформленный в общей палитре Orbitra.</summary>
internal sealed class OrbitraMapVoteWindow : BaseWindow
{
    private readonly IVoteManager _voteManager;
    private readonly IResourceCache _resources;
    private readonly GridContainer _cards;
    private readonly Label _status;
    private readonly Button _back;
    private readonly Dictionary<int, Button> _voteButtons = new();
    private bool _hasVoted;

    public OrbitraMapVoteWindow(VoteManager.ActiveVote vote)
    {
        IoCManager.InjectDependencies(this);
        _voteManager = IoCManager.Resolve<IVoteManager>();
        _resources = IoCManager.Resolve<IResourceCache>();
        Stylesheet = IoCManager.Resolve<IStylesheetManager>().SheetSystem;

        MinSize = SetSize = new Vector2(920, 700);
        MaxSize = new Vector2(1200, 900);
        MouseFilter = MouseFilterMode.Stop;

        var surface = new PanelContainer { StyleClasses = { "OrbitraWindowSurface" } };
        AddChild(surface);

        var layout = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = OrbitraUiMetrics.Medium,
            Margin = new Thickness(OrbitraUiMetrics.Large),
        };
        surface.AddChild(layout);

        var header = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        header.AddChild(new Label
        {
            Text = Loc.GetString("orbitra-map-vote-title"),
            StyleClasses = { "OrbitraLobbyTitle" },
            HorizontalExpand = true,
        });
        _status = new Label
        {
            Text = Loc.GetString("orbitra-map-vote-prompt"),
            StyleClasses = { "OrbitraLobbyMuted" },
            VerticalAlignment = VAlignment.Center,
        };
        header.AddChild(_status);
        layout.AddChild(header);

        layout.AddChild(new Label
        {
            Text = Loc.GetString("orbitra-map-vote-description"),
            StyleClasses = { "OrbitraLobbyMuted" },
        });

        _cards = new GridContainer
        {
            Columns = 2,
            HorizontalExpand = true,
            VerticalExpand = true,
            HSeparationOverride = OrbitraUiMetrics.Medium,
            VSeparationOverride = OrbitraUiMetrics.Medium,
        };
        layout.AddChild(_cards);

        _back = new OrbitraLobbyButton
        {
            Text = Loc.GetString("orbitra-map-vote-back"),
            StyleClasses = { OrbitraButtonStyles.Secondary },
            HorizontalExpand = true,
            SetHeight = OrbitraUiMetrics.ActionHeight,
        };
        _back.OnPressed += _ => Close();
        layout.AddChild(_back);

        UpdateData(vote);
        OrbitraEntryWindow.Attach(this);
        OrbitraEditorStyles.Apply(this);
    }

    public void UpdateData(VoteManager.ActiveVote vote)
    {
        if (_cards.ChildCount == 0)
            BuildCards(vote);

        for (var i = 0; i < vote.Entries.Length && i < _cards.ChildCount; i++)
        {
            if (_cards.GetChild(i) is not PanelContainer card || card.GetChild(0) is not BoxContainer content)
                continue;

            if (content.GetChild(2) is Label votes)
                votes.Text = vote.DisplayVotes
                    ? Loc.GetString("orbitra-map-vote-count", ("count", vote.Entries[i].Votes))
                    : string.Empty;
        }

        if (vote.OurVote is { } selected && _voteButtons.TryGetValue(selected, out var selectedButton))
            selectedButton.Pressed = true;
        _hasVoted |= vote.OurVote != null;
        _status.Text = _hasVoted
            ? Loc.GetString("orbitra-map-vote-selected")
            : Loc.GetString("orbitra-map-vote-prompt");
    }

    private void BuildCards(VoteManager.ActiveVote vote)
    {
        for (var i = 0; i < vote.Entries.Length; i++)
        {
            var index = i;
            var name = vote.Entries[i].Text;
            var preview = new TextureRect
            {
                Texture = FindPreview(name),
                Stretch = TextureRect.StretchMode.KeepAspectCovered,
                SetHeight = 180,
                HorizontalExpand = true,
            };
            var title = new Label
            {
                Text = name,
                StyleClasses = { "OrbitraLobbyHeading" },
                HorizontalAlignment = HAlignment.Center,
                ClipText = true,
                HorizontalExpand = true,
            };
            var votes = new Label
            {
                StyleClasses = { "OrbitraLobbyMuted" },
                HorizontalAlignment = HAlignment.Center,
                HorizontalExpand = true,
            };
            var button = new OrbitraLobbyButton
            {
                Text = Loc.GetString("orbitra-map-vote-select"),
                StyleClasses = { OrbitraButtonStyles.Primary },
                HorizontalExpand = true,
                SetHeight = OrbitraUiMetrics.ActionHeight,
                Disabled = _hasVoted,
            };
            button.OnPressed += _ => Select(index);
            _voteButtons[index] = button;

            var content = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                SeparationOverride = OrbitraUiMetrics.Small,
                Margin = new Thickness(OrbitraUiMetrics.Small),
            };
            content.AddChild(preview);
            content.AddChild(title);
            content.AddChild(votes);
            content.AddChild(button);
            var card = new PanelContainer
            {
                StyleClasses = { "OrbitraRoundPanel" },
                HorizontalExpand = true,
                VerticalExpand = true,
            };
            card.AddChild(content);
            _cards.AddChild(card);
        }
    }

    private void Select(int index)
    {
        if (_hasVoted)
            return;

        _hasVoted = true;
        _voteManager.SendCastVote(GetVoteId(), index);
        foreach (var button in _voteButtons.Values)
            button.Disabled = true;
        if (_voteButtons.TryGetValue(index, out var selected))
            selected.Pressed = true;
        _status.Text = Loc.GetString("orbitra-map-vote-selected");
    }

    private int GetVoteId()
    {
        // The vote ID is attached to the window when it is created by the manager.
        return _voteId;
    }

    private int _voteId;

    public void SetVoteId(int voteId)
    {
        _voteId = voteId;
    }

    private Texture FindPreview(string mapName)
    {
        var slug = new string(mapName.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        var stationSlug = slug.EndsWith("station", StringComparison.Ordinal)
            ? slug[..^"station".Length]
            : slug;
        foreach (var candidate in new[] { $"/Textures/Structures/Wallmounts/posters.rsi/{slug}map.png", $"/Textures/Structures/Wallmounts/posters.rsi/{stationSlug}map.png", "/Textures/LobbyScreens/terminalstation.webp" })
        {
            if (_resources.TryGetResource<TextureResource>(new ResPath(candidate), out var texture))
                return texture.Texture;
        }

        return default!;
    }
}
