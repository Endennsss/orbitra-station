using System.Numerics;
using System.Linq;
using Content.Client._Orbitra.Lobby;
using Content.Shared.Humanoid.Prototypes;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private bool _orbitraEditorInitialized;
    private readonly Dictionary<int, Button> _orbitraSections = new();
    private readonly List<(string Title, Control Row, Control Category)> _orbitraJobRows = new();
    private readonly BoxContainer _orbitraSaveActions = new() { SeparationOverride = 8, HorizontalExpand = true };
    private Control? _orbitraBack;

    private void InitializeOrbitraEditor()
    {
        _orbitraEditorInitialized = true;
        foreach (var field in new[] { SpeciesButton, SexButton, VoiceButton, PronounsButton, SpawnPriorityButton })
            field.SetWidth = 200;
        AgeEdit.SetWidth = 100;
        foreach (var field in new Control[] { SpeciesButton, AgeEdit, SexButton, VoiceButton, PronounsButton, SpawnPriorityButton })
        {
            if (field.Parent is not BoxContainer row)
                continue;
            var inputs = new BoxContainer { HorizontalExpand = true, SeparationOverride = 8 };
            foreach (var child in row.Children.ToArray())
            {
                if (child is Label caption)
                {
                    var label = new RichTextLabel { VerticalAlignment = VAlignment.Center };
                    label.SetMessage(caption.Text ?? string.Empty);
                    caption.Orphan();
                    caption.Dispose();
                    row.AddChild(label);
                    continue;
                }
                child.Orphan();
                if (child.GetType() == typeof(Control))
                    child.Dispose();
                else
                    inputs.AddChild(child);
            }
            row.AddChild(inputs);
            field.SetPositionFirst();
            // Резервируем одинаковое место справки во всех строках: края полей совпадают.
            if (inputs.ChildCount == 1)
                inputs.AddChild(new Control { SetWidth = 24 });
            else
                inputs.GetChild(1).SetWidth = 24;
            field.HorizontalExpand = true;
            field.HorizontalAlignment = HAlignment.Stretch;
            field.SetWidth = float.NaN;
        }
        ResetButton.Orphan();
        SaveButton.Orphan();
        _orbitraSaveActions.AddChild(ResetButton);
        _orbitraSaveActions.AddChild(SaveButton);
        OrbitraEditorFooter.AddChild(_orbitraSaveActions);
        TabContainer.PanelStyleBoxOverride = new StyleBoxFlat(Color.Transparent);
        RandomizeToggle.OnToggled += args => RandomizePanel.Visible = args.Pressed;
        OrbitraSectionSelect.OnItemSelected += args => TabContainer.CurrentTab = args.Id;
        TabContainer.OnTabChanged += _ => UpdateOrbitraSectionSelection();
        OrbitraJobSearch.OnTextChanged += _ => FilterOrbitraJobs();
        RefreshOrbitraSections();
        UpdateOrbitraEditorLabels();
        OrbitraEditorStyles.Apply(this);
    }

    internal void AttachOrbitraSetupControls(Control close, BoxContainer tools)
    {
        close.Orphan();
        _orbitraBack = close;
        OrbitraEditorFooter.AddChild(close);
        OrbitraProfileTools.Orphan();
        OrbitraProfileTools.Visible = true;
        tools.AddChild(OrbitraProfileTools);
    }

    private void RefreshOrbitraSections()
    {
        if (!_orbitraEditorInitialized)
            return;
        OrbitraNavigation.RemoveAllChildren();
        OrbitraSectionSelect.Clear();
        _orbitraSections.Clear();
        foreach (var index in new[] { 0, 4, 1, 2, 3, 5 })
        {
            if (index >= TabContainer.ChildCount || !TabContainer.GetTabVisible(index))
                continue;
            var title = index switch
            {
                1 => Loc.GetString("orbitra-editor-jobs"),
                3 => Loc.GetString("orbitra-editor-traits"),
                4 => Loc.GetString("orbitra-editor-markings"),
                _ => TabContainer.GetActualTabTitle(index),
            };
            OrbitraSectionSelect.AddItem(title, index);
            var button = new OrbitraLobbyButton { Text = title, ToggleMode = true };
            button.AddStyleClass("OrbitraLobbyButton");
            button.AddStyleClass("OrbitraNavigationButton");
            button.OnPressed += _ =>
            {
                TabContainer.CurrentTab = index;
                UpdateOrbitraSectionSelection();
            };
            _orbitraSections.Add(index, button);
            OrbitraNavigation.AddChild(button);
        }
        UpdateOrbitraSectionSelection();
    }

    private void UpdateOrbitraSectionSelection()
    {
        if (!_orbitraSections.ContainsKey(TabContainer.CurrentTab))
            TabContainer.CurrentTab = 0;
        OrbitraSectionSelect.SelectId(TabContainer.CurrentTab);
        foreach (var (index, button) in _orbitraSections)
            button.Pressed = index == TabContainer.CurrentTab;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        // Выбираем компоновку до измерения детей, иначе старые размеры раздувают родителя.
        if (_orbitraEditorInitialized)
            UpdateOrbitraEditorLayout(availableSize);
        return base.MeasureOverride(availableSize);
    }

    private void UpdateOrbitraEditorLayout(Vector2 availableSize)
    {
        var width = availableSize.X;
        var narrow = width < 900;
        var shortWindow = availableSize.Y < 600;
        OrbitraNavigation.Visible = width >= 1200;
        OrbitraSectionSelect.Visible = !OrbitraNavigation.Visible;
        OrbitraEditorBody.Orientation = narrow ? LayoutOrientation.Vertical : LayoutOrientation.Horizontal;
        OrbitraPreviewContent.Orientation = narrow ? LayoutOrientation.Horizontal : LayoutOrientation.Vertical;
        // Узкий экран: превью над формой; не создаём вторую сущность для отображения.
        OrbitraPreviewPanel.SetPositionInParent(narrow ? 0 : OrbitraEditorBody.ChildCount - 1);
        OrbitraPreviewPanel.SetWidth = narrow ? float.NaN : width < 1200 ? 224 : 280;
        // На низком окне оставляем место форме, а не только превью и кнопкам.
        var size = narrow ? shortWindow ? 96 : 160 : shortWindow ? 160 : width < 1200 ? 192 : 224;
        SpriteView.SetSize = new Vector2(size);
        SpriteView.Scale = new Vector2(size / 32f);
        OrbitraEditorFooter.Orientation = width < 700 ? LayoutOrientation.Vertical : LayoutOrientation.Horizontal;
        if (_orbitraBack != null)
            _orbitraBack.HorizontalExpand = width < 700;
        ResetButton.HorizontalExpand = SaveButton.HorizontalExpand = true;
        OrbitraEditorBody.SeparationOverride = shortWindow ? 8 : 16;
        SeparationOverride = shortWindow ? 6 : 12;
        OrbitraSaveStatus.Visible = width >= 1200;
        foreach (var action in OrbitraEditorFooter.Children)
        {
            if (action is Button button)
                button.HorizontalExpand = width < 1100;
        }
    }

    private void UpdateOrbitraEditorLabels()
    {
        if (!_orbitraEditorInitialized)
            return;
        OrbitraPreviewName.Text = Profile?.Name ?? string.Empty;
        OrbitraPreviewName.ToolTip = Profile?.Name;
        OrbitraPreviewSpecies.Text = Profile != null && _prototypeManager.TryIndex<SpeciesPrototype>(Profile.Species, out var species)
            ? Loc.GetString(species.Name) : string.Empty;
        OrbitraSaveStatus.Text = Loc.GetString(IsDirty ? "orbitra-editor-unsaved" : "orbitra-editor-saved");
    }

    internal void ShowOrbitraSaved() => OrbitraMotion.Pulse(OrbitraSaveStatus.VisibleInTree ? OrbitraSaveStatus : SaveButton);

    private void FilterOrbitraJobs()
    {
        var query = OrbitraJobSearch.Text.Trim();
        foreach (var (_, _, category) in _orbitraJobRows)
            category.Visible = false;
        foreach (var (title, row, category) in _orbitraJobRows)
        {
            row.Visible = title.Contains(query, StringComparison.CurrentCultureIgnoreCase);
            if (row.Visible)
                category.Visible = true;
        }
    }
}
