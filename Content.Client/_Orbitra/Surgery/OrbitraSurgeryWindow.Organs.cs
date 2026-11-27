using System.Numerics;
using Content.Client._Orbitra.UserInterface;
using Content.Shared._Orbitra.Surgery;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Orbitra.Surgery;

public sealed partial class OrbitraSurgeryWindow
{
    private readonly Dictionary<SurgeryOrgan, OrbitraButton> _organButtons = new();
    private SurgeryOrgan? _organ;
    private OrbitraOrganSystem _organs = default!;

    private void InitializeOrgans()
    {
        _organs = _entity.System<OrbitraOrganSystem>();
        foreach (var kind in OrbitraOrganSystem.Kinds)
        {
            var button = new OrbitraButton { ToggleMode = true, ClipText = true, HorizontalExpand = true };
            button.AddStyleClass("OrbitraSurgeryRow");
            button.TextAlign = Label.AlignMode.Left;
            button.Label.Margin = new Thickness(30, 0, 0, 0);
            button.AddChild(new TextureRect
            {
                Texture = _sprites.GetPrototypeIcon(OrbitraOrganSystem.Prototype(kind)).Default,
                SetSize = new Vector2(26),
                HorizontalAlignment = Control.HAlignment.Left,
                VerticalAlignment = Control.VAlignment.Center,
                Stretch = TextureRect.StretchMode.KeepAspectCentered,
                MouseFilter = Control.MouseFilterMode.Ignore,
            });
            button.OnPressed += _ =>
            {
                if (_state?.Busy == true)
                    return;
                ClearTask();
                _feedback = null;
                _organ = _organ == kind ? null : kind;
                RefreshSelection();
            };
            OrganButtons.AddChild(button);
            _organButtons.Add(kind, button);
        }
    }

    private string OrganName(SurgeryOrgan kind) => _loc.GetString($"orbitra-surgery-organ-{kind.ToString().ToLowerInvariant()}");
    private string ConditionName(SurgeryOrganCondition condition) =>
        _loc.GetString($"orbitra-surgery-organ-condition-{condition.ToString().ToLowerInvariant()}");

    private string OrganReading(SurgeryOrgan kind, SurgeryOrganCondition condition) =>
        _loc.GetString("orbitra-surgery-organ-reading", ("organ", OrganName(kind)), ("condition", ConditionName(condition)));

    private void UpdateOrgans()
    {
        OrganPanel.Visible = false;
        if (_zone == null || _state == null)
            return;

        foreach (var (kind, button) in _organButtons)
        {
            button.Visible = _state.Organs.TryGetValue(kind, out var condition) && OrbitraOrganSystem.ZoneFor(kind) == _zone;
            if (!button.Visible)
                continue;
            OrganPanel.Visible = true;
            button.Disabled = _state.FieldTable || _state.Busy;
            button.Pressed = _organ == kind;
            button.RemoveStyleClass(condition == SurgeryOrganCondition.Healthy ? "OrbitraSurgeryFinding" : "OrbitraSurgeryRow");
            button.AddStyleClass(condition == SurgeryOrganCondition.Healthy ? "OrbitraSurgeryRow" : "OrbitraSurgeryFinding");
            button.Text = MarkSelected(OrganReading(kind, condition), button.Pressed);
            button.ToolTip = button.Text;
        }
    }
}
